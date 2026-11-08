using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// Finding 113 (2026-11-08, read directly in the store): "St Ives (Cornwall)" and the heading "St. Ives (Cornwall, England)" were queued and
/// minted with TGN's Cornwall record and LoC's heading at 1.00, because the project's two legacy "St Ives" entities, bare-labelled and
/// holding only their LoC headings (the pass through Wikidata left them alone), were read as unplaced, carried into one group by an LCSH
/// park record, and the Cambridgeshire entity's vetoed heading vetoed the group. An entity whose label carries no qualifier is placed
/// through its records: TGN's parents (finding 84) and now LoC's parenthetical too.
/// </summary>
[Collection("postgres")]
public class Finding113Tests(PostgresFixture pg)
{
    private const string TgnCornwall = "http://vocab.getty.edu/tgn/7011362", TgnCambs = "http://vocab.getty.edu/tgn/7012007";
    private const string LocCornwall = "http://id.loc.gov/authorities/names/n80103996", LocCambs = "http://id.loc.gov/authorities/names/n81035494";
    private const string Park = "http://id.loc.gov/authorities/subjects/sh2008008555";

    [PostgresFact]
    public async Task A_bare_labelled_entity_is_placed_through_its_LoC_heading()
    {
        var ds = "f113" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(TgnCornwall, "tgn", "Saint Ives", ["Saint Ives", "St Ives", "St. Ives"], "Place", null, [], "{\"parentString\":\"Cornwall, England, United Kingdom, Europe, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(TgnCambs, "tgn", "Saint Ives", ["Saint Ives", "St Ives", "St. Ives"], "Place", null, [], "{\"parentString\":\"Cambridgeshire, England, United Kingdom, Europe, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(LocCornwall, "loc", "St. Ives (Cornwall, England)", ["St. Ives (Cornwall, England)", "Saint Ives (Cornwall, England)"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(LocCambs, "loc", "St. Ives (Cambridgeshire, England)", ["St. Ives (Cambridgeshire, England)", "Saint Ives (Cambridgeshire, England)"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Park, "lcsh", "Saint Ives (England : Park)", ["Saint Ives (England : Park)"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        // the two legacy entities: bare labels, each holding its LoC heading and nothing TGN
        var cornwall = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "St Ives", ["St Ives"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(cornwall.Id, new Equivalent(LocCornwall, "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var cambs = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "St Ives", ["St Ives"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(cambs.Id, new Equivalent(LocCambs, "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc"), new LocalIndexSource(pg.Store, "lcsh")], ProjectManifest.PmcDefault.ToOptions());

        // the qualified string is Cornwall's entity, placed through its heading; the heading as written likewise
        var sub = await pg.Store.CollectAsync(ds, "keywords:sub", "St Ives (Cornwall)");
        var e = await reconciler.EvaluateAsync(ds, sub, includeStore: true);
        Assert.Equal(Outcome.MatchedStore, e.Outcome);
        Assert.Equal(cornwall.Id, e.StoreMatch!.Id);
        var head = await pg.Store.CollectAsync(ds, "keywords:head", "St. Ives (Cornwall, England)");
        var h = await reconciler.EvaluateAsync(ds, head, includeStore: true);
        Assert.Equal(Outcome.MatchedStore, h.Outcome);
        Assert.Equal(cornwall.Id, h.StoreMatch!.Id);
        // and Cambridgeshire's string is Cambridgeshire's entity
        var hunts = await pg.Store.CollectAsync(ds, "place", "St Ives, Cambridgeshire");
        var c = await reconciler.EvaluateAsync(ds, hunts, includeStore: true);
        Assert.Equal(Outcome.MatchedStore, c.Outcome);
        Assert.Equal(cambs.Id, c.StoreMatch!.Id);
    }
}
