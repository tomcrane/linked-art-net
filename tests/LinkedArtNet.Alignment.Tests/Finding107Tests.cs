using System.Text.Json;
using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// The three gaps run v6gjx5 showed (2026-11-05, doc 10 7.6 "Measured 2026-11-05"): the bridged vote (finding 107), finding 105's
/// lookalike counted as a rival, and the evidence's cap cutting a group that holds a project entity.
/// </summary>
[Collection("postgres")]
public class Finding107Tests(PostgresFixture pg)
{
    private static AuthorityRecord Aat(string id, string label) => new($"http://vocab.getty.edu/aat/{id}", "aat", label, [label], "Type", null, [], null, DateTimeOffset.UtcNow, "test");
    private static AuthorityRecord Tgn(string id, string label, string parent, string placeType) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label], "Place", null, [],
        $"{{\"parentString\":\"{parent}\",\"placeType\":\"http://vocab.getty.edu/aat/{placeType}\"}}", DateTimeOffset.UtcNow, "test");
    private static AuthorityRecord Heading(string source, string uri, string label, string type) => new(uri, source, label, [label, Qualified.Split(label).Name], type, null, [], null, DateTimeOffset.UtcNow, "test");

    [PostgresFact]
    public async Task Two_sources_joined_only_through_a_contradicted_entity_do_not_carry_a_vote()
    {
        // finding 107: "Red House (Yorkshire)" and "Red House (Bexleyheath, Kent)", queued by finding 106 in v6gjx5's first pass, were voted
        // onto the project's Red House at Aldeburgh in the second: the entity's own LoC heading and ULAN record, each found by name, were
        // grouped through the entity as two independent sources, and CarriedBy read the two sources before it read the contradiction.
        // Here the entity's two records are found by name and nothing else of the name is in the index, so without the clause the group
        // carries as a vote
        var ds = "f107a" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(Aat("300000771", "counties"));
        await pg.Store.PutAuthorityAsync(Tgn("7008591", "Yorkshire", "England, United Kingdom, Europe, World", "300000771"));
        const string aldeburgh = "http://id.loc.gov/authorities/names/n2003091234";
        const string ulan = "http://vocab.getty.edu/ulan/500304730";
        await pg.Store.PutAuthorityAsync(Heading("loc", aldeburgh, "Red House (Aldeburgh, England)", "Group"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(ulan, "ulan", "Red House", ["Red House"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        var britten = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Red House", ["Red House", "Red House (Aldeburgh, England)"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(britten.Id, new Equivalent(aldeburgh, "loc", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        await pg.Store.AddEquivalentAsync(britten.Id, new Equivalent(ulan, "ulan", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn"), new LocalIndexSource(pg.Store, "loc"), new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());

        var yorkshire = await pg.Store.CollectAsync(ds, "keywords", "Red House (Yorkshire)");
        var e = await reconciler.EvaluateAsync(ds, yorkshire, includeStore: true);
        Assert.Equal("England, United Kingdom, Europe, World", e.Reading.QualifierAncestors);
        var group = Assert.Single(e.Groups, g => g.Members.Any(m => m.Candidate.EntityId == britten.Id));
        Assert.True(group.Sources.Count >= 2, string.Join(", ", group.Sources)); // the entity's own two records, joined by nothing but the entity
        Assert.DoesNotContain(group.Members, m => m.QualifierAgrees == true); // and nothing in the group the qualifier places
        Assert.Equal(Outcome.Queued, e.Outcome);
        Assert.Contains(group.Members, m => m.Candidate.EntityId == britten.Id && m.Reason.Contains("the qualifier names a known place and the project's entity is placed elsewhere: not carried alone (finding 106)"));
    }

    [PostgresFact]
    public async Task A_possessive_lookalike_is_no_rival()
    {
        // finding 105's lookalike, shown for the desk and never carried, had been counted as a rival (run v6gjx5): "Landmark" lost its
        // exact-single to ULAN's "Landmarks" at 0.90, "Chartwell (Kent)" its single to LoC's "Chartwells (Firm)", "Still" its vote to the
        // project's "Stills (Gallery)" and "Fleming" its vote to the project's Robert Fleming Holdings, "Flemings" among its labels. The
        // lookalike is no rival for the single's second group, the exact-single's count of groups or the outvoted policy's rivals: here
        // "Remondini" is an exact-single on LoC's one "Remondini (Firm)" (finding 42) beside the project's own "Remondinis Ltd"
        var ds = "f107b" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n50064239", "loc", "Remondini (Firm)", ["Remondini (Firm)"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        var firm = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Remondinis Ltd", ["Remondinis Ltd"], Status: EntityStatus.Aligned), scheme);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "loc"), new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());
        var remondini = await pg.Store.CollectAsync(ds, "corpauthor", "Remondini");
        var e = await reconciler.EvaluateAsync(ds, remondini, includeStore: true);
        var lookalike = Assert.Single(e.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == firm.Id); // still shown, with the reason
        Assert.True(lookalike.Candidate.ByPossessive);
        Assert.Equal(0.90, lookalike.Candidate.NameScore);
        Assert.Equal(Outcome.Accepted, e.Outcome);
        Assert.Equal(DecisionMethod.ExactSingle, e.AcceptedMethod);
        Assert.Contains(e.Accepted!.Uris, u => u.EndsWith("n50064239"));
    }

    [Fact]
    public void The_evidence_never_cuts_a_group_holding_a_project_entity()
    {
        // "Fleming" (keywords:head, run v6gjx5): twelve namesakes at 1.00 filled the evidence's cap, and the thirteenth group, the project's
        // own Robert Fleming Holdings at 0.90, the lookalike that stopped the outvoted policy, was cut; the reason a string was queued must be
        // in its evidence (finding 92)
        var now = DateTimeOffset.UtcNow;
        var s = new SourceString(1, "t", "keywords:head", "Fleming", "fleming", null, now, now, 1);
        var reading = new StringReading(s, "Fleming", ["fleming"], [EntityClass.Type, EntityClass.Person, EntityClass.Group, EntityClass.Place], null, [], [], null);
        static CandidateGroup Group(string uri, string source, double score, string? entityId = null)
        {
            var c = new Candidate(uri, source, "Fleming", EntityClass.Place, null, [], "local", score, entityId);
            return new CandidateGroup([new ScoredCandidate(c, score, false, $"name {score:0.00}")], score, [source], [uri]);
        }
        var groups = Enumerable.Range(1, 12).Select(i => Group($"http://vocab.getty.edu/tgn/{i}", "tgn", 1.0)).ToList();
        const string entity = "https://data.example.org/t/group/sssxdnrx";
        groups.Add(Group(entity, "store", 0.9, entity));
        using var doc = JsonDocument.Parse(Reconciler.Evidence("queued", reading, groups, null));
        var shown = doc.RootElement.GetProperty("groups").EnumerateArray().Select(g => g.GetProperty("members")[0].GetProperty("uri").GetString()).ToList();
        Assert.Equal(12, shown.Count);
        Assert.Contains(entity, shown);
        Assert.Contains("http://vocab.getty.edu/tgn/11", shown);
        Assert.DoesNotContain("http://vocab.getty.edu/tgn/12", shown); // the twelfth namesake gives way, not the entity
    }
}
