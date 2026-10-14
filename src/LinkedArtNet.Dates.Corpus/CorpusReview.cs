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
        var detail = x.Canonical ?? x.Edtf ?? (x.Years is { } y ? $"{y.Start?.ToString() ?? "?"}..{y.End?.ToString() ?? "?"}" : null);
        return (x.Status ?? "parsed") + (detail is null ? "" : " " + Code(detail));
    }

    private static string Actual(EntryResult r) =>
        r.Result.Status.ToString().ToLowerInvariant() + (r.Result.Canonical is { } c ? " " + Code(c) : "");

    /// <summary>A code span safe inside a table cell: pipes escaped, a longer fence when the text contains backticks.</summary>
    private static string Code(string text)
    {
        var fence = text.Contains('`') ? "``" : "`";
        var body = text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        return text.Contains('`') ? $"{fence} {body} {fence}" : $"{fence}{body}{fence}";
    }

    private static string Escape(string text) => text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
