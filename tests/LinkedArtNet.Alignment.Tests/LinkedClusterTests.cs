using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class LinkedClusterTests(PostgresFixture pg)
{
    private static AuthorityRecord Person(string uri, string source, string label, Lifespan? lifespan, params string[] sameAs) => new(uri, source, label, [label, Qualified.Split(label).Name], "Person", lifespan, sameAs, null, DateTimeOffset.UtcNow, "test");

    [PostgresFact]
    public async Task A_linked_cluster_that_disagrees_with_itself_stands_when_one_record_agrees_with_the_string()
    {
        // finding 96 (2026-10-24 night, run wy2aqs; the owner's policy): about ninety of finding 93's strings were queued because a linked
        // record's dates conflicted while another record of the cluster agreed, and a vetoed linked member poisons its group (finding 44)
        var ds = "lc" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        // Agar: LoC's heading is the string verbatim and agrees; ULAN's linked record says 1904-1991
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/nr89016548", "loc", "Agar, Eileen, 1899-1991", ["Agar, Eileen, 1899-1991", "Agar, Eileen"], "Person", GettyVocabLoader.LifespanFromYears("1899", "1991"), [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(Person("http://vocab.getty.edu/ulan/500009712", "ulan", "Agar, Eileen", GettyVocabLoader.LifespanFromYears("1904", "1991"), "http://id.loc.gov/authorities/names/nr89016548"));
        var agar = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Agar, Eileen, 1899-1991"));
        Assert.Equal(Outcome.Accepted, agar.Outcome);
        Assert.Equal(DecisionMethod.Single, agar.AcceptedMethod);
        Assert.Contains("http://id.loc.gov/authorities/names/nr89016548", agar.Accepted!.Uris);
        Assert.Contains("http://vocab.getty.edu/ulan/500009712", agar.Accepted.Uris); // the cluster stands: both records join
        var ulanAgar = Assert.Single(agar.Accepted.Members, m => m.Candidate.Source == "ulan");
        Assert.False(ulanAgar.Veto); Assert.True(ulanAgar.SetAside); Assert.True(ulanAgar.DatesConflict);
        Assert.Contains("linked to loc's record, which agrees with the string: the authorities disagree among themselves, and the cluster stands", ulanAgar.Reason);

        // Kelly: ULAN agrees; LoC's linked record carries a bare birth year a year off (finding 88's)
        await pg.Store.PutAuthorityAsync(Person("http://vocab.getty.edu/ulan/500001878", "ulan", "Kelly, Felix", GettyVocabLoader.LifespanFromYears("1917", "1994"), "http://id.loc.gov/authorities/names/n50048143"));
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/n50048143", "loc", "Kelly, Felix", GettyVocabLoader.LifespanFromYears("1916", null)));
        var kelly = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Kelly, Felix, 1917-1994"));
        Assert.Equal(Outcome.Accepted, kelly.Outcome);
        Assert.Equal(DecisionMethod.Single, kelly.AcceptedMethod);
        Assert.Contains("http://vocab.getty.edu/ulan/500001878", kelly.Accepted!.Uris);
        Assert.Contains(kelly.Accepted.Members, m => m.Candidate.Source == "loc" && m.SetAside && !m.Veto);

        // no record of the cluster agrees: the conflict stands, and the string is the desk's (finding 93), not accepted
        await pg.Store.PutAuthorityAsync(Person("http://vocab.getty.edu/ulan/500900101", "ulan", "Nobody, Ann", GettyVocabLoader.LifespanFromYears("1800", "1860"), "http://id.loc.gov/authorities/names/n99900101"));
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/n99900101", "loc", "Nobody, Ann", GettyVocabLoader.LifespanFromYears("1800", "1861")));
        var nobody = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Nobody, Ann, 1900-1950"));
        Assert.Equal(Outcome.Queued, nobody.Outcome);
        Assert.All(nobody.Groups.SelectMany(g => g.Members), m => Assert.True(m.Veto));
    }
}
