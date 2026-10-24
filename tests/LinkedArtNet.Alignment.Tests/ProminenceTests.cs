using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class ProminenceTests(PostgresFixture pg)
{
    private static AuthorityRecord Tgn(string id, string label, string parent) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label], "Place", null, [], $"{{\"parentString\":\"{parent}\"}}", DateTimeOffset.UtcNow, "test");
    private static AuthorityRecord Loc(string id, string label) => new($"http://id.loc.gov/authorities/names/{id}", "loc", label, [label], "Place", null, [], null, DateTimeOffset.UtcNow, "test");
    private static AuthorityRecord Wikidata(string q, string label, int? sitelinks, params string[] sameAs) =>
        new($"http://www.wikidata.org/entity/{q}", "wikidata", label, [label], "Place", null, sameAs, sitelinks is null ? "{\"ids\":{}}" : $"{{\"ids\":{{}},\"sitelinks\":{sitelinks}}}", DateTimeOffset.UtcNow, "test");

    [PostgresFact]
    public async Task Prominence_breaks_the_famous_name_tie_among_places_and_declines_a_close_one()
    {
        // finding 85 (2026-10-24): "Sheffield" in keywords:sub ties between the English city with LoC's heading and Alabama's with its own;
        // Wikidata's items, reached through the TGN and LoC ids in their sameAs links, say 122 sitelinks against 45, and the English one is
        // taken. "Murray" ties the same way at 45 against 44 and is declined. The Wikidata index is not among the reconciler's sources
        var ds = "pr" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(Tgn("7010692", "Sheffield", "South Yorkshire, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Loc("n79072766", "Sheffield (England)"));
        await pg.Store.PutAuthorityAsync(Tgn("2030130", "Sheffield", "Colbert, Alabama, United States, North and Central America, World"));
        await pg.Store.PutAuthorityAsync(Loc("n81138429", "Sheffield (Ala.)"));
        await pg.Store.PutAuthorityAsync(Wikidata("Q42448", "Sheffield", 122, "http://vocab.getty.edu/tgn/7010692", "http://id.loc.gov/authorities/names/n79072766"));
        await pg.Store.PutAuthorityAsync(Wikidata("Q79568", "Sheffield", 45, "http://id.loc.gov/authorities/names/n81138429"));
        await pg.Store.PutAuthorityAsync(Tgn("2040431", "Murray", "Calloway, Kentucky, United States, North and Central America, World"));
        await pg.Store.PutAuthorityAsync(Loc("n50038635", "Murray (Ky.)"));
        await pg.Store.PutAuthorityAsync(Tgn("2109103", "Murray", "Salt Lake, Utah, United States, North and Central America, World"));
        await pg.Store.PutAuthorityAsync(Loc("n82157490", "Murray (Utah)"));
        await pg.Store.PutAuthorityAsync(Wikidata("Q984364", "Murray", 45, "http://vocab.getty.edu/tgn/2040431"));
        await pg.Store.PutAuthorityAsync(Wikidata("Q482822", "Murray", 44, "http://vocab.getty.edu/tgn/2109103"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        var sheffield = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:sub", "Sheffield"));
        Assert.Equal(Outcome.Accepted, sheffield.Outcome);
        Assert.Equal(DecisionMethod.Vote, sheffield.AcceptedMethod);
        Assert.Contains("http://vocab.getty.edu/tgn/7010692", sheffield.Accepted!.Uris);
        Assert.Contains("the prominent one: 122 sitelinks (http://www.wikidata.org/entity/Q42448) against 45", sheffield.Accepted.Best.Reason);
        Assert.Contains(sheffield.Groups.SelectMany(g => g.Members), m => m.Reason.Contains("less prominent: 45 sitelinks against 122"));

        var murray = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:sub", "Murray"));
        Assert.Equal(Outcome.Queued, murray.Outcome);
        Assert.Contains(murray.Groups.SelectMany(g => g.Members), m => m.Reason.Contains("prominence does not separate them (45, 44 sitelinks)"));

        // a tie is among groups at the top score (finding 86): the German Münster, exact at 1.00 with LoC's heading, is not broken towards
        // the project's "Munster (Ireland)" at 0.97 through its folded umlaut, by the project's entity (finding 35) or by prominence
        await pg.Store.PutAuthorityAsync(Tgn("7004333", "Münster", "Nordrhein-Westfalen, Germany, Europe, World"));
        await pg.Store.PutAuthorityAsync(Loc("n79043433", "Münster (Germany)"));
        await pg.Store.PutAuthorityAsync(Tgn("1127460", "Munster", "Ireland, Europe, World"));
        await pg.Store.PutAuthorityAsync(Loc("n79047888", "Munster (Ireland)"));
        await pg.Store.PutAuthorityAsync(Wikidata("Q245546", "Munster", 90, "http://vocab.getty.edu/tgn/1127460"));
        var irish = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Munster (Ireland)", ["Munster (Ireland)", "Munster"], null, [], [], EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(irish.Id, new Equivalent("http://vocab.getty.edu/tgn/1127460", "tgn", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var munster = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "place", "Münster"));
        Assert.True(munster.Outcome != Outcome.Accepted || munster.Accepted!.Uris.Contains("http://vocab.getty.edu/tgn/7004333"), $"{munster.Outcome} {string.Join(",", munster.Accepted?.Uris ?? [])}");

        // prominence checks the project's entity before it breaks a tie (finding 89): the legacy "Orkney (Sask.)" sits in the Saskatchewan group,
        // but Scotland's Orkney is the prominent one by prominence's own margins, so "Orkney" is the desk's rather than Saskatchewan's
        await pg.Store.PutAuthorityAsync(Tgn("1016052", "Orkney", "Saskatchewan, Canada, North and Central America, World"));
        await pg.Store.PutAuthorityAsync(Loc("n83197128", "Orkney (Sask.)"));
        await pg.Store.PutAuthorityAsync(Tgn("7009907", "Orkney", "Scotland, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Loc("n81071458", "Orkney (Scotland)"));
        await pg.Store.PutAuthorityAsync(Wikidata("Q100166", "Orkney", 150, "http://vocab.getty.edu/tgn/7009907", "http://id.loc.gov/authorities/names/n81071458"));
        await pg.Store.PutAuthorityAsync(Wikidata("Q7103527", "Orkney", 5, "http://id.loc.gov/authorities/names/n83197128"));
        var sask = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Orkney (Sask.)", ["Orkney (Sask.)"], null, [], [], EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(sask.Id, new Equivalent("http://id.loc.gov/authorities/names/n83197128", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var orkney = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:sub", "Orkney"));
        Assert.Equal(Outcome.Queued, orkney.Outcome);
        Assert.Contains(orkney.Groups.SelectMany(g => g.Members), m => m.Reason.Contains("the project's own entity is the less prominent: 5 sitelinks against 150"));

        // the rule off: the tie stands
        var off = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions() with { ProminenceMinSitelinks = 0 });
        Assert.Equal(Outcome.Queued, (await off.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:sub", "Sheffield"))).Outcome);
    }
}
