using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class DuplicatesTests(PostgresFixture pg)
{
    private static readonly DateTimeOffset T0 = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Entity E(string id, EntityClass cls, string label, EntityStatus status = EntityStatus.Provisional, Lifespan? lifespan = null, int age = 0, params (string Source, string Uri)[] ids) =>
        new(id, "d", cls, label, [], lifespan, [], [], status, null, ids.Select(q => new Equivalent(q.Uri, q.Source, "imported", 1.0, T0)).ToList(), T0.AddDays(age), T0, null);

    [Fact]
    public void Twins_merge_where_their_ids_do_not_conflict_and_people_need_dates_or_a_shared_id()
    {
        // places: the one with ids survives, the bare one merges into it
        var england = DuplicateMerger.Plan([E("p1", EntityClass.Place, "England"), E("p2", EntityClass.Place, "England", EntityStatus.Aligned, age: 3, ids: [("tgn", "http://vocab.getty.edu/tgn/7002445")])]);
        Assert.Equal("p2", england.Survivor.Id); Assert.Equal(["p1"], england.Merge.Select(e => e.Id)); Assert.Empty(england.Kept);
        // two bare twins: the older survives
        var evanston = DuplicateMerger.Plan([E("p3", EntityClass.Place, "Evanston", age: 2), E("p4", EntityClass.Place, "Evanston", age: 1)]);
        Assert.Equal("p4", evanston.Survivor.Id); Assert.Equal(["p3"], evanston.Merge.Select(e => e.Id));
        // a confirmed entity survives over one with more ids
        var confirmed = DuplicateMerger.Plan([E("t1", EntityClass.Type, "Painting", EntityStatus.Aligned, ids: [("aat", "a1"), ("lcsh", "l1")]), E("t2", EntityClass.Type, "Painting", EntityStatus.Confirmed, ids: [("aat", "a1")])]);
        Assert.Equal("t2", confirmed.Survivor.Id); Assert.Equal(["t1"], confirmed.Merge.Select(e => e.Id));
        // the same source with different ids is a conflict; ids from different sources with nothing shared are kept apart
        var conflict = DuplicateMerger.Plan([E("g1", EntityClass.Group, "Agnew's", ids: [("ulan", "u1")]), E("g2", EntityClass.Group, "Agnew's", ids: [("ulan", "u2")])]);
        Assert.Empty(conflict.Merge); Assert.Equal("different ulan ids", conflict.Kept.Single().Reason);
        var disjoint = DuplicateMerger.Plan([E("g3", EntityClass.Group, "Agnew's", ids: [("ulan", "u1")]), E("g4", EntityClass.Group, "Agnew's", ids: [("loc", "n1")])]);
        Assert.Empty(disjoint.Merge); Assert.Equal("ids from different sources, none shared", disjoint.Kept.Single().Reason);
        // VIAF and LUX are clusters: differing VIAF ids beside a shared Wikidata id are not a conflict (the legacy twins of "Evanston"),
        // differing VIAF ids with nothing else shared keep the twins apart, and a differing authoritative id is a conflict whatever is shared
        var evanstonTwins = DuplicateMerger.Plan([E("p8", EntityClass.Place, "Evanston", EntityStatus.Aligned, ids: [("lux", "x1"), ("viaf", "v1"), ("wikidata", "q1")]), E("p9", EntityClass.Place, "Evanston", EntityStatus.Aligned, age: 1, ids: [("lux", "x2"), ("viaf", "v2"), ("wikidata", "q1")])]);
        Assert.Equal("p8", evanstonTwins.Survivor.Id); Assert.Equal(["p9"], evanstonTwins.Merge.Select(e => e.Id));
        var viafOnly = DuplicateMerger.Plan([E("p10", EntityClass.Place, "Newbury", ids: [("lux", "x1"), ("viaf", "v1")]), E("p11", EntityClass.Place, "Newbury", ids: [("lux", "x2"), ("viaf", "v2")])]);
        Assert.Empty(viafOnly.Merge); Assert.Equal("different lux and viaf ids, nothing else shared", viafOnly.Kept.Single().Reason);
        var wikidataDiffers = DuplicateMerger.Plan([E("p12", EntityClass.Place, "Washington", ids: [("viaf", "v1"), ("wikidata", "q1")]), E("p13", EntityClass.Place, "Washington", ids: [("viaf", "v1"), ("wikidata", "q2")])]);
        Assert.Empty(wikidataDiffers.Merge); Assert.Equal("different wikidata ids", wikidataDiffers.Kept.Single().Reason);
        // shared id with extra ids on one side is the same thing
        var superset = DuplicateMerger.Plan([E("g5", EntityClass.Group, "Agnew's", ids: [("ulan", "u1"), ("loc", "n1")]), E("g6", EntityClass.Group, "Agnew's", ids: [("ulan", "u1")])]);
        Assert.Equal("g5", superset.Survivor.Id); Assert.Equal(["g6"], superset.Merge.Select(e => e.Id));
        // people: a bare name twice stays apart; the same dates, or a shared id, merge
        var smiths = DuplicateMerger.Plan([E("h1", EntityClass.Person, "Smith, John"), E("h2", EntityClass.Person, "Smith, John")]);
        Assert.Empty(smiths.Merge); Assert.Equal("a person twice without a shared id or the same dates", smiths.Kept.Single().Reason);
        var dated = new Lifespan(new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero), null, new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero), null, "1900-1980");
        var sameDates = DuplicateMerger.Plan([E("h3", EntityClass.Person, "Smith, John", lifespan: dated), E("h4", EntityClass.Person, "Smith, John", lifespan: dated with { Text = "1900 - 1980" })]);
        Assert.Single(sameDates.Merge);
        var otherDates = DuplicateMerger.Plan([E("h5", EntityClass.Person, "Smith, John", lifespan: dated), E("h6", EntityClass.Person, "Smith, John", lifespan: dated with { EndEarliest = new DateTimeOffset(1981, 1, 1, 0, 0, 0, TimeSpan.Zero) })]);
        Assert.Empty(otherDates.Merge);
        var sharedId = DuplicateMerger.Plan([E("h7", EntityClass.Person, "Smith, John", ids: [("ulan", "u9")]), E("h8", EntityClass.Person, "Smith, John", ids: [("ulan", "u9"), ("loc", "n9")])]);
        Assert.Equal("h8", sharedId.Survivor.Id); Assert.Single(sharedId.Merge);
        // people and bodies are twins by the words of the name in any order too, under the same rules
        Assert.Equal(DuplicateMerger.WordKey("Piper, John"), DuplicateMerger.WordKey("John Piper")); Assert.Equal("", DuplicateMerger.WordKey("Abbott"));
        var inverted = DuplicateMerger.Plan([E("h11", EntityClass.Person, "Piper, John", ids: [("viaf", "v5"), ("lux", "x5")]), E("h12", EntityClass.Person, "John Piper", ids: [("viaf", "v5")])]);
        Assert.Equal("h11", inverted.Survivor.Id); Assert.Single(inverted.Merge);
        Assert.Contains("merge h12 \"John Piper\" (provisional; viaf)", new DuplicateReport([inverted]).ToString());
        Assert.Empty(DuplicateMerger.Plan([E("h13", EntityClass.Person, "Piper, John"), E("h14", EntityClass.Person, "John Piper")]).Merge);
        // three twins: each is judged against the survivor
        var three = DuplicateMerger.Plan([E("p5", EntityClass.Place, "Paris", ids: [("tgn", "t1")]), E("p6", EntityClass.Place, "Paris"), E("p7", EntityClass.Place, "Paris", ids: [("tgn", "t2")])]);
        Assert.Equal("p5", three.Survivor.Id); Assert.Equal(["p6"], three.Merge.Select(e => e.Id)); Assert.Equal("p7", three.Kept.Single().Entity.Id);
        // the report reads as lines and a count by class
        var report = new DuplicateReport([england, smiths]).ToString();
        Assert.Contains("Place \"England\": keep p2 (aligned; tgn); merge p1 (provisional; no ids)", report);
        Assert.Contains("kept apart h2 (provisional; no ids): a person twice", report);
        Assert.Matches(@"Place\s+1\s+2\s+1\s+0", report); Assert.Matches(@"all\s+2\s+4\s+1\s+1", report);
    }

    [PostgresFact]
    public async Task The_store_finds_twins_and_the_pass_merges_them()
    {
        var ds = "dup-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var bare = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "England", ["England"]), scheme);
        var placed = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "ENGLAND", ["England"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(placed.Id, new Equivalent("http://vocab.getty.edu/tgn/7002445", "tgn", "imported", 1.0, DateTimeOffset.UtcNow));
        await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Scotland", ["Scotland"]), scheme); // alone: not a twin
        await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "England", ["England"]), scheme); // another class: not a twin of the places
        var smith1 = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Smith, John", ["Smith, John"]), scheme);
        await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Smith, John", ["Smith, John"]), scheme);
        await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Agnew, Thomas", ["Agnew, Thomas"]), scheme); // inverted-form twins of a body, no ids: merge
        await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Thomas Agnew", ["Thomas Agnew"]), scheme);
        var s = await pg.Store.CollectAsync(ds, "place", "England");
        await pg.Store.DecideAsync(s.Id, bare.Id, "minted");

        var twins = await pg.Store.EntitiesSharingLabelAsync(ds);
        Assert.Equal([EntityClass.Person, EntityClass.Person, EntityClass.Place, EntityClass.Place], twins.Select(e => e.Class).Order()); // the lone Scotland and the Group are not twins
        Assert.Contains(twins, e => e.Id == placed.Id && e.Equivalents.Count == 1);

        var merger = new DuplicateMerger(pg.Store);
        var plan = await merger.PlanAsync(ds);
        Assert.Equal(3, plan.Twins.Count); Assert.Equal(2, plan.Merges); Assert.Equal(1, plan.KeptApart);
        Assert.Single(plan.Twins.Single(t => t.Class == EntityClass.Group).Merge);
        Assert.Equal(placed.Id, plan.Twins.Single(t => t.Class == EntityClass.Place).Survivor.Id);
        Assert.Single((await merger.PlanAsync(ds, [EntityClass.Person])).Twins); // limited to a class
        Assert.NotNull(await pg.Store.GetEntityAsync(bare.Id)); // a plan changes nothing
        Assert.Null((await pg.Store.GetEntityAsync(bare.Id))!.MergedInto);

        Assert.Equal(2, await merger.ApplyAsync(plan, "test"));
        var merged = (await pg.Store.GetEntityAsync(bare.Id))!;
        Assert.Equal(placed.Id, merged.MergedInto); // the identifier stays, pointing at the survivor, for the export to write as an equivalent
        Assert.Equal(placed.Id, (await pg.Store.ResolveAsync(ds, "place", "England"))!.Id); // the string's decision follows
        Assert.Null((await pg.Store.GetEntityAsync(smith1.Id))!.MergedInto);
        Assert.Empty(await merger.PlanAsync(ds, [EntityClass.Place]).ContinueWith(t => t.Result.Twins)); // nothing left to do for places
    }
}
