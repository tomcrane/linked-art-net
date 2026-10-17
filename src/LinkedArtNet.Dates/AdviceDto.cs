using System.Text.Json.Nodes;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates;

/// <summary>
/// The profile advisor's report as the service returns it from <c>POST /v1/advise</c> and the site shows it at
/// <c>/dates/advise</c>. Shared by both so the site can read the service's answer or produce the same shape in-process.
/// </summary>
public sealed record AdviceDto(
    int Strings,
    int Skipped,
    IReadOnlyDictionary<string, int> Bands,
    IReadOnlyDictionary<string, int> Hedges,
    IReadOnlyDictionary<string, int> Eras,
    IReadOnlyDictionary<string, int> CenturyNotations,
    IReadOnlyDictionary<string, int> Languages,
    AdviceShapes Shapes,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Examples,
    IReadOnlyList<string> Recommendations,
    JsonObject SuggestedProfile,
    string Markdown)
{
    /// <param name="skipped">Strings the caller dropped before analysis (over the length limit), so the reader knows the count is not the whole file.</param>
    public static AdviceDto From(ProfileAdvisor.Report report, int skipped = 0) => new(
        report.Strings, skipped, report.Bands, report.Hedges, report.Eras, report.CenturyNotations, report.Languages,
        new AdviceShapes(report.NumericDates, report.NumericDayFirstEvidence, report.NumericMonthFirstEvidence, report.TwoDigitYears,
            report.Brackets, report.Seasons, report.Undated, report.OpenEnds, report.Decades, report.PeriodWords),
        report.Examples, report.Recommendations, report.SuggestedProfileJson(), report.ToMarkdown());

    /// <summary>True when the suggested profile adds nothing to the default (only inherits, name and description).</summary>
    public bool SuggestsDefault => SuggestedProfile.Count <= 3;
}

/// <summary>Counts of strings with each shape; see the advisor for what each one means.</summary>
public sealed record AdviceShapes(int NumericDates, int DayFirstEvidence, int MonthFirstEvidence, int TwoDigitYears,
    int Brackets, int Seasons, int Undated, int OpenEnds, int Decades, int PeriodWords);
