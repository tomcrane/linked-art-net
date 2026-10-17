using System.Net.Http.Json;
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

    /// <summary>
    /// The URL visitors can reach the service at, for links from the playground to the raw API response. Behind
    /// a reverse proxy <see cref="BaseUrl"/> is an internal name, so this is set separately; when empty, the
    /// playground links to its own <c>/dates/result.json</c>, which returns the same shape.
    /// </summary>
    public string? PublicUrl { get; set; }
}

/// <summary>A parse result as the playground shows it, whichever way it was obtained.</summary>
public sealed record PlaygroundResult(string Input, string Status, string Kind, double Confidence, string? Canonical, string? Edtf,
    IReadOnlyList<string> Flags, string Strategy, string? TimeSpanJson, string? BeginJson, string? EndJson,
    IReadOnlyList<(string Canonical, double Confidence, string Note)> Alternatives, IReadOnlyList<string> Notes, string? Unconsumed,
    string Profile, string Source, IReadOnlyList<TimelineBar> Bars,
    LinkedArtTimeSpan? Span, LinkedArtTimeSpan? BeginSpan, LinkedArtTimeSpan? EndSpan)
{
    /// <summary>The C# that produces this result, in each form the libraries offer.</summary>
    public IReadOnlyList<CSharpSnippet> Snippets => CSharpSnippets.For(this);

    /// <summary>
    /// Whether the timeline says anything the Canonical row does not: a begin and end pair, a bound that could lie
    /// anywhere within a range, or an open end. A single span with exact bounds draws as one block, so it is not shown.
    /// </summary>
    public bool TimelineInformative => Bars.Count > 1 || Bars.Any(b => b.IsFuzzy);

}

/// <summary>
/// One block on the playground timeline. <see cref="StartYear"/> to <see cref="EndYear"/> is the outer extent
/// (begin_of_the_begin to end_of_the_end); <see cref="CoreStartYear"/> to <see cref="CoreEndYear"/> is the part the
/// span certainly covers (end_of_the_begin to begin_of_the_end) when that is narrower, so a vague edge shows as a
/// pale fringe round a solid core. An open bound has no outer edge and is drawn fading out.
/// </summary>
public sealed record TimelineBar(string Label, long StartYear, long EndYear, long? CoreStartYear, long? CoreEndYear, bool OpenStart, bool OpenEnd)
{
    public bool IsFuzzy => CoreStartYear is not null || CoreEndYear is not null || OpenStart || OpenEnd;
    public bool HasCore => CoreStartYear is not null || CoreEndYear is not null;

    public static TimelineBar From(string label, long? begin, long? endOfBegin, long? beginOfEnd, long? end)
    {
        var start = begin ?? end!.Value - 100;
        var stop = end ?? begin!.Value + 100;
        var coreStart = endOfBegin is { } eob && eob > start ? eob : (long?)null;
        var coreEnd = beginOfEnd is { } boe && boe < stop ? boe : (long?)null;
        return new TimelineBar(label, start, stop, coreStart, coreEnd, begin is null, end is null);
    }
}

/// <summary>
/// Calls the public service when <see cref="TimeSpanServiceOptions.BaseUrl"/> is set, so the playground exercises the
/// real API; otherwise parses in-process with the repository's profiles.
/// </summary>
public sealed class DateClient
{
    private readonly HttpClient? _http;
    private readonly string? _publicUrl;
    private readonly ProfileCatalog _catalog;
    private readonly LinkedArtNet.Dates.Periods.INamedPeriodSource _periods;
    private readonly Dictionary<string, TimeSpanParser> _parsers = new(StringComparer.OrdinalIgnoreCase);

    public DateClient(IOptions<TimeSpanServiceOptions> options, ExampleIndex index, IHttpClientFactory httpFactory)
    {
        var profilesDir = Path.Combine(index.RepoRoot, "profiles");
        _catalog = Directory.Exists(profilesDir) ? ProfileCatalog.Load(profilesDir) : new ProfileCatalog();
        _periods = LinkedArtNet.Dates.Periods.PeriodOSource.ForRepository(index.RepoRoot);
        _publicUrl = string.IsNullOrWhiteSpace(options.Value.PublicUrl) ? null : options.Value.PublicUrl.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(options.Value.BaseUrl))
        {
            _http = httpFactory.CreateClient("dates");
            _http.BaseAddress = new Uri(options.Value.BaseUrl.TrimEnd('/') + "/");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }
    }

    public bool UsesService => _http is not null;

    /// <summary>The service's public base URL for documentation and links: the configured one, else the production host.</summary>
    public string ApiBase => _publicUrl ?? "https://dates.linkedart.net";

    /// <summary>Where a reader can see the raw API response for this request: the public service, or the site's own copy of the endpoint.</summary>
    public string ApiUrl(string input, string intent, string profile)
    {
        var query = $"s={Uri.EscapeDataString(input)}&intent={Uri.EscapeDataString(intent)}&profile={Uri.EscapeDataString(profile)}";
        return _publicUrl is null ? $"/dates/result.json?{query}" : $"{_publicUrl}/v1/timespan?{query}";
    }

    /// <summary>The API response as JSON text: the service's own when one is configured and answers, otherwise produced in-process in the same shape.</summary>
    public async Task<string?> RawJsonAsync(string input, string intent, string profile, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (_http is not null)
        {
            try
            {
                using var response = await _http.GetAsync($"v1/timespan?s={Uri.EscapeDataString(input)}&intent={Uri.EscapeDataString(intent)}&profile={Uri.EscapeDataString(profile)}", ct);
                if (response.IsSuccessStatusCode) return await response.Content.ReadAsStringAsync(ct);
            }
            catch (HttpRequestException) { /* fall through to in-process */ }
            catch (TaskCanceledException) { /* fall through to in-process */ }
        }
        var dto = TimeSpanResultDto.From(ParseInProcess(input, intent, profile), null);
        return JsonSerializer.Serialize(dto, RawJsonOptions);
    }

    private static readonly JsonSerializerOptions RawJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    };

    /// <summary>
    /// The profile advisor's report for a column of strings: from the service's <c>POST /v1/advise</c> when one is
    /// configured and answers, otherwise computed in-process in the same shape.
    /// </summary>
    public async Task<AdviceDto> AdviseAsync(IReadOnlyList<string> inputs, CancellationToken ct)
    {
        if (_http is not null)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync("v1/advise", new { inputs }, ct);
                if (response.IsSuccessStatusCode)
                {
                    var dto = await response.Content.ReadFromJsonAsync<AdviceDto>(RawJsonOptions, ct);
                    if (dto is not null) return dto;
                }
            }
            catch (HttpRequestException) { /* fall through to in-process */ }
            catch (TaskCanceledException) { /* fall through to in-process */ }
            catch (JsonException) { /* fall through to in-process */ }
        }
        var kept = inputs.Where(s => s.Length <= 500).ToList();
        return AdviceDto.From(ProfileAdvisor.Analyse(kept), inputs.Count - kept.Count);
    }

    private TimeSpanParseResult ParseInProcess(string input, string intent, string profile)
    {
        if (!_catalog.TryGet(profile, out var p)) p = ParseProfile.Default;
        if (!_parsers.TryGetValue(p.Name, out var parser)) _parsers[p.Name] = parser = new TimeSpanParser(p, periods: _periods);
        var intentValue = Enum.TryParse<Intent>(intent, true, out var iv) ? iv : Intent.Auto;
        return parser.Parse(input, intentValue);
    }

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
        return FromResult(ParseInProcess(input, intent, profile), _http is null ? "in-process" : "in-process (service unavailable)");
    }

    private static PlaygroundResult FromResult(TimeSpanParseResult r, string source)
    {
        var bars = new List<TimelineBar>();
        void Bar(string label, LinkedArtTimeSpan? ts)
        {
            if (ts?.BeginOfTheBegin is null && ts?.EndOfTheEnd is null) return;
            bars.Add(TimelineBar.From(label, ts!.BeginOfTheBegin?.Year, ts.EndOfTheBegin?.Year, ts.BeginOfTheEnd?.Year, ts.EndOfTheEnd?.Year));
        }
        Bar("span", r.TimeSpan); Bar("begin", r.Begin); Bar("end", r.End);
        return new PlaygroundResult(r.Input, r.Status.ToString().ToLowerInvariant(), Camel(r.Kind.ToString()), r.Confidence, r.Canonical, r.Edtf,
            r.Flags.Select(f => f.ToString()).Order(StringComparer.Ordinal).ToList(), r.Strategy,
            r.TimeSpan is null ? null : LinkedArtJson.Serialize(r.TimeSpan), r.Begin is null ? null : LinkedArtJson.Serialize(r.Begin), r.End is null ? null : LinkedArtJson.Serialize(r.End),
            r.Alternatives.Select(a => (a.Canonical, a.Confidence, a.Note)).ToList(), r.Notes, r.UnconsumedText, r.Profile, source, bars,
            r.TimeSpan, r.Begin, r.End);
    }

    private static PlaygroundResult FromServiceJson(JsonNode n, string source)
    {
        var bars = new List<TimelineBar>();
        void Bar(string label, JsonNode? ts)
        {
            if (ts is null) return;
            long? Year(string key) => ts[key]?.GetValue<string>() is { } s ? long.Parse(s[..s.IndexOf('-', 1)]) : null;
            var b = Year("begin_of_the_begin"); var e = Year("end_of_the_end");
            if (b is null && e is null) return;
            bars.Add(TimelineBar.From(label, b, Year("end_of_the_begin"), Year("begin_of_the_end"), e));
        }
        Bar("span", n["timespan"]); Bar("begin", n["begin"]); Bar("end", n["end"]);
        string? Json(JsonNode? x) => x?.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        return new PlaygroundResult(
            n["input"]!.GetValue<string>(), n["status"]!.GetValue<string>(), n["kind"]!.GetValue<string>(), n["confidence"]!.GetValue<double>(),
            n["canonical"]?.GetValue<string>(), n["edtf"]?.GetValue<string>(),
            n["flags"]!.AsArray().Select(f => f!.GetValue<string>()).ToList(), n["strategy"]!.GetValue<string>(),
            Json(n["timespan"]), Json(n["begin"]), Json(n["end"]),
            n["alternatives"]!.AsArray().Select(a => (a!["canonical"]!.GetValue<string>(), a["confidence"]!.GetValue<double>(), a["note"]!.GetValue<string>())).ToList(),
            n["notes"]!.AsArray().Select(x => x!.GetValue<string>()).ToList(), n["unconsumed"]?.GetValue<string>(), n["profile"]!.GetValue<string>(), source, bars,
            Span(n["timespan"]), Span(n["begin"]), Span(n["end"]));

        static LinkedArtTimeSpan? Span(JsonNode? ts)
        {
            if (ts is null) return null;
            LinkedArtDate? Date(string key) => ts[key]?.GetValue<string>() is { } s && LinkedArtDate.TryParse(s, null, out var d) ? d : null;
            return new LinkedArtTimeSpan
            {
                Label = ts["_label"]?.GetValue<string>(),
                BeginOfTheBegin = Date("begin_of_the_begin"), EndOfTheBegin = Date("end_of_the_begin"),
                BeginOfTheEnd = Date("begin_of_the_end"), EndOfTheEnd = Date("end_of_the_end"),
            };
        }
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
        return new CorpusEvaluator(profiles.Get, periods: LinkedArtNet.Dates.Periods.PeriodOSource.ForRepository(index.RepoRoot)).Evaluate(entries);
    });

    public IReadOnlyList<EntryResult> Results => _results.Value;

    public IReadOnlyList<string> Files => Results.Select(r => r.Entry.File).Distinct().Order(StringComparer.Ordinal).ToList();

    public IReadOnlyList<(string Tag, int Count)> Tags => Results.SelectMany(r => r.Entry.Tags).GroupBy(t => t).Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).ThenBy(x => x.Key).ToList();
}
