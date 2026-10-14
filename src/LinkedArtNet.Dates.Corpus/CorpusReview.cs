using System.Text;

namespace LinkedArtNet.Dates.Corpus;

/// <summary>
/// A Markdown review of corpus files for a pull request: every entry with what the reviewer expected and what the
/// parser says, failures with their reasons, and the <c>promote</c> commands that would verify the files.
/// Posted as a pull request comment and job summary by <c>.github/workflows/corpus-review.yml</c>.
/// </summary>
public static class CorpusReview
{
    /// <summary>First line of the Markdown, so the workflow can find and update its own comment.</summary>
    public const string Marker = "<!-- corpus-review -->";

    /// <param name="files">The files to review, as loaded by <see cref="CorpusFile.Load"/> with the corpus root so paths are relative to it.</param>
    /// <param name="maxPassingRows">Passing entries listed per file before the rest are summarised as a count; failures are always listed.</param>
    public static string Markdown(IReadOnlyList<CorpusFile> files, CorpusEvaluator evaluator, string parserVersion, int maxPassingRows = 50)
    {
        var sb = new StringBuilder();
        var all = files.Select(f => (File: f, Results: evaluator.Evaluate(f.Entries))).ToList();
        var total = all.Sum(x => x.Results.Count);
        var failing = all.Sum(x => x.Results.Count(r => !r.Passed));
        var verifiedFailing = all.Sum(x => x.Results.Count(r => !r.Passed && r.Entry.Status == EntryStatus.Verified));

        sb.AppendLine(Marker);
        sb.AppendLine("## Corpus review");
        sb.AppendLine();
        sb.Append($"Parser {parserVersion}. {Count(files.Count, "file")} changed, {Count(total, "entry", "entries")}: {total - failing} pass, {failing} fail.");
        sb.AppendLine(verifiedFailing > 0
            ? $" **{Count(verifiedFailing, "verified entry", "verified entries")} fail{(verifiedFailing == 1 ? "s" : "")}, which blocks CI.**"
            : " Only `verified` entries gate CI; `proposed` and `disputed` ones are informative.");
        sb.AppendLine();

        foreach (var (file, results) in all)
        {
            var byStatus = string.Join(", ", file.Entries.GroupBy(e => e.Status).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}"));
            var failed = results.Where(r => !r.Passed).ToList();
            var passed = results.Where(r => r.Passed).ToList();
            sb.AppendLine($"### `corpus/{file.Path}`");
            sb.AppendLine();
            sb.AppendLine($"{file.Source.Name} ({file.Source.Licence}): {Count(results.Count, "entry", "entries")} ({byStatus}), {passed.Count} pass, {failed.Count} fail.");
            if (file.Source.Notes is { } notes) sb.AppendLine($"> {Escape(notes)}");
            sb.AppendLine();
            sb.AppendLine("| | id | input | expected | parser says |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in failed) sb.AppendLine($"| **fail** | {r.Entry.Id} | {Code(r.Entry.Input)} | {Expected(r.Entry)} | {Actual(r)} |");
            foreach (var r in passed.Take(maxPassingRows)) sb.AppendLine($"| pass | {r.Entry.Id} | {Code(r.Entry.Input)} | {Expected(r.Entry)} | {Actual(r)} |");
            if (passed.Count > maxPassingRows) sb.AppendLine($"| pass | | _and {passed.Count - maxPassingRows} more passing entries_ | | |");
            sb.AppendLine();
            if (failed.Count > 0)
            {
                sb.AppendLine("Failures:");
                sb.AppendLine();
                foreach (var r in failed)
                {
                    sb.AppendLine($"- **{r.Entry.Id}** {Code(r.Entry.Input)}{(r.Entry.Notes is { } n ? $" ({Escape(n)})" : "")}");
                    foreach (var reason in r.Failures) sb.AppendLine($"  - {Escape(reason)}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("### Next");
        sb.AppendLine();
        sb.AppendLine("A maintainer checks that each expectation is right (the entry's note says why the reviewer chose it), then on this branch:");
        sb.AppendLine();
        sb.AppendLine("```");
        foreach (var (file, results) in all)
        {
            var unverified = results.Where(r => r.Entry.Status != EntryStatus.Verified).ToList();
            if (unverified.Count == 0) continue;
            var ids = unverified.Where(r => !r.Passed).Select(r => r.Entry.Id).ToList();
            if (ids.Count == 0) sb.AppendLine($"dotnet run --project tools/Corpus -- promote corpus/{file.Path}");
            else
            {
                sb.AppendLine($"dotnet run --project tools/Corpus -- promote corpus/{file.Path} {string.Join(" ", ids)} --to disputed   # or fix these first");
                sb.AppendLine($"dotnet run --project tools/Corpus -- promote corpus/{file.Path}");
            }
        }
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("`promote` edits only the status tokens, refuses a failing entry, and rewrites `corpus/report.md`; commit the result to this branch. See `docs/08-corpus-contributions.md` §5.");
        return sb.ToString();
    }

    private static string Count(int n, string one, string? many = null) => $"{n} {(n == 1 ? one : many ?? one + "s")}";

    private static string Expected(CorpusEntry e)
    {
        var x = e.Expect;
        var detail = x.Canonical ?? x.Edtf ?? (x.Years is { } y ? $"{y.Start?.ToString() ?? ".."}..{y.End?.ToString() ?? ".."}" : null)
            ?? ((x.TimeSpan ?? x.Begin) is { } ts || (x.End) is { } ? $"{Date((x.TimeSpan ?? x.Begin)?.BeginOfTheBegin)}..{Date((x.TimeSpan ?? x.End)?.EndOfTheEnd)}" : null);
        return (x.Status ?? "parsed") + (detail is null ? "" : " " + Code(detail));
    }

    private static string Actual(EntryResult r) =>
        r.Result.Status.ToString().ToLowerInvariant() + (r.Result.Canonical is { } c ? " " + Code(c) : "") + Years(r.Result);

    /// <summary>The realised outer bounds as years, or as dates when a bound is not a whole year: " 1875..1899", " 1776-03-20..1776-06-21", " ..1935", " 1941..".</summary>
    private static string Years(TimeSpanParseResult result)
    {
        var start = (result.TimeSpan ?? result.Begin)?.BeginOfTheBegin;
        var end = (result.TimeSpan ?? result.End)?.EndOfTheEnd;
        if (start is null && end is null) return "";
        return $" {(start is { } s ? Edge(s, true) : "")}..{(end is { } e ? Edge(e, false) : "")}";
    }

    /// <summary>"1776-07-04T00:00:00Z" -> "1776-07-04"; a whole-year edge -> the year; null -> "".</summary>
    private static string Date(string? iso) => iso is null ? "" : iso.Length >= 10 && iso[5..10] is "01-01" or "12-31" ? iso[..4].TrimStart('0') is { Length: > 0 } y ? y : "0" : iso.Length >= 10 ? iso[..10] : iso;

    private static string Edge(LinkedArtDate d, bool start) =>
        (start ? d.Month == 1 && d.Day == 1 : d.Month == 12 && d.Day == 31) ? d.Year.ToString() : $"{d.Year:0000}-{d.Month:00}-{d.Day:00}";

    /// <summary>A code span safe inside a table cell: pipes escaped, a longer fence when the text contains backticks.</summary>
    private static string Code(string text)
    {
        var fence = text.Contains('`') ? "``" : "`";
        var body = text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        return text.Contains('`') ? $"{fence} {body} {fence}" : $"{fence}{body}{fence}";
    }

    private static string Escape(string text) => text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
