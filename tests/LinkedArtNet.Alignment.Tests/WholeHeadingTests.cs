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

    [PostgresFact]
    public async Task The_whole_heading_breaks_a_tie_among_qualified_places()
    {
        // finding 91 (2026-10-24, run rkjzag): "Lincoln (England)" in keywords:head tied between TGN's Lincoln, with LoC's "Lincoln (England)"
        // placed through it, and TGN's Lincolnshire, exact through its variant "Lincoln", with LoC's "Lincolnshire (England)"; the string is
        // LoC's heading as written, so the group that holds that heading is the city. Missouri's Lincoln is vetoed by the resolved qualifier
        var ds = "wh" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Tgn(string id, string label, string parent, string placeType, params string[] variants) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label, .. variants], "Place", null, [],
            $"{{\"parentString\":\"{parent}\",\"placeType\":\"http://vocab.getty.edu/aat/{placeType}\"}}", DateTimeOffset.UtcNow, "test");
        static AuthorityRecord Loc(string id, string label, params string[] variants) => new($"http://id.loc.gov/authorities/names/{id}", "loc", label, [label, .. variants], "Place", null, [], null, DateTimeOffset.UtcNow, "test");
        static AuthorityRecord Aat(string id, string label) => new($"http://vocab.getty.edu/aat/{id}", "aat", label, [label], "Type", null, [], null, DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Aat("300000771", "counties")); await pg.Store.PutAuthorityAsync(Aat("300128207", "nations")); await pg.Store.PutAuthorityAsync(Aat("300008347", "inhabited places"));
        await pg.Store.PutAuthorityAsync(Tgn("7002445", "England", "United Kingdom, Europe, World", "300128207"));
        await pg.Store.PutAuthorityAsync(Tgn("7008157", "Lincolnshire", "England, United Kingdom, Europe, World", "300000771", "Lincoln"));
        await pg.Store.PutAuthorityAsync(Tgn("7010723", "Lincoln", "Lincolnshire, England, United Kingdom, Europe, World", "300008347"));
        await pg.Store.PutAuthorityAsync(Tgn("1002598", "Lincoln", "Missouri, United States, North and Central America, World", "300008347"));
        await pg.Store.PutAuthorityAsync(Loc("n79107974", "Lincolnshire (England)", "Lincoln (England : County)"));
        await pg.Store.PutAuthorityAsync(Loc("n50057360", "Lincoln (England)"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        var lincoln = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:head", "Lincoln (England)"));
        Assert.True(lincoln.Groups.Count(g => g.Sources.Count >= 2 && g.Score >= 0.99) >= 2, string.Join(" | ", lincoln.Groups.Select(g => $"{g.Score} [{string.Join(",", g.Sources)}] {string.Join("; ", g.Members.Select(m => m.Candidate.Label + ": " + m.Reason))}")));
        Assert.Equal(Outcome.Accepted, lincoln.Outcome);
        Assert.Equal(DecisionMethod.Vote, lincoln.AcceptedMethod);
        Assert.Contains("http://vocab.getty.edu/tgn/7010723", lincoln.Accepted!.Uris);
        Assert.Contains("http://id.loc.gov/authorities/names/n50057360", lincoln.Accepted.Uris);
        Assert.Contains(lincoln.Accepted.Members, m => m.Reason.Contains("holds the whole heading as written (Lincoln (England)): breaks the tie"));
        Assert.Contains(lincoln.Groups.SelectMany(g => g.Members), m => m.Candidate.Uri.EndsWith("7008157") && m.Reason.Contains("the whole heading as written is Lincoln (England), elsewhere"));
        Assert.Contains(lincoln.Groups.SelectMany(g => g.Members), m => m.Candidate.Uri.EndsWith("1002598") && m.Veto);

        // a bare "Lincoln" has no whole heading to hold: the tie stands (and prominence, with no Wikidata items here, does not separate them)
        var bare = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords:sub", "Lincoln"));
        Assert.NotEqual(Outcome.Accepted, bare.Outcome);
    }
}
