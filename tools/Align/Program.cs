// The alignment toolkit's command line (design doc 10). Step 2 scope: the store and its importers.
//
//   dotnet run --project tools/Align -- init                                   create or upgrade the schema
//   dotnet run --project tools/Align -- stats [--dataset pmc]                  counts
//   dotnet run --project tools/Align -- import-overrides <file.json> [--dataset pmc] [--base https://data.paul-mellon-centre.ac.uk/] [--class Group]
//                                                                              a pmc-lux authority-strings file
//   dotnet run --project tools/Align -- import-legacy [--dataset pmc] [--base ...] [--reuse-by-equivalent] [--only-referenced] [--version legacy]
//                                                                              the owner's earlier authorities and source_string_map tables; the two
//                                                                              flags are for a second copy of them with its own identifiers
//
//   dotnet run --project tools/Align -- load-getty <file.nt[.gz] | directory>... --vocab ulan|aat|tgn [--version 2026-10]
//                                                                              a Getty full N-Triples download into the local authority index
//   dotnet run --project tools/Align -- reconcile [--dataset pmc] [--limit 1000] [--accept 0.9] [--review 0.6] [--no-single-source] [--no-mint]
//                                                                              decide undecided strings from overrides, the store and the local indexes
//
// Connection strings come from the environment, never from arguments or files in the repository:
//   LINKEDART_PG         the store (e.g. "Host=localhost;Database=linkedart;Username=...;Password=...")
//   LINKEDART_LEGACY_PG  the earlier PMC database, for import-legacy

using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Import;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Alignment.Store;
using Npgsql;

if (args.Length == 0 || args[0] is "-h" or "--help") { Usage(); return 0; }

string? Option(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
var dataset = Option("--dataset") ?? "pmc";
var scheme = Option("--base") is { } b ? new IdentifierScheme(b) : IdentifierScheme.Pmc;

var connection = Environment.GetEnvironmentVariable("LINKEDART_PG");
if (string.IsNullOrWhiteSpace(connection)) { Console.Error.WriteLine("Set LINKEDART_PG to the store's connection string."); return 2; }
await using var store = new PostgresAlignmentStore(connection);

switch (args[0])
{
    case "init":
        await store.InitialiseAsync();
        Console.WriteLine("Schema is current.");
        return 0;

    case "stats":
    {
        var c = await store.CountsAsync(dataset == "all" ? null : dataset);
        Console.WriteLine($"dataset {dataset}: entities {c.Entities}, source strings {c.SourceStrings} (decided {c.Decided}, unresolved {c.Unresolved}), overrides {c.Overrides}, authority records {c.AuthorityRecords}, runs {c.Runs}");
        return 0;
    }

    case "import-overrides":
    {
        var path = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        if (path is null || !File.Exists(path)) { Console.Error.WriteLine("Give the path of a pmc-lux authority-strings JSON file."); return 2; }
        var defaultClass = Enum.TryParse<EntityClass>(Option("--class"), true, out var cls) ? cls
            : Path.GetFileNameWithoutExtension(path).Contains("people", StringComparison.OrdinalIgnoreCase) ? EntityClass.Person : EntityClass.Group;
        await store.InitialiseAsync();
        var result = await PmcLuxOverrides.Load(path).ImportAsync(store, scheme, dataset, "pmc-lux/" + Path.GetFileName(path), defaultClass);
        Console.WriteLine($"entities added {result.EntitiesAdded}, already present {result.EntitiesExisting}; equivalents {result.Equivalents}; overrides {result.Overrides} (targets without an authority record {result.UnknownTargets})");
        return 0;
    }

    case "import-legacy":
    {
        var legacy = Environment.GetEnvironmentVariable("LINKEDART_LEGACY_PG");
        if (string.IsNullOrWhiteSpace(legacy)) { Console.Error.WriteLine("Set LINKEDART_LEGACY_PG to the earlier PMC database's connection string."); return 2; }
        await store.InitialiseAsync();
        await using var source = NpgsqlDataSource.Create(legacy);
        var options = new LegacyImportOptions(ReuseByEquivalent: args.Contains("--reuse-by-equivalent"), OnlyReferenced: args.Contains("--only-referenced"), DatasetVersion: Option("--version") ?? "legacy");
        var result = await new LegacyPmcImporter(source).ImportAsync(store, scheme, options, dataset);
        Console.WriteLine($"entities added {result.EntitiesAdded}, already present {result.EntitiesExisting}, reused through a shared authority id {result.EntitiesReused}, skipped for unknown type {result.SkippedForType}; equivalents {result.Equivalents}");
        Console.WriteLine($"strings {result.Strings}: decided {result.Decided}, unresolved {result.Unresolved} (of which pointing at a missing authority {result.DanglingAuthority})");
        return 0;
    }

    case "load-getty":
    {
        var optionValues = new HashSet<string>(new[] { Option("--vocab"), Option("--version"), Option("--dataset"), Option("--base") }.OfType<string>(), StringComparer.Ordinal);
        var files = args.Skip(1).Where(a => !a.StartsWith("--", StringComparison.Ordinal) && !optionValues.Contains(a)).SelectMany(GettyVocabLoader.Files).ToList();
        if (files.Count == 0) { Console.Error.WriteLine("Give Getty N-Triples files (.nt or .nt.gz) or the directory a Getty zip unpacked to."); return 2; }
        if (!Enum.TryParse<GettyVocabulary>(Option("--vocab"), true, out var vocab)) { Console.Error.WriteLine("Say which vocabulary: --vocab ulan|aat|tgn"); return 2; }
        await store.InitialiseAsync();
        var progress = new Progress<string>(m => Console.Error.WriteLine($"  {m}"));
        Console.Error.WriteLine($"  {files.Count} file(s)");
        var result = await new GettyVocabLoader(store).LoadAsync(files, vocab, Option("--version"), progress);
        Console.WriteLine($"{result.Vocabulary}: {result.TriplesRead:N0} triples read, {result.TriplesKept:N0} used, {result.Records:N0} records in {result.Elapsed:hh\\:mm\\:ss}");
        return 0;
    }

    case "reconcile":
    {
        await store.InitialiseAsync();
        var limit = int.TryParse(Option("--limit"), out var l) ? l : 1000;
        var options = new ReconcilerOptions
        {
            AcceptScore = double.TryParse(Option("--accept"), System.Globalization.CultureInfo.InvariantCulture, out var a) ? a : 0.9,
            ReviewScore = double.TryParse(Option("--review"), System.Globalization.CultureInfo.InvariantCulture, out var rv) ? rv : 0.6,
            AcceptSingleSourceWithDates = !args.Contains("--no-single-source"),
            MintWhenNothingFound = !args.Contains("--no-mint"),
            Contexts = PmcContexts(),
        };
        var sources = new IAuthoritySource[] { new LocalIndexSource(store, "ulan"), new LocalIndexSource(store, "aat"), new LocalIndexSource(store, "tgn"), new LocalIndexSource(store, "loc") };
        var run = await store.StartRunAsync(dataset, System.Text.Json.JsonSerializer.Serialize(options));
        var reconciler = new Reconciler(store, scheme, sources, options);
        var progress = new Progress<string>(m => Console.Error.WriteLine($"  {m}"));
        var report = await reconciler.RunAsync(dataset, limit, run.Id, progress);
        await store.FinishRunAsync(run.Id, System.Text.Json.JsonSerializer.Serialize(report), report.ToString());
        Console.WriteLine($"run {run.Id}: {report}");
        return 0;
    }

    case "decisions":
    {
        var runId = Option("--run");
        if (runId is null) { Console.Error.WriteLine("Give the run: --run run-..."); return 2; }
        var limit = int.TryParse(Option("--limit"), out var l) ? l : 20;
        Console.WriteLine("by context and method:");
        foreach (var row in await store.RunBreakdownAsync(runId)) Console.WriteLine($"  {row.Context,-16} {row.Method,-12} {row.Count}");
        Console.WriteLine();
        foreach (var item in await store.DecisionsForRunAsync(runId, Option("--method"), limit))
        {
            Console.WriteLine($"[{item.String.Context}] {item.String.Raw}");
            Console.WriteLine($"   -> {item.Decision.Method}  {item.Decision.EntityId}");
            if (item.Decision.EvidenceJson is { } ev)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(ev);
                var root = doc.RootElement;
                if (root.TryGetProperty("reading", out var reading)) Console.WriteLine($"   read as: {reading.GetProperty("text").GetString()}" + (reading.TryGetProperty("lifespan", out var ls) && ls.ValueKind == System.Text.Json.JsonValueKind.String ? $" [{ls.GetString()}]" : "") + $" ({string.Join("/", reading.GetProperty("classes").EnumerateArray().Select(c => c.GetString()))})");
                if (root.TryGetProperty("groups", out var groups))
                    foreach (var g in groups.EnumerateArray().Take(3))
                        foreach (var m in g.GetProperty("members").EnumerateArray().Take(3))
                            Console.WriteLine($"   {m.GetProperty("score").GetDouble():0.00} {m.GetProperty("source").GetString(),-6} {m.GetProperty("label").GetString()}  <{m.GetProperty("uri").GetString()}>  {m.GetProperty("reason").GetString()}");
            }
        }
        return 0;
    }

    default:
        Usage();
        return 2;
}

// How the PMC library's fields are read (the old source_string_map sources). Other datasets will carry this in their manifest (step 4).
static Dictionary<string, ContextRule> PmcContexts() => new(StringComparer.OrdinalIgnoreCase)
{
    ["persauthorfull"] = new([EntityClass.Person], "default"),
    ["corpauthor"] = new([EntityClass.Group], "default"),
    ["publisher"] = new([EntityClass.Group], "default"),
    ["place"] = new([EntityClass.Place]),
    ["keywords"] = new([EntityClass.Type, EntityClass.Person, EntityClass.Group, EntityClass.Place]),
};

static void Usage() => Console.WriteLine(
    """
    Align: the alignment store.
      init                                   create or upgrade the schema (LINKEDART_PG)
      stats [--dataset pmc|all]              counts
      import-overrides <file> [--dataset] [--base] [--class]
                                             import a pmc-lux authority-strings file as entities and overrides
      load-getty <file|dir>... --vocab ulan|aat|tgn [--version v]
                                             load a Getty full N-Triples download into the local authority index
      reconcile [--dataset] [--limit n] [--accept 0.9] [--review 0.6] [--no-single-source] [--no-mint]
                                             decide undecided strings from overrides, the store and the local indexes
      decisions --run <id> [--method m] [--limit 20]
                                             a run's decisions by context and method, then a sample with evidence
      import-legacy [--dataset] [--base]     import the earlier PMC tables (LINKEDART_LEGACY_PG)
        [--reuse-by-equivalent] [--only-referenced] [--version v]
                                             for a second copy of those tables: map authorities onto entities that share
                                             an authority id, add only authorities a new decision needs, tag the version
    """);
