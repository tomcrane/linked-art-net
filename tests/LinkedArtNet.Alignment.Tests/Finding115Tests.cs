using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// Finding 115 (2026-11-08, the owner at "Hall, Harry, 1815-1882"): ULAN's and LoC's Harry Hall of 1813-1882, the painter the string
/// names, greyed at 0.00 for two years on the birth while undated namesakes stood above. A year or two out on one side with the other
/// the same year is a near miss, neither agreement nor conflict; a real conflict still vetoes.
/// </summary>
[Collection("postgres")]
public class Finding115Tests(PostgresFixture pg)
{
    private const string Ulan = "http://vocab.getty.edu/ulan/500015962", Loc = "http://id.loc.gov/authorities/names/no2011170815";
    private const string UlanPainter = "http://vocab.getty.edu/ulan/500025601";

    private static Lifespan Life(int b, int e) => new(new DateTimeOffset(b, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(b, 12, 31, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(e, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(e, 12, 31, 0, 0, 0, TimeSpan.Zero), $"{b}-{e}");

    [Fact]
    public void A_near_miss_is_a_year_or_two_on_one_side_with_the_other_the_same()
    {
        Assert.True(Life(1815, 1882).NearMiss(Life(1813, 1882)));
        Assert.True(Life(1563, 1639).NearMiss(Life(1563, 1638)));
        Assert.False(Life(1815, 1882).NearMiss(Life(1812, 1882))); // three years is not near
        Assert.False(Life(1815, 1882).NearMiss(Life(1814, 1881))); // both sides out is not near
        Assert.False(Life(1800, 1852).NearMiss(Life(1839, 1892)));
        Assert.Equal(DateAgreement.Conflict, Life(1815, 1882).Compare(Life(1813, 1882))); // the comparison itself is unchanged
    }

    [PostgresFact]
    public async Task A_near_miss_does_not_veto_and_two_sources_still_carry_the_vote()
    {
        var ds = "f115" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Ulan, "ulan", "Hall, Harry", ["Hall, Harry", "Harry Hall"], "Person", Life(1813, 1882), [], "{\"description\":\"English painter and illustrator, 1813-1882\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Loc, "loc", "Hall, Harry, 1813-1882", ["Hall, Harry, 1813-1882", "Hall, Harry"], "Person", Life(1813, 1882), [Ulan], "{\"headingDates\":\"1813-1882\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/no2005030705", "loc", "Hall, Harry", ["Hall, Harry"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(UlanPainter, "ulan", "Sheffield, George", ["Sheffield, George"], "Person", Life(1839, 1892), [], null, DateTimeOffset.UtcNow, "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());

        var hall = await pg.Store.CollectAsync(ds, "persauthorfull", "Hall, Harry, 1815-1882");
        var e = await reconciler.EvaluateAsync(ds, hall, includeStore: true);
        Assert.Equal(Outcome.Accepted, e.Outcome);
        Assert.Contains(Ulan, e.Accepted!.Uris); Assert.Contains(Loc, e.Accepted.Uris);
        Assert.Contains(e.Accepted.Members, m => m.Reason.Contains("dates near"));
        // a real conflict still vetoes: the engraver of 1800-1852 against the painter of 1839-1892
        var sheffield = await pg.Store.CollectAsync(ds, "persauthorfull", "Sheffield, George, 1800-1852");
        var s = await reconciler.EvaluateAsync(ds, sheffield, includeStore: true);
        Assert.NotEqual(Outcome.Accepted, s.Outcome);
        Assert.Contains(s.Groups.SelectMany(g => g.Members), m => m.Candidate.Uri == UlanPainter && m.Veto && m.Reason.Contains("dates conflict"));
    }
}
