using System.Text;
using LinkedArtNet.Dates.Corpus;

namespace LinkedArtNet.Dates.Tests;

/// <summary>
/// <c>tools/Corpus promote</c>: flipping entry statuses in place. The edit must touch only status tokens, so a
/// maintainer's diff is one line per entry, and promoting to verified must refuse anything that fails.
/// </summary>
public sealed class CorpusPromotionTests : IDisposable
{
    private static readonly Lazy<Realisation.ProfileCatalog> Profiles = new(() => Realisation.ProfileCatalog.Load(TestPaths.Profiles));
    private readonly CorpusEvaluator _evaluator = new(Profiles.Value.Get);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lan-promote-" + Guid.NewGuid().ToString("N")[..8]);

    private const string TrainerFile = """
        {
          "$schema": "../schema/corpus-entry.schema.json",
          "source": { "name": "Trainer submissions", "licence": "CC0-1.0", "notes": "Proposed until a maintainer verifies them." },
          "defaults": { "status": "proposed", "tags": ["trainer"] },
          "entries": [
            { "id": "tr-001", "input": "1984", "expect": { "status": "parsed", "canonical": "1984" } },
            { "id": "tr-002", "input": "1850~", "status": "proposed", "tags": ["circa"], "expect": { "status": "parsed", "canonical": "1850~" } },
            { "id": "tr-003", "input": "century 19", "expect": { "status": "parsed", "canonical": "century 19" } }
          ]
        }

        """;

    private string Write(string name, string content, bool bom = false)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content, new UTF8Encoding(bom));
        return path;
    }

    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static IReadOnlyDictionary<string, EntryStatus> Statuses(string path) =>
        CorpusFile.Load(path).Entries.ToDictionary(e => e.Id, e => e.Status, StringComparer.Ordinal);

    [Fact]
    public void Whole_file_promotion_flips_the_default_and_every_explicit_status_and_nothing_else()
    {
        var path = Write("trainer.json", TrainerFile);
        var outcome = CorpusPromotion.Promote(path, null, EntryStatus.Verified, _evaluator);

        Assert.True(outcome.Applied);
        Assert.Equal(["tr-001", "tr-002", "tr-003"], outcome.Changed);
        Assert.All(Statuses(path).Values, s => Assert.Equal(EntryStatus.Verified, s));
        Assert.Equal(TrainerFile.Replace("\"proposed\"", "\"verified\""), File.ReadAllText(path));
    }

    [Fact]
    public void Partial_promotion_inserts_a_status_after_the_id_and_leaves_the_others_proposed()
    {
        var path = Write("trainer.json", TrainerFile);
        var outcome = CorpusPromotion.Promote(path, ["tr-001"], EntryStatus.Verified, _evaluator);

        Assert.Equal(["tr-001"], outcome.Changed);
        var statuses = Statuses(path);
        Assert.Equal(EntryStatus.Verified, statuses["tr-001"]);
        Assert.Equal(EntryStatus.Proposed, statuses["tr-002"]);
        Assert.Equal(EntryStatus.Proposed, statuses["tr-003"]);
        var text = File.ReadAllText(path);
        Assert.Contains("{ \"id\": \"tr-001\", \"status\": \"verified\", \"input\": \"1984\"", text);
        Assert.Contains("\"defaults\": { \"status\": \"proposed\"", text);
    }

    [Fact]
    public void Partial_promotion_replaces_an_explicit_status_in_place()
    {
        var path = Write("trainer.json", TrainerFile);
        CorpusPromotion.Promote(path, ["tr-002"], EntryStatus.Verified, _evaluator);

        Assert.Equal(EntryStatus.Verified, Statuses(path)["tr-002"]);
        Assert.Equal(TrainerFile.Replace("\"status\": \"proposed\", \"tags\": [\"circa\"]", "\"status\": \"verified\", \"tags\": [\"circa\"]"), File.ReadAllText(path));
    }

    [Fact]
    public void Verified_is_refused_when_an_entry_fails_and_the_file_is_untouched()
    {
        var wrong = TrainerFile.Replace("\"canonical\": \"1984\"", "\"canonical\": \"1985\"");
        var path = Write("trainer.json", wrong);
        var outcome = CorpusPromotion.Promote(path, null, EntryStatus.Verified, _evaluator);

        Assert.False(outcome.Applied);
        Assert.Empty(outcome.Changed);
        Assert.Equal("tr-001", Assert.Single(outcome.Failures).Entry.Id);
        Assert.Equal(wrong, File.ReadAllText(path));
        Assert.All(Statuses(path).Values, s => Assert.Equal(EntryStatus.Proposed, s));
    }

    [Fact]
    public void Disputed_does_not_require_the_entry_to_pass()
    {
        var path = Write("trainer.json", TrainerFile.Replace("\"canonical\": \"1984\"", "\"canonical\": \"1985\""));
        var outcome = CorpusPromotion.Promote(path, ["tr-001"], EntryStatus.Disputed, _evaluator);

        Assert.True(outcome.Applied);
        Assert.Equal(EntryStatus.Disputed, Statuses(path)["tr-001"]);
    }

    [Fact]
    public void Unknown_ids_are_an_error()
    {
        var path = Write("trainer.json", TrainerFile);
        var ex = Assert.Throws<ArgumentException>(() => CorpusPromotion.Promote(path, ["tr-001", "nope"], EntryStatus.Verified, _evaluator));
        Assert.Contains("nope", ex.Message);
        Assert.Equal(TrainerFile, File.ReadAllText(path));
    }

    [Fact]
    public void Entries_already_at_the_status_are_reported_not_changed()
    {
        var path = Write("trainer.json", TrainerFile.Replace("\"status\": \"proposed\", \"tags\": [\"circa\"]", "\"status\": \"verified\", \"tags\": [\"circa\"]"));
        var outcome = CorpusPromotion.Promote(path, null, EntryStatus.Verified, _evaluator);

        Assert.Equal(["tr-001", "tr-003"], outcome.Changed);
        Assert.Equal(["tr-002"], outcome.Unchanged);
    }

    [Fact]
    public void Dry_run_reports_what_would_change_without_writing()
    {
        var path = Write("trainer.json", TrainerFile);
        var outcome = CorpusPromotion.Promote(path, null, EntryStatus.Verified, _evaluator, dryRun: true);

        Assert.Equal(3, outcome.Changed.Count);
        Assert.Equal(TrainerFile, File.ReadAllText(path));
    }

    [Fact]
    public void A_file_without_defaults_gets_an_explicit_status_on_every_entry()
    {
        var noDefaults = TrainerFile.Replace("  \"defaults\": { \"status\": \"proposed\", \"tags\": [\"trainer\"] },\n", "");
        var path = Write("plain.json", noDefaults);
        CorpusPromotion.Promote(path, null, EntryStatus.Verified, _evaluator);

        Assert.All(Statuses(path).Values, s => Assert.Equal(EntryStatus.Verified, s));
        Assert.Contains("{ \"id\": \"tr-003\", \"status\": \"verified\", \"input\"", File.ReadAllText(path));
    }

    [Fact]
    public void Defaults_without_a_status_gain_one()
    {
        var path = Write("plain.json", TrainerFile.Replace("\"defaults\": { \"status\": \"proposed\", \"tags\": [\"trainer\"] }", "\"defaults\": { \"tags\": [\"trainer\"] }"));
        CorpusPromotion.Promote(path, null, EntryStatus.Verified, _evaluator);

        Assert.All(Statuses(path).Values, s => Assert.Equal(EntryStatus.Verified, s));
        Assert.Contains("\"defaults\": { \"status\": \"verified\", \"tags\": [\"trainer\"] }", File.ReadAllText(path));
    }

    [Fact]
    public void A_byte_order_mark_is_preserved_and_offsets_stay_right()
    {
        var path = Write("bom.json", TrainerFile, bom: true);
        CorpusPromotion.Promote(path, ["tr-002"], EntryStatus.Verified, _evaluator);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal(EntryStatus.Verified, Statuses(path)["tr-002"]);
    }
}
