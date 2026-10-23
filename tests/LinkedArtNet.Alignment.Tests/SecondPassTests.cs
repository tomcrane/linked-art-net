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
}
