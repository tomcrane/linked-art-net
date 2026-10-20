using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class ReviewServiceTests(PostgresFixture pg)
{
    private const string UlanSample = """
        <http://vocab.getty.edu/ulan/500000001> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#PersonConcept> .
        <http://vocab.getty.edu/ulan/500000001> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1500000001-en> .
        <http://vocab.getty.edu/ulan/500000001> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1500000001-en> .
        <http://vocab.getty.edu/ulan/term/1500000001-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Smith, John"@en .
        <http://vocab.getty.edu/ulan/500000001> <http://www.w3.org/2004/02/skos/core#exactMatch> <http://www.wikidata.org/entity/Q1> .
        <http://vocab.getty.edu/ulan/500000002> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#PersonConcept> .
        <http://vocab.getty.edu/ulan/500000002> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1500000002-en> .
        <http://vocab.getty.edu/ulan/500000002> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1500000002-en> .
        <http://vocab.getty.edu/ulan/term/1500000002-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Smith, John"@en .
        """;

    [PostgresFact]
    public async Task The_desk_shows_a_queued_string_and_its_actions_decide_and_undo()
    {
        var ds = "rv-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test");
        var manifest = ProjectManifest.PmcDefault;
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], manifest.ToOptions());
        var desk = new ReviewService(pg.Store, scheme, reconciler);

        var smith = await pg.Store.CollectAsync(ds, "persauthorfull", "Smith, John (author)");
        var nobody = await pg.Store.CollectAsync(ds, "persauthorfull", "Nobody, Zed");
        await reconciler.RunAsync(ds);

        // the queue holds the ambiguous Smith; the minted Nobody is not queued
        var counts = await desk.QueueCountsAsync(ds);
        Assert.Equal(1, Assert.Single(counts).Count);
        var item = (await desk.NextAsync(ds))!;
        Assert.Equal(smith.Id, item.String.Id);
        Assert.True(item.IsQueued);
        Assert.Equal("Smith, John", item.Evidence.Reading!.Text);
        Assert.Equal(2, item.Evidence.Groups.Count);
        Assert.Equal(2, item.Records.Count); // both ULAN records fetched for the cards
        Assert.Equal(EntityStatus.Provisional, item.Entity!.Status);
        Assert.Null(await desk.NextAsync(ds, afterStringId: smith.Id));

        // accept one candidate: a new entity with the ULAN id and its Wikidata link; the decision is human; the provisional lookalike is folded into it
        var provisionalId = item.Entity.Id;
        var accepted = await desk.AcceptAsync(ds, smith.Id, "http://vocab.getty.edu/ulan/500000001", "tester", "the painter, not the engraver");
        Assert.Equal(EntityStatus.Confirmed, accepted.Status);
        Assert.Equal(accepted.Id, (await pg.Store.ResolveEntityAsync(provisionalId))!.Id);
        Assert.Equal(["http://vocab.getty.edu/ulan/500000001", "http://www.wikidata.org/entity/Q1"], accepted.Equivalents.Select(q => q.Uri).OrderBy(x => x));
        var decision = (await pg.Store.DecisionForAsync(smith.Id))!;
        Assert.Equal(DecisionMethod.Human, decision.Method);
        Assert.Equal("tester", decision.Reviewer);
        Assert.Contains("500000001", decision.EvidenceJson);
        Assert.Single(await pg.Store.NotesAsync(ds, ReviewService.StringTarget(smith.Id)));
        Assert.Empty(await desk.QueueCountsAsync(ds));

        // another project's desk cannot touch this string, and a made-up target is refused
        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.AcceptAsync("other-project", smith.Id, "http://vocab.getty.edu/ulan/500000002", "intruder"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.UndoAsync("other-project", smith.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.AcceptAsync(ds, smith.Id, scheme.Uri(EntityClass.Person, "madeup12"), "tester"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.AcceptAsync(ds, smith.Id, "http://example.org/not-an-authority", "tester"));
        Assert.Equal(DecisionMethod.Human, (await pg.Store.DecisionForAsync(smith.Id))!.Method);

        // undo: the queued decision stands again
        var reverted = (await desk.UndoAsync(ds, smith.Id))!;
        Assert.Equal(DecisionMethod.Queued, reverted.Method);
        Assert.Equal(1, Assert.Single(await desk.QueueCountsAsync(ds)).Count);

        // accepting again reuses the entity that already has the id
        var again = await desk.AcceptAsync(ds, smith.Id, "http://vocab.getty.edu/ulan/500000001", "tester");
        Assert.Equal(accepted.Id, again.Id);

        // keep local: the minted Nobody becomes a confirmed local authority with a better label
        var local = await desk.KeepLocalAsync(ds, nobody.Id, "tester", label: "Nobody, Zedekiah", note: "Edinburgh dealer");
        Assert.Equal(EntityStatus.Confirmed, local.Status);
        Assert.Equal("Nobody, Zedekiah", local.Label);
        Assert.Contains("Nobody, Zed", local.Labels);
        Assert.Equal(DecisionMethod.Human, (await pg.Store.DecisionForAsync(nobody.Id))!.Method);

        // same as: a third string is the same as the accepted Smith
        var smithToo = await pg.Store.CollectAsync(ds, "corpauthor", "J. Smith");
        await reconciler.ReconcileOneAsync(ds, smithToo);
        var same = await desk.SameAsAsync(ds, smithToo.Id, accepted.Id, "tester");
        Assert.Equal(accepted.Id, same.Id);
        Assert.Equal(accepted.Id, (await pg.Store.ResolveAsync(ds, "corpauthor", "J. Smith"))!.Id);

        // override: a spelling is pinned to the other Smith for good, and the reconciler honours it next time
        var pinned = await pg.Store.CollectAsync(ds, "persauthorfull", "Smith, John, the engraver");
        await reconciler.ReconcileOneAsync(ds, pinned);
        var overrideEntity = await desk.OverrideAsync(ds, pinned.Id, "http://vocab.getty.edu/ulan/500000002", "tester");
        Assert.NotEqual(accepted.Id, overrideEntity.Id);
        Assert.Equal(DecisionMethod.Override, (await pg.Store.DecisionForAsync(pinned.Id))!.Method);
        Assert.Equal(overrideEntity.Id, (await pg.Store.OverrideForAsync(ds, pinned.Context, pinned.Normalised))!.Target);
        Assert.Null(await pg.Store.OverrideForAsync(ds, "corpauthor", pinned.Normalised)); // scoped to the field it came from
        var evaluation = await reconciler.EvaluateAsync(ds, pinned);
        Assert.Equal(Outcome.Overridden, evaluation.Outcome);
        // undoing an "always" removes the override it made; undoing one that replaced an earlier override restores that one
        await desk.UndoAsync(ds, pinned.Id);
        Assert.Null(await pg.Store.OverrideForAsync(ds, pinned.Context, pinned.Normalised));
        Assert.Equal(DecisionMethod.Minted, (await pg.Store.DecisionForAsync(pinned.Id))!.Method);
        await desk.OverrideAsync(ds, pinned.Id, "http://vocab.getty.edu/ulan/500000001", "tester");
        var firstTarget = (await pg.Store.OverrideForAsync(ds, pinned.Context, pinned.Normalised))!.Target;
        await desk.OverrideAsync(ds, pinned.Id, "http://vocab.getty.edu/ulan/500000002", "tester");
        Assert.NotEqual(firstTarget, (await pg.Store.OverrideForAsync(ds, pinned.Context, pinned.Normalised))!.Target);
        await desk.UndoAsync(ds, pinned.Id);
        Assert.Equal(firstTarget, (await pg.Store.OverrideForAsync(ds, pinned.Context, pinned.Normalised))!.Target);
        // widened to any field on request
        await desk.OverrideAsync(ds, pinned.Id, "http://vocab.getty.edu/ulan/500000002", "tester", anyField: true);
        Assert.NotNull(await pg.Store.OverrideForAsync(ds, "corpauthor", pinned.Normalised));

        // searching for "same as" and for other records
        Assert.Contains(await desk.SearchEntitiesAsync(ds, "john smith"), e => e.Id == accepted.Id);
        Assert.Equal(2, (await desk.SearchIndexesAsync("Smith, John", "ulan")).Count);
    }

    [PostgresFact]
    public async Task Projects_users_memberships_and_notes_round_trip()
    {
        var ds = "pj-" + IdMinter.Generate(6);
        await pg.Store.UpsertProjectAsync(new Project(ds, "Test project", "a description", "https://data.example.org/" + ds, ProjectManifest.PmcDefault.ToJson(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var project = (await pg.Store.GetProjectAsync(ds))!;
        Assert.Equal("Test project", project.Name);
        Assert.Equal(7, ProjectManifest.FromJson(project.ManifestJson).Contexts.Count); // the five PMC fields and the two derived keyword contexts
        Assert.Contains(await pg.Store.ListProjectsAsync(), p => p.Dataset == ds);

        await pg.Store.UpsertUserAsync("alice", "Alice");
        await pg.Store.UpsertUserAsync("alice", null); // keeps the name
        Assert.Equal("Alice", (await pg.Store.GetUserAsync("alice"))!.DisplayName);
        await pg.Store.SetMembershipAsync(ds, "alice", ProjectRole.Reviewer);
        await pg.Store.SetMembershipAsync(ds, "alice", ProjectRole.Operator);
        Assert.Equal(ProjectRole.Operator, Assert.Single(await pg.Store.MembershipsAsync(ds)).Role);
        Assert.Contains(await pg.Store.MembershipsForAsync("alice"), m => m.Dataset == ds);
        await Assert.ThrowsAsync<ArgumentException>(() => pg.Store.SetMembershipAsync(ds, "alice", "king"));
        Assert.True(ProjectRole.Covers(ProjectRole.Owner, ProjectRole.Reviewer));
        Assert.False(ProjectRole.Covers(ProjectRole.Reviewer, ProjectRole.Operator));
        await pg.Store.RemoveMembershipAsync(ds, "alice");
        Assert.Empty(await pg.Store.MembershipsAsync(ds));

        var note = await pg.Store.AddNoteAsync(ds, "string:1", "alice", "check this");
        Assert.Equal("check this", Assert.Single(await pg.Store.NotesAsync(ds, "string:1")).Text);
        Assert.True(note.Id > 0);
    }
}
