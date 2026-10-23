using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class WholeHeadingTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task Two_store_entities_one_labelled_with_the_whole_heading_answer_the_heading_with_that_one()
    {
        // finding 81 (2026-10-23): the project already holds "Surrealism" and "Surrealism (Literature)" as concepts; the heading
        // "Surrealism (Literature)" in keywords:head is the second, matched in the store, not an ambiguity of two
        var ds = "wh" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var bare = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Type, "Surrealism", ["Surrealism"], null, [], [], EntityStatus.Aligned), scheme);
        var whole = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Type, "Surrealism (Literature)", ["Surrealism (Literature)"], null, [], [], EntityStatus.Aligned), scheme);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "lcsh")], ProjectManifest.PmcDefault.ToOptions());
        var heading = await pg.Store.CollectAsync(ds, "keywords:head", "Surrealism (Literature)");
        var e = await reconciler.EvaluateAsync(ds, heading);
        Assert.Equal(Outcome.MatchedStore, e.Outcome); Assert.Equal(whole.Id, e.StoreMatch!.Id);
        // the bare heading still finds the bare entity alone
        Assert.Equal(bare.Id, (await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Surrealism"))).StoreMatch!.Id);
    }
}
