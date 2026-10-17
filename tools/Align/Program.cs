// The alignment toolkit's command line (design doc 10). Step 2 scope: the store and its importers.
//
//   dotnet run --project tools/Align -- init                                   create or upgrade the schema
//   dotnet run --project tools/Align -- stats [--dataset pmc]                  counts
//   dotnet run --project tools/Align -- import-overrides <file.json> [--dataset pmc] [--base https://data.paul-mellon-centre.ac.uk/] [--class Group]
//                                                                              a pmc-lux authority-strings file
//   dotnet run --project tools/Align -- import-legacy [--dataset pmc] [--base ...]
//                                                                              the owner's earlier authorities and source_string_map tables
//
// Connection strings come from the environment, never from arguments or files in the repository:
//   LINKEDART_PG         the store (e.g. "Host=localhost;Database=linkedart;Username=...;Password=...")
//   LINKEDART_LEGACY_PG  the earlier PMC database, for import-legacy

using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Import;
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
        var result = await new LegacyPmcImporter(source).ImportAsync(store, scheme, dataset);
        Console.WriteLine($"entities added {result.EntitiesAdded}, already present {result.EntitiesExisting}, skipped for unknown type {result.SkippedForType}; equivalents {result.Equivalents}");
        Console.WriteLine($"strings {result.Strings}: decided {result.Decided}, unresolved {result.Unresolved} (of which pointing at a missing authority {result.DanglingAuthority})");
        return 0;
    }

    default:
        Usage();
        return 2;
}

static void Usage() => Console.WriteLine(
    """
    Align: the alignment store.
      init                                   create or upgrade the schema (LINKEDART_PG)
      stats [--dataset pmc|all]              counts
      import-overrides <file> [--dataset] [--base] [--class]
                                             import a pmc-lux authority-strings file as entities and overrides
      import-legacy [--dataset] [--base]     import the earlier PMC tables (LINKEDART_LEGACY_PG)
    """);
