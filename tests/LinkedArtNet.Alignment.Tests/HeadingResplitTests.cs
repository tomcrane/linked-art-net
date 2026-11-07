using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>Finding 112 (2026-11-07): the hyphen inside a heading, doubled by the 2024 cleaning and split as a separator.</summary>
[Collection("postgres")]
public class HeadingResplitTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_hyphen_inside_a_word_or_a_known_name_is_not_a_separator()
    {
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/subjects/sh85106186", "lcsh", "Pre-Raphaelitism", ["Pre-Raphaelitism"], "Type", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n79041977", "loc", "New-York Historical Society", ["New-York Historical Society"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n85076990", "loc", "Kit-Cat Club (London, England)", ["Kit-Cat Club (London, England)", "Kit-Cat Club"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        var resplit = new HeadingResplit(pg.Store);

        async Task Is(string heading, string rule, params string[] parts)
        {
            var (got, r) = await resplit.SplitAsync(heading);
            Assert.Equal(parts, got);
            Assert.Equal(rule, r);
        }
        // (a) lower-case after the separator is a hyphen, except the catalogue's lower-case chronological subdivision
        await Is("Wood--carving--England--Devon", "a", "Wood-carving", "England", "Devon");
        await Is("Still--life painting--20th century", "a", "Still-life painting", "20th century");
        await Is("All Hallows by--the--Tower (Church : London, England)", "a", "All Hallows by-the-Tower (Church : London, England)");
        await Is("Barton--upon--Humber (England)--Antiquities", "a", "Barton-upon-Humber (England)", "Antiquities");
        await Is("Architecture, Domestic--England--to 1500", "", "Architecture, Domestic", "England", "to 1500");
        await Is("Art--Bio--bibliography", "a", "Art", "Bio-bibliography");
        // a bound prefix joins the next part (the splitter's own rule, widened)
        await Is("Anglo--Saxons--Exhibitions", "a", "Anglo-Saxons", "Exhibitions");
        await Is("Counter--Reformation--England", "a", "Counter-Reformation", "England");
        await Is("Pre--Raphaelitism--England--Exhibitions", "a", "Pre-Raphaelitism", "England", "Exhibitions");
        // (b) a one-word head whose join is a label an index holds with the hyphen; a plain heading stands
        await Is("New--York Historical Society (New York, New York, USA)", "b", "New-York Historical Society (New York, New York, USA)");
        await Is("Kit--Cat Club (London)--Portraits", "b", "Kit-Cat Club (London)", "Portraits");
        await Is("Art--16th century--England", "", "Art", "16th century", "England");
        await Is("Drury--Lowe family", "", "Drury", "Lowe family"); // a surname the indexes do not hold: the split stands, for the desk

        // the dry run over a dataset: a heading split today stands; one whose parts were made by an older split (the store's, from
        // the first runs before finding 4) is listed with its parts as stored and as today's rules read it
        var ds = "rs-" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "lcsh"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        await pg.Store.CollectAsync(ds, "keywords", "Art--16th century--England");
        await reconciler.RunAsync(ds);
        var stale = await pg.Store.CollectAsync(ds, "keywords", "Wood--carving--England");
        var wood = await pg.Store.CollectAsync(ds, "keywords:head", "Wood");
        var carving = await pg.Store.CollectAsync(ds, "keywords:sub", "carving");
        var england = await pg.Store.CollectAsync(ds, "keywords:sub", "England");
        await pg.Store.LinkPartAsync(stale.Id, 0, wood.Id, "head"); await pg.Store.LinkPartAsync(stale.Id, 1, carving.Id, "sub"); await pg.Store.LinkPartAsync(stale.Id, 2, england.Id, "sub");
        var concept = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Type, "Wood--carving--England", ["Wood--carving--England"]), scheme);
        await pg.Store.DecideAsync(stale.Id, concept.Id, DecisionMethod.Composite);
        var report = await resplit.DryRunAsync(ds);
        Assert.Equal(2, report.Read);
        var change = Assert.Single(report.Changes);
        Assert.Equal(stale.Id, change.Heading.Id);
        Assert.Equal(["Wood", "carving", "England"], change.OldParts);
        Assert.Equal(["Wood-carving", "England"], change.NewParts);
        Assert.Equal("a", change.Rule);
    }
}
