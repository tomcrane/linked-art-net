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
//   dotnet run --project tools/Corpus -- periodo [--from <dataset.json[.gz]>]
//                                                           download https://data.perio.do/dataset/ (or compact a local copy) into periods/periodo.json
//   dotnet run --project tools/Corpus -- agents [--all]      evaluate the agent corpus (corpus/agents); verified entries gate, the rest inform
//   dotnet run --project tools/Corpus -- interpret (--strings <file> | --capture <jsonl> | --corpus <file> [--failing] [--status disputed]) [--profile <name>] [--all] [--out <corpus file>] [--model <id>] [--limit <n>] [--dry-run]
//                                                           ask the Claude interpreter (ANTHROPIC_API_KEY) for the strings the parser cannot read; write them as proposed
//                                                           entries for review, or, over corpus entries, report agreement with the expectations
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
if (command == "periodo") return await PeriodO(args.Skip(1).ToArray());
if (command == "interpret") return await Interpret(args.Skip(1).ToArray());
if (command == "agents") return Agents(args.Skip(1).ToArray());

return command switch
{
    "validate" => Validate(),
    "run" => Run(args.Contains("--all") ? null : StatusArg(args) ?? EntryStatus.Verified, args.Contains("--write")),
    "stats" => Stats(),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Usage: Corpus validate | run [--all | --status verified|proposed|disputed] [--write] | stats | promote <file> [id ...] [--to verified|disputed|proposed] [--note <text>] [--dry-run] | review <file ...> [--match <regex>] [--tag <tag>] [--failing] [--out <markdown>] | accept <file> [id ...] [--match <regex>] [--tag <tag>] [--verify] [--dry-run] | import yearrange|timetwister <path> | import getty <dir of ulan-*.json> | periodo [--from <dataset.json[.gz]>] | interpret (--strings <file> | --capture <jsonl> | --corpus <file> [--failing] [--status s]) [--profile p] [--all] [--out <file>] [--model id] [--limit n] [--dry-run]");
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
    foreach (var path in Directory.GetFiles(corpusRoot, "*.json", SearchOption.AllDirectories).Where(p => !p.Contains("schema") && !p.Replace('\\', '/').Contains("/agents/") && Path.GetFileName(p) != "report.json").Order())
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
    var results = new CorpusEvaluator(catalog.Get, periods: Periods()).Evaluate(entries);
    var reportJsonPath = Path.Combine(corpusRoot, "report.json");
    var previous = File.Exists(reportJsonPath)
        ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(reportJsonPath))
        : null;

    var report = CorpusEvaluator.Report(results, previous);
    Console.WriteLine(report);

    if (write)
    {
        var header = $"# Corpus run\n\nGenerated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC by `dotnet run --project tools/Corpus -- run --all --write` with parser {TimeSpanParser.Version}. " +
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
    var evaluator = new CorpusEvaluator(catalog.Get, periods: Periods());
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
    var outcome = CorpusAcceptance.Accept(path, ids.Count == 0 ? null : ids, filter, verify ? EntryStatus.Verified : null, new CorpusEvaluator(catalog.Get, periods: Periods()), dryRun);
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
    var outcome = CorpusPromotion.Promote(path, ids.Count == 0 ? null : ids, to, new CorpusEvaluator(catalog.Get, periods: Periods()), dryRun, note);
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

/// <summary>The built-in period tables plus the repository's PeriodO file, as the site and the service use them.</summary>
LinkedArtNet.Dates.Periods.INamedPeriodSource Periods() => LinkedArtNet.Dates.Periods.PeriodOSource.ForRepository(repoRoot);

// periodo [--from <dataset.json[.gz]>]: download (or read) the full PeriodO dataset and write the compact periods/periodo.json.
async Task<int> PeriodO(string[] rest)
{
    string? from = null;
    for (var i = 0; i < rest.Length; i++)
    {
        if (rest[i] == "--from" && i + 1 < rest.Length) from = rest[++i];
        else { Console.Error.WriteLine($"periodo: unknown option {rest[i]}"); return 2; }
    }
    byte[] bytes; string version; string modified;
    if (from is not null)
    {
        bytes = await File.ReadAllBytesAsync(from);
        version = "local:" + Path.GetFileName(from);
        modified = File.GetLastWriteTimeUtc(from).ToString("yyyy-MM-dd");
    }
    else
    {
        using var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All });
        http.DefaultRequestHeaders.UserAgent.ParseAdd("linked-art-net-corpus-tool/1.0 (+https://github.com/tomcrane/linked-art-net)");
        http.Timeout = TimeSpan.FromMinutes(3);
        Console.WriteLine($"Downloading {LinkedArtNet.Dates.Periods.PeriodOData.DatasetUrl} ...");
        using var response = await http.GetAsync(LinkedArtNet.Dates.Periods.PeriodOData.DatasetUrl);
        response.EnsureSuccessStatusCode();
        bytes = await response.Content.ReadAsByteArrayAsync();
        version = response.Headers.ETag?.Tag.Trim('"').Replace("W/", "").Trim('"') ?? "unknown";
        modified = (response.Content.Headers.LastModified ?? DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");
    }
    Stream stream = new MemoryStream(bytes);
    if (bytes.Length > 2 && bytes[0] == 0x1f && bytes[1] == 0x8b) stream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress);
    LinkedArtNet.Dates.Periods.PeriodOData data;
    using (stream) data = LinkedArtNet.Dates.Periods.PeriodOData.FromDataset(stream, version, modified);
    var outPath = Path.Combine(repoRoot, "periods", "periodo.json");
    data.Save(outPath);
    var size = new FileInfo(outPath).Length;
    Console.WriteLine($"Wrote {Path.GetRelativePath(repoRoot, outPath)}: {data.Periods.Count} periods from {data.Authorities.Count} authorities, version {data.Version}, modified {data.Modified}, {size / 1024} KB.");
    var shared = data.Periods.GroupBy(p => LinkedArtNet.Dates.Periods.JsonNamedPeriodSource.Normalise(p.Label)).Count(g => g.Count() > 1);
    Console.WriteLine($"{shared} labels are defined by more than one authority and resolve to a hull unless narrowed.");
    return 0;
}

// interpret: the LLM as a corpus-growing assistant. Strings the deterministic parser cannot read (or all of them with
// --all) go to the Claude interpreter; its readings come back as proposed corpus entries for a reviewer to confirm in
// the trainer or by promoting the file. Over existing corpus entries it reports where the interpreter agrees with the
// expectation, which is how its value is measured before anyone trusts it. Never touches verified entries.
async Task<int> Interpret(string[] rest)
{
    string? strings = null, capture = null, corpusFile = null, outPath = null, profileName = null, model = null, status = null;
    var all = false; var failing = false; var dryRun = false; var limit = int.MaxValue;
    for (var i = 0; i < rest.Length; i++)
    {
        switch (rest[i])
        {
            case "--strings" when i + 1 < rest.Length: strings = rest[++i]; break;
            case "--capture" when i + 1 < rest.Length: capture = rest[++i]; break;
            case "--corpus" when i + 1 < rest.Length: corpusFile = rest[++i]; break;
            case "--out" when i + 1 < rest.Length: outPath = rest[++i]; break;
            case "--profile" when i + 1 < rest.Length: profileName = rest[++i]; break;
            case "--model" when i + 1 < rest.Length: model = rest[++i]; break;
            case "--status" when i + 1 < rest.Length: status = rest[++i]; break;
            case "--limit" when i + 1 < rest.Length && int.TryParse(rest[i + 1], out var limitValue): limit = limitValue; i++; break;
            case "--all": all = true; break;
            case "--failing": failing = true; break;
            case "--dry-run": dryRun = true; break;
            default: Console.Error.WriteLine($"interpret: unknown option {rest[i]}"); return 2;
        }
    }
    if ((strings is null ? 0 : 1) + (capture is null ? 0 : 1) + (corpusFile is null ? 0 : 1) != 1)
    {
        Console.Error.WriteLine("interpret needs exactly one of --strings <file>, --capture <jsonl>, --corpus <file>.");
        return 2;
    }
    if (!dryRun && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
    {
        Console.Error.WriteLine("ANTHROPIC_API_KEY is not set. Set it (a key from the Console organisation that holds your credits), or add --dry-run to see what would be asked.");
        return 2;
    }

    var catalog = LinkedArtNet.Dates.Realisation.ProfileCatalog.Load(Path.Combine(repoRoot, "profiles"));
    var periods = Periods();
    var parsers = new Dictionary<string, TimeSpanParser>(StringComparer.OrdinalIgnoreCase);
    TimeSpanParser ParserFor(string name) => parsers.TryGetValue(name, out var p) ? p : parsers[name] = new TimeSpanParser(catalog.TryGet(name, out var profile) ? profile : LinkedArtNet.Dates.Realisation.ParseProfile.Default, periods: periods);

    // What to ask about: (input, profile, intent, expectation when measuring).
    var items = new List<(string Input, string Profile, string Intent, CorpusEntry? Entry)>();
    if (strings is not null)
        items.AddRange(File.ReadLines(strings).Select(l => l.Trim()).Where(l => l.Length > 0 && l.Length <= 500).Distinct(StringComparer.Ordinal).Select(l => (l, profileName ?? "default", "auto", (CorpusEntry?)null)));
    else if (capture is not null)
    {
        foreach (var line in File.ReadLines(capture))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            System.Text.Json.Nodes.JsonNode? node;
            try { node = System.Text.Json.Nodes.JsonNode.Parse(line); } catch (JsonException) { continue; }
            if (node?["input"]?.GetValue<string>() is { Length: > 0 and <= 500 } input)
                items.Add((input, profileName ?? node["profile"]?.GetValue<string>() ?? "default", "auto", null));
        }
        items = items.DistinctBy(i => (i.Input, i.Profile)).ToList();
    }
    else
    {
        var path = ResolveCorpusPath(corpusFile!);
        if (path is null) { Console.Error.WriteLine($"No such corpus file: {corpusFile}"); return 2; }
        var file = CorpusFile.Load(path, corpusRoot);
        var evaluator = new CorpusEvaluator(catalog.Get, periods: periods);
        IEnumerable<CorpusEntry> entries = file.Entries;
        if (Enum.TryParse<EntryStatus>(status, true, out var wanted)) entries = entries.Where(e => e.Status == wanted);
        if (failing) entries = entries.Where(e => !evaluator.Evaluate(e).Passed);
        items.AddRange(entries.Select(e => (e.Input, e.Profile, e.Intent, (CorpusEntry?)e)));
        all = true; // over corpus entries the question is agreement, so every selected entry is asked
    }

    // Ask only where the deterministic parser is weak, unless --all.
    var asked = new List<(string Input, string Profile, string Intent, CorpusEntry? Entry, TimeSpanParseResult Deterministic)>();
    var confident = 0;
    foreach (var item in items)
    {
        var parser = ParserFor(item.Profile);
        var deterministic = parser.Parse(item.Input, Trainer_ParseIntent(item.Intent));
        if (!all && !TimeSpanParser.NeedsInterpreter(deterministic, parser.Profile)) { confident++; continue; }
        asked.Add((item.Input, item.Profile, item.Intent, item.Entry, deterministic));
        if (asked.Count >= limit) break;
    }
    Console.WriteLine($"{items.Count} strings; {confident} the parser reads confidently; {asked.Count} to ask{(dryRun ? " (dry run, nothing sent)" : "")}.");
    if (dryRun)
    {
        foreach (var a in asked.Take(50)) Console.WriteLine($"  \"{a.Input}\" [{a.Profile}] parser: {a.Deterministic.Status.ToString().ToLowerInvariant()} {a.Deterministic.Canonical}");
        if (asked.Count > 50) Console.WriteLine($"  ... and {asked.Count - 50} more");
        return 0;
    }
    if (asked.Count == 0) return 0;

    var interpreter = new LinkedArtNet.Dates.Llm.ClaudeDateInterpreter(new LinkedArtNet.Dates.Llm.ClaudeInterpreterOptions { Model = model ?? new LinkedArtNet.Dates.Llm.ClaudeInterpreterOptions().Model });
    var proposals = new List<CorpusEntry>();
    int agreed = 0, disagreed = 0, sameAsParserCount = 0, noAnswer = 0, n = 0;
    var started = DateTime.UtcNow;
    foreach (var a in asked)
    {
        n++;
        var parser = ParserFor(a.Profile);
        var result = await parser.ParseAsync(a.Input, interpreter, new ParseOptions { Intent = Trainer_ParseIntent(a.Intent), Profile = parser.Profile, AlwaysInterpret = all });
        var interpreted = result.Flags.Contains(DateFlag.Interpreted);
        var rationale = result.Notes.FirstOrDefault(x => x.StartsWith("claude:", StringComparison.Ordinal))?["claude:".Length..].Trim()
                        ?? result.Notes.FirstOrDefault(x => x.Contains("interpreter", StringComparison.Ordinal)); // or why there was no reading
        if (a.Entry is { } entry)
        {
            // What there is to compare with: the expectation's canonical form when it has one, else its years or status.
            var expected = entry.Expect.Canonical
                           ?? (entry.Expect.Years is { } y ? $"years {y.Start?.ToString() ?? ".."}..{y.End?.ToString() ?? ".."}" : null)
                           ?? (entry.Expect.Status is { } st ? "status " + st : "?");
            var got = interpreted ? result.Canonical ?? "?" : "(no reading)";
            var agree = interpreted && entry.Expect.Canonical is not null && string.Equals(entry.Expect.Canonical, result.Canonical, StringComparison.Ordinal);
            var sameAsParser = interpreted && !agree && a.Deterministic.Canonical is not null && string.Equals(a.Deterministic.Canonical, result.Canonical, StringComparison.Ordinal);
            if (!interpreted) noAnswer++; else if (agree) agreed++; else if (sameAsParser) sameAsParserCount++; else disagreed++;
            var label = !interpreted ? "NOTHING " : agree ? "AGREE   " : sameAsParser ? "PARSER  " : "DIFFER  ";
            Console.WriteLine($"{label} {entry.Id} \"{a.Input}\"  expected {expected}  parser {a.Deterministic.Canonical ?? a.Deterministic.Status.ToString().ToLowerInvariant()}  claude {got}{(rationale is null ? "" : "  ; " + rationale)}");
        }
        else
        {
            if (!interpreted) { noAnswer++; Console.WriteLine($"NOTHING  \"{a.Input}\""); continue; }
            Console.WriteLine($"PROPOSE  \"{a.Input}\" -> {result.Canonical} ({result.Confidence:0.00}){(rationale is null ? "" : "  ; " + rationale)}");
            var canonical = result.Status == ParseStatus.Undated ? null : result.Canonical;
            var expectedStatus = result.Status == ParseStatus.Undated ? "undated" : "parsed";
            var note = $"Proposed by {interpreter.Model} on {DateTime.UtcNow:yyyy-MM-dd}" + (rationale is null ? "" : $": {rationale}") +
                       $" Deterministic parser: {a.Deterministic.Status.ToString().ToLowerInvariant()}{(a.Deterministic.Canonical is null ? "" : " " + a.Deterministic.Canonical)}. Review before promoting.";
            var expect = new Expectation { Status = expectedStatus, Canonical = canonical };
            proposals.Add(new CorpusEntry
            {
                Id = $"llm-{DateTime.UtcNow:yyyyMMdd}-{proposals.Count + 1:000}", Input = a.Input, Profile = a.Profile, Intent = a.Intent,
                Tags = ["llm-proposed", interpreter.Model], Expect = expect, Status = EntryStatus.Proposed, Notes = note,
            });
        }
    }
    var usage = interpreter.Usage;
    Console.WriteLine();
    Console.WriteLine($"{n} asked in {(DateTime.UtcNow - started).TotalSeconds:0}s; tokens in {usage.InputTokens} (cache read {usage.CacheReadTokens}, cache write {usage.CacheWriteTokens}), out {usage.OutputTokens}; model {interpreter.Model}.");
    if (corpusFile is not null)
    {
        Console.WriteLine($"Of {n}: {agreed} agree with the expectation, {sameAsParserCount} match the parser's own reading where the expectation has no canonical form, {disagreed} differ from both, {noAnswer} no reading.");
        return 0;
    }
    if (proposals.Count == 0) { Console.WriteLine("Nothing to propose."); return 0; }
    var target = outPath ?? Path.Combine(corpusRoot, "proposed", $"llm-{DateTime.UtcNow:yyyyMMdd-HHmm}.json");
    var corpus = new CorpusFile
    {
        Schema = "../schema/corpus-entry.schema.json",
        Source = new CorpusSource($"Readings proposed by {interpreter.Model}", "MIT", "https://github.com/tomcrane/linked-art-net", null,
            $"Strings the deterministic parser could not read, with canonical expressions proposed by the Claude interpreter (LinkedArtNet.Dates.Llm) on {DateTime.UtcNow:yyyy-MM-dd} from {(strings is not null ? Path.GetFileName(strings) : Path.GetFileName(capture!))}. Every entry is proposed and must be reviewed; the model's rationale is in each entry's notes."),
        Defaults = new CorpusDefaults(Status: EntryStatus.Proposed, Tags: ["llm-proposed"]),
        Entries = proposals,
    };
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllText(target, JsonSerializer.Serialize(corpus, CorpusFile.JsonOptions) + "\n", new UTF8Encoding(false));
    Console.WriteLine($"Wrote {proposals.Count} proposed entries to {Path.GetRelativePath(repoRoot, target)}. Review them in the trainer or with: review {Path.GetRelativePath(corpusRoot, target)}; promote when confirmed.");
    return 0;
}

// agents [--all]: the agent-string corpus under corpus/agents, evaluated with the repository's dates profiles.
int Agents(string[] rest)
{
    var root = Path.Combine(corpusRoot, "agents");
    var files = LinkedArtNet.Agents.AgentCorpusFile.LoadAll(root);
    if (files.Count == 0) { Console.Error.WriteLine($"No agent corpus files under {root}."); return 2; }
    var evaluator = new LinkedArtNet.Agents.AgentCorpusEvaluator(datesProfiles: LinkedArtNet.Dates.Realisation.ProfileCatalog.Load(Path.Combine(repoRoot, "profiles")));
    var all = rest.Contains("--all");
    var failedVerified = 0;
    foreach (var file in files)
    {
        var results = evaluator.Evaluate(file.Entries);
        var verified = results.Where(r => r.Entry.Status == EntryStatus.Verified).ToList();
        var others = results.Where(r => r.Entry.Status != EntryStatus.Verified).ToList();
        Console.WriteLine($"{file.Path,-32} verified {verified.Count(r => r.Passed)}/{verified.Count}" + (others.Count > 0 ? $"  other {others.Count(r => r.Passed)}/{others.Count}" : ""));
        foreach (var r in results.Where(r => !r.Passed && (all || r.Entry.Status == EntryStatus.Verified)))
        {
            if (r.Entry.Status == EntryStatus.Verified) failedVerified++;
            Console.WriteLine($"  FAIL {r.Entry.Id} ({r.Entry.Status.ToString()!.ToLowerInvariant()}) \"{r.Entry.Input}\"");
            foreach (var f in r.Failures) Console.WriteLine("       " + f);
        }
    }
    Console.WriteLine(failedVerified == 0 ? "All verified agent entries pass." : $"{failedVerified} verified agent entries fail.");
    return failedVerified == 0 ? 0 : 1;
}

static Intent Trainer_ParseIntent(string? intent) => Enum.TryParse<Intent>(intent, true, out var i) ? i : Intent.Auto;

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
