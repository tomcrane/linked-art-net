using System.Text;
using LinkedArtNet.Dates.Corpus;

namespace LinkedArtNet.Dates.Tests;

/// <summary>The pull request review that corpus-review.yml posts: what the maintainer needs to decide, and the commands to act.</summary>
public sealed class CorpusReviewTests : IDisposable
{
    private static readonly Lazy<Realisation.ProfileCatalog> Profiles = new(() => Realisation.ProfileCatalog.Load(TestPaths.Profiles));
    private readonly CorpusEvaluator _evaluator = new(Profiles.Value.Get);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lan-review-" + Guid.NewGuid().ToString("N")[..8]);

    private CorpusFile Load(string name, string json)
    {
        Directory.CreateDirectory(Path.Combine(_dir, "proposed"));
        var path = Path.Combine(_dir, "proposed", name);
        File.WriteAllText(path, json, new UTF8Encoding(false));
        return CorpusFile.Load(path, _dir);
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private const string Mixed = """
        {
          "source": { "name": "Trainer submissions", "licence": "CC0-1.0", "notes": "Reviewed by local." },
          "defaults": { "status": "proposed", "tags": ["trainer"] },
          "entries": [
            { "id": "tr-001", "input": "1984", "expect": { "status": "parsed", "canonical": "1984" } },
            { "id": "tr-002", "input": "a | b", "notes": "pipes in the input", "expect": { "status": "parsed", "canonical": "1985" } },
            { "id": "tr-003", "input": "century 19", "expect": { "status": "parsed", "canonical": "century 19" } }
          ]
        }
        """;

    [Fact]
    public void Starts_with_the_marker_and_summarises_the_files()
    {
        var md = CorpusReview.Markdown([Load("trainer.json", Mixed)], _evaluator, "0.2.0");

        Assert.StartsWith(CorpusReview.Marker + "\n", md.Replace("\r\n", "\n"));
        Assert.Contains("Parser 0.2.0. 1 file changed, 3 entries: 2 pass, 1 fail.", md);
        Assert.Contains("### `corpus/proposed/trainer.json`", md);
        Assert.Contains("3 entries (3 proposed), 2 pass, 1 fail.", md);
        Assert.Contains("Only `verified` entries gate CI", md);
    }

    [Fact]
    public void Failures_come_first_with_their_reasons_and_the_reviewer_note()
    {
        var md = CorpusReview.Markdown([Load("trainer.json", Mixed)], _evaluator, "0.2.0");
        var lines = md.Split('\n').Select(l => l.TrimEnd()).ToList();

        var firstRow = lines.First(l => l.StartsWith("| **fail** |") || l.StartsWith("| pass |"));
        Assert.StartsWith("| **fail** | tr-002 | `a \\| b` |", firstRow);
        Assert.Contains("- **tr-002** `a \\| b` (pipes in the input)", md);
        Assert.Contains("  - canonical: expected '1985', got '", md);
        Assert.Contains("| pass | tr-001 | `1984` | parsed `1984` | parsed `1984` |", md);
    }

    [Fact]
    public void Next_steps_name_the_promote_commands_for_the_branch()
    {
        var md = CorpusReview.Markdown([Load("trainer.json", Mixed)], _evaluator, "0.2.0");

        Assert.Contains("dotnet run --project tools/Corpus -- promote corpus/proposed/trainer.json tr-002 --to disputed", md);
        Assert.Contains("dotnet run --project tools/Corpus -- promote corpus/proposed/trainer.json\n", md.Replace("\r\n", "\n"));
    }

    [Fact]
    public void A_clean_file_gets_a_single_promote_command()
    {
        var clean = Mixed.Replace("\"canonical\": \"1985\"", "\"canonical\": \"a\"").Replace("\"input\": \"a | b\"", "\"input\": \"1985\"").Replace("\"canonical\": \"a\"", "\"canonical\": \"1985\"");
        var md = CorpusReview.Markdown([Load("clean.json", clean)], _evaluator, "0.2.0");

        Assert.Contains("3 pass, 0 fail.", md);
        Assert.DoesNotContain("--to disputed", md);
        Assert.DoesNotContain("Failures:", md);
        Assert.Contains("promote corpus/proposed/clean.json", md);
    }

    [Fact]
    public void Passing_rows_are_capped_but_failures_never_are()
    {
        var entries = string.Join(",\n", Enumerable.Range(1, 60).Select(i => $$$"""{ "id": "p-{{{i:000}}}", "input": "{{{1900 + i}}}", "expect": { "status": "parsed", "canonical": "{{{1900 + i}}}" } }"""));
        var json = $$"""{ "source": { "name": "Big", "licence": "CC0-1.0" }, "entries": [ {{entries}} ] }""";
        var md = CorpusReview.Markdown([Load("big.json", json)], _evaluator, "0.2.0", maxPassingRows: 10);

        Assert.Equal(10, md.Split('\n').Count(l => l.StartsWith("| pass | p-")));
        Assert.Contains("_and 50 more passing entries_", md);
    }

    [Fact]
    public void A_failing_verified_entry_is_called_out_as_blocking()
    {
        var md = CorpusReview.Markdown([Load("v.json", Mixed.Replace("\"status\": \"proposed\"", "\"status\": \"verified\""))], _evaluator, "0.2.0");

        Assert.Contains("**1 verified entry fails, which blocks CI.**", md);
    }
}
