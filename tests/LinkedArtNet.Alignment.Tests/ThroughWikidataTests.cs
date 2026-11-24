using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// The pass over the legacy alignments (2026-11-06, the owner at "Birmingham (England)"): an aligned entity with a Wikidata item and no
/// local record gains the TGN, LoC and ULAN records its item is the same as, one per source, reversibly; then the rules reach it.
/// </summary>
[Collection("postgres")]
public class ThroughWikidataTests(PostgresFixture pg)
{
    private static AuthorityRecord Tgn(string id, string label, string parent) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label], "Place", null, [], $"{{\"parentString\":\"{parent}\"}}", DateTimeOffset.UtcNow, "test");
    private static AuthorityRecord Item(string q, string label, string type, params string[] sameAs) => new($"http://www.wikidata.org/entity/{q}", "wikidata", label, [label], type, null, sameAs, "{\"sitelinks\":40}", DateTimeOffset.UtcNow, "test");
    private static Equivalent Wikidata(string q) => new($"http://www.wikidata.org/entity/{q}", "wikidata", DecisionMethod.Human, 1, DateTimeOffset.UtcNow);

    [PostgresFact]
    public async Task A_legacy_alignment_gains_the_records_its_item_names_and_the_rules_then_reach_it()
    {
        var ds = "twd" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        const string england = "http://vocab.getty.edu/tgn/7010955", alabama = "http://vocab.getty.edu/tgn/7013425";
        const string heading = "http://id.loc.gov/authorities/names/n79084311";
        await pg.Store.PutAuthorityAsync(Tgn("7010955", "Birmingham", "West Midlands, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7013425", "Birmingham", "Jefferson, Alabama, United States, North and Central America, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7010874", "Cambridge", "Cambridgeshire, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7013596", "Cambridge", "Middlesex, Massachusetts, United States, North and Central America, World"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(heading, "loc", "Birmingham (England)", ["Birmingham (England)"], "Place", null, [], null, DateTimeOffset.UtcNow, "test"));
        // the items: England's links TGN's city and LoC's heading (and a ULAN record the store does not hold); Alabama's links its TGN record;
        // Cambridge's links two TGN records at once; Antwerp's item is not in the store
        await pg.Store.PutAuthorityAsync(Item("Q2256", "Birmingham", "Place", england, heading, "http://vocab.getty.edu/ulan/500999999"));
        await pg.Store.PutAuthorityAsync(Item("Q79867", "Birmingham", "Place", alabama));
        await pg.Store.PutAuthorityAsync(Item("Q350", "Cambridge", "Place", "http://vocab.getty.edu/tgn/7010874", "http://vocab.getty.edu/tgn/7013596"));
        var birmingham = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Birmingham", ["Birmingham"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(birmingham.Id, Wikidata("Q2256"));
        var alabamas = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Birmingham", ["Birmingham"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(alabamas.Id, Wikidata("Q79867"));
        var cambridge = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Cambridge", ["Cambridge"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(cambridge.Id, Wikidata("Q350"));
        var antwerp = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Antwerp", ["Antwerp"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(antwerp.Id, Wikidata("Q12892"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        // before the pass: the heading meets two unplaced, unjoined project entities of the name at 1.00 and is the desk's
        var string_ = await pg.Store.CollectAsync(ds, "keywords:head", "Birmingham (England)");
        Assert.Equal(Outcome.Queued, (await reconciler.EvaluateAsync(ds, string_, includeStore: true)).Outcome);

        var run = await pg.Store.StartRunAsync(ds);
        var report = await new ThroughWikidata(pg.Store).RunAsync(ds, run.Id);
        Assert.Equal(4, report.Read);
        Assert.Equal(2, report.Enriched);
        Assert.Equal(2, report.AddedBySource["tgn"]); Assert.Equal(1, report.AddedBySource["loc"]);
        Assert.Equal(1, report.ItemsNotInIndex);
        Assert.Single(report.Ambiguous); Assert.Contains("Cambridge", report.Ambiguous[0]);
        var gained = (await pg.Store.GetEntityAsync(birmingham.Id))!;
        Assert.Contains(gained.Equivalents, q => q.Uri == england && q.Method == DecisionMethod.ThroughWikidata && q.RunId == run.Id);
        Assert.Contains(gained.Equivalents, q => q.Uri == heading && q.Method == DecisionMethod.ThroughWikidata);
        Assert.DoesNotContain((await pg.Store.GetEntityAsync(cambridge.Id))!.Equivalents, q => q.Source == "tgn");
        // the pass run again adds nothing: the two given a record are out of its reach, the two it could not help are read again and left
        var again = await new ThroughWikidata(pg.Store).RunAsync(ds, run.Id);
        Assert.Equal(2, again.Read); Assert.Equal(0, again.Enriched);

        // after: England's entity is placed through its TGN record and Alabama's elsewhere, and the store stage takes the heading onto the
        // one whose placement the qualifier bears out, before the indexes are asked (findings 84 and 106 read the placement)
        var after = await reconciler.EvaluateAsync(ds, string_, includeStore: true);
        Assert.Equal(Outcome.MatchedStore, after.Outcome);
        Assert.Equal(birmingham.Id, after.StoreMatch!.Id);

        // undo-run takes the pass back, the entities still aligned by their items
        await pg.Store.UndoRunAsync(run.Id);
        var undone = (await pg.Store.GetEntityAsync(birmingham.Id))!;
        Assert.DoesNotContain(undone.Equivalents, q => q.Source == "tgn");
        Assert.Equal(EntityStatus.Aligned, undone.Status);
    }

    [PostgresFact]
    public async Task A_thin_place_gains_the_item_that_names_its_heading_and_the_items_gazetteer_record()
    {
        // the owner, 2026-11-23, after "Chatham": the project's Chatham (Kent) held LoC's heading alone, so it was the one candidate not
        // on the map. A VIAF identifier is not followed: the old import's were often a namesake's ("Darlington", a suburb of Perth)
        var ds = "thh" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Heading(string id, string label) => new($"http://id.loc.gov/authorities/names/{id}", "loc", label, [label], "Place", null, [], null, DateTimeOffset.UtcNow, "test");
        static Equivalent Loc(string id) => new($"http://id.loc.gov/authorities/names/{id}", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow);
        var kent = Tgn("7011427" + ds.GetHashCode().ToString("x")[..3], "Chatham", "Medway, England, United Kingdom, Europe, World");
        var h = "thh" + Guid.NewGuid().ToString("N")[..8];
        await pg.Store.PutAuthorityAsync(kent);
        await pg.Store.PutAuthorityAsync(Heading(h + "1", "Chatham (Kent)"));
        await pg.Store.PutAuthorityAsync(Heading(h + "2", "Castles"));
        await pg.Store.PutAuthorityAsync(Heading(h + "3", "Wells (England)"));
        await pg.Store.PutAuthorityAsync(Item("Q" + h + "1", "Chatham", "Place", $"http://id.loc.gov/authorities/names/{h}1", kent.Uri));
        await pg.Store.PutAuthorityAsync(Item("Q" + h + "2", "castle", "Type", $"http://id.loc.gov/authorities/names/{h}2"));
        await pg.Store.PutAuthorityAsync(Item("Q" + h + "3", "Wells", "Place", $"http://id.loc.gov/authorities/names/{h}3"));
        await pg.Store.PutAuthorityAsync(Item("Q" + h + "4", "Darlington", "Place", "http://viaf.org/viaf/" + h));
        var chatham = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Chatham (Kent)", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(chatham.Id, Loc(h + "1"));
        var castles = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Castles", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(castles.Id, Loc(h + "2"));
        var wells = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Wells (England)", Status: EntityStatus.Confirmed), scheme);
        await pg.Store.AddEquivalentAsync(wells.Id, Loc(h + "3"));
        var other = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Wells", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(other.Id, Wikidata("Q" + h + "3"));
        var darlington = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Darlington", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(darlington.Id, new Equivalent("http://viaf.org/viaf/" + h, "viaf", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));

        // a dry run writes nothing and says what it would do
        var dry = await new ThroughHeadings(pg.Store).RunAsync(ds, null);
        Assert.Equal((4, 1, 1), (dry.Read, dry.Given, dry.NoHeading));
        Assert.Single((await pg.Store.GetEntityAsync(chatham.Id))!.Equivalents);

        var run = await pg.Store.StartRunAsync(ds);
        var report = await new ThroughHeadings(pg.Store).RunAsync(ds, run.Id);
        Assert.Equal(1, report.Given);
        Assert.Equal((1, 1), (report.AddedBySource["wikidata"], report.AddedBySource["tgn"]));
        var gained = (await pg.Store.GetEntityAsync(chatham.Id))!;
        Assert.Contains(gained.Equivalents, q => q.Uri == kent.Uri && q.RunId == run.Id);
        Assert.Contains(gained.Equivalents, q => q.Source == "wikidata");
        // what is no place, and what another entity of the project holds, is left alone and said
        Assert.Contains(report.LeftAlone, l => l.StartsWith("Castles") && l.Contains("no place"));
        Assert.Contains(report.LeftAlone, l => l.StartsWith("Wells (England)") && l.Contains("one place twice?"));
        Assert.Single((await pg.Store.GetEntityAsync(darlington.Id))!.Equivalents);
        await pg.Store.UndoRunAsync(run.Id);
        Assert.Single((await pg.Store.GetEntityAsync(chatham.Id))!.Equivalents);
    }
}
