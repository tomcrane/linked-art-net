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
    public async Task A_bare_name_on_a_place_labelled_as_another_is_listed_with_the_better_known_place()
    {
        // the owner, 2026-11-24: "Milano" in 100 records stood on the import's Milano (Tex.). The first check lists it beside the
        // best-known place of the name, and its Decide screen is offered that place as it is offered a place at home
        var x = "z" + string.Concat(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(7));
        var ds = "ow" + x;
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Counted(string q, string label, int sitelinks, string descr, params string[] sameAs) =>
            new($"http://www.wikidata.org/entity/{q}", "wikidata", label, [label], "Place", null, sameAs, $"{{\"sitelinks\":{sitelinks},\"description\":\"{descr}\"}}", DateTimeOffset.UtcNow, "test");
        var lombardy = Tgn("9" + x, "Mil" + x, "Lombardy, Italy, Europe, World");
        await pg.Store.PutAuthorityAsync(lombardy);
        await pg.Store.PutAuthorityAsync(Counted("Q1" + x, "Mil" + x, 31, "city in Milam County, Texas, United States"));
        await pg.Store.PutAuthorityAsync(Counted("Q2" + x, "Mil" + x, 226, "Italian comune", lombardy.Uri));
        var texas = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, $"Mil{x} (Tex.)", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(texas.Id, new Equivalent("http://www.wikidata.org/entity/Q1" + x, "wikidata", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var bare = await pg.Store.CollectAsync(ds, "place", "Mil" + x);
        await pg.Store.DecideAsync(bare.Id, texas.Id, DecisionMethod.Imported, null, "tester");
        var curation = new CurationService(pg.Store, scheme, new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions()));

        var listed = Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"));
        Assert.True(listed.Outweighed); Assert.False(listed.Likely);
        Assert.Equal((texas.Id, $"Mil{x} (Tex.)", lombardy.Uri, 226, 31), (listed.Row.EntityId, listed.Row.EntityLabel, listed.Row.HomeUri, listed.Row.HomeSitelinks, listed.Row.ItemSitelinks));
        var plan = (await curation.MendPlanAsync(ds, texas.Id, "United Kingdom"))!;
        Assert.Equal(lombardy.Uri, Assert.Single(plan.Homes).HomeUri);
        // mended whole to the better-known place: the identifiers named go, and the label that said the wrong place goes with them
        var (mended, said) = await curation.ApplyAsync(ds, texas.Id, [bare.Id], lombardy.Uri, ["http://www.wikidata.org/entity/Q1" + x], null, null, "tester");
        Assert.Equal(("Mil" + x, texas.Id), (mended.Label, mended.Id));
        Assert.Equal(lombardy.Uri, Assert.Single(mended.Equivalents).Uri);
        Assert.Contains("1 identifier removed", said);
        Assert.Empty(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"));
        await pg.Store.AddEquivalentAsync(texas.Id, new Equivalent("http://www.wikidata.org/entity/Q1" + x, "wikidata", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        await curation.SetLabelAsync(ds, texas.Id, $"Mil{x} (Tex.)", "tester");
        // the other kind: the same place under another record. "West Sussex (England)" held a military unit's item; the row says so,
        // and the item is replaced by the better-known record, the library's heading and the label kept
        var county = Tgn("8" + x, "Wes" + x, "England, United Kingdom, Europe, World");
        await pg.Store.PutAuthorityAsync(county);
        await pg.Store.PutAuthorityAsync(Counted("Q3" + x, "Wes" + x, 1, "military unit"));
        await pg.Store.PutAuthorityAsync(Counted("Q4" + x, "Wes" + x, 82, "ceremonial county of England", county.Uri));
        var sussex = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, $"Wes{x} (England)", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(sussex.Id, new Equivalent("http://www.wikidata.org/entity/Q3" + x, "wikidata", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        await pg.Store.AddEquivalentAsync(sussex.Id, new Equivalent("http://id.loc.gov/authorities/names/n" + x, "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "place", "Wes" + x)).Id, sussex.Id, DecisionMethod.Imported, null, "tester");
        var same = Assert.Single(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == sussex.Id);
        Assert.True(same.SamePlace); Assert.Contains("its Wikidata item is military unit", same.Reason);
        var choices = (await curation.MendPlanAsync(ds, sussex.Id, "United Kingdom"))!;
        Assert.NotNull(choices.Swap);
        // the person is shown the records of the name to choose among, the most written-about first, and the gazetteer's of the name
        Assert.Equal("http://www.wikidata.org/entity/Q4" + x, choices.SwapItems[0].Record.Uri);
        Assert.Contains("Wes" + x, choices.SwapItems[0].Gazetteer);
        Assert.Equal(county.Uri, Assert.Single(choices.SwapGazetteer).Uri);
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.ReplaceItemAsync(ds, sussex.Id, "tester", county.Uri));
        var (swapped, did) = await curation.ReplaceItemAsync(ds, sussex.Id, "tester");
        Assert.Equal($"Wes{x} (England)", swapped.Label);
        Assert.Equal(new[] { "http://id.loc.gov/authorities/names/n" + x, "http://www.wikidata.org/entity/Q4" + x, county.Uri }.Order(), swapped.Equivalents.Select(q => q.Uri).Order());
        Assert.Contains("replaced by the record chosen", did);
        Assert.DoesNotContain(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"), f => f.Row.EntityId == sussex.Id);
        // a label that names a division is the same place too, and is not offered the replacement: it is the division, mended whole
        Assert.Equal((true, "District"), CurationService.SamePlace(new AwayRow("e", "Chichester", "aligned", 1, "i", "district", 17, "h", "West Sussex, England, United Kingdom, Europe, World", 78, "hi") { EntityLabel = "Chichester (England : District)" }));
        Assert.Equal((true, (string?)null), CurationService.SamePlace(new AwayRow("e", "County Wicklow", "aligned", 1, "i", "municipal district", 0, "h", "county in Ireland", 66, "hi", false, "county in Ireland") { EntityLabel = "Wicklow (Ireland : County)" }));
        Assert.False(CurationService.SamePlace(new AwayRow("e", "Milan", "aligned", 1, "i", "city in Texas", 31, "h", "Lombardy, Italy, Europe, World", 226, "hi") { EntityLabel = "Milano (Tex.)" }).Same);
        // a string that says where it is does not list the entity, and nor does a person's word for it
        var qualified = await pg.Store.CollectAsync(ds, "place", $"Mil{x}, Texas");
        await pg.Store.DecideAsync(qualified.Id, texas.Id, DecisionMethod.Imported, null, "tester");
        await pg.Store.DecideAsync(bare.Id, texas.Id, DecisionMethod.Human, null, "tester");
        Assert.Empty(await curation.PlacesAlignedAwayAsync(ds, "United Kingdom"));
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

        // the fourth check lists the thin places whose name the gazetteer has, and offers the one record of the name; a place a person
        // has said is in no gazetteer leaves the list
        var curation = new CurationService(pg.Store, scheme, new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions()));
        var thin = await curation.ThinPlacesAsync(ds, "United Kingdom");
        var listed = Assert.Single(thin);
        Assert.Equal((chatham.Id, kent.Uri, 1), (listed.Row.EntityId, listed.Offer?.Uri, listed.AtHome));
        Assert.Null(listed.Holder);
        Assert.Equal("Chatham", listed.Name);
        await curation.NoteAsync(ds, chatham.Id, "tester", CurationService.NoGazetteerRecord + ": a test");
        Assert.Empty(await curation.ThinPlacesAsync(ds, "United Kingdom"));

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
