using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// The desk's evidence after the run (2026-11-07): the second pass keeps its reasons on a string it leaves queued, and a card knows which
/// project entity holds its record now, named with its placement.
/// </summary>
[Collection("postgres")]
public class DeskEvidenceTests(PostgresFixture pg)
{
    private const string A = "http://vocab.getty.edu/ulan/500900001", B = "http://vocab.getty.edu/ulan/500900002";

    [PostgresFact]
    public async Task The_second_pass_keeps_its_reasons_on_a_string_it_leaves_queued_and_a_card_knows_its_records_holder()
    {
        var ds = "de-" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        // two undated namesakes tie at 1.00: queued in the first pass and still queued in the second
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(A, "ulan", "Hall, Harry", ["Hall, Harry"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(B, "ulan", "Hall, Harry", ["Hall, Harry"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n79045553", "loc", "Boston (Mass.)", ["Boston (Mass.)", "Boston"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var desk = new ReviewService(pg.Store, scheme, reconciler);
        var hall = await pg.Store.CollectAsync(ds, "persauthorfull", "Hall, Harry");
        var report = await reconciler.RunAsync(ds);
        Assert.Equal(1, report.Queued);
        Assert.Equal(1, report.Reconsidered);
        var decision = (await pg.Store.DecisionForAsync(hall.Id))!;
        Assert.Equal(DecisionMethod.Queued, decision.Method);
        Assert.Contains("secondPass", decision.EvidenceJson!); // the second pass's own evidence stands, not the first pass's
        Assert.NotNull(decision.EntityId); // the placeholder unchanged
        Assert.Equal(DecisionMethod.Queued, (await pg.Store.DecisionForAsync(hall.Id))!.Method);

        // after the run, a person makes an entity hold record A: the card for the string knows, without "Look again"
        var before = (await desk.GetAsync(hall.Id))!;
        Assert.True(before.Holders is null || before.Holders.Count == 0);
        var boston = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Boston", ["Boston"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(boston.Id, new Equivalent("http://id.loc.gov/authorities/names/n79045553", "loc", DecisionMethod.Human, 1, DateTimeOffset.UtcNow));
        var harry = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Harry Hall", ["Harry Hall"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(harry.Id, new Equivalent(A, "ulan", DecisionMethod.Human, 1, DateTimeOffset.UtcNow));
        var after = (await desk.GetAsync(hall.Id))!;
        Assert.NotNull(after.Holders);
        Assert.Equal(harry.Id, after.Holders![A].Entity.Id);
        Assert.False(after.Holders.ContainsKey(B));
        // a place entity's facts carry its placement, read from the LoC heading it holds
        var facts = (await desk.FactsAsync(boston.Id))!;
        Assert.Equal("Mass.", facts.Placement);
        Assert.Null((await desk.FactsAsync(harry.Id))!.Placement);
    }
}
