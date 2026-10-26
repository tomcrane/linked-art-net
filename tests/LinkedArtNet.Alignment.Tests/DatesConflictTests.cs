using LinkedArtNet.Agents;
using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

public class LifespanDisplayTests
{
    private static DateTimeOffset Y(int year, bool end = false) => end ? new DateTimeOffset(year, 12, 31, 0, 0, 0, TimeSpan.Zero) : new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_window_of_possibility_shows_nothing_and_a_dated_side_shows_alone()
    {
        // the desk, 2026-10-26: ULAN's undated owners all wore "1200~-~2080", its estimate for "unknown", as if it were a life
        Assert.Null(new Lifespan(Y(1200), null, null, Y(2080, true), "1200~-~2080").Display);
        Assert.Equal("1952-", new Lifespan(Y(1952), Y(1952, true), null, Y(2102, true), "1952-~2102").Display);
        Assert.Equal("-1748", new Lifespan(Y(1648), null, Y(1748), Y(1748, true), "1648~-1748").Display);
        Assert.Equal("c. 1648-1748", new Lifespan(Y(1644), Y(1652, true), Y(1748), Y(1748, true), "c. 1648-1748").Display); // both sides dated (an approximate year within a decade is tight): the text as given
        Assert.Equal("active 1720-1749", new Lifespan(null, Y(1720), Y(1749), null, "active 1720-1749").Display); // working dates as given
        Assert.Null(new Lifespan(null, null, null, null).Display);
    }
}

[Collection("postgres")]
public class DatesConflictTests(PostgresFixture pg)
{
    private static AuthorityRecord Person(string uri, string source, string label, Lifespan? lifespan) => new(uri, source, label, [label], "Person", lifespan, [], null, DateTimeOffset.UtcNow, "test");

    [Fact]
    public void An_approximate_year_in_a_lifespan_is_widened_on_its_own_side()
    {
        // finding 97 (2026-10-26, run k22erz): "Cavaceppi, Bartolomeo, c.1716-1799" against LoC's 1717-1799 was a conflict, and the vote with
        // ULAN's agreeing record was lost; the lone year had been widened since finding 82, the range had not
        var rule = new ContextRule([EntityClass.Person], "default");
        var parser = new AgentStringParser(AgentProfileCatalog.BuiltIn.Get("default"));
        static SourceString S(string raw) => new(1, "ds", "keywords", raw, StringKey.Normalise(raw), null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1);
        var cavaceppi = StringReading.Read(S("Cavaceppi, Bartolomeo, c.1716-1799"), rule, parser).Lifespan!;
        Assert.Equal(1712, cavaceppi.BeginEarliest!.Value.Year); Assert.Equal(1720, cavaceppi.BeginLatest!.Value.Year);
        Assert.Equal(1799, cavaceppi.EndEarliest!.Value.Year); Assert.Equal(1799, cavaceppi.EndLatest!.Value.Year);
        Assert.Equal(DateAgreement.Agree, cavaceppi.Compare(GettyVocabLoader.LifespanFromYears("1717", "1799")!, 120));
        var ward = StringReading.Read(S("Ward, Edwin Arthur, 1859-c.1927"), rule, parser).Lifespan!;
        Assert.Equal(1859, ward.BeginEarliest!.Value.Year); Assert.Equal(1859, ward.BeginLatest!.Value.Year);
        Assert.Equal(1923, ward.EndEarliest!.Value.Year); Assert.Equal(1931, ward.EndLatest!.Value.Year);
        var tait = StringReading.Read(S("Tait, Robert Scott, c 1816- c 1875"), rule, parser).Lifespan!;
        Assert.Equal(1812, tait.BeginEarliest!.Value.Year); Assert.Equal(1879, tait.EndLatest!.Value.Year);
        var exact = StringReading.Read(S("Alken, Henry, 1774-1850"), rule, parser).Lifespan!;
        Assert.Equal(1774, exact.BeginEarliest!.Value.Year); Assert.Equal(1774, exact.BeginLatest!.Value.Year); Assert.Equal(1850, exact.EndLatest!.Value.Year);
        Assert.Equal((true, false), StringReading.ApproximateSides("c.1716-1799"));
        Assert.Equal((true, false), StringReading.ApproximateSides("1733?-1810"));
        Assert.Equal((false, true), StringReading.ApproximateSides("1859-c.1927"));
        Assert.Equal((true, true), StringReading.ApproximateSides("c 1816- c 1875"));
        Assert.Equal((false, false), StringReading.ApproximateSides("1774-1850"));
    }

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
