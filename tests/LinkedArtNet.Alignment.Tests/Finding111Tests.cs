using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// Finding 111 (2026-11-07, run d6h8va, after the pass through Wikidata): a store entity with no dates of its own borrowed the lifespan of
/// the first dated record among its ULAN and LoC equivalents, so the legacy "James Gillray", given LoC's "Gillray, James, 1756-1815" and
/// ULAN's Gillray of 1757 by the pass, took 1756 against the string's 1757, was vetoed, and its veto spread through the group it holds
/// ULAN's record in, which agreed: minted, with the right record in the index and the right entity in the store (nine strings, and five to
/// the desk by the mirror). An entity dated through its records agrees when any of its dated records does, and conflicts only when all do.
/// </summary>
[Collection("postgres")]
public class Finding111Tests(PostgresFixture pg)
{
    private const string Ulan = "http://vocab.getty.edu/ulan/500115563", Loc = "http://id.loc.gov/authorities/names/n79084260";
    private const string UlanPainter = "http://vocab.getty.edu/ulan/500025601", LocEngraver = "http://id.loc.gov/authorities/names/nb2003047283";

    private static Lifespan Life(int b, int e, string? text = null) => new(new DateTimeOffset(b, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(b, 12, 31, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(e, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(e, 12, 31, 0, 0, 0, TimeSpan.Zero), text ?? $"{b}-{e}");

    private Reconciler Reconciler(string ds) => new(pg.Store, new IdentifierScheme("https://data.example.org/" + ds), [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

    [PostgresFact]
    public async Task An_entity_dated_through_its_records_agrees_when_any_of_them_does()
    {
        var ds = "f111a" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        // ULAN says 1757, LoC says 13 August 1756: the authorities disagree by a year, as they do
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Ulan, "ulan", "Gillray, James", ["Gillray, James"], "Person", Life(1757, 1815), [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Loc, "loc", "Gillray, James, 1756-1815", ["Gillray, James, 1756-1815", "Gillray, James"], "Person",
            new Lifespan(new DateTimeOffset(1756, 8, 13, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1756, 8, 13, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1815, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1815, 12, 31, 0, 0, 0, TimeSpan.Zero), "1756-08-13-1815"),
            [Ulan], "{\"headingDates\":\"1756-1815\"}", DateTimeOffset.UtcNow, "test"));
        // the legacy entity, its label in LUX's natural order, no dates of its own, LoC's record before ULAN's among its equivalents (the pass added them)
        var gillray = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "James Gillray", ["James Gillray"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(gillray.Id, new Equivalent(Loc, "loc", DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow));
        await pg.Store.AddEquivalentAsync(gillray.Id, new Equivalent(Ulan, "ulan", DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow));

        var s = await pg.Store.CollectAsync(ds, "persauthorfull", "Gillray, James, 1757-1815");
        var e = await Reconciler(ds).EvaluateAsync(ds, s, includeStore: true);
        Assert.NotEqual(Outcome.Minted, e.Outcome);
        Assert.Equal(Outcome.MatchedStore, e.Outcome);
        Assert.Equal(gillray.Id, e.StoreMatch!.Id);
    }

    [PostgresFact]
    public async Task An_entity_all_of_whose_records_conflict_is_not_this_person()
    {
        var ds = "f111b" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        // the legacy "George Sheffield", whose item named ULAN's painter of 1839-1892; the string names the engraver LoC dates 1800-1852
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(UlanPainter, "ulan", "Sheffield, George", ["Sheffield, George"], "Person", Life(1839, 1892), [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(LocEngraver, "loc", "Sheffield, George, 1800-1852", ["Sheffield, George, 1800-1852", "Sheffield, George"], "Person", Life(1800, 1852), [], "{\"headingDates\":\"1800-1852\"}", DateTimeOffset.UtcNow, "test"));
        var painter = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "George Sheffield", ["George Sheffield"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(painter.Id, new Equivalent(UlanPainter, "ulan", DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow));

        var s = await pg.Store.CollectAsync(ds, "persauthorfull", "Sheffield, George, 1800-1852");
        var e = await Reconciler(ds).EvaluateAsync(ds, s, includeStore: true);
        Assert.NotEqual(Outcome.MatchedStore, e.Outcome);
        Assert.True(e.Outcome == Outcome.Accepted, EvaluationText.Narrate(e, candidates: true));
        Assert.Contains(LocEngraver, e.Accepted!.Uris);
        Assert.DoesNotContain(painter.Id, e.Accepted.Uris);
    }
}
