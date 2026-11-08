using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// Finding 114 (2026-11-08, the owner at "Birmingham" in keywords:sub): the heading "Architecture--England--Birmingham" says England in its
/// own part, and the sub "Birmingham" stood between TGN's Birmingham in England and in Alabama at 1.00 with Alabama's first. A place
/// subdivision with no qualifier of its own is placed by the one place its sibling parts resolve to.
/// </summary>
[Collection("postgres")]
public class Finding114Tests(PostgresFixture pg)
{
    private const string England = "http://vocab.getty.edu/tgn/7002445", Alabama = "http://vocab.getty.edu/tgn/7007475";
    private const string BhamEngland = "http://vocab.getty.edu/tgn/7010955", BhamAlabama = "http://vocab.getty.edu/tgn/7013425";

    [PostgresFact]
    public async Task A_place_subdivision_is_placed_by_its_sibling_part()
    {
        var ds = "f114" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(England, "tgn", "England", ["England"], "Place", null, [], "{\"parentString\":\"United Kingdom, Europe, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Alabama, "tgn", "Alabama", ["Alabama"], "Place", null, [], "{\"parentString\":\"United States, North and Central America, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(BhamEngland, "tgn", "Birmingham", ["Birmingham"], "Place", null, [], "{\"parentString\":\"West Midlands, England, United Kingdom, Europe, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(BhamAlabama, "tgn", "Birmingham", ["Birmingham"], "Place", null, [], "{\"parentString\":\"Jefferson, Alabama, United States, North and Central America, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n79023147", "loc", "England", ["England"], "Place", null, [England], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/subjects/sh85006611", "lcsh", "Architecture", ["Architecture"], "Type", null, [], "{\"collections\":[\"LCSHAuthorizedHeadings\"]}", DateTimeOffset.UtcNow, "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc"), new LocalIndexSource(pg.Store, "lcsh")], ProjectManifest.PmcDefault.ToOptions());

        // a bare "Birmingham" with no heading around it stands between the two
        var bare = await pg.Store.CollectAsync(ds, "place", "Birmingham");
        Assert.Equal(Outcome.Queued, (await reconciler.EvaluateAsync(ds, bare, includeStore: true)).Outcome);

        // the heading: its England part is decided first, and its Birmingham part is then placed by it
        var heading = await pg.Store.CollectAsync(ds, "keywords", "Architecture--England--Birmingham");
        await reconciler.RunAsync(ds);
        var birmingham = (await pg.Store.PartsAsync(heading.Id)).Single(p => p.Raw == "Birmingham");
        var decision = (await pg.Store.DecisionForAsync(birmingham.Id))!;
        Assert.NotEqual(DecisionMethod.Queued, decision.Method);
        var entity = (await pg.Store.GetEntityAsync(decision.EntityId!))!;
        Assert.Contains(entity.Equivalents, q => q.Uri == BhamEngland);
        Assert.DoesNotContain(entity.Equivalents, q => q.Uri == BhamAlabama);
        Assert.Contains("its heading's part England", decision.EvidenceJson!);
    }
}
