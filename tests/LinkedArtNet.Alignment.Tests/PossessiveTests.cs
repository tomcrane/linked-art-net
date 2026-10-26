using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class PossessiveTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_firm_named_for_the_man_is_offered_for_his_name_and_never_carried_alone()
    {
        // finding 105 (2026-10-26, the desk): "Mr Dowell" in corpauthor, three Edinburgh sale catalogues behind it, and the project's own
        // "Dowell's Ltd", the auctioneers, found by the desk's search but never among the candidates, since the keys "dowell" and
        // "dowells" never met. A reading that may be a body looks for the possessive too, and the firm scores 0.90: shown, not carried
        var ds = "po" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500437664", "ulan", "Dowell", ["Dowell", "Dowel"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500125256", "ulan", "Christie's", ["Christie's", "Christie, Manson & Woods"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        // the project's firm, minted from its own string
        var firm = await pg.Store.CollectAsync(ds, "corpauthor", "Dowell's Ltd");
        var report = await reconciler.RunAsync(ds, runId: "run-test-possessive");
        Assert.Equal(1, report.Minted);
        var firmEntity = (await pg.Store.ResolveAsync(ds, "corpauthor", "Dowell's Ltd"))!;
        // then the strings the desk sees, evaluated with the store in play and not yet decided
        var dowell = await pg.Store.CollectAsync(ds, "corpauthor", "Mr Dowell");
        var christie = await pg.Store.CollectAsync(ds, "corpauthor", "Messrs Christie");
        var person = await pg.Store.CollectAsync(ds, "persauthorfull", "Dowell, John (author)");

        var e = await reconciler.EvaluateAsync(ds, dowell, includeStore: true);
        Assert.Equal(Outcome.Queued, e.Outcome);
        var members = e.Groups.SelectMany(g => g.Members).ToList();
        var offered = Assert.Single(members, m => m.Candidate.EntityId == firmEntity.Id);
        Assert.Equal(0.90, offered.Candidate.NameScore);
        Assert.True(offered.Candidate.ByPossessive);
        Assert.Contains("a firm named for the man", offered.Reason);
        Assert.Contains(members, m => m.Candidate.Uri == "http://vocab.getty.edu/ulan/500437664" && m.Candidate.NameScore >= 0.97); // the ULAN person, still the exact one

        // the same for an authority record: Christie's for "Messrs Christie", reached through the possessive key, and the string the desk's
        var c = await reconciler.EvaluateAsync(ds, christie, includeStore: true);
        Assert.Equal(Outcome.Queued, c.Outcome);
        var christies = Assert.Single(c.Groups.SelectMany(g => g.Members), m => m.Candidate.Uri == "http://vocab.getty.edu/ulan/500125256");
        Assert.Equal(0.90, christies.Candidate.NameScore);

        // a reading that cannot be a body does not look for the firm
        Assert.DoesNotContain("dowells", reconciler.Read(person).Keys);
        Assert.Contains("dowells", reconciler.Read(dowell).Keys);
    }
}
