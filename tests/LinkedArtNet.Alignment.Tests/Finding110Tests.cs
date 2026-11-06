using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// Finding 110 (2026-11-06, run jr8hf5): a queued string reconsidered is one its own indexes could not settle, and an entity with records
/// behind it answers it silently only when the string's index stage puts one of those records at the top score. Seven of the second pass's
/// 107 settlements were onto an entity whose record the first pass had scored below the top, three of them wrong ("Apollo" in publisher
/// onto the deity made minutes earlier from "Apollo (Deity)", under ULAN's body and LoC's "Apollo, 1980-" at 1.00), and no score
/// separated them from the four right: the desk reads all seven.
/// </summary>
[Collection("postgres")]
public class Finding110Tests(PostgresFixture pg)
{
    private const string Body = "http://vocab.getty.edu/ulan/500452191";
    private const string Dated = "http://id.loc.gov/authorities/names/n95071498";
    private const string Deity = "http://id.loc.gov/authorities/names/n2014033988";

    private async Task<Reconciler> IndexedAsync(string ds)
    {
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Body, "ulan", "Apollo", ["Apollo"], "Group", null, [], "{\"description\":\"British publisher\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Dated, "loc", "Apollo, 1980-", ["Apollo, 1980-"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(Deity, "loc", "Apollo (Deity)", ["Apollo (Deity)"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        return new Reconciler(pg.Store, new IdentifierScheme("https://data.example.org/" + ds), [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
    }

    [PostgresFact]
    public async Task A_record_that_lost_in_the_first_pass_does_not_answer_silently_in_the_second()
    {
        var ds = "f110a" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = await IndexedAsync(ds);
        // the first pass: ULAN's body and LoC's dated person tie at 1.00, the deity at 0.85 under them, queued
        var apollo = await pg.Store.CollectAsync(ds, "publisher", "Apollo");
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, apollo));
        // another string's first pass made the deity's entity, the bare name among its labels
        var deity = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Apollo (Deity)", ["Apollo (Deity)", "Apollo"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(deity.Id, new Equivalent(Deity, "loc", DecisionMethod.Single, 1, DateTimeOffset.UtcNow));
        // the second pass: the entity's record is at 0.85 under two at 1.00, and the string stays queued with the entity shown and told why
        var again = await reconciler.EvaluateAsync(ds, apollo, includeStore: true);
        Assert.Equal(Outcome.Queued, again.Outcome);
        Assert.Contains(again.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == deity.Id && m.Reason.Contains("not at the top here") && m.Reason.Contains("finding 110"));
    }

    [PostgresFact]
    public async Task An_entity_whose_record_is_at_the_top_answers_as_before()
    {
        var ds = "f110b" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var reconciler = await IndexedAsync(ds);
        var apollo = await pg.Store.CollectAsync(ds, "publisher", "Apollo");
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, apollo));
        // the project's Apollo holds ULAN's body, one of the two at the top: the entity answers, as the second pass always has
        var body = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Apollo", ["Apollo"], Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(body.Id, new Equivalent(Body, "ulan", DecisionMethod.Single, 1, DateTimeOffset.UtcNow));
        var again = await reconciler.EvaluateAsync(ds, apollo, includeStore: true);
        Assert.Equal(Outcome.MatchedStore, again.Outcome);
        Assert.Equal(body.Id, again.StoreMatch!.Id);
    }
}
