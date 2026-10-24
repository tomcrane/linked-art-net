using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class SecondPassTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_string_queued_early_in_a_run_is_settled_at_its_end_by_an_entity_the_run_made_later()
    {
        // finding 51 (2026-10-23): "Lorem, Ipsum" is queued between ULAN's two undated namesakes; later in the run an override decides
        // "Lorem, Ipsum (editor)" onto one of them and makes the aligned entity; the second pass answers the first string from the store.
        // "Dolor, Sit", queued the same way with nothing to settle it, stays queued with its placeholder
        var ds = "sp" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500900001", "ulan", "Lorem, Ipsum", ["Lorem, Ipsum"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500900002", "ulan", "Lorem, Ipsum", ["Lorem, Ipsum"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500900003", "ulan", "Dolor, Sit", ["Dolor, Sit"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500900004", "ulan", "Dolor, Sit", ["Dolor, Sit"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        var lorem = await pg.Store.CollectAsync(ds, "persauthorfull", "Lorem, Ipsum");
        var dolor = await pg.Store.CollectAsync(ds, "persauthorfull", "Dolor, Sit");
        var editor = await pg.Store.CollectAsync(ds, "persauthorfull", "Lorem, Ipsum (editor)");
        await pg.Store.PutOverrideAsync(new Override(ds, "persauthorfull", editor.Normalised, "http://vocab.getty.edu/ulan/500900001", "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());
        var narrated = new List<string>();
        var report = await reconciler.RunAsync(ds, runId: "run-test-second-pass", onDecided: (e, n, total) => narrated.Add($"{n}/{total} {e.Reading.Text} {e.Outcome}"));
        Assert.Equal(3, report.Considered);
        Assert.Equal(1, report.Overridden); Assert.Equal(1, report.MatchedStore); Assert.Equal(1, report.Queued);
        Assert.Equal(2, report.Reconsidered); Assert.Equal(1, report.Settled);
        Assert.Contains("second pass: 2 queued strings reconsidered, 1 settled", report.ToString());
        Assert.Contains("1/2 Lorem, Ipsum MatchedStore", narrated); // the settled string, numbered within the pass
        var loremDecision = (await pg.Store.DecisionForAsync(lorem.Id))!;
        Assert.Equal(DecisionMethod.ExactName, loremDecision.Method); Assert.Equal("run-test-second-pass", loremDecision.RunId);
        var editorDecision = (await pg.Store.DecisionForAsync(editor.Id))!;
        Assert.Equal(editorDecision.EntityId, loremDecision.EntityId); // the same aligned entity
        var loremEntities = await pg.Store.FindEntitiesByLabelAsync(StringKey.Normalise("Lorem, Ipsum"), ds, EntityClass.Person);
        Assert.Single(loremEntities); Assert.NotEqual(EntityStatus.Provisional, loremEntities[0].Status); // the placeholder went; the overridden (confirmed) entity stands
        var dolorDecision = (await pg.Store.DecisionForAsync(dolor.Id))!;
        Assert.Equal(DecisionMethod.Queued, dolorDecision.Method);
        Assert.NotNull(await pg.Store.GetEntityAsync(dolorDecision.EntityId!)); // still queued, placeholder kept
        // the undo takes the second pass's decision with the rest of the run
        var undone = await pg.Store.UndoRunAsync("run-test-second-pass");
        Assert.Equal(3, undone.DecisionsInvalidated);
        Assert.Null(await pg.Store.DecisionForAsync(lorem.Id));
    }

    [PostgresFact]
    public async Task A_queued_strings_own_placeholder_never_settles_it()
    {
        // finding 95 (2026-10-24 night, run wy2aqs): a string queued on a dates conflict (finding 93) gets a placeholder minted with its own
        // lifespan; in the second pass that placeholder came back as a store candidate whose dates agree, and the single-source rule
        // accepted the string onto it ("Cozens, John Robert, 1752-1798" -> accepted single store Cozens, John Robert): 265 strings taken
        // off the desk onto their own placeholders, finding 93's seventeen among them. A placeholder awaiting review carries nothing
        var ds = "pl" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n79068451", "loc", "Cozens, John Robert", ["Cozens, John Robert"], "Person", GettyVocabLoader.LifespanFromYears("1752", "1797"), [], null, DateTimeOffset.UtcNow, "test"));
        var cozens = await pg.Store.CollectAsync(ds, "keywords", "Cozens, John Robert, 1752-1799");
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var report = await reconciler.RunAsync(ds, runId: "run-test-placeholder");
        Assert.Equal(1, report.Queued); Assert.Equal(0, report.Minted); Assert.Equal(0, report.Accepted);
        Assert.Equal(1, report.Reconsidered); Assert.Equal(0, report.Settled);
        var decision = (await pg.Store.DecisionForAsync(cozens.Id))!;
        Assert.Equal(DecisionMethod.Queued, decision.Method);
        var placeholder = (await pg.Store.GetEntityAsync(decision.EntityId!))!;
        Assert.Equal(EntityStatus.Provisional, placeholder.Status);
        Assert.Equal("1752-1799", placeholder.Lifespan?.Text);
        // evaluated again with the placeholder in the store, the string is still the desk's, and the placeholder says why it carries nothing
        var again = await reconciler.EvaluateAsync(ds, cozens, includeStore: true);
        Assert.Equal(Outcome.Queued, again.Outcome);
        var own = Assert.Single(again.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == placeholder.Id);
        Assert.Contains("a placeholder awaiting review: carries nothing", own.Reason);
        Assert.Contains(again.Groups.SelectMany(g => g.Members), m => m.Candidate.Source == "loc" && m.Veto && m.DatesConflict); // the conflict that queued it, still there
    }
}
