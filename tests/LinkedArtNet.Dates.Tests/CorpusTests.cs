using LinkedArtNet.Dates.Corpus;
using Xunit.Abstractions;

namespace LinkedArtNet.Dates.Tests;

/// <summary>
/// The ground-truth corpus under corpus/. Every file must be well-formed; every <c>verified</c> entry must pass
/// (that is what verified means); proposed and disputed entries are reported by <c>tools/Corpus run</c>, not here.
/// </summary>
public class CorpusTests(ITestOutputHelper output)
{
    private static readonly string CorpusRoot = Path.Combine(TestPaths.RepoRoot, "corpus");
    private static readonly string SchemaPath = Path.Combine(CorpusRoot, "schema", "corpus-entry.schema.json");
    private static readonly Lazy<IReadOnlyList<CorpusFile>> Files = new(() => CorpusFile.LoadAll(CorpusRoot));
    private static readonly Lazy<Realisation.ProfileCatalog> Profiles = new(() => Realisation.ProfileCatalog.Load(TestPaths.Profiles));

    public static TheoryData<string> FilePaths() => new(Files.Value.Select(f => f.Path));

    public static TheoryData<string, string> VerifiedEntries()
    {
        var data = new TheoryData<string, string>();
        foreach (var e in Files.Value.SelectMany(f => f.Entries).Where(e => e.Status == EntryStatus.Verified)) data.Add(e.File, e.Id);
        return data;
    }

    [Theory]
    [MemberData(nameof(FilePaths))]
    public void File_is_valid_against_the_schema(string relativePath)
    {
        var errors = CorpusFile.SchemaErrors(Path.Combine(CorpusRoot, relativePath), SchemaPath);
        Assert.True(errors.Count == 0, $"{relativePath}:\n  " + string.Join("\n  ", errors));
    }

    [Theory]
    [MemberData(nameof(FilePaths))]
    public void Entries_are_well_formed(string relativePath)
    {
        var file = Files.Value.Single(f => f.Path == relativePath);
        var problems = new List<string>();
        foreach (var e in file.Entries)
        {
            if (!Profiles.Value.TryGet(e.Profile, out _)) problems.Add($"{e.Id}: unknown profile '{e.Profile}'");
            if (e.Expect.Canonical is { } c && !DateExpression.TryParse(c, out _)) problems.Add($"{e.Id}: canonical '{c}' does not parse");
            foreach (var flag in (e.Expect.Flags ?? []).Concat(e.Expect.NotFlags ?? []))
                if (!Enum.TryParse<DateFlag>(flag, true, out _)) problems.Add($"{e.Id}: unknown flag '{flag}'");
            if (e.Expect.Status is "parsed" or "partial" && e.Expect.TimeSpan is null && e.Expect.Begin is null && e.Expect.End is null && e.Expect.Years is null && e.Expect.Canonical is null && e.Expect.Kind is null)
                problems.Add($"{e.Id}: a parsed expectation needs a canonical, a span, years or a kind");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Ids_are_unique_across_the_corpus()
    {
        var duplicates = Files.Value.SelectMany(f => f.Entries).GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, "Duplicate ids: " + string.Join(", ", duplicates));
    }

    [Theory]
    [MemberData(nameof(VerifiedEntries))]
    public void Verified_entry_passes(string file, string id)
    {
        var entry = Files.Value.Single(f => f.Path == file).Entries.Single(e => e.Id == id);
        var result = new CorpusEvaluator(Profiles.Value.Get).Evaluate(entry);
        output.WriteLine($"\"{entry.Input}\" -> {result.Result.Canonical ?? result.Result.Status.ToString()} (confidence {result.Result.Confidence:0.00})");
        foreach (var note in result.Result.Notes) output.WriteLine("  " + note);
        Assert.True(result.Passed, $"{file} {id} \"{entry.Input}\":\n  " + string.Join("\n  ", result.Failures));
    }

    [Fact]
    public void Evaluator_checks_each_set_field_and_nothing_else()
    {
        var result = TimeSpanParser.Default.Parse("1850~");
        Assert.Empty(CorpusEvaluator.Check(new Expectation { Canonical = "1850~", Flags = ["Approximate"], NotFlags = ["Uncertain"], MinConfidence = 0.9 }, result));
        Assert.Empty(CorpusEvaluator.Check(new Expectation { TimeSpan = new ExpectedTimeSpan { BeginOfTheBegin = "1850-01-01T00:00:00Z", EndOfTheEnd = "1850-12-31T23:59:59Z" } }, result));
        Assert.Empty(CorpusEvaluator.Check(new Expectation { Years = new ExpectedYears(1849, 1851), ToleranceYears = 1 }, result));

        var failures = CorpusEvaluator.Check(new Expectation
        {
            Status = "undated", Kind = "birthDeath", Canonical = "1850", Edtf = "1851", Flags = ["Uncertain"], NotFlags = ["Approximate"],
            MaxConfidence = 0.5, TimeSpan = new ExpectedTimeSpan { BeginOfTheBegin = "1850-01-01T00:00:00Z" }, Begin = new ExpectedTimeSpan(),
        }, result);
        Assert.Equal(9, failures.Count);
        Assert.Contains(failures, f => f.StartsWith("timespan.end_of_the_end: expected none"));
        Assert.Contains(failures, f => f.StartsWith("begin: expected a span"));
    }

    [Fact]
    public void Calibration_error_is_zero_for_a_perfectly_calibrated_run_and_grows_with_overconfidence()
    {
        var entry = new CorpusEntry { Id = "x", Input = "1850", Expect = new Expectation() };
        var sure = new TimeSpanParseResult { Input = "1850", Status = ParseStatus.Parsed, Confidence = 1.0 };
        var good = new EntryResult(entry, sure, []);
        var bad = new EntryResult(entry, sure, ["wrong"]);
        Assert.Equal(0, CorpusEvaluator.ExpectedCalibrationError([good, good]));
        Assert.Equal(0.5, CorpusEvaluator.ExpectedCalibrationError([good, bad]), 3);
    }
}
