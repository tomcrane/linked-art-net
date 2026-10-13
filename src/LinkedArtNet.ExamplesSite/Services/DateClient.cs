using System.Text.Json;
using System.Text.Json.Nodes;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Corpus;
using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.Serialization;
using Microsoft.Extensions.Options;

namespace LinkedArtNet.ExamplesSite.Services;

public sealed class TimeSpanServiceOptions
{
    /// <summary>Base URL of the deployed LinkedArtNet.Dates.Service; when empty the site parses in-process.</summary>
    public string? BaseUrl { get; set; }
}

/// <summary>A parse result as the playground shows it, whichever way it was obtained.</summary>
public sealed record PlaygroundResult(string Input, string Status, string Kind, double Confidence, string? Canonical, string? Edtf,
    IReadOnlyList<string> Flags, string Strategy, string? TimeSpanJson, string? BeginJson, string? EndJson,
    IReadOnlyList<(string Canonical, double Confidence, string Note)> Alternatives, IReadOnlyList<string> Notes, string? Unconsumed,
    string Profile, string Source, IReadOnlyList<(string Label, long StartYear, long EndYear)> Bars)
{
    /// <summary>A C# snippet a reader can paste.</summary>
    public string CSharp => Kind switch
    {
        "birthDeath" => $"person.WithLifespan({Quote(Input)});",
        "formationDissolution" => $"group.WithExistence({Quote(Input)});",
        _ => $"production.WithTimeSpan({Quote(Input)});",
    };

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}

/// <summary>
/// Calls the public service when <see cref="TimeSpanServiceOptions.BaseUrl"/> is set, so the playground exercises the
/// real API; otherwise parses in-process with the repository's profiles.
/// </summary>
public sealed class DateClient
{
    private readonly HttpClient? _http;
    private readonly ProfileCatalog _catalog;
    private readonly Dictionary<string, TimeSpanParser> _parsers = new(StringComparer.OrdinalIgnoreCase);

    public DateClient(IOptions<TimeSpanServiceOptions> options, ExampleIndex index, IHttpClientFactory httpFactory)
    {
        var profilesDir = Path.Combine(index.RepoRoot, "profiles");
        _catalog = Directory.Exists(profilesDir) ? ProfileCatalog.Load(profilesDir) : new ProfileCatalog();
        if (!string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            _http = httpFactory.CreateClient("dates");
            _http.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }
    }

    public bool UsesService => _http is not null;

    public IReadOnlyList<(string Name, string? Description)> Profiles =>
        _catalog.Profiles.Values.OrderBy(p => p.Name == "default" ? "" : p.Name, StringComparer.Ordinal).Select(p => (p.Name, p.Description)).ToList();

    public async Task<PlaygroundResult?> ParseAsync(string input, string intent, string profile, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (_http is not null)
        {
            try
            {
                var url = $"v1/timespan?s={Uri.EscapeDataString(input)}&intent={Uri.EscapeDataString(intent)}&profile={Uri.EscapeDataString(profile)}";
                using var response = await _http.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;
                    return FromServiceJson(node, "service");
                }
            }
            catch (HttpRequestException) { /* fall through to in-process */ }
            catch (TaskCanceledException) { /* fall through to in-process */ }
        }
        if (!_catalog.TryGet(profile, out var p)) p = ParseProfile.Default;
        if (!_parsers.TryGetValue(p.Name, out var parser)) _parsers[p.Name] = parser = new TimeSpanParser(p);
        var intentValue = Enum.TryParse<Intent>(intent, true, out var iv) ? iv : Intent.Auto;
        return FromResult(parser.Parse(input, intentValue), _http is null ? "in-process" : "in-process (service unavailable)");
    }

    private static PlaygroundResult FromResult(TimeSpanParseResult r, string source)
    {
        var bars = new List<(string, long, long)>();
        void Bar(string label, LinkedArtTimeSpan? ts)
        {
            if (ts?.BeginOfTheBegin is null && ts?.EndOfTheEnd is null) return;
            var start = ts!.BeginOfTheBegin?.Year ?? (ts.EndOfTheEnd!.Value.Year - 100);
            var end = ts.EndOfTheEnd?.Year ?? (ts.BeginOfTheBegin!.Value.Year + 100);
            bars.Add((label, start, end));
        }
        Bar("span", r.TimeSpan); Bar("begin", r.Begin); Bar("end", r.End);
        return new PlaygroundResult(r.Input, r.Status.ToString().ToLowerInvariant(), Camel(r.Kind.ToString()), r.Confidence, r.Canonical, r.Edtf,
            r.Flags.Select(f => f.ToString()).Order(StringComparer.Ordinal).ToList(), r.Strategy,
            r.TimeSpan is null ? null : LinkedArtJson.Serialize(r.TimeSpan), r.Begin is null ? null : LinkedArtJson.Serialize(r.Begin), r.End is null ? null : LinkedArtJson.Serialize(r.End),
            r.Alternatives.Select(a => (a.Canonical, a.Confidence, a.Note)).ToList(), r.Notes, r.UnconsumedText, r.Profile, source, bars);
    }

    private static PlaygroundResult FromServiceJson(JsonNode n, string source)
    {
        var bars = new List<(string, long, long)>();
        void Bar(string label, JsonNode? ts)
        {
            if (ts is null) return;
            var b = ts["begin_of_the_begin"]?.GetValue<string>(); var e = ts["end_of_the_end"]?.GetValue<string>();
            if (b is null && e is null) return;
            long Year(string s) => long.Parse(s[..s.IndexOf('-', 1)]);
            var start = b is null ? Year(e!) - 100 : Year(b);
            var end = e is null ? Year(b!) + 100 : Year(e);
            bars.Add((label, start, end));
        }
        Bar("span", n["timespan"]); Bar("begin", n["begin"]); Bar("end", n["end"]);
        string? Json(JsonNode? x) => x?.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return new PlaygroundResult(
            n["input"]!.GetValue<string>(), n["status"]!.GetValue<string>(), n["kind"]!.GetValue<string>(), n["confidence"]!.GetValue<double>(),
            n["canonical"]?.GetValue<string>(), n["edtf"]?.GetValue<string>(),
            n["flags"]!.AsArray().Select(f => f!.GetValue<string>()).ToList(), n["strategy"]!.GetValue<string>(),
            Json(n["timespan"]), Json(n["begin"]), Json(n["end"]),
            n["alternatives"]!.AsArray().Select(a => (a!["canonical"]!.GetValue<string>(), a["confidence"]!.GetValue<double>(), a["note"]!.GetValue<string>())).ToList(),
            n["notes"]!.AsArray().Select(x => x!.GetValue<string>()).ToList(), n["unconsumed"]?.GetValue<string>(), n["profile"]!.GetValue<string>(), source, bars);
    }

    private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

/// <summary>The corpus with every entry evaluated against the current parser, built on first use.</summary>
public sealed class CorpusIndex(ExampleIndex index)
{
    private readonly Lazy<IReadOnlyList<EntryResult>> _results = new(() =>
    {
        var root = Path.Combine(index.RepoRoot, "corpus");
        var profiles = ProfileCatalog.Load(Path.Combine(index.RepoRoot, "profiles"));
        var entries = CorpusFile.LoadAll(root).SelectMany(f => f.Entries).ToList();
        return new CorpusEvaluator(profiles.Get).Evaluate(entries);
    });

    public IReadOnlyList<EntryResult> Results => _results.Value;

    public IReadOnlyList<string> Files => Results.Select(r => r.Entry.File).Distinct().Order(StringComparer.Ordinal).ToList();

    public IReadOnlyList<(string Tag, int Count)> Tags => Results.SelectMany(r => r.Entry.Tags).GroupBy(t => t).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key).ToList();
}
