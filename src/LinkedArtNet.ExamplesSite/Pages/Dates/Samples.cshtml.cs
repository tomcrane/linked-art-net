using System.Text.RegularExpressions;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Corpus;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Dates;

/// <summary>
/// Hundreds of dates the parser reads, drawn from the corpus it is tested against so the page can never drift from the
/// parser: every authored set in full, then one example of each distinct shape of reading from the imported collections.
/// Each input links to the playground.
/// </summary>
public partial class SamplesModel(CorpusIndex corpus) : PageModel
{
    public sealed record Sample(string Input, string Profile, string Intent, string? Canonical, string? Years, string Status);
    public sealed record Section(string Key, string Title, string? Intro, IReadOnlyList<Sample> Items, bool Authored);

    public IReadOnlyList<Section> Sections { get; private set; } = [];
    public int Total => Sections.Sum(s => s.Items.Count);

    /// <summary>Verified passing entries per imported file; one per shape of reading, so the section shows range rather than bulk.</summary>
    public const int ImportedPerFile = 40;

    public void OnGet()
    {
        var verified = corpus.Results.Where(r => r.Passed && r.Entry.Status == EntryStatus.Verified).ToList();
        var sections = new List<Section>();

        foreach (var group in verified.Where(r => r.Entry.File.StartsWith("authored/", StringComparison.Ordinal)).GroupBy(r => r.Entry.File).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var source = corpus.Sources.GetValueOrDefault(group.Key);
            var title = source?.Name?.Replace("Authored: ", "", StringComparison.Ordinal) ?? group.Key;
            sections.Add(new Section(Key(group.Key), char.ToUpperInvariant(title[0]) + title[1..], source?.Notes, group.Select(ToSample).ToList(), Authored: true));
        }
        // Imported and seed collections; trainer files under proposed/ are reviewers' work in progress, not a showcase.
        foreach (var group in verified.Where(r => !r.Entry.File.StartsWith("authored/", StringComparison.Ordinal) && !r.Entry.File.StartsWith("proposed/", StringComparison.Ordinal)).GroupBy(r => r.Entry.File).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var source = corpus.Sources.GetValueOrDefault(group.Key);
            var picked = group.GroupBy(r => Shape(r.Result.Canonical ?? r.Result.Status.ToString()))
                .Select(g => g.OrderBy(r => r.Entry.Input.Length).First()) // the shortest example of each shape
                .OrderBy(r => r.Entry.Id, StringComparer.Ordinal)
                .Take(ImportedPerFile)
                .Select(ToSample).ToList();
            if (picked.Count == 0) continue;
            sections.Add(new Section(Key(group.Key), source?.Name ?? group.Key, $"One example of each shape of reading among the {group.Count()} verified entries from this collection.", picked, Authored: false));
        }
        Sections = sections;
    }

    private static Sample ToSample(EntryResult r)
    {
        var span = r.Result.TimeSpan ?? r.Result.Begin;
        var end = r.Result.End?.EndOfTheEnd ?? r.Result.TimeSpan?.EndOfTheEnd;
        string? years = null;
        if (span?.BeginOfTheBegin is { } b || end is not null)
        {
            var start = (r.Result.TimeSpan ?? r.Result.Begin)?.BeginOfTheBegin;
            years = $"{(start is null ? ".." : YearText(start.Value.Year))} to {(end is null ? ".." : YearText(end.Value.Year))}";
            if (start is not null && end is not null && start.Value.Year == end.Value.Year) years = YearText(start.Value.Year);
        }
        return new Sample(r.Entry.Input, r.Entry.Profile, r.Entry.Intent, r.Result.Canonical, years, r.Result.Status.ToString().ToLowerInvariant());
    }

    private static string YearText(long year) => year switch
    {
        <= -1_000_000 => $"{-year / 1_000_000.0:0.#} Ma ago",
        <= -10_000 => $"{-year / 1000.0:0.#} ka ago",
        <= 0 => $"{1 - year} BCE",
        _ => year.ToString(),
    };

    /// <summary>"late 195X" and "late 176X" are one shape; "born 1606/died 1669" another.</summary>
    private static string Shape(string canonical) => Digits().Replace(canonical, "#");

    private static string Key(string file) => Path.GetFileNameWithoutExtension(file);

    public static string PlaygroundUrl(Sample s) => $"/dates?s={Uri.EscapeDataString(s.Input)}&intent={s.Intent}&profile={s.Profile}";

    [GeneratedRegex(@"\d")]
    private static partial Regex Digits();
}
