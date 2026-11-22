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
    public async Task The_check_lists_places_an_import_aligned_to_a_namesake_abroad()
    {
        // the owner, 2026-11-15: the project's "Glasgow" on Glasgow, Kentucky. Names unlike any real place keep the test to its own rows
        var ds = "cu-" + IdMinter.Generate(6);
        var tag = "Q" + IdMinter.Generate(8);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var curation = new CurationService(pg.Store, scheme, new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions()));
        AuthorityRecord Tgn(string id, string label, string parent) => new($"http://vocab.getty.edu/tgn/{tag}{id}", "tgn", label, [label], "Place", null, [], $"{{\"parentString\":\"{parent}\"}}", DateTimeOffset.UtcNow, "test");
        AuthorityRecord Item(string q, string label, string descr, int sitelinks, params string[] sameAs) =>
            new($"http://www.wikidata.org/entity/{tag}{q}", "wikidata", label, [label], "Place", null, sameAs, $"{{\"ids\":{{}},\"sitelinks\":{sitelinks},\"description\":\"{descr}\"}}", DateTimeOffset.UtcNow, "test");
        async Task<Entity> Imported(string label, string q)
        {
            var e = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, label, Status: EntityStatus.Aligned), scheme);
            await pg.Store.AddEquivalentAsync(e.Id, new Equivalent($"http://www.wikidata.org/entity/{tag}{q}", "wikidata", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
            return e;
        }
        var (glas, york, hounslow) = ("Glasgo" + tag, "Newyor" + tag, "Hounslo" + tag);
        await pg.Store.PutAuthorityAsync(Tgn("1", glas, $"{glas}, Scotland, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("1", glas, "city in Scotland", 180, $"http://vocab.getty.edu/tgn/{tag}1"));
        await pg.Store.PutAuthorityAsync(Item("2", glas, "city in Barren County, Kentucky, United States of America", 41));
        await pg.Store.PutAuthorityAsync(Tgn("3", york, "Lincolnshire, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("3", york, "most populous city in the United States", 325));
        await pg.Store.PutAuthorityAsync(Tgn("4", hounslow, "Greater London, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("4", hounslow, "suburban district of West London", 21));
        var kentucky = await Imported(glas, "2");
        var perth = "Pert" + tag;
        await pg.Store.PutAuthorityAsync(Tgn("5", perth, "Perth and Kinross, Scotland, United Kingdom, Europe, World")); // no Wikidata item known for it
        await pg.Store.PutAuthorityAsync(Item("5", perth, "town in Tasmania, Australia", 14));
        var tasmania = await Imported(perth, "5");
        var newYork = await Imported(york, "3");
        // it holds LoC's heading, which says which place it is: the label the checks suggest for an entity right as aligned
        await pg.Store.PutAuthorityAsync(new AuthorityRecord($"http://id.loc.gov/authorities/names/{tag}ny", "loc", $"{york} (N.Y.)", [$"{york} (N.Y.)"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.AddEquivalentAsync(newYork.Id, new Equivalent($"http://id.loc.gov/authorities/names/{tag}ny", "loc", "through-wikidata", 1, DateTimeOffset.UtcNow));
        await Imported(hounslow, "4"); // its description names the home chain ("London"): not listed

        // the strings are read: "..., Ala." on the Alabama item is right and not listed; "... (England)" on a Vermont item leads the likely
        var (brum, stowe) = ("Birmingha" + tag, "Stow" + tag);
        await pg.Store.PutAuthorityAsync(Tgn("6", brum, "West Midlands, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("6", brum, "city in and county seat of Jefferson County, Alabama, United States", 117));
        var alabama = await Imported(brum, "6");
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", brum + ", Ala.")).Id, alabama.Id, DecisionMethod.Imported, null);
        await pg.Store.PutAuthorityAsync(Tgn("7", stowe, "Buckinghamshire, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("7", stowe, "town in Lamoille County, Vermont, United States", 28));
        var vermont = await Imported(stowe, "7");
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", stowe)).Id, vermont.Id, DecisionMethod.Imported, null);
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", stowe + " (England)")).Id, vermont.Id, DecisionMethod.Imported, null);

        // an item the index does not have says nothing: its entity is listed, but a string is not a reason against a description that is not there
        var biggles = "Biggleswad" + tag;
        await pg.Store.PutAuthorityAsync(Tgn("8", biggles, "Bedfordshire, England, United Kingdom, Europe, World"));
        var unknownItem = await Imported(biggles, "8"); // no Wikidata record put for it
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", biggles + ", Bedfordshire")).Id, unknownItem.Id, DecisionMethod.Imported, null);
        var quiet = Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == unknownItem.Id);
        Assert.False(quiet.Likely); Assert.Null(quiet.Reason); Assert.Null(quiet.Row.ItemDescription);
        await curation.ConfirmAsync(ds, unknownItem.Id, "tester", null);

        // of two places of the name at home, the one the string's qualifier names is shown, whatever order the store gives them in;
        // and a string that places the entity in neither says so
        var compton = "Compto" + tag;
        await pg.Store.PutAuthorityAsync(Tgn("11", compton, "Leeds, West Yorkshire, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Tgn("12", compton, "Surrey, England, United Kingdom, Europe, World"));
        var surrey = await Imported(compton, "11x"); // its item is not in the index
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", compton + ", Surrey")).Id, surrey.Id, DecisionMethod.Imported, null);
        var shown = Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == surrey.Id);
        Assert.Contains("Surrey", shown.Row.HomeChain); Assert.Null(shown.HomeDoubt);
        Assert.Contains("Surrey", (await curation.MendPlanAsync(ds, surrey.Id, "United Kingdom"))!.Homes[0].HomeChain);
        await curation.ConfirmAsync(ds, surrey.Id, "tester", null);
        var jamaica = await Imported(compton, "11y");
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", compton + ", Jamaica")).Id, jamaica.Id, DecisionMethod.Imported, null);
        Assert.Contains("Jamaica", Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == jamaica.Id).HomeDoubt);
        await curation.ConfirmAsync(ds, jamaica.Id, "tester", null);
        // an item whose description names the place at home: the plan says the entity is probably right, and it can be kept with that place's identifier added
        var hounslowEntity = (await pg.Store.FindEntitiesByLabelAsync(StringKey.Normalise(hounslow), ds, EntityClass.Place)).Single();
        var agreeing = (await curation.MendPlanAsync(ds, hounslowEntity.Id, "United Kingdom"))!;
        Assert.Equal($"http://vocab.getty.edu/tgn/{tag}4", agreeing.Agreeing); Assert.Null(agreeing.Rival);
        var keptHounslow = await curation.RelabelAndConfirmAsync(ds, hounslowEntity.Id, hounslow, "tester", agreeing.Agreeing);
        Assert.Equal(EntityStatus.Confirmed, keptHounslow.Status); Assert.Contains(keptHounslow.Equivalents, q => q.Uri == agreeing.Agreeing);
        // two places of the name at home: the import's item is one (a hamlet in Aberdeenshire), the entity holds TGN's other (in Wales).
        // It is listed as likely wrong though the item is "at home", and the plan says which the entity is
        var bridgend = "Bridgen" + tag;
        await pg.Store.PutAuthorityAsync(Tgn("13", bridgend, "Aberdeenshire, Scotland, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Tgn("14", bridgend, "Wales, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("13", bridgend, "hamlet in Gartly, Aberdeenshire, Scotland, UK", 0));
        var wales = await Imported(bridgend, "13");
        await pg.Store.AddEquivalentAsync(wales.Id, new Equivalent($"http://vocab.getty.edu/tgn/{tag}14", "tgn", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var two = Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == wales.Id);
        Assert.True(two.Likely); Assert.Contains("another place of the name", two.Reason); Assert.Contains("Wales", two.Row.HomeChain);
        var twoPlan = (await curation.MendPlanAsync(ds, wales.Id, "United Kingdom"))!;
        Assert.Equal(($"http://vocab.getty.edu/tgn/{tag}13", $"http://vocab.getty.edu/tgn/{tag}14"), (twoPlan.Agreeing, twoPlan.Meant));
        var welsh = await curation.MendAwayAsync(ds, wales.Id, twoPlan.Entity.Equivalents.Where(q => q.Method == DecisionMethod.Imported).Select(q => q.Uri).ToList(), twoPlan.Meant!, "tester");
        Assert.Equal($"http://vocab.getty.edu/tgn/{tag}14", Assert.Single(welsh.Equivalents).Uri);
        // a string that places the entity somewhere its item is not, the item being at home: still listed, and the plan says which string
        var wallington = "Wallingto" + tag;
        await pg.Store.PutAuthorityAsync(Tgn("15", wallington, "Sutton, Greater London, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("15", wallington, "town in the London Borough of Sutton, Greater London, England", 0));
        var sutton = await Imported(wallington, "15");
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", wallington)).Id, sutton.Id, DecisionMethod.Imported, null);
        Assert.DoesNotContain(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == sutton.Id); // the bare string alone: at home, and right
        Assert.Null((await curation.MendPlanAsync(ds, sutton.Id, "United Kingdom"))!.Odd);
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "keywords", wallington + " (Northumberland)")).Id, sutton.Id, DecisionMethod.Imported, null);
        // the plan says which string, but the entity is not listed for that alone ("Bath (Avon)" is Bath): it is once someone leaves it with a note
        Assert.Equal(wallington + " (Northumberland)", (await curation.MendPlanAsync(ds, sutton.Id, "United Kingdom"))!.Odd);
        Assert.DoesNotContain(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == sutton.Id);
        await curation.NoteAsync(ds, sutton.Id, "tester", CurationService.LeftUndecided[0] + " the heading is the house in Northumberland");
        var kept = Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == sutton.Id);
        Assert.False(kept.Likely); Assert.Contains("left undecided with a note", kept.Reason);
        await curation.ConfirmAsync(ds, sutton.Id, "tester", null);
        // a better-known place of the name elsewhere: the bare label would be taken for it
        Assert.Equal(180, (await curation.MendPlanAsync(ds, kentucky.Id, "United Kingdom"))!.Rival!.Value.Sitelinks); // Kentucky's, beside the city in Scotland

        var all = await curation.PlacesAlignedAwayAsync(ds, "United Kingdom");
        Assert.DoesNotContain(all, f => f.Row.EntityId == alabama.Id);
        Assert.Equal(vermont.Id, all[0].Row.EntityId); Assert.True(all[0].Likely);
        Assert.Contains("(England)\" says so", all[0].Reason); Assert.Equal(2, all[0].Strings.Count);
        await curation.ConfirmAsync(ds, vermont.Id, "tester", null);
        var rows = await curation.PlacesAlignedAwayAsync(ds, "United Kingdom");
        Assert.Equal(3, rows.Count);
        Assert.Equal((kentucky.Id, true), (rows[0].Row.EntityId, rows[0].Likely));   // Scotland's 180 against Kentucky's 41
        Assert.Equal($"http://vocab.getty.edu/tgn/{tag}1", rows[0].Row.HomeUri);
        Assert.Equal((tasmania.Id, false), (rows[1].Row.EntityId, rows[1].Likely));   // no count at home: to look at, the obscure first
        Assert.Null(rows[1].Row.HomeItem);
        Assert.Equal((newYork.Id, false), (rows[2].Row.EntityId, rows[2].Likely));    // right as it is, most likely: to look at, last
        // mended in one step: with no entity of the project at the home place, the identifiers go and the home place's is added
        var plan = (await curation.MendPlanAsync(ds, kentucky.Id, "United Kingdom"))!;
        Assert.Equal($"http://vocab.getty.edu/tgn/{tag}1", Assert.Single(plan.Homes).HomeUri); Assert.Empty(plan.Holders);
        var mended = await curation.MendAwayAsync(ds, kentucky.Id, plan.Entity.Equivalents.Select(q => q.Uri).ToList(), plan.Homes[0].HomeUri, "tester");
        Assert.Equal(kentucky.Id, mended.Id);
        Assert.Equal($"http://vocab.getty.edu/tgn/{tag}1", Assert.Single(mended.Equivalents).Uri);
        // with one, the entity is merged into it and its strings follow
        var scot = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, perth + " (Scotland)", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(scot.Id, new Equivalent($"http://vocab.getty.edu/tgn/{tag}5", "tgn", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var perthString = await pg.Store.CollectAsync(ds, "place", perth);
        await pg.Store.DecideAsync(perthString.Id, tasmania.Id, DecisionMethod.Imported, null);
        var perthPlan = (await curation.MendPlanAsync(ds, tasmania.Id, "United Kingdom"))!;
        Assert.Equal(scot.Id, perthPlan.Holders[perthPlan.Homes[0].HomeUri].Id);
        var into = await curation.MendAwayAsync(ds, tasmania.Id, perthPlan.Entity.Equivalents.Select(q => q.Uri).ToList(), perthPlan.Homes[0].HomeUri, "tester");
        Assert.Equal(scot.Id, into.Id);
        Assert.DoesNotContain(into.Equivalents, q => q.Source == "wikidata");
        Assert.Equal(scot.Id, (await pg.Store.ResolveEntityAsync((await pg.Store.DecisionForAsync(perthString.Id))!.EntityId!))!.Id);
        Assert.Null(await curation.MendPlanAsync(ds, kentucky.Id, "United Kingdom")); // mended, it is not on the list
        Assert.Equal(newYork.Id, Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom")).Row.EntityId);
        // the place meant may be abroad and not the import's: the project's other entity of the name is offered and merged into
        var raleigh = "Raleig" + tag;
        await pg.Store.PutAuthorityAsync(Tgn("9", raleigh, "Devon, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("9", raleigh, "neighborhood in and suburb of Memphis, Tennessee, United States", 2));
        var memphis = await Imported(raleigh, "9");
        var carolina = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, raleigh, Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(carolina.Id, new Equivalent($"http://vocab.getty.edu/tgn/{tag}99", "tgn", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var raleighPlan = (await curation.MendPlanAsync(ds, memphis.Id, "United Kingdom"))!;
        Assert.Equal(carolina.Id, Assert.Single(raleighPlan.Others).Id);
        var nc = await curation.MendIntoAsync(ds, memphis.Id, raleighPlan.Entity.Equivalents.Select(q => q.Uri).ToList(), carolina.Id, "tester");
        Assert.Equal(carolina.Id, nc.Id); Assert.DoesNotContain(nc.Equivalents, q => q.Source == "wikidata");
        // an entity right for some strings and wrong for others: the wrong ones are moved, and it is left as it was
        var peter = "Peterboroug" + tag;
        await pg.Store.PutAuthorityAsync(Tgn("10", peter, "Cambridgeshire, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Item("10", peter, "city in Ontario, Canada", 44));
        var ontario = await Imported(peter, "10");
        var bare = await pg.Store.CollectAsync(ds, "place", peter);
        var qualified = await pg.Store.CollectAsync(ds, "place", peter + ", Ontario");
        await pg.Store.DecideAsync(bare.Id, ontario.Id, DecisionMethod.Imported, null);
        await pg.Store.DecideAsync(qualified.Id, ontario.Id, DecisionMethod.Imported, null);
        var england = await curation.MoveStringsAsync(ds, ontario.Id, [bare.Id], $"http://vocab.getty.edu/tgn/{tag}10", "tester"); // no entity holds it: one is made
        Assert.NotEqual(ontario.Id, england.Id);
        Assert.Equal($"{peter} (Cambridgeshire, England)", england.Label);
        Assert.Equal($"http://vocab.getty.edu/tgn/{tag}10", Assert.Single(england.Equivalents).Uri);
        Assert.Equal(england.Id, (await pg.Store.DecisionForAsync(bare.Id))!.EntityId);
        Assert.Equal(DecisionMethod.Human, (await pg.Store.DecisionForAsync(bare.Id))!.Method);
        Assert.Equal(ontario.Id, (await pg.Store.DecisionForAsync(qualified.Id))!.EntityId);
        Assert.Single((await pg.Store.GetEntityAsync(ontario.Id))!.Equivalents); // as it was
        Assert.DoesNotContain(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == ontario.Id); // its one string left agrees with the item
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.MoveStringsAsync(ds, ontario.Id, [bare.Id], "entity:" + england.Id, "tester")); // it stands there no more
        // one decision said string by string: this one stays, that one goes; the entity is confirmed for what stays
        var split = await Imported("Spli" + tag, "20");
        var stays = await pg.Store.CollectAsync(ds, "place", "Spli" + tag + ", Ontario");
        var leaves = await pg.Store.CollectAsync(ds, "place", "Spli" + tag);
        await pg.Store.DecideAsync(stays.Id, split.Id, DecisionMethod.Imported, null);
        await pg.Store.DecideAsync(leaves.Id, split.Id, DecisionMethod.Imported, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.ApplyAsync(ds, split.Id, [leaves.Id], null, [], null, null, "tester")); // going, and nowhere to go
        var (after, said) = await curation.ApplyAsync(ds, split.Id, [leaves.Id], "entity:" + england.Id, [], "Spli" + tag + " (Ont.)", null, "tester");
        Assert.Equal((split.Id, EntityStatus.Confirmed, "Spli" + tag + " (Ont.)"), (after.Id, after.Status, after.Label));
        Assert.Equal(england.Id, (await pg.Store.DecisionForAsync(leaves.Id))!.EntityId);
        Assert.Equal(split.Id, (await pg.Store.DecisionForAsync(stays.Id))!.EntityId);
        Assert.Contains("1 string moved", said); Assert.Contains("confirmed for the 1 string that stays", said);
        // none goes: kept; all go: mended whole
        var (keptWhole, keptSaid) = await curation.ApplyAsync(ds, split.Id, [], null, [], null, null, "tester");
        Assert.Contains("confirmed as it is", keptSaid); Assert.Equal(split.Id, keptWhole.Id);
        var (gone, goneSaid) = await curation.ApplyAsync(ds, split.Id, [stays.Id], "entity:" + england.Id, split.Equivalents.Select(q => q.Uri).ToList(), null, null, "tester");
        Assert.Equal(england.Id, gone.Id); Assert.Contains("merged into", goneSaid);
        var suggested = Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == newYork.Id).SuggestedLabel;
        Assert.Equal($"{york} (N.Y.)", suggested);
        var relabelled = await curation.RelabelAndConfirmAsync(ds, newYork.Id, suggested!, "tester");   // relabelled and confirmed, it leaves the list
        Assert.Equal((suggested, EntityStatus.Confirmed), (relabelled.Label, relabelled.Status));
        Assert.Contains(york, relabelled.Labels); // the bare name is still a name of it
        Assert.Empty(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"));
        Assert.Empty(await curation.PlacesAlignedAwayAsync(ds, null));
    }

    [PostgresFact]
    public async Task A_string_that_is_not_a_name_is_decided_with_no_entity_and_its_placeholder_goes()
    {
        // the owner, 2026-11-22: "S.l." stood as a place with 157 records, "s.n." waited at the desk with 375
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);
        var sl = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "S.l."), scheme);
        var one = await pg.Store.CollectAsync(ds, "place", "S.l.");
        var two = await pg.Store.CollectAsync(ds, "place", "[S. l.]");
        await pg.Store.DecideAsync(one.Id, sl.Id, DecisionMethod.Minted);
        await pg.Store.DecideAsync(two.Id, sl.Id, DecisionMethod.ExactName);
        var letter = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "S"), scheme);
        var s = await pg.Store.CollectAsync(ds, "place", "S");
        await pg.Store.DecideAsync(s.Id, letter.Id, DecisionMethod.Queued);
        // a name beside them, and an entity with one string of each sort: neither is listed
        var uk = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "UK"), scheme);
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", "UK")).Id, uk.Id, DecisionMethod.Queued);
        var mixed = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Publisher"), scheme);
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "publisher", "Publisher not identified")).Id, mixed.Id, DecisionMethod.Minted);
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "publisher", "Publisher & Co")).Id, mixed.Id, DecisionMethod.ExactName);

        // a number among the concepts is a date subdivision of a heading, and an entity rightly
        var years = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Type, "1837-1901"), scheme);
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "keywords:sub", "1837-1901")).Id, years.Id, DecisionMethod.Minted);

        var listed = await curation.NotNameEntitiesAsync(ds);
        Assert.Equal(new[] { sl.Id, letter.Id }.Order(), listed.Select(n => n.EntityId).Order());
        Assert.Equal(CurationService.NothingKnown, CurationService.SuggestedReason(listed.Single(n => n.EntityId == sl.Id).Strings));
        Assert.Equal((CurationService.RecordWantsFixing, 1), (CurationService.SuggestedReason(listed.Single(n => n.EntityId == letter.Id).Strings), listed.Single(n => n.EntityId == letter.Id).Queued));

        // a spelling's placeholder merged into it beforehand goes with it (the owner merged them first, 2026-11-22)
        var typo = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "S.1."), scheme);
        await pg.Store.MergeAsync(typo.Id, sl.Id, "tester");
        // the check's button: both strings decided with no entity, the entity gone
        Assert.Equal((2, true), await curation.NotANameEntityAsync(ds, sl.Id, "tester", CurationService.NothingKnown));
        var decision = (await pg.Store.DecisionForAsync(one.Id))!;
        Assert.Equal((DecisionMethod.NotAName, (string?)null, "tester"), (decision.Method, decision.EntityId, decision.Reviewer));
        Assert.Contains("nothing-known", decision.EvidenceJson);
        Assert.Null(await pg.Store.GetEntityAsync(sl.Id));
        Assert.Null(await pg.Store.GetEntityAsync(typo.Id));
        Assert.Equal(letter.Id, Assert.Single(await curation.NotNameEntitiesAsync(ds)).EntityId);

        // the card's button, and what it refuses: a reason that is neither, a string on an entity with identifiers
        await Assert.ThrowsAsync<ArgumentException>(() => curation.NotANameAsync(ds, s.Id, "tester", "because"));
        await pg.Store.PutExamplesAsync([new StringExample(s.Id, "ZB270", "library", "A selected portion", "1937", "place", "{\"place\":[\"S\"]}")]);
        Assert.True(await curation.NotANameAsync(ds, s.Id, "tester", CurationService.RecordWantsFixing, "what is left of S.l."));
        // the record is on the cataloguers' list, once; nothing known put nothing there; a status is set and counted; an export sends the open
        var finding = Assert.Single(await pg.Store.FindingsAsync(ds));
        Assert.Equal(("library", "ZB270", "place", "S", Finding.NotAName, Finding.Open, "A selected portion"), (finding.System, finding.RecordId, finding.Field, finding.Value, finding.Kind, finding.Status, finding.Title));
        Assert.Equal(0, await pg.Store.RaiseFindingsForStringAsync(s.Id, Finding.NotAName, "S", null, null, "tester"));
        Assert.Equal(finding.Id, Assert.Single(await pg.Store.SendOpenFindingsAsync(ds, "tester")).Id);
        Assert.Equal(1, (await pg.Store.FindingCountsAsync(ds))[Finding.Sent]);
        Assert.True(await pg.Store.SetFindingStatusAsync(ds, finding.Id, Finding.Mended, "tester", "corrected to S.l."));
        Assert.Equal((Finding.Mended, "corrected to S.l."), ((await pg.Store.FindingsAsync(ds, Finding.Mended))[0].Status, (await pg.Store.FindingsAsync(ds, Finding.Mended))[0].Note));
        Assert.False(await pg.Store.SetFindingStatusAsync("another-" + ds, finding.Id, Finding.Open, "tester"));
        // an undo withdraws a finding nobody has acted on
        var desk = new ReviewService(pg.Store, scheme, reconciler);
        var b = await pg.Store.CollectAsync(ds, "publisher", "B");
        await pg.Store.DecideAsync(b.Id, (await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "B"), scheme)).Id, DecisionMethod.Queued);
        await pg.Store.PutExamplesAsync([new StringExample(b.Id, "R9", "library", "A book", "1900", "publisher", "{\"publisher\":[\"B\"]}")]);
        await curation.NotANameAsync(ds, b.Id, "tester", CurationService.RecordWantsFixing);
        Assert.Single(await pg.Store.FindingsAsync(ds, Finding.Open));
        await desk.UndoAsync(ds, b.Id);
        Assert.Empty(await pg.Store.FindingsAsync(ds, Finding.Open));
        Assert.Equal(DecisionMethod.Queued, (await pg.Store.DecisionForAsync(b.Id))!.Method);

        // the manifest's list: a reconcile decides the cataloguer's forms as not a name and makes nothing of them; a real two letters is not one
        var listing = new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions());
        Assert.True(listing.IsNotName("[S. l.]")); Assert.True(listing.IsNotName("Publisher not identified]")); Assert.False(listing.IsNotName("SL")); Assert.False(listing.IsNotName("Snell"));
        Assert.False(reconciler.IsNotName("s.n.") && ProjectManifest.PmcDefault.NotNames.Count == 0);
        var sn = await pg.Store.CollectAsync(ds, "publisher", "[s.n.]");
        Assert.Equal(Outcome.Skipped, await listing.ReconcileOneAsync(ds, sn));
        var byRule = (await pg.Store.DecisionForAsync(sn.Id))!;
        Assert.Equal((DecisionMethod.NotAName, (string?)null, (string?)null), (byRule.Method, byRule.EntityId, byRule.Reviewer));
        Assert.Contains("manifest", byRule.EvidenceJson);
        Assert.Empty(await curation.NotNameEntitiesAsync(ds));
        var real = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "London", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(real.Id, new Equivalent("http://vocab.getty.edu/tgn/7011781", "tgn", DecisionMethod.Human, 1.0, DateTimeOffset.UtcNow));
        var l = await pg.Store.CollectAsync(ds, "place", "L");
        await pg.Store.DecideAsync(l.Id, real.Id, DecisionMethod.Human, null, "tester");
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.NotANameAsync(ds, l.Id, "tester", CurationService.RecordWantsFixing));
        // not undecided: a reconcile leaves it alone
        Assert.DoesNotContain(await pg.Store.UndecidedAsync(ds), x => x.Id == s.Id || x.Id == one.Id);
    }

    [PostgresFact]
    public async Task A_name_set_aside_stops_matching_stays_aside_and_reopens_what_a_rule_matched_by_it()
    {
        // the owner, 2026-11-15: the project's Plymouth gathered TGN's former name "Sutton", and the place of publication "Sutton" (two
        // books from Sutton in London) was matched to it by exact name
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);
        var plymouth = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Plymouth", ["Sudtone", "Sutton"], Status: EntityStatus.Aligned), scheme);
        var byHand = await pg.Store.CollectAsync(ds, "place", "Plymouth");
        await pg.Store.DecideAsync(byHand.Id, plymouth.Id, DecisionMethod.Human, null, "tester");
        var sutton = await pg.Store.CollectAsync(ds, "place", "Sutton");
        Assert.Equal(Outcome.MatchedStore, await reconciler.ReconcileOneAsync(ds, sutton));
        Assert.Equal(plymouth.Id, (await pg.Store.DecisionForAsync(sutton.Id))!.EntityId);

        Assert.Equal(1, await curation.SetNameAsideAsync(ds, plymouth.Id, "Sutton", true, "tester"));
        var after = (await pg.Store.GetEntityAsync(plymouth.Id))!;
        Assert.DoesNotContain("Sutton", after.Labels); Assert.Contains("Sudtone", after.Labels); Assert.Equal(["Sutton"], after.SetAsideLabels);
        Assert.Null(await pg.Store.DecisionForAsync(sutton.Id));            // reopened: a rule's match by that name
        Assert.NotNull(await pg.Store.DecisionForAsync(byHand.Id));         // a person's decision stands
        Assert.NotEqual(Outcome.MatchedStore, await reconciler.ReconcileOneAsync(ds, sutton));
        Assert.Contains(await pg.Store.NotesAsync(ds, plymouth.Id), n => n.Text.Contains("\"Sutton\" set aside"));

        // nothing puts it back: an update from a copy that still has it, and a merge of an entity that has it
        await pg.Store.UpdateEntityAsync(after with { Labels = [.. after.Labels, "Sutton"] });
        Assert.DoesNotContain("Sutton", (await pg.Store.GetEntityAsync(plymouth.Id))!.Labels);
        var twin = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Plymouth (England)", ["Sutton"]), scheme);
        await pg.Store.MergeAsync(twin.Id, plymouth.Id, "tester");
        var merged = (await pg.Store.GetEntityAsync(plymouth.Id))!;
        Assert.DoesNotContain("Sutton", merged.Labels); Assert.Contains("Plymouth (England)", merged.Labels);
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.SetNameAsideAsync(ds, plymouth.Id, "Plymouth", true));

        // restored, it is a name again
        await curation.SetNameAsideAsync(ds, plymouth.Id, "Sutton", false, "tester");
        var restored = (await pg.Store.GetEntityAsync(plymouth.Id))!;
        Assert.Contains("Sutton", restored.Labels); Assert.Empty(restored.SetAsideLabels);
    }

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
