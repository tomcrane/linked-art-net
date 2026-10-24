using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class StorePlacementTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_qualified_heading_never_takes_a_store_entity_placed_elsewhere_in_any_context()
    {
        // finding 84 (2026-10-24, from run fk53p7): "Lincoln (England)" in keywords:head had matched the project's "Lincoln, Neb", whose
        // label says nothing TGN-shaped but whose TGN equivalent is in Nebraska. A label's county is widened to its ancestors, so "Boughton House (England)"
        // still finds the project's "Boughton House (Northamptonshire)"; an unqualified heading is untouched. (The corpauthor rule drops a
        // parenthetical without reading it as a qualifier, so that context compares nothing and matches as before: noted in the plan.)
        var ds = "spl" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Tgn(string id, string label, string parent, string? placeType = null) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label], "Place", null, [],
            placeType is null ? $"{{\"parentString\":\"{parent}\"}}" : $"{{\"parentString\":\"{parent}\",\"placeType\":\"http://vocab.getty.edu/aat/{placeType}\"}}", DateTimeOffset.UtcNow, "test");
        static AuthorityRecord Aat(string id, string label) => new($"http://vocab.getty.edu/aat/{id}", "aat", label, [label], "Type", null, [], null, DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Aat("300000771", "counties")); await pg.Store.PutAuthorityAsync(Aat("300128207", "nations"));
        await pg.Store.PutAuthorityAsync(Tgn("7013892", "Lincoln", "Lancaster, Nebraska, United States, North and Central America, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7002445", "England", "United Kingdom, Europe, World", "300128207"));
        await pg.Store.PutAuthorityAsync(Tgn("7002370", "Northamptonshire", "England, United Kingdom, Europe, World", "300000771"));
        var nebraska = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Lincoln, Neb", ["Lincoln, Neb", "Lincoln"], null, [], [], EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(nebraska.Id, new Equivalent("http://vocab.getty.edu/tgn/7013892", "tgn", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var boughton = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Boughton House (Northamptonshire)", ["Boughton House (Northamptonshire)", "Boughton House"], null, [], [], EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(boughton.Id, new Equivalent("http://id.loc.gov/authorities/names/n85000001", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var cornwall = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Cornwall (England : County)", ["Cornwall (England : County)", "Cornwall"], null, [], [], EntityStatus.Aligned), scheme);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        var lincoln = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Lincoln (England)"));
        Assert.NotEqual(Outcome.MatchedStore, lincoln.Outcome); Assert.Null(lincoln.StoreMatch);
        Assert.Contains(lincoln.Groups.SelectMany(g => g.Members), m => m.Candidate.Source == "store" && m.Reason.Contains("elsewhere"));
        // a town's qualifier resolves to nothing, and the project's entity placed at country level still answers it (run t629cj had
        // lost "Harewood House (Leeds)" to the queue through a filter on any disagreement, withdrawn)
        var harewood = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Harewood House (England)", ["Harewood House (England)", "Harewood House"], null, [], [], EntityStatus.Aligned), scheme);
        var leeds = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Harewood House (Leeds)"));
        Assert.Equal(Outcome.MatchedStore, leeds.Outcome); Assert.Equal(harewood.Id, leeds.StoreMatch!.Id);
        var house = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Boughton House (England)"));
        Assert.Equal(Outcome.MatchedStore, house.Outcome); Assert.Equal(boughton.Id, house.StoreMatch!.Id);
        Assert.Contains("qualifier places it", house.StoreChoice!.Reason);
        // a label's lone country is a national body's qualifier, not a placement, as PlacementOf leaves it: the project's "Statens museum for
        // kunst (Denmark)" is neither placed nor vetoed by "(Copenhagen, Denmark)" and answers as before (run t629cj had widened it in place to
        // "Denmark, Danmark, Europe, World", vetoed it as elsewhere and minted the museum again)
        await pg.Store.PutAuthorityAsync(Tgn("1000084", "Danmark", "Europe, World", "300128207") with { Labels = ["Danmark", "Denmark"] });
        var statens = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Statens museum for kunst (Denmark)", ["Statens museum for kunst (Denmark)", "Statens Museum for Kunst"], null, [], [], EntityStatus.Aligned), scheme);
        var museum = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Statens Museum for Kunst (Copenhagen, Denmark)"));
        Assert.Equal(Outcome.MatchedStore, museum.Outcome); Assert.Equal(statens.Id, museum.StoreMatch!.Id);
        Assert.Null(museum.StoreChoice!.Candidate.Placement); Assert.Null(museum.StoreChoice.QualifierAgrees);
        // a label's own lone country is a national body's qualifier, not a placement: the project's "National Maritime Museum (Great
        // Britain)" is neither placed nor vetoed by "(London)" (run t629cj had queued the heading that was a vote before); and "Great Britain"
        // and "United Kingdom" are one token, so LoC's qualifier agrees with TGN's chain
        await pg.Store.PutAuthorityAsync(Tgn("7008591", "United Kingdom", "Europe, World", "300128207") with { Labels = ["United Kingdom", "Great Britain"] });
        var maritime = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "National Maritime Museum (Great Britain)", ["National Maritime Museum (Great Britain)", "National Maritime Museum"], null, [], [], EntityStatus.Aligned), scheme);
        var greenwich = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "National Maritime Museum (London)"));
        var maritimeMember = Assert.Single(greenwich.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == maritime.Id);
        Assert.False(maritimeMember.Veto); Assert.Null(maritimeMember.Candidate.Placement); Assert.Null(maritimeMember.QualifierAgrees);
        Assert.True(Qualified.Agrees("London, Greater London, England, United Kingdom, Europe, World", "Great Britain"));
        var bare = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:sub", "Cornwall"));
        Assert.Equal(Outcome.MatchedStore, bare.Outcome); Assert.Equal(cornwall.Id, bare.StoreMatch!.Id);
    }

    [PostgresFact]
    public async Task A_qualifier_with_several_tgn_records_above_county_level_resolves_by_level_then_by_the_shared_chain()
    {
        // finding 87 (2026-10-24, run t629cj): TGN has Dublin the county, the region and "County Dublin", all under "Ireland, Europe, World",
        // and "Italy" is its nation "Italia" beside the Roman province "Italia"; the walk had seen several and resolved neither, so
        // "Newbridge (Dublin)" went to Kildare's Newbridge by a vote and "Venice (Italy)" matched a person entity labelled "Venice"
        var ds = "anc" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Tgn(string id, string label, string parent, string placeType, params string[] variants) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label, .. variants], "Place", null, [],
            $"{{\"parentString\":\"{parent}\",\"placeType\":\"http://vocab.getty.edu/aat/{placeType}\"}}", DateTimeOffset.UtcNow, "test");
        static AuthorityRecord Aat(string id, string label) => new($"http://vocab.getty.edu/aat/{id}", "aat", label, [label], "Type", null, [], null, DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Aat("300000771", "counties")); await pg.Store.PutAuthorityAsync(Aat("300128207", "nations"));
        await pg.Store.PutAuthorityAsync(Aat("300387067", "regions")); await pg.Store.PutAuthorityAsync(Aat("300000774", "provinces")); await pg.Store.PutAuthorityAsync(Aat("300008347", "inhabited places"));
        await pg.Store.PutAuthorityAsync(Tgn("7001304", "Dublin", "Ireland, Europe, World", "300000771"));
        await pg.Store.PutAuthorityAsync(Tgn("7593747", "Dublin", "Ireland, Europe, World", "300387067"));
        await pg.Store.PutAuthorityAsync(Tgn("7901003", "County Dublin", "County Fingal, Ireland, Europe, World", "300000771", "Dublin"));
        await pg.Store.PutAuthorityAsync(Tgn("7001306", "Dublin", "Dublin, Ireland, Europe, World", "300008347"));
        await pg.Store.PutAuthorityAsync(Tgn("1042763", "Newbridge", "Kildare, Ireland, Europe, World", "300008347"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n92014274", "loc", "Newbridge (Kildare, Ireland)", ["Newbridge (Kildare, Ireland)"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(Tgn("1000080", "Italia", "Europe, World", "300128207", "Italy"));
        await pg.Store.PutAuthorityAsync(Tgn("7030349", "Italia", "Roman Empire, World", "300000774", "Italy"));
        var venice = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Venice", ["Venice"], null, [], [], EntityStatus.Aligned), scheme);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        var newbridge = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Newbridge (Dublin)"));
        Assert.Equal("Ireland, Europe, World", newbridge.Reading.QualifierAncestors);
        Assert.NotEqual(Outcome.Accepted, newbridge.Outcome);
        Assert.Contains(newbridge.Groups.SelectMany(g => g.Members), m => m.Veto && m.Reason.Contains("the qualifier names a known place and this is elsewhere"));
        // finding 90: a heading placed in a town the walk does not know is not elsewhere. Sussex resolves (two counties under England, so the
        // shared chain), and LCSH's "Arundel Castle (Arundel, England)" disagrees with "(Sussex)" only for naming the town; it is not vetoed,
        // where Kildare, a known county outside Dublin's chain, is (above). Run rkjzag had minted nineteen Sussex and Glamorgan houses this way
        await pg.Store.PutAuthorityAsync(Tgn("7008144", "East Sussex", "England, United Kingdom, Europe, World", "300000771", "Sussex"));
        await pg.Store.PutAuthorityAsync(Tgn("7008145", "West Sussex", "England, United Kingdom, Europe, World", "300000771", "Sussex"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/subjects/sh85007000", "lcsh", "Arundel Castle (Arundel, England)", ["Arundel Castle (Arundel, England)"], "Type", null, [], null, DateTimeOffset.UtcNow, "test"));
        var withLcsh = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc"), new LocalIndexSource(pg.Store, "lcsh")], ProjectManifest.PmcDefault.ToOptions());
        var arundel = await withLcsh.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Arundel Castle (Sussex)"));
        Assert.Equal("England, United Kingdom, Europe, World", arundel.Reading.QualifierAncestors);
        var castle = Assert.Single(arundel.Groups.SelectMany(g => g.Members), m => m.Candidate.Source == "lcsh");
        Assert.False(castle.Veto); Assert.False(castle.QualifierAgrees == true);
        Assert.NotEqual(Outcome.Minted, arundel.Outcome);
        var veniceHeading = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Venice (Italy)"));
        Assert.Equal("Italia, Europe, World", veniceHeading.Reading.QualifierAncestors);
        Assert.NotEqual(Outcome.MatchedStore, veniceHeading.Outcome);
        Assert.Contains(veniceHeading.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == venice.Id && m.Veto && m.Reason.Contains("not a person"));
    }
}
