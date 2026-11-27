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
        // and of the kind the reviewer says, when it is not the field's (the owner, 2026-11-14: a publisher that is a house), noted
        var wasClass = local.Class;
        var asPlace = await desk.KeepLocalAsync(ds, nobody.Id, "tester", cls: EntityClass.Place);
        Assert.Equal(EntityClass.Place, asPlace.Class); Assert.Equal(local.Id, asPlace.Id);
        Assert.Contains(await pg.Store.NotesAsync(ds, asPlace.Id), n => n.Text.StartsWith($"Class changed from {wasClass.ToString().ToLowerInvariant()} to place"));
        await desk.KeepLocalAsync(ds, nobody.Id, "tester", cls: wasClass); // back, for what follows
        Assert.Equal(DecisionMethod.Human, (await pg.Store.DecisionForAsync(nobody.Id))!.Method);
        // keep local with a URL the thing is known at: the identifier in one step; a URL in no recognised namespace is refused
        var dowell = await pg.Store.CollectAsync(ds, "corpauthor", "Dowell's Ltd");
        await reconciler.RunAsync(ds);
        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.KeepLocalAsync(ds, dowell.Id, "tester", knownAt: "https://example.org/nothing/1"));
        // a URL another entity of the project holds is refused with the holder named (doc 12 section 13, item 3)
        var holder = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Dowell's of Edinburgh", ["Dowell's of Edinburgh"], Status: EntityStatus.Confirmed), scheme);
        await pg.Store.AddEquivalentAsync(holder.Id, new Equivalent("http://viaf.org/viaf/999999999", "viaf", DecisionMethod.Human, 1, DateTimeOffset.UtcNow));
        // a string reopened from an entity's page has no standing decision; the desk reconciles it as it is opened and shows it (the owner, 2026-11-14)
        Assert.True(await pg.Store.ReopenStringAsync(dowell.Id));
        Assert.Null(await pg.Store.DecisionForAsync(dowell.Id));
        var opened = await desk.GetAsync(dowell.Id);
        Assert.NotNull(opened);
        Assert.NotNull(await pg.Store.DecisionForAsync(dowell.Id));
        Assert.Null(await desk.GetAsync(987654321)); // no such string: nothing, as before
        // a refused URL changes nothing: the entity is not confirmed or relabelled, and the string is as it was (the owner, 2026-11-14)
        var before = (await pg.Store.DecisionForAsync(dowell.Id))!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.KeepLocalAsync(ds, dowell.Id, "tester", label: "Not this", knownAt: "https://example.org/nothing"));
        var after = (await pg.Store.DecisionForAsync(dowell.Id))!;
        Assert.Equal(before.Id, after.Id);
        Assert.NotEqual("Not this", (await pg.Store.GetEntityAsync(after.EntityId!))!.Label);
        Assert.NotEqual(EntityStatus.Confirmed, (await pg.Store.GetEntityAsync(after.EntityId!))!.Status);
        var inUse = await Assert.ThrowsAsync<IdentifierInUseException>(() => desk.KeepLocalAsync(ds, dowell.Id, "tester", knownAt: "http://viaf.org/viaf/999999999"));
        Assert.Equal(holder.Id, inUse.Holder.Id);
        var known = await desk.KeepLocalAsync(ds, dowell.Id, "tester", note: "Edinburgh auctioneers", knownAt: "http://viaf.org/viaf/123456789");
        Assert.Equal(EntityStatus.Confirmed, known.Status);
        Assert.Contains(known.Equivalents, q => q.Uri == "http://viaf.org/viaf/123456789" && q.Source == "viaf" && q.Method == DecisionMethod.Human);
        Assert.Contains("knownAt", (await pg.Store.DecisionForAsync(dowell.Id))!.EvidenceJson!);

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
        // capitals run together are tried as initials too (the owner at T. S. Eliot's card, 2026-11-27)
        Assert.Contains("t s eliot", ReviewService.SearchKeys("TS Eliot"));
        Assert.Contains("j m w turner", ReviewService.SearchKeys("JMW Turner"));
        Assert.Contains("t s eliot", ReviewService.SearchKeys("Eliot, TS"));
        Assert.DoesNotContain(ReviewService.SearchKeys("BBC"), k => k.Contains(' '));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://www.wikidata.org/entity/Q-initials-" + ds, "wikidata", "T. S. Eliot", ["T. S. Eliot"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        Assert.Contains(await desk.SearchIndexesAsync("TS Eliot", "wikidata"), r => r.Label == "T. S. Eliot");

        // skip sends a string to the back of the desk, kept across sessions (the owner, 2026-11-07): two queued namesakes, the first skipped,
        // the second is the desk's next from the front; after the last unskipped comes the skipped; after the last skipped, nothing
        var jones = await pg.Store.CollectAsync(ds, "persauthorfull", "Jones, Tom");
        var jones2 = await pg.Store.CollectAsync(ds, "corpauthor", "Jones, Tom");
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500000011", "ulan", "Jones, Tom", ["Jones, Tom"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500000012", "ulan", "Jones, Tom", ["Jones, Tom"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await reconciler.RunAsync(ds);
        Assert.Equal(jones.Id, (await desk.NextAsync(ds))!.String.Id);
        await desk.SkipAsync(ds, jones.Id);
        Assert.Equal(jones2.Id, (await desk.NextAsync(ds))!.String.Id); // from the front: the unskipped first (Skip's redirect goes to the front)
        Assert.Equal(jones.Id, (await desk.NextAsync(ds, afterStringId: jones2.Id))!.String.Id); // after the last unskipped: the skipped
        Assert.Null(await desk.NextAsync(ds, afterStringId: jones.Id)); // nothing after the last skipped one: the desk is done
        await desk.SkipAsync(ds, jones2.Id); // both skipped: the order is the order of skipping
        Assert.Equal(jones.Id, (await desk.NextAsync(ds))!.String.Id);
        Assert.Equal(jones2.Id, (await desk.NextAsync(ds, afterStringId: jones.Id))!.String.Id);
        Assert.NotNull((await desk.GetAsync(jones.Id))!.SkippedAt);
        await desk.UnskipAsync(ds, jones2.Id); // unskipped, it is in its place by id again, ahead of the skipped one
        Assert.Null((await desk.GetAsync(jones2.Id))!.SkippedAt);
        Assert.Equal(jones2.Id, (await desk.NextAsync(ds))!.String.Id);
        Assert.Equal(jones.Id, (await desk.NextAsync(ds, afterStringId: jones2.Id))!.String.Id);
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

    [PostgresFact]
    public async Task Two_of_the_projects_entities_ticked_together_are_merged_on_the_reviewers_word()
    {
        // the owner, 2026-11-06, at "Chris Beetles Ltd": the firm's two entities, "Chris Beetles" holding ULAN's record and "Chris Beetles
        // Limited" holding LoC's, in one group with the string's placeholder and a person placeholder of the name; ticking every card met
        // "merge them on the entity page first". The desk now shows the facts of each entity on its card, says what accepting them together
        // will do, and does the merge on the reviewer's word
        var ds = "rv-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        const string ulan = "http://vocab.getty.edu/ulan/500301722", loc = "http://id.loc.gov/authorities/names/n89672369";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(ulan, "ulan", "Chris Beetles", ["Chris Beetles"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(loc, "loc", "Chris Beetles Limited", ["Chris Beetles Limited", "Beetles Limited"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        var beetles = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Chris Beetles", ["Chris Beetles"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(beetles.Id, new Equivalent(ulan, "ulan", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var limited = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Chris Beetles Limited", ["Chris Beetles Limited"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(limited.Id, new Equivalent(loc, "loc", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var person = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Chris Beetles", ["Chris Beetles"]), scheme);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var desk = new ReviewService(pg.Store, scheme, reconciler);
        // a string of each entity's own, so the cards have something to stand for
        var earlier = await pg.Store.CollectAsync(ds, "corpauthor", "Chris Beetles");
        await pg.Store.DecideAsync(earlier.Id, beetles.Id, DecisionMethod.Human, "{}", "tester");
        var man = await pg.Store.CollectAsync(ds, "persauthorfull", "Chris Beetles");
        await pg.Store.DecideAsync(man.Id, person.Id, DecisionMethod.Queued, "{}");
        // the string under review, queued onto its own placeholder with the run's evidence: the four in one group, the placeholders beside
        var ltd = await pg.Store.CollectAsync(ds, "corpauthor", "Chris Beetles Ltd");
        var placeholder = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Chris Beetles Ltd", ["Chris Beetles Ltd"]), scheme);
        static ScoredCandidate Member(string uri, string source, string label, double score, string? entityId = null) =>
            new(new Candidate(uri, source, label, EntityClass.Group, null, [], source == "store" ? "store" : "local", score, entityId), score, false, $"name {score:0.00}");
        var group = new CandidateGroup([Member(ulan, "ulan", "Chris Beetles", 0.97), Member(loc, "loc", "Chris Beetles Limited", 0.97), Member(limited.Id, "store", "Chris Beetles Limited", 0.95, limited.Id), Member(beetles.Id, "store", "Chris Beetles", 0.95, beetles.Id)], 0.97, ["ulan", "loc"], [ulan, loc, limited.Id, beetles.Id]);
        var own = new CandidateGroup([Member(placeholder.Id, "store", "Chris Beetles Ltd", 1.0, placeholder.Id)], 1.0, ["store"], [placeholder.Id]);
        var him = new CandidateGroup([Member(person.Id, "store", "Chris Beetles", 0.95, person.Id)], 0.95, ["store"], [person.Id]);
        var reading = reconciler.Read(ltd);
        await pg.Store.DecideAsync(ltd.Id, placeholder.Id, DecisionMethod.Queued, Reconciler.Evidence("queued", reading, [own, group, him], null));

        // the cards carry the entities' facts: class, status, the strings they stand for
        var item = (await desk.GetAsync(ltd.Id))!;
        Assert.Equal(4, item.Entities.Count);
        Assert.Equal(EntityClass.Person, item.Entities[person.Id].Entity.Class);
        Assert.True(item.Entities[person.Id].IsPlaceholder);
        Assert.False(item.Entities[beetles.Id].IsPlaceholder);
        Assert.Equal(["corpauthor"], item.Entities[beetles.Id].Contexts);
        Assert.Single(item.Entities[beetles.Id].Strings);
        // and the other queued string under the name, the man's; the earlier string, decided, is not waiting
        Assert.Equal([man.Id], item.Siblings.Select(x => x.String.Id));

        // every card ticked: refused until the plan has been seen; the plan names the survivor (the first with authority behind it), the
        // entities merging into it, and the records to add (none: each is held already)
        string[] all = [placeholder.Id, ulan, loc, limited.Id, beetles.Id, person.Id];
        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.AcceptTogetherAsync(ds, ltd.Id, all, "tester"));
        var plan = await desk.PlanTogetherAsync(ds, ltd.Id, all);
        Assert.Equal(limited.Id, plan.Survivor.Id);
        Assert.Equal([placeholder.Id, beetles.Id, person.Id], plan.Merging.Select(e => e.Id));
        Assert.Empty(plan.Identifiers);
        // the reviewer chooses the other survivor and leaves the person out
        string[] firm = [placeholder.Id, ulan, loc, limited.Id, beetles.Id];
        var chosen = await desk.PlanTogetherAsync(ds, ltd.Id, firm, beetles.Id);
        Assert.Equal(beetles.Id, chosen.Survivor.Id);
        Assert.Equal([placeholder.Id, limited.Id], chosen.Merging.Select(e => e.Id));

        var entity = await desk.AcceptTogetherAsync(ds, ltd.Id, firm, "tester", "the gallery", beetles.Id, mergeOurs: true);
        Assert.Equal(beetles.Id, entity.Id);
        Assert.Equal([loc, ulan], entity.Equivalents.Select(q => q.Uri).OrderBy(x => x));
        Assert.Equal(beetles.Id, (await pg.Store.ResolveEntityAsync(limited.Id))!.Id); // merged into the survivor
        Assert.Equal(beetles.Id, (await pg.Store.ResolveEntityAsync(placeholder.Id))!.Id); // the string's own placeholder folded in
        Assert.Equal(EntityStatus.Provisional, (await pg.Store.GetEntityAsync(person.Id))!.Status); // the man, left alone
        var decision = (await pg.Store.DecisionForAsync(ltd.Id))!;
        Assert.Equal(DecisionMethod.Human, decision.Method); Assert.Equal(beetles.Id, decision.EntityId);
        Assert.Contains(limited.Id, decision.EvidenceJson); // the merge is in the evidence
        Assert.Equal(2, (await pg.Store.StringsForEntityAsync(beetles.Id)).Count); // its own string and this one; the merged entity had none
        Assert.Single(await pg.Store.NotesAsync(ds, ReviewService.StringTarget(ltd.Id)));
        // the desk finishes the name before it leaves it: the man's string is what comes next, and the survivor's page knows it is waiting
        Assert.Equal([man.Id], (await desk.SiblingsAsync(ds, ltd.Id, entity)).Select(x => x.String.Id));
        Assert.Equal([man.Id], (await desk.WaitingForAsync(ds, entity)).Select(x => x.String.Id));
        // undo takes back the decision, not the merge
        await desk.UndoAsync(ds, ltd.Id);
        Assert.Equal(DecisionMethod.Queued, (await pg.Store.DecisionForAsync(ltd.Id))!.Method);
        Assert.Equal(beetles.Id, (await pg.Store.ResolveEntityAsync(limited.Id))!.Id);

        // the group's "Always, in this field": accepted together and the spelling overridden to the entity in this field; undo removes the override
        var pub = await pg.Store.CollectAsync(ds, "publisher", "Chris Beetles Ltd");
        var pubPlaceholder = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Chris Beetles Ltd", ["Chris Beetles Ltd"]), scheme);
        await pg.Store.DecideAsync(pub.Id, pubPlaceholder.Id, DecisionMethod.Queued, "{}");
        var always = await desk.AcceptTogetherAsync(ds, pub.Id, [ulan, loc, beetles.Id], "tester", always: true);
        Assert.Equal(beetles.Id, always.Id);
        Assert.Equal(DecisionMethod.Override, (await pg.Store.DecisionForAsync(pub.Id))!.Method);
        var rule = (await pg.Store.OverrideForAsync(ds, "publisher", pub.Normalised))!;
        Assert.Equal(beetles.Id, rule.Target); Assert.Equal("publisher", rule.Context);
        await desk.UndoAsync(ds, pub.Id);
        Assert.Null(await pg.Store.OverrideForAsync(ds, "publisher", pub.Normalised));
        Assert.Equal(DecisionMethod.Queued, (await pg.Store.DecisionForAsync(pub.Id))!.Method);
    }
}
