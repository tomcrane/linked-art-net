using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class TitleOnlyTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_title_alone_is_not_exact_enough_for_one_source_to_carry()
    {
        // finding 99 (2026-10-26, run k22erz): "Yarmouth, Earl of (author)" was an exact-single on ULAN's "Hertford, Francis Charles
        // Seymour-Conway, 3rd Marquess of", which bears "Yarmouth, Earl of" as a variant: a courtesy title several men have borne, no
        // personal name, and no more exact than one word (finding 42's ground). The desk's, with the record as its candidate
        var ds = "ti" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500436285", "ulan", "Hertford, Francis Charles Seymour-Conway, 3rd Marquess of", ["Hertford, Francis Charles Seymour-Conway, 3rd Marquess of", "Yarmouth, Earl of"], "Person", GettyVocabLoader.LifespanFromYears("1777", "1842"), [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500436286", "ulan", "Seymour, Jonathan", ["Seymour, Jonathan"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var yarmouth = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Yarmouth, Earl of (author)"));
        Assert.True(yarmouth.Reading.Agent!.TitleOnly);
        Assert.Equal("Yarmouth, Earl of", yarmouth.Reading.Text);
        Assert.Equal(Outcome.Queued, yarmouth.Outcome);
        Assert.Contains(yarmouth.Groups.SelectMany(g => g.Members), m => m.Candidate.Uri.EndsWith("500436285") && !m.Veto);
        // a personal name against one source's one exact record is an exact-single as before
        var seymour = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Seymour, Jonathan (author)"));
        Assert.Equal(Outcome.Accepted, seymour.Outcome); Assert.Equal(DecisionMethod.ExactSingle, seymour.AcceptedMethod);
    }
}
