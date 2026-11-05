using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// Finding 106 (2026-11-05, measured over awn284 and evhvuw): a firm project entity whose placement the string's qualifier contradicts, short
/// of finding 66's veto, had answered silently in the store stage and carried a vote in the index stage.
/// </summary>
[Collection("postgres")]
public class Finding106Tests(PostgresFixture pg)
{
    private static AuthorityRecord Aat(string id, string label) => new($"http://vocab.getty.edu/aat/{id}", "aat", label, [label], "Type", null, [], null, DateTimeOffset.UtcNow, "test");
    private static AuthorityRecord Tgn(string id, string label, string parent, string placeType) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label], "Place", null, [],
        $"{{\"parentString\":\"{parent}\",\"placeType\":\"http://vocab.getty.edu/aat/{placeType}\"}}", DateTimeOffset.UtcNow, "test");
    private static AuthorityRecord Heading(string source, string uri, string label, string type) => new(uri, source, label, [label, Qualified.Split(label).Name], type, null, [], null, DateTimeOffset.UtcNow, "test");

    [PostgresFact]
    public async Task A_resolved_qualifier_the_entitys_placement_contradicts_is_neither_matched_silently_nor_voted()
    {
        // "Red House (Yorkshire)" and "Red House (Bexleyheath, Kent)" were matched silently to the project's "Red House" placed at Aldeburgh,
        // England (Britten's), made the same run from "Red House (Aldeburgh, England)"; Aldeburgh is a town, so not "elsewhere" under finding
        // 90, and England is inside Yorkshire's chain. The walk resolves "Yorkshire"; the entity's placement contradicts it; the store stage
        // passes the entity over, and the index stage, where the entity is grouped with its own heading, does not vote on its strength alone
        var ds = "f106a" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(Aat("300000771", "counties"));
        await pg.Store.PutAuthorityAsync(Tgn("7008591", "Yorkshire", "England, United Kingdom, Europe, World", "300000771"));
        const string aldeburgh = "http://id.loc.gov/authorities/names/n2003091234";
        await pg.Store.PutAuthorityAsync(Heading("loc", aldeburgh, "Red House (Aldeburgh, England)", "Group"));
        await pg.Store.PutAuthorityAsync(Heading("loc", "http://id.loc.gov/authorities/names/n92019935", "Red House (Musical group)", "Group"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500304730", "ulan", "Red House", ["Red House"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        var britten = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Red House", ["Red House", "Red House (Aldeburgh, England)"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(britten.Id, new Equivalent(aldeburgh, "loc", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc"), new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());

        var yorkshire = await pg.Store.CollectAsync(ds, "keywords", "Red House (Yorkshire)");
        var e = await reconciler.EvaluateAsync(ds, yorkshire, includeStore: true);
        Assert.Equal("England, United Kingdom, Europe, World", e.Reading.QualifierAncestors);
        Assert.Equal(Outcome.Queued, e.Outcome);
        var entityMember = Assert.Single(e.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == britten.Id); // shown on the desk, with the reason
        Assert.Contains("the qualifier names a known place and the project's entity is placed elsewhere: not carried alone (finding 106)", entityMember.Reason);
        Assert.Equal(false, entityMember.QualifierAgrees);

        // the boundary kept from 2026-10-24: a qualifier the walk resolves to nothing ("Leeds" is a town) leaves the silent match as it was,
        // and the project's "Harewood House (England)" still answers "Harewood House (Leeds)"
        const string harewoodLoc = "http://id.loc.gov/authorities/names/n85000001";
        await pg.Store.PutAuthorityAsync(Heading("loc", harewoodLoc, "Harewood House (England)", "Group"));
        var harewood = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Harewood House (England)", ["Harewood House (England)", "Harewood House"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(harewood.Id, new Equivalent(harewoodLoc, "loc", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var leeds = await pg.Store.CollectAsync(ds, "keywords", "Harewood House (Leeds)");
        var e2 = await reconciler.EvaluateAsync(ds, leeds, includeStore: true);
        Assert.Null(e2.Reading.QualifierAncestors);
        Assert.Equal(Outcome.MatchedStore, e2.Outcome);
        Assert.Equal(harewood.Id, e2.StoreMatch!.Id);
    }

    [PostgresFact]
    public async Task A_level_word_against_an_entity_of_another_level_is_not_matched_silently()
    {
        // "Hannover (Province)" was matched silently to the project's "Hannover", aligned to TGN's Hannover in North Dakota, an inhabited
        // place; "New York (State)" to the entity whose chain is the city's. A level word resolves to no place, so the resolved-qualifier
        // form cannot see it; the entity's own TGN record says its level
        var ds = "f106b" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(Aat("300008347", "inhabited places")); await pg.Store.PutAuthorityAsync(Aat("300000774", "provinces"));
        await pg.Store.PutAuthorityAsync(Tgn("2029873", "Hannover", "Oliver, North Dakota, United States, North and Central America, World", "300008347"));
        await pg.Store.PutAuthorityAsync(Tgn("7003474", "Hannover", "Niedersachsen, Deutschland, Europe, World", "300000774"));
        var dakota = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Place, "Hannover", ["Hannover"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(dakota.Id, new Equivalent("http://vocab.getty.edu/tgn/2029873", "tgn", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn")], ProjectManifest.PmcDefault.ToOptions());

        var province = await pg.Store.CollectAsync(ds, "keywords", "Hannover (Province)");
        var e = await reconciler.EvaluateAsync(ds, province, includeStore: true);
        Assert.Null(e.Reading.QualifierAncestors);
        Assert.Equal(Outcome.Queued, e.Outcome); // the two TGN records are the desk's question; the entity no longer answers for either
        var entityMember = Assert.Single(e.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == dakota.Id);
        Assert.Equal(2, entityMember.Candidate.Level);
        Assert.Contains("the qualifier names a level (state or province) and the project's entity is at another (inhabited place): not carried alone (finding 106)", entityMember.Reason);
        // an entity of the level named still answers silently
        var city = await pg.Store.CollectAsync(ds, "keywords", "Hannover (City)");
        var e2 = await reconciler.EvaluateAsync(ds, city, includeStore: true);
        Assert.Equal(Outcome.MatchedStore, e2.Outcome);
        Assert.Equal(dakota.Id, e2.StoreMatch!.Id);
    }

    [PostgresFact]
    public async Task A_name_of_one_letter_matches_nothing()
    {
        // "l" in the place field was accepted as an exact single onto TGN's one "L", a locality in Idaho, and the entity so made then carried
        // "L (colonnade)" in the second pass
        var ds = "f106c" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(Tgn("2443265", "L", "Idaho, Idaho, United States, North and Central America, World", "300008347"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn")], ProjectManifest.PmcDefault.ToOptions());
        var letter = await pg.Store.CollectAsync(ds, "place", "l");
        var colonnade = await pg.Store.CollectAsync(ds, "place", "L (colonnade)");
        var report = await reconciler.RunAsync(ds, runId: "run-test-one-letter");
        Assert.Equal(2, report.Queued); Assert.Equal(0, report.Accepted); Assert.Equal(0, report.Settled);
        foreach (var s in new[] { letter, colonnade })
        {
            var decision = (await pg.Store.DecisionForAsync(s.Id))!;
            Assert.Equal(DecisionMethod.Queued, decision.Method);
            Assert.Contains("a name of one letter is not a name", decision.EvidenceJson);
        }
        Assert.Empty(await pg.Store.FindEntitiesByLabelAsync("l", ds, EntityClass.Place).ContinueWith(t => t.Result.Where(x => x.Status != EntityStatus.Provisional).ToList()));
    }
}
