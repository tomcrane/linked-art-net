using System.Text;
using LinkedArtNet.Dates.Corpus;

namespace LinkedArtNet.Dates.Tests;

/// <summary>
/// <c>tools/Corpus accept</c>: recording that the parser is right where an imported source was wrong. The source's
/// expectation must survive in sourceExpectation, the edit must touch only the entry's own tokens, and the entry must pass afterwards.
/// </summary>
public sealed class CorpusAcceptanceTests : IDisposable
{
    private static readonly Lazy<Realisation.ProfileCatalog> Profiles = new(() => Realisation.ProfileCatalog.Load(TestPaths.Profiles));
    private readonly CorpusEvaluator _evaluator = new(Profiles.Value.Get);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lan-accept-" + Guid.NewGuid().ToString("N")[..8]);
    private static readonly DateOnly Today = new(2026, 10, 14);

    private const string Imported = """
        {
          "$schema": "../schema/corpus-entry.schema.json",
          "source": { "name": "An import", "licence": "MIT" },
          "defaults": { "status": "proposed", "tags": ["imported"] },
          "entries": [
            { "id": "im-001", "input": "1858, this edition published around 1900", "tags": ["edition"],
              "expect": { "status": "parsed", "years": { "start": 1858, "end": 1858 } } },
            { "id": "im-002", "input": "1984", "tags": ["year"],
              "expect": { "status": "parsed", "years": { "start": 1984, "end": 1984 } } },
            { "id": "im-003", "input": "before 1936", "tags": ["open"], "status": "proposed",
              "expect": { "status": "parsed", "years": { "start": 1936, "end": 1936 } }, "sourceExpectation": "1936 (the source gives a point)", "notes": "Kept for review." },
            { "id": "im-004", "input": "n.d.", "tags": ["undated"],
              "expect": { "status": "parsed", "years": { "start": 1900, "end": 1900 } } }
          ]
        }

        """;

    private string Write(string content)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "import.json");
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    [Fact]
    public void Accepting_rewrites_the_expectation_keeps_the_source_and_makes_the_entry_pass()
    {
        var path = Write(Imported);
        var outcome = CorpusAcceptance.Accept(path, ["im-001"], null, null, _evaluator, today: Today);

        var change = Assert.Single(outcome.Changed);
        Assert.Equal("status parsed; years 1858..1858", change.Before);
        Assert.StartsWith("status parsed; kind single; canonical 1900~; edtf 1900~; timespan 1900-01-01T00:00:00Z..1900-12-31T23:59:59Z", change.After);

        var reloaded = CorpusFile.Load(path);
        var entry = reloaded.Entries.Single(e => e.Id == "im-001");
        Assert.True(_evaluator.Evaluate(entry).Passed);
        Assert.Equal("1900~", entry.Expect.Canonical);
        Assert.Equal("status parsed; years 1858..1858", entry.SourceExpectation);
        Assert.Equal("Accepted the parser's reading on 2026-10-14; the expectation had been: status parsed; years 1858..1858.", entry.Notes);
        Assert.Equal(EntryStatus.Proposed, entry.Status);

        // the other entries' text is untouched
        var text = File.ReadAllText(path);
        Assert.Contains("{ \"id\": \"im-002\", \"input\": \"1984\", \"tags\": [\"year\"],\n      \"expect\": { \"status\": \"parsed\", \"years\": { \"start\": 1984, \"end\": 1984 } } },", text.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Passing_entries_are_left_alone_and_reported()
    {
        var path = Write(Imported);
        var outcome = CorpusAcceptance.Accept(path, null, e => e.Tags.Contains("year") || e.Tags.Contains("edition"), null, _evaluator, today: Today);

        Assert.Equal(["im-002"], outcome.AlreadyPassing);
        Assert.Equal(["im-001"], outcome.Changed.Select(c => c.Id));
    }

    [Fact]
    public void An_existing_source_expectation_and_notes_are_kept_and_extended()
    {
        var path = Write(Imported);
        CorpusAcceptance.Accept(path, ["im-003"], null, null, _evaluator, today: Today);

        var entry = CorpusFile.Load(path).Entries.Single(e => e.Id == "im-003");
        Assert.Equal("1936 (the source gives a point)", entry.SourceExpectation);
        Assert.StartsWith("Kept for review. Accepted the parser's reading on 2026-10-14;", entry.Notes);
        Assert.Equal("before 1936", entry.Expect.Canonical);
        Assert.Null(entry.Expect.TimeSpan!.BeginOfTheBegin);
        Assert.True(_evaluator.Evaluate(entry).Passed);
    }

    [Fact]
    public void Undated_readings_pin_only_the_status()
    {
        var path = Write(Imported);
        CorpusAcceptance.Accept(path, ["im-004"], null, null, _evaluator, today: Today);

        var entry = CorpusFile.Load(path).Entries.Single(e => e.Id == "im-004");
        Assert.Equal("undated", entry.Expect.Status);
        Assert.Null(entry.Expect.Canonical);
        Assert.True(_evaluator.Evaluate(entry).Passed);
    }

    [Fact]
    public void Status_can_be_set_in_the_same_edit()
    {
        var path = Write(Imported);
        CorpusAcceptance.Accept(path, ["im-001", "im-003"], null, EntryStatus.Verified, _evaluator, today: Today);

        var statuses = CorpusFile.Load(path).Entries.ToDictionary(e => e.Id, e => e.Status);
        Assert.Equal(EntryStatus.Verified, statuses["im-001"]); // inserted after the id
        Assert.Equal(EntryStatus.Verified, statuses["im-003"]); // replaced the explicit "proposed"
        Assert.Equal(EntryStatus.Proposed, statuses["im-002"]);
    }

    [Fact]
    public void Dry_run_reports_without_writing()
    {
        var path = Write(Imported);
        var outcome = CorpusAcceptance.Accept(path, null, null, null, _evaluator, dryRun: true, today: Today);

        Assert.Equal(3, outcome.Changed.Count);
        Assert.Equal(Imported, File.ReadAllText(path));
    }

    [Fact]
    public void Unknown_ids_are_an_error()
    {
        var path = Write(Imported);
        Assert.Throws<ArgumentException>(() => CorpusAcceptance.Accept(path, ["nope"], null, null, _evaluator, today: Today));
    }
}
