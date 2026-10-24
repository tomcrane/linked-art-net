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
        // ancestors widen agreement, never narrow it (finding 56's rule): the project's "Statens museum for kunst (Denmark)" is placed by
        // "(Copenhagen, Denmark)" on its own label before its widened chain "Denmark, Danmark, Europe, World" is consulted (run t629cj had
        // vetoed it as elsewhere and minted the museum again)
        await pg.Store.PutAuthorityAsync(Tgn("1000084", "Danmark", "Europe, World", "300128207") with { Labels = ["Danmark", "Denmark"] });
        var statens = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Statens museum for kunst (Denmark)", ["Statens museum for kunst (Denmark)", "Statens Museum for Kunst"], null, [], [], EntityStatus.Aligned), scheme);
        var museum = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Statens Museum for Kunst (Copenhagen, Denmark)"));
        Assert.Equal(Outcome.MatchedStore, museum.Outcome); Assert.Equal(statens.Id, museum.StoreMatch!.Id);
        Assert.Contains("qualifier places it", museum.StoreChoice!.Reason);
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
        var veniceHeading = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Venice (Italy)"));
        Assert.Equal("Italia, Europe, World", veniceHeading.Reading.QualifierAncestors);
        Assert.NotEqual(Outcome.MatchedStore, veniceHeading.Outcome);
        Assert.Contains(veniceHeading.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == venice.Id && m.Veto && m.Reason.Contains("not a person"));
    }
}
