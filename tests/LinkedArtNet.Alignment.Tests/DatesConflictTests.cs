using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class DatesConflictTests(PostgresFixture pg)
{
    private static AuthorityRecord Person(string uri, string source, string label, Lifespan? lifespan) => new(uri, source, label, [label], "Person", lifespan, [], null, DateTimeOffset.UtcNow, "test");

    [PostgresFact]
    public async Task A_namesake_vetoed_on_its_dates_alone_is_queued_not_minted()
    {
        // finding 93 (2026-10-24, run bp8unt): finding 88 dated 354,991 LoC person records from Wikidata, and seventeen accepted strings were
        // minted on the conflicts that surfaced: the project's own "Hadid, Zaha" against "Hadid, Zaha, 1950-2006" (the catalogue's death year
        // is wrong), LoC's one "Broderick, Laurence" against "Broderick, Laurence, 1953-" (born 1935). Both are the desk's, never a new entity
        var ds = "dc" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        // the project's own entity, undated itself, takes its dates from its LoC equivalent (StoreCandidateAsync), and they conflict
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/n83825289", "loc", "Hadid, Zaha", GettyVocabLoader.LifespanFromYears("1950", "2016")));
        await pg.Store.PutAuthorityAsync(Person("http://vocab.getty.edu/ulan/500018455", "ulan", "Hadid, Zaha", GettyVocabLoader.LifespanFromYears("1950", "2016")));
        var hadid = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Hadid, Zaha", ["Hadid, Zaha"], null, [], [], EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(hadid.Id, new Equivalent("http://id.loc.gov/authorities/names/n83825289", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var wrongDeath = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Hadid, Zaha, 1950-2006"));
        Assert.Equal(Outcome.Queued, wrongDeath.Outcome);
        var own = Assert.Single(wrongDeath.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == hadid.Id);
        Assert.True(own.Veto); Assert.True(own.DatesConflict);
        Assert.Contains("the project's own entity of the name, its dates conflicting: the desk's, not a second entity", own.Reason);
        Assert.Contains("loc's one record of the name, its dates conflicting: the desk's, not a mint", wrongDeath.Groups.SelectMany(g => g.Members).First(m => m.Candidate.Source == "loc").Reason);

        // a source's one exact record of the name, conflicting: the desk's
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/n2003069091", "loc", "Broderick, Laurence", GettyVocabLoader.LifespanFromYears("1935", null)));
        var broderick = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Broderick, Laurence, 1953-"));
        Assert.Equal(Outcome.Queued, broderick.Outcome);
        Assert.Contains(broderick.Groups.SelectMany(g => g.Members), m => m.Reason.Contains("loc's one record of the name, its dates conflicting"));

        // two exact records of the source, both conflicting, are other people: the string mints as before
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/n50000001", "loc", "Smith, John", GettyVocabLoader.LifespanFromYears("1781", "1852")));
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/n50000002", "loc", "Smith, John", GettyVocabLoader.LifespanFromYears("1660", "1720")));
        var smith = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "keywords", "Smith, John, 1900-1950"));
        Assert.Equal(Outcome.Minted, smith.Outcome);
        Assert.DoesNotContain(smith.Groups.SelectMany(g => g.Members), m => m.Reason.Contains("the desk's, not a mint"));
    }
}
