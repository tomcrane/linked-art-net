using System.Text.Json;
using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Import;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class CurationServiceTests(PostgresFixture pg)
{
    private const string UlanSample = """
        <http://vocab.getty.edu/ulan/500301417> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#GroupConcept> .
        <http://vocab.getty.edu/ulan/500301417> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1-en> .
        <http://vocab.getty.edu/ulan/500301417> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1-en> .
        <http://vocab.getty.edu/ulan/term/1-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Thomas Agnew & Sons"@en .
        <http://vocab.getty.edu/ulan/500301417> <http://www.w3.org/2004/02/skos/core#exactMatch> <https://viaf.org/viaf/135146590> .
        """;

    [PostgresFact]
    public async Task Variants_cluster_into_one_entity_with_overrides_ids_and_an_export()
    {
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test");
        var manifest = ProjectManifest.PmcDefault;
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], manifest.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);

        var spellings = new[] { "Thos. Agnew & Sons Ltd", "Agnew's", "Thos Agnew and Sons", "Agnew & Son", "Messrs Agnew" };
        var strings = new List<SourceString>();
        foreach (var s in spellings) strings.Add(await pg.Store.CollectAsync(ds, "corpauthor", s));
        strings.Add(await pg.Store.CollectAsync(ds, "corpauthor", "Agnew, Joan (editor)")); // not the firm
        await reconciler.RunAsync(ds);

        // the variants view: everything containing "agnew", grouped by what it resolves to
        Assert.Equal("agnew", CurationService.SeedWord("Thos. Agnew & Sons Ltd"));
        var groups = await curation.VariantsAsync(ds, "Thos. Agnew & Sons Ltd");
        Assert.Equal(6, groups.Sum(g => g.Strings.Count));

        // cluster the five firm spellings into a new confirmed local authority with the ULAN id attached
        var firm = await curation.ClusterAsync(ds, strings.Take(5).Select(s => s.Id).ToList(), null, "Thomas Agnew & Sons", ["http://vocab.getty.edu/ulan/500301417"], "tester", "London dealers");
        Assert.Equal(EntityStatus.Confirmed, firm.Status);
        Assert.Equal("Thomas Agnew & Sons", firm.Label);
        Assert.Contains("Agnew's", firm.Labels);
        Assert.Contains(firm.Equivalents, q => q.Uri == "http://vocab.getty.edu/ulan/500301417" && q.Method == DecisionMethod.Human);
        foreach (var s in strings.Take(5))
        {
            Assert.Equal(firm.Id, (await pg.Store.ResolveAsync(ds, "corpauthor", s.Raw))!.Id);
            Assert.Equal(DecisionMethod.Human, (await pg.Store.DecisionForAsync(s.Id))!.Method);
            Assert.Equal(firm.Id, (await pg.Store.OverrideForAsync(ds, "corpauthor", s.Normalised))!.Target);
            Assert.Null(await pg.Store.OverrideForAsync(ds, "publisher", s.Normalised)); // scoped to the field the strings came from
        }
        Assert.NotEqual(firm.Id, (await pg.Store.ResolveAsync(ds, "corpauthor", "Agnew, Joan (editor)"))!.Id);
        Assert.Single(await pg.Store.NotesAsync(ds, firm.Id));
        // the same spelling in another field is not caught until the rule is widened to any field
        var twin = await pg.Store.CollectAsync(ds, "publisher", "Agnew's");
        Assert.NotEqual(Outcome.Overridden, await reconciler.EvaluateAsync(ds, twin).ContinueWith(t => t.Result.Outcome));
        await curation.ClusterAsync(ds, [strings[1].Id], firm.Id, null, [], "tester", anyField: true);
        Assert.Equal(Outcome.Overridden, await reconciler.ReconcileOneAsync(ds, twin));

        // the entity page's view, label, identifiers, confirm, reopen
        var view = (await curation.EntityAsync(ds, firm.Id))!;
        Assert.Equal(6, view.Strings.Count);
        Assert.Single(view.Records); // the ULAN record behind the id
        await curation.SetLabelAsync(ds, firm.Id, "Agnew's (Thomas Agnew & Sons)");
        Assert.Contains("Thomas Agnew & Sons", (await pg.Store.GetEntityAsync(firm.Id))!.Labels);
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.AddEquivalentAsync(ds, firm.Id, "http://example.org/nowhere", "tester"));
        await curation.AddEquivalentAsync(ds, firm.Id, "http://viaf.org/viaf/135146590", "tester"); // a recognised namespace needs no cached record
        await curation.RemoveEquivalentAsync(ds, firm.Id, "http://viaf.org/viaf/135146590");
        Assert.Single((await pg.Store.GetEntityAsync(firm.Id))!.Equivalents);
        var reopened = await curation.ReopenAsync(ds, [strings[4].Id]);
        Assert.Equal(1, reopened);
        Assert.Null(await pg.Store.DecisionForAsync(strings[4].Id));
        Assert.Single(await pg.Store.UndecidedAsync(ds));

        // another project cannot touch the entity or its strings
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.SetLabelAsync("other", firm.Id, "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.ClusterAsync("other", [strings[0].Id], null, "x", [], "tester"));

        // merge through the curation service, and the export in the pmc-lux shape
        var stray = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Agnew & Sons (stray)"), scheme);
        var merged = await curation.MergeAsync(ds, stray.Id, firm.Id, "tester");
        Assert.Equal(firm.Id, merged.Id);
        var json = await curation.ExportOverridesJsonAsync(ds);
        using var doc = JsonDocument.Parse(json);
        var map = doc.RootElement.GetProperty("local_string_map");
        Assert.Equal(firm.Id.Split('/')[^1], map.GetProperty("Agnew's").GetString());
        Assert.Equal(5, map.EnumerateObject().Count()); // one spelling per entry, whatever fields it is scoped to
        var authority = Assert.Single(doc.RootElement.GetProperty("authorities").EnumerateArray());
        Assert.Equal("500301417", authority.GetProperty("ulan").GetString());
        Assert.Equal("Group", authority.GetProperty("type").GetString());
        // and the exported file imports back as overrides
        var reimport = PmcLuxOverrides.Parse(json);
        Assert.Equal(5, reimport.LocalStringMap.Count);

        // orphan housekeeping: the provisional entities the cluster re-pointed away from were folded in or are gone
        Assert.Equal(0, await pg.Store.RemoveOrphanProvisionalsAsync(ds));
    }

    /// <summary>
    /// The owner, 2026-11-07, at "Ackermann and Son": a cluster of persauthorfull strings given ULAN's corporate body was a confirmed Person
    /// holding a Group's record, and the owner would not merge a Person with a Group. The cluster takes the record's class when the field
    /// allows it; a person can change the class on the entity page, noted; a merge whose two entities differ in class can take the chosen one.
    /// </summary>
    [PostgresFact]
    public async Task An_entity_takes_its_records_class_and_a_person_can_change_it()
    {
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test");
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);
        var a = await pg.Store.CollectAsync(ds, "persauthorfull", "Agnew and Son");
        var b = await pg.Store.CollectAsync(ds, "persauthorfull", "A. Agnew & Son");
        await reconciler.RunAsync(ds);

        // the field says Person; ULAN's record says Group, and corpauthor's field would allow it, but persauthorfull allows only Person: the field's class stands
        var firm = await curation.ClusterAsync(ds, [a.Id, b.Id], null, "Agnew and Son", ["http://vocab.getty.edu/ulan/500301417"], "tester");
        Assert.Equal(EntityClass.Person, firm.Class);
        // a person says what it is: the class changes, the note says from what, the identifier keeps its path
        var group = await curation.SetClassAsync(ds, firm.Id, EntityClass.Group, "tester");
        Assert.Equal(EntityClass.Group, group.Class);
        Assert.Equal(firm.Id, group.Id);
        Assert.Contains(await pg.Store.NotesAsync(ds, firm.Id), n => n.Text.Contains("Class changed from Person to Group"));
        Assert.Equal(EntityClass.Group, (await pg.Store.GetEntityAsync(firm.Id))!.Class);

        // a cluster from a field that allows both takes the record's class (the identifier first taken off the first entity: since
        // doc 12 section 13 item 3 an identifier another entity holds is refused, and this test wants a fresh entity)
        await curation.RemoveEquivalentAsync(ds, firm.Id, "http://vocab.getty.edu/ulan/500301417");
        var c = await pg.Store.CollectAsync(ds, "corpauthor", "Agnew's of Bond Street");
        await reconciler.RunAsync(ds);
        var other = await curation.ClusterAsync(ds, [c.Id], null, "Agnew's of Bond Street", ["http://vocab.getty.edu/ulan/500301417"], "tester");
        Assert.Equal(EntityClass.Group, other.Class);

        // a merge of two entities that differ in class takes the chosen class
        var person = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Agnew & Sons Ltd", ["Agnew & Sons Ltd"], Status: EntityStatus.Confirmed), scheme);
        var merged = await curation.MergeAsync(ds, other.Id, person.Id, "tester", EntityClass.Group);
        Assert.Equal(person.Id, merged.Id);
        Assert.Equal(EntityClass.Group, merged.Class);
        Assert.Contains(await pg.Store.NotesAsync(ds, person.Id), n => n.Text.Contains("Class changed from Person to Group"));
    }

    /// <summary>
    /// The owner, 2026-11-07, after merging the "Ackermann" placeholder into the firm from its entity page: the string stayed queued, since a
    /// merge re-points a decision and leaves its method. A person's merge settles the loser's queued and minted strings as the person's own;
    /// the ones merged before that are listed and settled by settle-merged.
    /// </summary>
    [PostgresFact]
    public async Task A_persons_merge_settles_the_placeholders_strings()
    {
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test");
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);
        var firm = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Thomas Agnew & Sons", ["Thomas Agnew & Sons"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(firm.Id, new Equivalent("http://vocab.getty.edu/ulan/500301417", "ulan", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        // two bare strings the run can only mint or queue, each with a placeholder of its own
        var agnew = await pg.Store.CollectAsync(ds, "corpauthor", "Agnew");
        var agnews = await pg.Store.CollectAsync(ds, "publisher", "Agnews of Bond St");
        await reconciler.RunAsync(ds);
        var before = (await pg.Store.DecisionForAsync(agnew.Id))!;
        Assert.Contains(before.Method, new[] { DecisionMethod.Queued, DecisionMethod.Minted });
        var placeholder = (await pg.Store.GetEntityAsync(before.EntityId!))!;
        Assert.Equal(EntityStatus.Provisional, placeholder.Status);

        // the person merges the placeholder into the firm from its page: the string is decided, as theirs, and resolves to the firm
        await curation.MergeAsync(ds, placeholder.Id, firm.Id, "tester");
        var after = (await pg.Store.DecisionForAsync(agnew.Id))!;
        Assert.Equal(DecisionMethod.Human, after.Method);
        Assert.Equal(firm.Id, after.EntityId);
        Assert.Contains("same-as-by-merge", after.EvidenceJson!);
        Assert.Contains(before.Method, after.EvidenceJson!);
        Assert.DoesNotContain(await pg.Store.DecidedStringsAsync(ds, DecisionMethod.Queued), d => d.String.Id == agnew.Id);
        Assert.DoesNotContain(await pg.Store.DecidedStringsAsync(ds, DecisionMethod.Minted), d => d.String.Id == agnew.Id);

        // a merge made before merges settled strings (the store's own, as the earlier workbench did it): listed, then settled on --apply
        var other = (await pg.Store.DecisionForAsync(agnews.Id))!;
        await pg.Store.MergeAsync(other.EntityId!, firm.Id, "tester");
        Assert.Equal(other.Method, (await pg.Store.DecisionForAsync(agnews.Id))!.Method);
        var listed = await curation.SettleResolvedAsync(ds, "tester", apply: false);
        Assert.Single(listed); Assert.Equal(agnews.Id, listed[0].String.String.Id); Assert.Equal(firm.Id, listed[0].Entity.Id);
        Assert.Equal(other.Method, (await pg.Store.DecisionForAsync(agnews.Id))!.Method); // a dry run decides nothing
        var settled = await curation.SettleResolvedAsync(ds, "tester", apply: true);
        Assert.Single(settled);
        Assert.Equal(DecisionMethod.Human, (await pg.Store.DecisionForAsync(agnews.Id))!.Method);
        Assert.Empty(await curation.SettleResolvedAsync(ds, "tester", apply: false));
    }

    /// <summary>Doc 12 section 13, item 1 (2026-11-08): notes are the audit log, every hand change written with before and after, and the project's recent changes read from them.</summary>
    [PostgresFact]
    public async Task Every_hand_change_writes_a_note_and_the_recent_changes_read_them()
    {
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);
        var a = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Agnews", ["Agnews"]), scheme);
        var b = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Thos. Agnew & Sons", ["Thos. Agnew & Sons"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(b.Id, new Equivalent("http://vocab.getty.edu/ulan/500301417", "ulan", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));

        await curation.SetLabelAsync(ds, a.Id, "Agnew's", "tester");
        await curation.AddEquivalentAsync(ds, a.Id, "http://viaf.org/viaf/135146590", "tester");
        await curation.RemoveEquivalentAsync(ds, a.Id, "http://viaf.org/viaf/135146590", "tester");
        await curation.ConfirmAsync(ds, a.Id, "tester", "the Bond Street firm");
        var notes = await pg.Store.NotesAsync(ds, a.Id);
        Assert.Contains(notes, n => n.Text == "Label changed from \"Agnews\" to \"Agnew's\"." && n.Author == "tester");
        Assert.Contains(notes, n => n.Text.StartsWith("Identifier added: http://viaf.org/viaf/135146590 (viaf)"));
        Assert.Contains(notes, n => n.Text.StartsWith("Identifier removed: http://viaf.org/viaf/135146590"));
        Assert.Contains(notes, n => n.Text.StartsWith("Confirmed (was ") && n.Text.EndsWith("): the Bond Street firm"));
        // the same label again writes nothing
        await curation.SetLabelAsync(ds, a.Id, "Agnew's", "tester");
        Assert.Equal(4, (await pg.Store.NotesAsync(ds, a.Id)).Count);

        // a merge writes on both sides
        await curation.MergeAsync(ds, a.Id, b.Id, "tester");
        Assert.Contains(await pg.Store.NotesAsync(ds, b.Id), n => n.Text.StartsWith("Merged in \"Agnew's\"") && n.Text.Contains("0 strings"));
        Assert.Contains(await pg.Store.NotesAsync(ds, a.Id), n => n.Text.StartsWith("Merged into \"Thos. Agnew & Sons\""));

        // the project's recent changes, newest first, and the human decisions beside them
        var recent = await pg.Store.RecentNotesAsync(ds, 10);
        Assert.True(recent.Count >= 6);
        Assert.True(recent[0].CreatedAt >= recent[^1].CreatedAt);
        Assert.StartsWith("Merged", recent[0].Text);
        Assert.Empty(await pg.Store.RecentHumanDecisionsAsync(ds));
        var s = await pg.Store.CollectAsync(ds, "corpauthor", "Agnew & Son");
        await pg.Store.DecideAsync(s.Id, b.Id, DecisionMethod.Human, null, "tester");
        var decided = await pg.Store.RecentHumanDecisionsAsync(ds);
        Assert.Equal(s.Id, Assert.Single(decided).String.Id);
    }

    /// <summary>Doc 12 section 13, item 3 (2026-11-08): an identifier another entity holds is refused with the holder named, so a twin is never made by hand.</summary>
    [PostgresFact]
    public async Task An_identifier_another_entity_holds_is_refused_with_the_holder_named()
    {
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);
        var one = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Agnew's", ["Agnew's"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(one.Id, new Equivalent("http://vocab.getty.edu/ulan/500301417", "ulan", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var two = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Thos. Agnew & Sons", ["Thos. Agnew & Sons"], Status: EntityStatus.Confirmed), scheme);
        var refused = await Assert.ThrowsAsync<IdentifierInUseException>(() => curation.AddEquivalentAsync(ds, two.Id, "http://vocab.getty.edu/ulan/500301417", "tester"));
        Assert.Equal(one.Id, refused.Holder.Id);
        Assert.Contains("Agnew's", refused.Message);
        Assert.Empty((await pg.Store.GetEntityAsync(two.Id))!.Equivalents);
        // the same URI on the holder itself is no refusal, and a URI nobody holds is added
        Assert.Null(await curation.HolderOfAsync(ds, "http://vocab.getty.edu/ulan/500301417", one.Id));
        var added = await curation.AddEquivalentAsync(ds, two.Id, "http://viaf.org/viaf/135146590", "tester");
        Assert.Contains(added.Equivalents, q => q.Source == "viaf");
        // merged-away holders do not count: after the merge the URI sits on the survivor, which is the entity being added to
        await curation.MergeAsync(ds, one.Id, two.Id, "tester");
        Assert.Null(await curation.HolderOfAsync(ds, "http://vocab.getty.edu/ulan/500301417", two.Id));
    }

    /// <summary>The owner, 2026-11-07, at "Ackermann & Son, Arthur (London)--History" in the entities list: a compound heading's own concept says what it is coordinated from.</summary>
    [PostgresFact]
    public async Task A_compound_headings_concept_lists_its_parts()
    {
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test");
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);
        var heading = await pg.Store.CollectAsync(ds, "keywords", "Thomas Agnew & Sons--History");
        await reconciler.RunAsync(ds);
        var decision = (await pg.Store.DecisionForAsync(heading.Id))!;
        Assert.Equal(DecisionMethod.Composite, decision.Method);
        var view = (await curation.EntityAsync(ds, decision.EntityId!))!;
        Assert.True(view.IsCompound);
        Assert.Equal(["Thomas Agnew & Sons", "History"], view.Parts.Select(p => p.Part.Raw));
        Assert.Equal("keywords:head", view.Parts[0].Part.Context);
        Assert.NotNull(view.Parts[0].Decision);
        Assert.Contains(decision.EntityId!, await pg.Store.CompositeEntityIdsAsync([decision.EntityId!, "https://data.example.org/nothing"]));
        // an ordinary entity has no parts
        var firmView = (await curation.EntityAsync(ds, view.Parts[0].Entity!.Id))!;
        Assert.False(firmView.IsCompound);
    }
}
