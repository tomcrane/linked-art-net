using LinkedArtNet.Dates;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Dates;

/// <summary>
/// The profile advisor: paste a column of dates, or pick a corpus sample, and see which conventions the data uses
/// and which profile settings follow. Calls <c>POST /v1/advise</c> on the service when one is configured.
/// </summary>
public class AdviseModel(DateClient client, CorpusIndex corpus) : PageModel
{
    public const int MaxInputs = 20_000;

    /// <summary>Corpus files a visitor can analyse without pasting anything, by the short name used in the URL.</summary>
    public static readonly IReadOnlyList<(string Key, string File, string Title)> Samples =
    [
        ("yearrange", "imported/yearrange.json", "yearrange (museum catalogues)"),
        ("timetwister", "imported/timetwister.json", "timetwister (US archives)"),
        ("getty", "imported/getty.json", "Getty ULAN (artist dates)"),
        ("edtf", "imported/edtf-spec.json", "EDTF specification examples"),
        ("pmc", "seed/pmc-outputs.json", "PMC (Paul Mellon Centre) outputs"),
    ];

    [BindProperty] public string? Inputs { get; set; }
    [BindProperty(SupportsGet = true)] public string? Sample { get; set; }

    public AdviceDto? Advice { get; private set; }
    public string? SampleTitle { get; private set; }
    public int Submitted { get; private set; }
    public bool UsesService => client.UsesService;
    public string ApiBase => client.ApiBase;

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Sample)) return;
        var sample = Samples.FirstOrDefault(s => s.Key.Equals(Sample, StringComparison.OrdinalIgnoreCase));
        if (sample.File is null) { Sample = null; return; }
        SampleTitle = sample.Title;
        var strings = corpus.Results.Where(r => r.Entry.File == sample.File).Select(r => r.Entry.Input).Distinct(StringComparer.Ordinal).ToList();
        Inputs = string.Join("\n", strings);
        await RunAsync(strings, ct);
    }

    public async Task OnPostAsync(CancellationToken ct)
    {
        Sample = null;
        var strings = (Inputs ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (strings.Count == 0) return;
        if (strings.Count > MaxInputs) strings = strings[..MaxInputs];
        await RunAsync(strings, ct);
    }

    private async Task RunAsync(IReadOnlyList<string> strings, CancellationToken ct)
    {
        Submitted = strings.Count;
        Advice = await client.AdviseAsync(strings, ct);
    }

    /// <summary>Rows for the vocabulary table: feature, count and examples, in the advisor's own grouping.</summary>
    public IEnumerable<(string Group, string Feature, int Count, IReadOnlyList<string> Examples)> VocabularyRows()
    {
        if (Advice is null) yield break;
        foreach (var (group, prefix, counts) in new[]
        {
            ("Band words", "band:", Advice.Bands), ("Hedges", "hedge:", Advice.Hedges), ("Era words", "era:", Advice.Eras),
            ("Century notations", "century:", Advice.CenturyNotations), ("Other languages", "lang:", Advice.Languages),
        })
            foreach (var (feature, count) in counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
                yield return (group, feature, count, Examples(prefix + feature));
    }

    public IEnumerable<(string Feature, int Count, IReadOnlyList<string> Examples, string Meaning)> ShapeRows()
    {
        if (Advice is null) yield break;
        var s = Advice.Shapes;
        yield return ("Numeric dates", s.NumericDates, Examples("numeric"), "Day, month and year as numbers.");
        yield return ("Day-first evidence", s.DayFirstEvidence, Examples("numeric:dayFirst"), "A first number over 12, so it can only be a day.");
        yield return ("Month-first evidence", s.MonthFirstEvidence, Examples("numeric:monthFirst"), "A second number over 12, so the first is the month.");
        yield return ("Two-digit years", s.TwoDigitYears, Examples("twoDigitYears"), "Need a window to become four-digit years.");
        yield return ("Bracketed", s.Brackets, Examples("brackets"), "Inferred dates in MARC style.");
        yield return ("Seasons", s.Seasons, Examples("seasons"), "Need a hemisphere and a winter policy.");
        yield return ("Undated", s.Undated, Examples("undated"), "The text says there is no date.");
        yield return ("Open ends", s.OpenEnds, Examples("openEnds"), "Before, after, present, a trailing dash.");
        yield return ("Decades", s.Decades, Examples("decades"), "1950s and the like; band words apply to them too.");
        yield return ("Period words", s.PeriodWords, Examples("periods"), "Dynasties, reigns and named periods need period tables.");
    }

    private IReadOnlyList<string> Examples(string key) => Advice is not null && Advice.Examples.TryGetValue(key, out var list) ? list : [];

    public string SuggestedJson => Advice?.SuggestedProfile.ToJsonString(LinkedArtNet.Dates.Realisation.ParseProfile.JsonOptions) ?? "";
}
