using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// Findings 108 and 109 (2026-11-06, the owner at "Mr Evans": nine at 1.00 in id order, the auction house sixth): the string as written
/// carries a tie, a heading in the heading fields, a name with an honorific in the name fields when the record is of the trade.
/// </summary>
[Collection("postgres")]
public class Finding108Tests(PostgresFixture pg)
{
    private static AuthorityRecord Ulan(string id, string label, string type, string[] labels, string? description = null) =>
        new($"http://vocab.getty.edu/ulan/{id}", "ulan", label, labels, type, null, [], description is null ? null : $"{{\"description\":\"{description}\"}}", DateTimeOffset.UtcNow, "test");

    [PostgresFact]
    public async Task A_heading_as_written_carries_the_tie_in_a_heading_field()
    {
        // "Girtin, Thomas, 1775-1802" in keywords:head tied at 1.00 between LoC's heading and ULAN's two Girtins; the heading is the
        // catalogue's own cataloguing, and the one record that bears it whole is the one it meant
        var ds = "f108a" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        const string loc = "http://id.loc.gov/authorities/names/n85306897";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(loc, "loc", "Girtin, Thomas, 1775-1802", ["Girtin, Thomas, 1775-1802", "Girtin, Thomas"], "Person", GettyVocabLoader.LifespanFromYears("1775", "1802"), [], "{\"headingDates\":\"1775-1802\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(Ulan("500011869", "Girtin, Thomas", "Person", ["Girtin, Thomas", "Thomas Girtin"]));
        await pg.Store.PutAuthorityAsync(Ulan("500300001", "Girtin, Thomas", "Person", ["Girtin, Thomas"]));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var girtin = await pg.Store.CollectAsync(ds, "keywords:head", "Girtin, Thomas, 1775-1802");
        var e = await reconciler.EvaluateAsync(ds, girtin, includeStore: true);
        Assert.Equal(Outcome.Accepted, e.Outcome);
        Assert.Equal(DecisionMethod.Single, e.AcceptedMethod);
        Assert.Contains(loc, e.Accepted!.Uris);
        Assert.Contains(e.Accepted.Members, m => m.Reason.Contains("bears the string as written (Girtin, Thomas, 1775-1802): carries the tie (finding 108)"));
        Assert.Contains(e.Groups.SelectMany(g => g.Members), m => m.Candidate.Source == "ulan" && m.Reason.Contains("borne elsewhere"));
        // a bare name is no heading: "Girtin, Thomas" against the three stays a tie
        var bare = await pg.Store.CollectAsync(ds, "keywords:head", "Girtin, Thomas");
        Assert.Equal(Outcome.Queued, (await reconciler.EvaluateAsync(ds, bare, includeStore: true)).Outcome);
    }

    [PostgresFact]
    public async Task A_name_with_an_honorific_carries_the_tie_when_the_record_is_of_the_trade()
    {
        // "Mr Farebrother" in publisher: ULAN's Farebrother, Charles, the auctioneer, bears "Mr. Farebrother" as a Provenance Index lookup
        // name, and a namesake does not; "Mr Evans" to the auction house, a body, likewise. "Mr Hone" bears "Mr. Hone" on Nathaniel Hone the
        // painter, from the sale of his collection: shown and left to the desk, since whether a catalogue's publisher may be the owner sold is
        // a specialist's question. And the natural-order form every ULAN person bears is no evidence: "Charles Farebrother" stays a tie
        var ds = "f109a" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(Ulan("500451543", "Farebrother, Charles", "Person", ["Farebrother, Charles", "Charles Farebrother", "Mr. Farebrother", "Farebrother"], "English auctioneer, active 1820s"));
        await pg.Store.PutAuthorityAsync(Ulan("500451544", "Farebrother", "Person", ["Farebrother", "Charles Farebrother"], "British owner"));
        await pg.Store.PutAuthorityAsync(Ulan("500451791", "Evans, Robert Harding", "Group", ["Evans, Robert Harding", "Evans", "Mr. Evans"], "English auction house"));
        await pg.Store.PutAuthorityAsync(Ulan("500008627", "Evans, George", "Person", ["Evans, George", "Evans", "George Evans"], "English landscapist and draftsman, 1763-1819"));
        await pg.Store.PutAuthorityAsync(Ulan("500009522", "Hone, Nathaniel, I", "Person", ["Hone, Nathaniel, I", "Hone", "Mr. Hone"], "Irish painter, 1718-1784"));
        await pg.Store.PutAuthorityAsync(Ulan("500009523", "Hone, Horace", "Person", ["Hone, Horace", "Hone"], "Irish painter, 1754-1825"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());

        var farebrother = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "publisher", "Mr Farebrother"), includeStore: true);
        Assert.Equal(Outcome.Accepted, farebrother.Outcome);
        Assert.Equal(DecisionMethod.Single, farebrother.AcceptedMethod);
        Assert.Contains("http://vocab.getty.edu/ulan/500451543", farebrother.Accepted!.Uris);
        Assert.Contains(farebrother.Accepted.Members, m => m.Reason.Contains("bears the string as written (Mr. Farebrother): carries the tie (finding 109)"));

        var evans = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "publisher", "Mr Evans"), includeStore: true);
        Assert.Equal(Outcome.Accepted, evans.Outcome);
        Assert.Contains("http://vocab.getty.edu/ulan/500451791", evans.Accepted!.Uris);

        var hone = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "corpauthor", "Mr Hone"), includeStore: true);
        Assert.Equal(Outcome.Queued, hone.Outcome);
        Assert.Contains(hone.Groups.SelectMany(g => g.Members), m => m.Candidate.Uri.EndsWith("500009522") && m.Reason.Contains("a Provenance Index name of a person outside the trade, for the desk (finding 109)"));

        var natural = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "publisher", "Charles Farebrother"), includeStore: true);
        Assert.Equal(Outcome.Queued, natural.Outcome);

        // the trades are the project's knowledge, in its manifest: a project that lists none carries no person by the rule, bodies only
        Assert.Contains("the trades", string.Join("; ", new ProjectManifest().Differences(ProjectManifest.PmcDefault)));
        var without = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], (ProjectManifest.PmcDefault with { Trades = [] }).ToOptions());
        Assert.Equal(Outcome.Queued, (await without.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "publisher", "Mr Farebrother"), includeStore: true)).Outcome);
        Assert.Equal(Outcome.Accepted, (await without.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "publisher", "Mr Evans"), includeStore: true)).Outcome);
    }

    [PostgresFact]
    public async Task The_rule_stands_aside_when_the_ladder_would_accept_the_string_anyway()
    {
        // run a98vpa (2026-11-06): 77 strings cpxv7k had voted came back single, 35 of them confirmed on the very same entity ("Blake,
        // William, 1757-1827", "Adam, Robert, 1728-1792"). The heading's two-source group was not in a tie the ladder knew: the other live
        // group at 1.00 was a provisional lookalike of the project's own, the placeholder of a bare "Blake, William" queued among ULAN's
        // namesakes, which the store stage declines (finding 95) and the ladder's `second` skips (finding 38), so the acceptance line took
        // the vote. Findings 108 and 109 were measured over queued strings, and a string the line accepts is not one of them: the vote
        // stands, with its method
        var ds = "f108c" + Guid.NewGuid().ToString("N")[..5];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        const string loc = "http://id.loc.gov/authorities/names/n78095331";
        const string ulan = "http://vocab.getty.edu/ulan/500012489";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(ulan, "ulan", "Blake, William", ["Blake, William", "William Blake"], "Person", GettyVocabLoader.LifespanFromYears("1757", "1827"), [], "{\"description\":\"English painter and poet, 1757-1827\"}", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(Ulan("500300002", "Blake, William", "Person", ["Blake, William"], "English architect, 1868-1940"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(loc, "loc", "Blake, William, 1757-1827", ["Blake, William, 1757-1827", "Blake, William"], "Person", GettyVocabLoader.LifespanFromYears("1757", "1827"), [ulan], "{\"headingDates\":\"1757-1827\"}", DateTimeOffset.UtcNow, "test"));
        var lookalike = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Blake, William", ["Blake, William"]), scheme);
        await pg.Store.DecideAsync((await pg.Store.CollectAsync(ds, "persauthorfull", "Blake, William")).Id, lookalike.Id, DecisionMethod.Queued);
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var blake = await reconciler.EvaluateAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Blake, William, 1757-1827"), includeStore: true);
        Assert.Equal(Outcome.Accepted, blake.Outcome);
        Assert.Equal(DecisionMethod.Vote, blake.AcceptedMethod);
        Assert.Contains(loc, blake.Accepted!.Uris); Assert.Contains(ulan, blake.Accepted.Uris);
        Assert.DoesNotContain(blake.Groups.SelectMany(g => g.Members), m => m.Reason.Contains("finding 108"));
    }
}
