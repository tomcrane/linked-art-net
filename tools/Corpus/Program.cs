// The corpus CLI.
//
//   dotnet run --project tools/Corpus -- validate            schema, unique ids, parseable canonical strings, ordered bounds, known flags
//   dotnet run --project tools/Corpus -- run [--all|--status verified|proposed] [--write]   evaluate; --write updates corpus/report.md and report.json
//   dotnet run --project tools/Corpus -- stats               counts by file, status and tag
//   dotnet run --project tools/Corpus -- promote <file> [id ...] [--to verified|disputed|proposed] [--note <text>] [--dry-run]
//                                                           set entry statuses in place (whole file when no ids); verified refuses failing entries; refreshes the report
//   dotnet run --project tools/Corpus -- review <file ...> [--match <regex>] [--tag <tag>] [--failing] [--out <markdown>]
//                                                           Markdown review of the files (or the entries a filter selects); corpus-review.yml posts it
//   dotnet run --project tools/Corpus -- accept <file> [id ...] [--match <regex>] [--tag <tag>] [--verify] [--dry-run]
//                                                           adopt the parser's reading as the expectation of failing entries, keeping the source's in sourceExpectation
//   dotnet run --project tools/Corpus -- import yearrange <date-tests.json> | import timetwister <dates_spec.rb> | import getty <dir of ulan-*.json>
//                                                           write corpus/imported/<source>.json (proposed; see THIRD-PARTY-NOTICES.md)
//
// Exit code 1 when validation fails or when a verified entry fails; proposed and disputed entries are informative.

using System.Text;
using System.Text.Json;
using LinkedArtNet;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Corpus;

var command = args.Length > 0 ? args[0] : "run";
var repoRoot = FindRepoRoot();
var corpusRoot = Path.Combine(repoRoot, "corpus");
var schemaPath = Path.Combine(corpusRoot, "schema", "corpus-entry.schema.json");

if (command == "tokens") return Tokens(args.Length > 2 ? args[1] : "default", args.Length > 2 ? args[2] : args.Length > 1 ? args[1] : "");
if (command == "advise") return Advise(args.Length > 1 ? args[1] : throw new ArgumentException("advise needs a file of strings, one per line"));
if (command == "import") return await Importers.Run(args.Length > 1 ? args[1] : "", args.Length > 2 ? args[2] : null, corpusRoot);
if (command == "promote") return Promote(args.Skip(1).ToArray());
if (command == "review") return Review(args.Skip(1).ToArray());
if (command == "accept") return Accept(args.Skip(1).ToArray());

return command switch
{
    "validate" => Validate(),
    "run" => Run(args.Contains("--all") ? null : StatusArg(args) ?? EntryStatus.Verified, args.Contains("--write")),
    "stats" => Stats(),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Usage: Corpus validate | run [--all | --status verified|proposed|disputed] [--write] | stats | promote <file> [id ...] [--to verified|disputed|proposed] [--note <text>] [--dry-run] | review <file ...> [--match <regex>] [--tag <tag>] [--failing] [--out <markdown>] | accept <file> [id ...] [--match <regex>] [--tag <tag>] [--verify] [--dry-run] | import yearrange|timetwister <path> | import getty <dir of ulan-*.json>");
    return 2;
}

static EntryStatus? StatusArg(string[] args)
{
    var i = Array.IndexOf(args, "--status");
    return i >= 0 && i + 1 < args.Length && Enum.TryParse<EntryStatus>(args[i + 1], true, out var s) ? s : null;
}

int Validate()
{
    var problems = new List<string>();
    var ids = new HashSet<string>(StringComparer.Ordinal);
    foreach (var path in Directory.GetFiles(corpusRoot, "*.json", SearchOption.AllDirectories).Where(p => !p.Contains("schema") && Path.GetFileName(p) != "report.json").Order())
    {
        var relative = Path.GetRelativePath(corpusRoot, path).Replace('\\', '/');
        foreach (var error in CorpusFile.SchemaErrors(path, schemaPath)) problems.Add($"{relative}: {error}");
        CorpusFile file;
        try { file = CorpusFile.Load(path, corpusRoot); }
        catch (Exception ex) { problems.Add($"{relative}: {ex.Message}"); continue; }

        foreach (var e in file.Entries)
        {
            if (!ids.Add(e.Id)) problems.Add($"{relative}: duplicate id {e.Id}");
            if (e.Expect.Canonical is { } c && !DateExpression.TryParse(c, out _)) problems.Add($"{relative} {e.Id}: canonical '{c}' does not parse");
            foreach (var flag in (e.Expect.Flags ?? []).Concat(e.Expect.NotFlags ?? []))
                if (!Enum.TryParse<DateFlag>(flag, true, out _)) problems.Add($"{relative} {e.Id}: unknown flag '{flag}'");
            foreach (var (name, span) in new[] { ("timespan", e.Expect.TimeSpan), ("begin", e.Expect.Begin), ("end", e.Expect.End) })
            {
                if (span is null) continue;
                var bounds = new[] { span.BeginOfTheBegin, span.EndOfTheBegin, span.BeginOfTheEnd, span.EndOfTheEnd }.Where(b => b is not null).Select(b => LinkedArtDate.Parse(b!)).ToList();
                for (var k = 1; k < bounds.Count; k++)
                    if (bounds[k] < bounds[k - 1]) problems.Add($"{relative} {e.Id}: {name} bounds are not in order");
            }
            if (e.Expect.Status is "parsed" or "partial" && e.Expect.TimeSpan is null && e.Expect.Begin is null && e.Expect.End is null && e.Expect.Years is null && e.Expect.Canonical is null && e.Expect.Kind is null)
                problems.Add($"{relative} {e.Id}: a parsed expectation needs a canonical, a span, years or a kind");
        }
        Console.WriteLine($"{relative}: {file.Entries.Count} entries");
    }
    foreach (var p in problems) Console.WriteLine("  ! " + p);
    Console.WriteLine(problems.Count == 0 ? $"OK: {ids.Count} entries, no problems." : $"{problems.Count} problem(s).");
    return problems.Count == 0 ? 0 : 1;
}

int Run(EntryStatus? status, bool write)
{
    var entries = CorpusFile.LoadAll(corpusRoot).SelectMany(f => f.Entries).Where(e => status is null || e.Status == status).ToList();
    var catalog = LinkedArtNet.Dates.Realisation.ProfileCatalog.Load(Path.Combine(repoRoot, "profiles"));
    var results = new CorpusEvaluator(catalog.Get).Evaluate(entries);
    var reportJsonPath = Path.Combine(corpusRoot, "report.json");
    var previous = File.Exists(reportJsonPath)
        ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(reportJsonPath))
        : null;

    var report = CorpusEvaluator.Report(results, previous);
    Console.WriteLine(report);

    if (write)
    {
        var header = $"# Corpus run\n\nGenerated by `dotnet run --project tools/Corpus -- run --all --write` with parser {TimeSpanParser.Version}. " +
                     "Verified entries gate CI; proposed and disputed ones are informative.\n\n";
        File.WriteAllText(Path.Combine(corpusRoot, "report.md"), header + report, new UTF8Encoding(false));
        var state = results.OrderBy(r => r.Entry.Id, StringComparer.Ordinal).ToDictionary(r => r.Entry.Id, r => r.Passed, StringComparer.Ordinal);
        File.WriteAllText(reportJsonPath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
        Console.WriteLine("-> corpus/report.md, corpus/report.json");
    }
    return results.Any(r => !r.Passed && r.Entry.Status == EntryStatus.Verified) ? 1 : 0;
}

string? ResolveCorpusPath(string given) => new[] { given, Path.Combine(corpusRoot, given), Path.Combine(repoRoot, given) }.FirstOrDefault(File.Exists);

int Review(string[] rest)
{
    string? outPath = null, match = null, tag = null;
    var failingOnly = false;
    var files = new List<string>();
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i] == "--out" && i + 1 < rest.Length) outPath = rest[++i];
        else if (rest[i] == "--match" && i + 1 < rest.Length) match = rest[++i];
        else if (rest[i] == "--tag" && i + 1 < rest.Length) tag = rest[++i];
        else if (rest[i] == "--failing") failingOnly = true;
        else if (rest[i].StartsWith("--")) { Console.Error.WriteLine($"review: unknown option {rest[i]}"); return 2; }
        else files.Add(rest[i]);
    }
    if (files.Count == 0) { Console.Error.WriteLine("review needs one or more corpus files: review <file ...> [--match <regex>] [--tag <tag>] [--failing] [--out <markdown>]"); return 2; }
    var loaded = new List<CorpusFile>();
    foreach (var given in files)
    {
        var path = ResolveCorpusPath(given);
        if (path is null) { Console.Error.WriteLine($"No such corpus file: {given}"); return 2; }
        loaded.Add(CorpusFile.Load(path, corpusRoot));
    }
    var catalog = LinkedArtNet.Dates.Realisation.ProfileCatalog.Load(Path.Combine(repoRoot, "profiles"));
    var evaluator = new CorpusEvaluator(catalog.Get);
    if (EntryFilter(match, tag, failingOnly, evaluator) is { } filter)
        loaded = loaded.Select(f => f with { Entries = f.Entries.Where(filter).ToList() }).Where(f => f.Entries.Count > 0).ToList();
    var markdown = CorpusReview.Markdown(loaded, evaluator, TimeSpanParser.Version);
    if (outPath is null) Console.Write(markdown);
    else
    {
        File.WriteAllText(outPath, markdown, new UTF8Encoding(false));
        Console.WriteLine($"-> {outPath}");
    }
    return 0;
}

// accept <file> [id ...] [--match <regex>] [--tag <tag>] [--verify] [--dry-run]: adopt the parser's reading where the source was wrong.
int Accept(string[] rest)
{
    if (rest.Length == 0 || rest[0].StartsWith("--")) { Console.Error.WriteLine("accept needs a corpus file: accept <file> [id ...] [--match <regex>] [--tag <tag>] [--verify] [--dry-run]"); return 2; }
    var path = ResolveCorpusPath(rest[0]);
    if (path is null) { Console.Error.WriteLine($"No such corpus file: {rest[0]}"); return 2; }
    string? match = null, tag = null;
    var ids = new List<string>();
    var verify = false; var dryRun = false;
    for (var i = 1; i < rest.Length; i++)
    {
        switch (rest[i])
        {
            case "--match" when i + 1 < rest.Length: match = rest[++i]; break;
            case "--tag" when i + 1 < rest.Length: tag = rest[++i]; break;
            case "--verify": verify = true; break;
            case "--dry-run": dryRun = true; break;
            case var o when o.StartsWith("--"): Console.Error.WriteLine($"accept: unknown option {o}"); return 2;
            default: ids.Add(rest[i]); break;
        }
    }
    var filter = EntryFilter(match, tag, failingOnly: false);
    var catalog = LinkedArtNet.Dates.Realisation.ProfileCatalog.Load(Path.Combine(repoRoot, "profiles"));
    var outcome = CorpusAcceptance.Accept(path, ids.Count == 0 ? null : ids, filter, verify ? EntryStatus.Verified : null, new CorpusEvaluator(catalog.Get), dryRun);
    var relative = Path.GetRelativePath(corpusRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    foreach (var c in outcome.Changed) Console.WriteLine($"{c.Id} \"{c.Input}\"\n    was: {c.Before}\n    now: {c.After}");
    Console.WriteLine();
    Console.WriteLine($"{(dryRun ? "Would accept" : "Accepted")} the parser's reading for {outcome.Changed.Count} entries in {relative}" +
                      (outcome.AlreadyPassing.Count > 0 ? $"; {outcome.AlreadyPassing.Count} targeted entries already pass" : "") +
                      (verify && outcome.Changed.Count > 0 ? "; set to verified" : "") + ".");
    if (dryRun || outcome.Changed.Count == 0) return 0;
    Console.WriteLine();
    return Run(null, true);
}

/// <summary>A filter over entries from the review/accept options: a regex on the input, a tag, or only the failing ones.</summary>
Func<CorpusEntry, bool>? EntryFilter(string? match, string? tag, bool failingOnly, CorpusEvaluator? evaluator = null)
{
    var regex = match is null ? null : new System.Text.RegularExpressions.Regex(match, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    if (regex is null && tag is null && !failingOnly) return null;
    return e => (regex is null || regex.IsMatch(e.Input)) && (tag is null || e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) && (!failingOnly || !evaluator!.Evaluate(e).Passed);
}

int Promote(string[] rest)
{
    if (rest.Length == 0 || rest[0].StartsWith("--")) { Console.Error.WriteLine("promote needs a corpus file: promote <file> [id ...] [--to verified|disputed|proposed] [--note <text>] [--dry-run]"); return 2; }
    var to = EntryStatus.Verified;
    string? note = null;
    var dryRun = false;
    var ids = new List<string>();
    for (var i = 1; i < rest.Length; i++)
    {
        switch (rest[i])
        {
            case "--to" when i + 1 < rest.Length && Enum.TryParse(rest[i + 1], true, out EntryStatus parsed): to = parsed; i++; break;
            case "--to": Console.Error.WriteLine("--to needs verified, disputed or proposed"); return 2;
            case "--note" when i + 1 < rest.Length: note = rest[++i]; break;
            case "--dry-run": dryRun = true; break;
            case var o when o.StartsWith("--"): Console.Error.WriteLine($"promote: unknown option {o}"); return 2;
            default: ids.Add(rest[i]); break;
        }
    }
    var given = rest[0];
    var path = ResolveCorpusPath(given);
    if (path is null) { Console.Error.WriteLine($"No such corpus file: {given}"); return 2; }

    var catalog = LinkedArtNet.Dates.Realisation.ProfileCatalog.Load(Path.Combine(repoRoot, "profiles"));
    var outcome = CorpusPromotion.Promote(path, ids.Count == 0 ? null : ids, to, new CorpusEvaluator(catalog.Get), dryRun, note);
    var relative = Path.GetRelativePath(corpusRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    foreach (var f in outcome.Failures)
    {
        Console.WriteLine($"FAIL {f.Entry.Id} \"{f.Entry.Input}\" -> {f.Result.Canonical ?? f.Result.Status.ToString()}");
        foreach (var reason in f.Failures) Console.WriteLine("     " + reason);
    }
    if (!outcome.Applied)
    {
        Console.WriteLine($"Not promoted: {outcome.Failures.Count} of the entries fail. Fix the expectation or mark them disputed (--to disputed).");
        return 1;
    }
    var status = to.ToString().ToLowerInvariant();
    if (outcome.Changed.Count == 0) { Console.WriteLine($"{relative}: nothing to do, {outcome.Unchanged.Count} entries already {status}."); return 0; }
    Console.WriteLine($"{(dryRun ? "Would set" : "Set")} {outcome.Changed.Count} entries to {status} in {relative}" +
                      (outcome.Unchanged.Count > 0 ? $" ({outcome.Unchanged.Count} already were)" : "") + ":");
    foreach (var id in outcome.Changed) Console.WriteLine("  " + id);
    if (dryRun) return 0;
    Console.WriteLine();
    return Run(null, true);
}

int Tokens(string profileName, string text)
{
    var catalog = LinkedArtNet.Dates.Realisation.ProfileCatalog.Load(Path.Combine(repoRoot, "profiles"));
    var profile = catalog[profileName];
    var lexicon = LinkedArtNet.Dates.Text.Lexicon.Merge(profile.Languages.Select(LinkedArtNet.Dates.Text.Lexicon.Load));
    var normalised = LinkedArtNet.Dates.Text.Normaliser.Normalise(text);
    Console.WriteLine($"normalised: \"{normalised.Text}\" flags: {string.Join(",", normalised.Flags)}");
    foreach (var tok in new LinkedArtNet.Dates.Text.Tokenizer(lexicon).Tokenize(normalised.Text)) Console.WriteLine($"  {tok.Kind,-12} \"{tok.Text}\" {tok.Value}");
    var result = new LinkedArtNet.Dates.TimeSpanParser(profile).Parse(text);
    Console.WriteLine($"-> {result.Status} {result.Canonical} ({result.Strategy}, {result.Confidence:0.00})");
    foreach (var n in result.Notes) Console.WriteLine("  " + n);
    return 0;
}

int Advise(string path)
{
    var report = LinkedArtNet.Dates.Realisation.ProfileAdvisor.Analyse(File.ReadLines(path));
    Console.WriteLine(report.ToMarkdown());
    return 0;
}

int Stats()
{
    var files = CorpusFile.LoadAll(corpusRoot);
    var all = files.SelectMany(f => f.Entries).ToList();
    foreach (var f in files)
        Console.WriteLine($"{f.Path,-45} {f.Entries.Count,5}  {string.Join(", ", f.Entries.GroupBy(e => e.Status).Select(g => $"{g.Key.ToString().ToLowerInvariant()} {g.Count()}"))}  [{f.Source.Licence}]");
    Console.WriteLine($"{"total",-45} {all.Count,5}");
    Console.WriteLine();
    Console.WriteLine("Tags: " + string.Join(", ", all.SelectMany(e => e.Tags).GroupBy(t => t).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
    return 0;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (linked-art-net.slnx).");
}
