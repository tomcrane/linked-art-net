using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class StorePlacementTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_qualified_heading_never_takes_a_store_entity_placed_elsewhere_in_any_context()
    {
        // finding 84 (2026-10-24, from run fk53p7): "Lincoln (England)" in keywords:head had matched the project's "Lincoln, Neb", whose
        // label says nothing TGN-shaped but whose TGN equivalent is in Nebraska; "Newbridge (Dublin)" its "Newbridge, Co. Kildare", whose
        // label placement disagreed and was taken anyway. A label's county is widened to its ancestors, so "Boughton House (England)"
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
        var kildare = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Newbridge, Co. Kildare", ["Newbridge, Co. Kildare", "Newbridge (Kildare, Ireland)", "Newbridge"], null, [], [], EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(kildare.Id, new Equivalent("http://id.loc.gov/authorities/names/n92014274", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var boughton = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Boughton House (Northamptonshire)", ["Boughton House (Northamptonshire)", "Boughton House"], null, [], [], EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(boughton.Id, new Equivalent("http://id.loc.gov/authorities/names/n85000001", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var cornwall = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Cornwall (England : County)", ["Cornwall (England : County)", "Cornwall"], null, [], [], EntityStatus.Aligned), scheme);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        var lincoln = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Lincoln (England)"));
        Assert.NotEqual(Outcome.MatchedStore, lincoln.Outcome); Assert.Null(lincoln.StoreMatch);
        Assert.Contains(lincoln.Groups.SelectMany(g => g.Members), m => m.Candidate.Source == "store" && m.Reason.Contains("elsewhere"));
        var newbridge = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Newbridge (Dublin)"));
        Assert.NotEqual(Outcome.MatchedStore, newbridge.Outcome); Assert.Null(newbridge.StoreMatch);
        var house = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Boughton House (England)"));
        Assert.Equal(Outcome.MatchedStore, house.Outcome); Assert.Equal(boughton.Id, house.StoreMatch!.Id);
        Assert.Contains("qualifier places it", house.StoreChoice!.Reason);
        var bare = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:sub", "Cornwall"));
        Assert.Equal(Outcome.MatchedStore, bare.Outcome); Assert.Equal(cornwall.Id, bare.StoreMatch!.Id);
    }
}
