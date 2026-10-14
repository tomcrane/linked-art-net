using LinkedArtNet.Dates.Corpus;

namespace LinkedArtNet.Dates.Tests;

/// <summary>The corpus report's failure grouping: a reader should tell parser gaps from flag mapping and convention differences at a glance.</summary>
public class CorpusReportTests
{
    private static readonly CorpusEvaluator Evaluator = new();

    private static EntryResult Result(string id, string input, Expectation expect) =>
        Evaluator.Evaluate(new CorpusEntry { Id = id, Input = input, Expect = expect, File = "t.json" });

    [Fact]
    public void Failures_are_classified_by_cause()
    {
        Assert.Equal(CorpusEvaluator.FailureCause.ReadingDiffers, CorpusEvaluator.Cause(Result("a", "1984", new Expectation { Canonical = "1985" })));
        Assert.Equal(CorpusEvaluator.FailureCause.FlagsOnly, CorpusEvaluator.Cause(Result("b", "1984", new Expectation { Canonical = "1984", Flags = ["Approximate"] })));
        Assert.Equal(CorpusEvaluator.FailureCause.NotParsed, CorpusEvaluator.Cause(Result("c", "n.d.", new Expectation { Status = "parsed" })));
        Assert.Equal(CorpusEvaluator.FailureCause.ParsedUnexpectedly, CorpusEvaluator.Cause(Result("d", "1984", new Expectation { Status = "unparsed" })));
        Assert.Equal(CorpusEvaluator.FailureCause.BoundsDiffer, CorpusEvaluator.Cause(Result("e", "1984", new Expectation { Years = new ExpectedYears(1990, 1990) })));
    }

    [Fact]
    public void Report_groups_failures_under_a_heading_per_cause()
    {
        var results = new[]
        {
            Result("a", "1984", new Expectation { Canonical = "1985" }),
            Result("b", "1984", new Expectation { Canonical = "1984", Flags = ["Approximate"] }),
            Result("b2", "1850~", new Expectation { Canonical = "1850~", Flags = ["Uncertain"] }),
            Result("e", "1984", new Expectation { Years = new ExpectedYears(1990, 1990) }),
            Result("ok", "1984", new Expectation { Canonical = "1984" }),
        };
        var report = CorpusEvaluator.Report(results);

        Assert.Contains("## Failures", report);
        Assert.Contains("By cause: reading differs 1, bounds differ 1, flags only 2.", report);
        Assert.Contains("### Reading differs (1)", report);
        Assert.Contains("### Flags only (2)", report);
        Assert.Contains("### Bounds differ (1)", report);
        Assert.DoesNotContain("`ok`", report);
        Assert.Contains("  - expected: canonical 1985\n", report.Replace("\r\n", "\n"));
        // Flag-only failures are listed after the reading and bounds ones, so the likely parser bugs come first.
        Assert.True(report.IndexOf("### Reading differs", StringComparison.Ordinal) < report.IndexOf("### Flags only", StringComparison.Ordinal));
    }
}
