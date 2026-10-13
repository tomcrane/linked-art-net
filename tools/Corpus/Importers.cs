// Importers for external corpora -> corpus/imported/*.json, every entry `proposed` until reviewed.
// Each importer records the source, its licence and the attribution the licence asks for; the full licence
// texts are in corpus/imported/THIRD-PARTY-NOTICES.md. Only MIT, public-domain and attribution-only sources
// are imported, because this repository is MIT.

using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinkedArtNet.Dates.Corpus;

internal static partial class Importers
{
    public static async Task<int> Run(string source, string? path, string corpusRoot)
    {
        var outDir = Path.Combine(corpusRoot, "imported");
        Directory.CreateDirectory(outDir);
        CorpusFile file = source switch
        {
            "yearrange" => YearRange(path ?? throw new ArgumentException("yearrange needs the path to test/date-tests.json")),
            "timetwister" => Timetwister(path ?? throw new ArgumentException("timetwister needs the path to spec/dates_spec.rb")),
            "getty" => await Getty(path ?? throw new ArgumentException("getty needs the directory holding the curl-fetched ulan-*.json SPARQL results")),
            _ => throw new ArgumentException($"Unknown import source '{source}'. Known: yearrange, timetwister, getty."),
        };
        var outPath = Path.Combine(outDir, source + ".json");
        var json = JsonSerializer.Serialize(file, CorpusFile.JsonOptions);
        File.WriteAllText(outPath, json + "\n", new UTF8Encoding(false));
        Console.WriteLine($"{file.Entries.Count} entries -> {Path.GetRelativePath(corpusRoot, outPath)}");
        return 0;
    }

    // ---- jeresig/node-yearrange: test/date-tests.json, [{original, start, end, circa?}] -----------------------------

    private static CorpusFile YearRange(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var entries = new List<CorpusEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var i = 0;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var input = item.GetProperty("original").GetString() ?? "";
            if (input.Length == 0 || !seen.Add(input)) continue;
            var start = item.TryGetProperty("start", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt64() : (long?)null;
            var end = item.TryGetProperty("end", out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt64() : (long?)null;
            var circa = item.TryGetProperty("circa", out var c) && c.ValueKind == JsonValueKind.True;
            i++;
            var expect = start is null && end is null
                ? new Expectation { Status = "unparsed" }
                : new Expectation { Status = "parsed", Years = new ExpectedYears(start, end), Flags = circa ? ["Approximate"] : null };
            entries.Add(new CorpusEntry { Id = $"yr-{i:0000}", Input = input, Expect = expect, Tags = Tags(input) });
        }
        return new CorpusFile
        {
            Schema = "../schema/corpus-entry.schema.json",
            Source = new CorpusSource(
                "jeresig/node-yearrange test data",
                "MIT",
                "https://github.com/jeresig/node-yearrange/blob/master/test/date-tests.json",
                "Copyright (c) 2013 John Resig. Licensed under the MIT License; see THIRD-PARTY-NOTICES.md.",
                "The strings are museum catalogue dates (largely Japanese print collections). The source knows only start and end " +
                "years and a circa flag, so expectations are coarse (years) and reflect yearrange's own conventions, e.g. " +
                "'late' = the last quarter of a century and 'circa' does not widen. Where they differ from our default profile " +
                "the entry fails and should be reviewed, not copied."),
            Defaults = new CorpusDefaults("single", "default", EntryStatus.Proposed, ["yearrange", "imported"]),
            Entries = entries,
        };
    }

    // ---- alexduryee/timetwister: spec/dates_spec.rb, Timetwister.parse("...") followed by expect(...) lines --------

    private static CorpusFile Timetwister(string path)
    {
        var text = File.ReadAllText(path);
        var entries = new List<CorpusEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var n = 0;

        // Blocks start at `Timetwister.parse("X")` or at `forms = ["a", "b"]` and run to the next block start.
        var starts = BlockStart().Matches(text);
        for (var b = 0; b < starts.Count; b++)
        {
            var header = starts[b];
            var bodyEnd = b + 1 < starts.Count ? starts[b + 1].Index : text.Length;
            var body = text[(header.Index + header.Length)..bodyEnd];
            var inputs = header.Groups["forms"].Success
                ? QuotedString().Matches(header.Groups["forms"].Value).Select(m => Unescape(m.Groups[1].Value)).ToList()
                : [Unescape(header.Groups["single"].Value)];

            string? Expect(string key)
            {
                var m = Regex.Match(body, @"\[:" + key + @"\]\)\.to eq\(""([^""]*)""\)");
                return m.Success ? m.Groups[1].Value : null;
            }
            var startFull = Expect("date_start_full") ?? AsFull(Expect("date_start"), true);
            var endFull = Expect("date_end_full") ?? AsFull(Expect("date_end"), false);
            var certainty = Expect("date_certainty");
            var isRange = body.Contains("[:inclusive_range]).to eq(true)");
            if (startFull is null && endFull is null) continue;

            var flags = new List<string>();
            if (certainty == "approximate") flags.Add("Approximate");
            if (certainty == "questionable") flags.Add("Uncertain");

            foreach (var input in inputs)
            {
                if (input.Length == 0 || !seen.Add(input)) continue;
                n++;
                entries.Add(new CorpusEntry
                {
                    Id = $"tt-{n:000}", Input = input, Tags = Tags(input).Append(isRange ? "range" : "single").Distinct().ToList(),
                    Expect = new Expectation
                    {
                        Status = "parsed",
                        TimeSpan = new ExpectedTimeSpan
                        {
                            BeginOfTheBegin = startFull is null ? null : startFull + "T00:00:00Z",
                            EndOfTheEnd = endFull is null ? null : endFull + "T23:59:59Z",
                        },
                        Flags = flags.Count == 0 ? null : flags,
                    },
                });
            }
        }
        return new CorpusFile
        {
            Schema = "../schema/corpus-entry.schema.json",
            Source = new CorpusSource(
                "alexduryee/timetwister spec",
                "MIT",
                "https://github.com/alexduryee/timetwister/blob/master/spec/dates_spec.rb",
                "Copyright (c) 2015 Alex Duryee (written at the New York Public Library). Licensed under the MIT License; see THIRD-PARTY-NOTICES.md.",
                "Archival (DACS/EAD) date forms: 'ca.', 'circa', brackets, 'between', 'undated', seasons, open ranges, " +
                "'and' lists. Expectations are timetwister's full start and end dates. timetwister treats an inverted range as " +
                "already ordered and 'ca.' as not widening, like our default profile; where its other conventions differ the " +
                "entry fails and should be reviewed."),
            Defaults = new CorpusDefaults("single", "default", EntryStatus.Proposed, ["timetwister", "archival", "imported"]),
            Entries = entries,
        };
    }

    private static string? AsFull(string? date, bool start) => date switch
    {
        null => null,
        { Length: 4 } y => y + (start ? "-01-01" : "-12-31"),
        { Length: 7 } ym => start ? ym + "-01" : ym + "-" + LinkedArtNet.LinkedArtDate.DaysInMonth(long.Parse(ym[..4], CultureInfo.InvariantCulture), int.Parse(ym[5..], CultureInfo.InvariantCulture)).ToString("00", CultureInfo.InvariantCulture),
        _ => date,
    };

    private static string Unescape(string s) => s.Replace("\\\"", "\"");

    [GeneratedRegex(@"Timetwister\.parse\(""(?<single>(?:[^""\\]|\\.)*)""\)|forms\s*=\s*\[(?<forms>[^\]]*)\]")]
    private static partial Regex BlockStart();

    [GeneratedRegex(@"""((?:[^""\\]|\\.)*)""")]
    private static partial Regex QuotedString();

    // ---- Getty ULAN biographies: prose description + estimated start/end years, via the public SPARQL endpoint ------

    // The sample is fetched with curl (the endpoint answers .NET's HttpClient with 400 for reasons not worth chasing):
    //   for each century c: curl -G https://vocab.getty.edu/sparql.json --data-urlencode "query=select ?d ?b ?e where
    //     { ?bio a gvp:Biography ; schema:description ?d ; gvp:estStart ?b ; gvp:estEnd ?e .
    //       filter(?b >= \"1700\"^^xsd:gYear && ?b < \"1800\"^^xsd:gYear) } limit 60" -o ulan-4.json
    // then: import getty <directory holding ulan-*.json>
    private static Task<CorpusFile> Getty(string directory)
    {
        var entries = new List<CorpusEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(directory, "ulan-*.json").Order(StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            foreach (var row in doc.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray())
            {
                var d = row.GetProperty("d").GetProperty("value").GetString() ?? "";
                if (!seen.Add(d) || !d.Any(char.IsAsciiDigit)) continue;
                var b = long.Parse(row.GetProperty("b").GetProperty("value").GetString()!, CultureInfo.InvariantCulture);
                var e = long.Parse(row.GetProperty("e").GetProperty("value").GetString()!, CultureInfo.InvariantCulture);
                entries.Add(new CorpusEntry
                {
                    Id = $"ulan-{entries.Count + 1:000}", Input = d, Tags = Tags(d),
                    Expect = new Expectation { Status = "parsed", Years = new ExpectedYears(b, e), ToleranceYears = 25 },
                });
            }
        }
        if (entries.Count == 0) throw new InvalidOperationException($"No ulan-*.json SPARQL result files in {directory}.");
        return Task.FromResult(new CorpusFile
        {
            Schema = "../schema/corpus-entry.schema.json",
            Source = new CorpusSource(
                "Getty ULAN biography descriptions (sample)",
                "ODC-By-1.0",
                "https://vocab.getty.edu/sparql",
                "This file contains information from the Getty Union List of Artist Names (ULAN), made available by the J. Paul Getty Trust " +
                "under the Open Data Commons Attribution License (ODC-By) 1.0, http://opendatacommons.org/licenses/by/1.0/. See THIRD-PARTY-NOTICES.md.",
                "Biography descriptions are prose ('Chinese calligrapher, active ca. 1023-1056'), so the date must be found inside " +
                "running text: these entries measure the salvage path. The expected years are ULAN's estStart/estEnd, which are " +
                "deliberately padded estimates (an active period is widened to plausible lifetime bounds), hence toleranceYears 25. " +
                "Sixty biographies from each of six bands of estimated start year (before 1400, then by century to 1900 and after)."),
            Defaults = new CorpusDefaults("single", "default", EntryStatus.Proposed, ["ulan", "prose", "imported"]),
            Entries = entries,
        });
    }

    // ---- rough tags from the surface form, for the per-tag report ---------------------------------------------------

    private static List<string> Tags(string input)
    {
        var tags = new List<string>();
        var lower = input.ToLowerInvariant();
        if (Regex.IsMatch(lower, @"\bc(a|irca)?\.?\s*\d|\(circa\)|\bcirca\b|\bca\.") ) tags.Add("circa");
        if (lower.Contains('?')) tags.Add("uncertain");
        if (Regex.IsMatch(lower, @"\d\s*-\s*\d|\bto\b|\bbetween\b|/")) tags.Add("range");
        if (Regex.IsMatch(lower, @"\d{3}0'?s\b")) tags.Add("decade");
        if (Regex.IsMatch(lower, @"\bcentury\b|\bcent\.|\d{1,2}(st|nd|rd|th)\s*c\b|\d+c\b")) tags.Add("century");
        if (Regex.IsMatch(lower, @"\b(early|mid|late|first half|second half|quarter)\b")) tags.Add("band");
        if (Regex.IsMatch(lower, @"\b(b\.c\.|bce?)\b")) tags.Add("bce");
        if (Regex.IsMatch(lower, @"\b(active|fl\.|flourished)\b")) tags.Add("floruit");
        if (Regex.IsMatch(lower, @"\b(born|died|b\.|d\.)\s*\d")) tags.Add("lifecycle");
        if (Regex.IsMatch(lower, @"\b(period|dynasty|era)\b|\p{IsCJKUnifiedIdeographs}")) tags.Add("named-period");
        if (input.Contains('[') || input.Contains('(')) tags.Add("bracketed");
        if (Regex.IsMatch(lower, @"\b(n\.d\.|undated|no date|not dated)\b")) tags.Add("undated");
        if (Regex.IsMatch(lower, @"\b(january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|jun|jul|aug|sep|sept|oct|nov|dec)\b")) tags.Add("month");
        if (Regex.IsMatch(lower, @"\b(spring|summer|autumn|fall|winter)\b")) tags.Add("season");
        return tags;
    }
}
