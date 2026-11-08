using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// Finding 116 (2026-11-08, the owner at "Boston" and "Birmingham" bare after the pass through Wikidata placed the legacy entities): two
/// project entities of the name, each in a group at 1.00, and prominence standing aside because a project entity was among the live groups,
/// so the string went to the desk with Alabama's above England's. When every tied group holds a project entity the entities cancel as
/// evidence and prominence decides as it does when none holds one; a tied group without one still leaves the string to the desk.
/// </summary>
[Collection("postgres")]
public class Finding116Tests(PostgresFixture pg)
{
    private const string TgnMass = "http://vocab.getty.edu/tgn/7013445", TgnLincs = "http://vocab.getty.edu/tgn/7010384", TgnGeorgia = "http://vocab.getty.edu/tgn/2068817";
    private const string LocMass = "http://id.loc.gov/authorities/names/n79045553", LocLincs = "http://id.loc.gov/authorities/names/n81040129", LocGeorgia = "http://id.loc.gov/authorities/names/n85157098";

    private static AuthorityRecord Wikidata(string q, string label, int sitelinks, params string[] sameAs) =>
        new($"http://www.wikidata.org/entity/{q}", "wikidata", label, [label], "Place", null, sameAs, $"{{\"ids\":{{}},\"sitelinks\":{sitelinks}}}", DateTimeOffset.UtcNow, "test");

    [PostgresFact]
    public async Task Prominence_decides_when_every_tied_group_holds_a_project_entity()
    {
        var ds = "f116" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(TgnMass, "tgn", "Boston", ["Boston"], "Place", null, [], "{\"parentString\":\"Suffolk, Massachusetts, United States, North and Central America, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(TgnLincs, "tgn", "Boston", ["Boston"], "Place", null, [], "{\"parentString\":\"Lincolnshire, England, United Kingdom, Europe, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(LocMass, "loc", "Boston (Mass.)", ["Boston (Mass.)"], "Place", null, [TgnMass], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(LocLincs, "loc", "Boston (England)", ["Boston (England)"], "Place", null, [TgnLincs], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(Wikidata("Q100", "Boston", 250, TgnMass, LocMass));
        await pg.Store.PutAuthorityAsync(Wikidata("Q27000", "Boston", 60, TgnLincs, LocLincs));
        // the two legacy entities, bare-labelled, each holding its records since the pass
        var mass = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Boston", ["Boston"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(mass.Id, new Equivalent(TgnMass, "tgn", DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow));
        await pg.Store.AddEquivalentAsync(mass.Id, new Equivalent(LocMass, "loc", DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow));
        var lincs = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Boston", ["Boston"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(lincs.Id, new Equivalent(TgnLincs, "tgn", DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow));
        await pg.Store.AddEquivalentAsync(lincs.Id, new Equivalent(LocLincs, "loc", DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        // both tied groups hold a project entity: the world writes about Massachusetts, and the string goes onto that entity
        var boston = await pg.Store.CollectAsync(ds, "place", "Boston");
        var e = await reconciler.EvaluateAsync(ds, boston, includeStore: true);
        Assert.Equal(Outcome.Accepted, e.Outcome);
        Assert.Contains(TgnMass, e.Accepted!.Uris);
        Assert.Contains(e.Accepted.Members, m => m.Reason.Contains("finding 116") && m.Reason.Contains("250 sitelinks"));

        // a third Boston in the tie with no project entity: the entities are evidence against it, not against each other, and prominence
        // stands aside as before (the owner's desk shape stays the desk's)
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(TgnGeorgia, "tgn", "Boston", ["Boston"], "Place", null, [], "{\"parentString\":\"Thomas, Georgia, United States, North and Central America, World\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(LocGeorgia, "loc", "Boston (Ga.)", ["Boston (Ga.)"], "Place", null, [TgnGeorgia], null, DateTimeOffset.UtcNow, "test"));
        var again = await reconciler.EvaluateAsync(ds, boston, includeStore: true);
        Assert.Equal(Outcome.Queued, again.Outcome);
        Assert.Contains(again.Groups.SelectMany(g => g.Members), m => m.Reason.Contains("prominence stands aside"));
    }
}
