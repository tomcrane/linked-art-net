using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class SecondPassTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_string_queued_early_in_a_run_is_settled_at_its_end_by_an_entity_the_run_made_later()
    {
        // finding 51 (2026-10-23): "Lorem, Ipsum" is queued between ULAN's two undated namesakes; later in the run an override decides
        // "Lorem, Ipsum (editor)" onto one of them and makes the aligned entity; the second pass answers the first string from the store.
        // "Dolor, Sit", queued the same way with nothing to settle it, stays queued with its placeholder
        var ds = "sp" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500900001", "ulan", "Lorem, Ipsum", ["Lorem, Ipsum"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500900002", "ulan", "Lorem, Ipsum", ["Lorem, Ipsum"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500900003", "ulan", "Dolor, Sit", ["Dolor, Sit"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500900004", "ulan", "Dolor, Sit", ["Dolor, Sit"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        var lorem = await pg.Store.CollectAsync(ds, "persauthorfull", "Lorem, Ipsum");
        var dolor = await pg.Store.CollectAsync(ds, "persauthorfull", "Dolor, Sit");
        var editor = await pg.Store.CollectAsync(ds, "persauthorfull", "Lorem, Ipsum (editor)");
        await pg.Store.PutOverrideAsync(new Override(ds, "persauthorfull", editor.Normalised, "http://vocab.getty.edu/ulan/500900001", "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], ProjectManifest.PmcDefault.ToOptions());
        var narrated = new List<string>();
        var report = await reconciler.RunAsync(ds, runId: "run-test-second-pass", onDecided: (e, n, total) => narrated.Add($"{n}/{total} {e.Reading.Text} {e.Outcome}"));
        Assert.Equal(3, report.Considered);
        Assert.Equal(1, report.Overridden); Assert.Equal(1, report.MatchedStore); Assert.Equal(1, report.Queued);
        Assert.Equal(2, report.Reconsidered); Assert.Equal(1, report.Settled);
        Assert.Contains("second pass: 2 queued strings reconsidered, 1 settled", report.ToString());
        Assert.Contains("1/2 Lorem, Ipsum MatchedStore", narrated); // the settled string, numbered within the pass
        var loremDecision = (await pg.Store.DecisionForAsync(lorem.Id))!;
        Assert.Equal(DecisionMethod.ExactName, loremDecision.Method); Assert.Equal("run-test-second-pass", loremDecision.RunId);
        var editorDecision = (await pg.Store.DecisionForAsync(editor.Id))!;
        Assert.Equal(editorDecision.EntityId, loremDecision.EntityId); // the same aligned entity
        var loremEntities = await pg.Store.FindEntitiesByLabelAsync(StringKey.Normalise("Lorem, Ipsum"), ds, EntityClass.Person);
        Assert.Single(loremEntities); Assert.NotEqual(EntityStatus.Provisional, loremEntities[0].Status); // the placeholder went; the overridden (confirmed) entity stands
        var dolorDecision = (await pg.Store.DecisionForAsync(dolor.Id))!;
        Assert.Equal(DecisionMethod.Queued, dolorDecision.Method);
        Assert.NotNull(await pg.Store.GetEntityAsync(dolorDecision.EntityId!)); // still queued, placeholder kept
        // the undo takes the second pass's decision with the rest of the run
        var undone = await pg.Store.UndoRunAsync("run-test-second-pass");
        Assert.Equal(3, undone.DecisionsInvalidated);
        Assert.Null(await pg.Store.DecisionForAsync(lorem.Id));
    }

    [PostgresFact]
    public async Task The_second_pass_goes_round_again_while_it_settles_something()
    {
        // finding 100 (2026-10-26, run k22erz): the two "Gibson, Jean, 1935-" strings. The keywords string stayed queued between LoC's agreeing
        // heading and ULAN's undated namesake; the keywords:head string was accepted in the second pass, because by its turn another string's
        // acceptance had put that namesake on an entity whose dates conflict, and the namesake's group went down with it. The keywords string
        // had been reconsidered before that acceptance, and one round left it where the order of the pass found it.
        // Here: "Brown, Ann, 1935-" is queued between LoC's agreeing heading and the undated pair ULAN and VIAF link; "Brown, Ann, 1920-" is
        // queued between that pair and LoC's undated record; an override decides "Brown, Ann, 1880-1950" onto LoC's undated record at the
        // run's end. Round one: the first string is still queued (the pair stands); the second is accepted onto the pair, LoC's record gone
        // down with the overridden entity. Round two: the pair goes down with the second string's entity, and the first string is a single
        var ds = "sp" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Person(string uri, string source, string label, Lifespan? lifespan, params string[] sameAs) => new(uri, source, label, [label, Qualified.Split(label).Name], "Person", lifespan, sameAs, null, DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/nb2010009060", "loc", "Brown, Ann, 1935-1991", GettyVocabLoader.LifespanFromYears("1935", "1991")));
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/nb2010009063", "loc", "Brown, Ann", null));
        await pg.Store.PutAuthorityAsync(Person("http://vocab.getty.edu/ulan/500375601", "ulan", "Brown, Ann", null, "http://viaf.org/viaf/900000001"));
        await pg.Store.PutAuthorityAsync(Person("http://viaf.org/viaf/900000001", "viaf", "Brown, Ann", null));
        var first = await pg.Store.CollectAsync(ds, "persauthorfull", "Brown, Ann, 1935-");
        var second = await pg.Store.CollectAsync(ds, "persauthorfull", "Brown, Ann, 1920-");
        var third = await pg.Store.CollectAsync(ds, "persauthorfull", "Brown, Ann, 1880-1950");
        await pg.Store.PutOverrideAsync(new Override(ds, "persauthorfull", third.Normalised, "http://id.loc.gov/authorities/names/nb2010009063", "test"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc"), new LocalIndexSource(pg.Store, "viaf")], ProjectManifest.PmcDefault.ToOptions());
        var report = await reconciler.RunAsync(ds, runId: "run-test-rounds");
        Assert.Equal(3, report.Considered); Assert.Equal(1, report.Overridden);
        Assert.Equal(2, report.Reconsidered); Assert.Equal(2, report.Settled); Assert.Equal(2, report.Rounds); Assert.Equal(0, report.Queued);
        Assert.Contains("second pass: 2 queued strings reconsidered, 2 settled over 2 rounds", report.ToString());
        var secondDecision = (await pg.Store.DecisionForAsync(second.Id))!;
        Assert.Equal(DecisionMethod.Vote, secondDecision.Method);
        Assert.Contains((await pg.Store.ResolveAsync(ds, "persauthorfull", "Brown, Ann, 1920-"))!.Equivalents, q => q.Uri.EndsWith("500375601"));
        var firstDecision = (await pg.Store.DecisionForAsync(first.Id))!;
        Assert.Equal(DecisionMethod.Single, firstDecision.Method); Assert.Equal("run-test-rounds", firstDecision.RunId);
        Assert.Contains((await pg.Store.ResolveAsync(ds, "persauthorfull", "Brown, Ann, 1935-"))!.Equivalents, q => q.Uri.EndsWith("nb2010009060"));
        Assert.NotEqual(firstDecision.EntityId, secondDecision.EntityId);
        // the placeholders of both went with their queued decisions
        Assert.All(await pg.Store.FindEntitiesByLabelAsync(StringKey.Normalise("Brown, Ann"), ds, EntityClass.Person), e => Assert.NotEqual(EntityStatus.Provisional, e.Status));
    }

    [PostgresFact]
    public async Task A_queued_strings_own_placeholder_never_settles_it()
    {
        // finding 95 (2026-10-24 night, run wy2aqs): a string queued on a dates conflict (finding 93) gets a placeholder minted with its own
        // lifespan; in the second pass that placeholder came back as a store candidate whose dates agree, and the single-source rule
        // accepted the string onto it ("Cozens, John Robert, 1752-1798" -> accepted single store Cozens, John Robert): 265 strings taken
        // off the desk onto their own placeholders, finding 93's seventeen among them. A placeholder awaiting review carries nothing
        var ds = "pl" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n79068451", "loc", "Cozens, John Robert", ["Cozens, John Robert"], "Person", GettyVocabLoader.LifespanFromYears("1752", "1797"), [], null, DateTimeOffset.UtcNow, "test"));
        var cozens = await pg.Store.CollectAsync(ds, "keywords", "Cozens, John Robert, 1752-1799");
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var report = await reconciler.RunAsync(ds, runId: "run-test-placeholder");
        Assert.Equal(1, report.Queued); Assert.Equal(0, report.Minted); Assert.Equal(0, report.Accepted);
        Assert.Equal(1, report.Reconsidered); Assert.Equal(0, report.Settled);
        var decision = (await pg.Store.DecisionForAsync(cozens.Id))!;
        Assert.Equal(DecisionMethod.Queued, decision.Method);
        var placeholder = (await pg.Store.GetEntityAsync(decision.EntityId!))!;
        Assert.Equal(EntityStatus.Provisional, placeholder.Status);
        Assert.Equal("1752-1799", placeholder.Lifespan?.Text);
        // evaluated again with the placeholder in the store, the string is still the desk's; since finding 104 its own placeholder is not a
        // candidate at all (a twin's would be, marked "a placeholder awaiting review: carries nothing")
        var again = await reconciler.EvaluateAsync(ds, cozens, includeStore: true);
        Assert.Equal(Outcome.Queued, again.Outcome);
        Assert.DoesNotContain(again.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == placeholder.Id);
        Assert.Contains(again.Groups.SelectMany(g => g.Members), m => m.Candidate.Source == "loc" && m.Veto && m.DatesConflict); // the conflict that queued it, still there
    }

    [PostgresFact]
    public async Task A_queued_strings_own_placeholder_dates_no_record_for_it()
    {
        // finding 101 (2026-10-26, run k22erz): "Gibson, Jean, 1930-" was queued between ULAN's undated "Gibson, Jean" (a window, 1800~-~2100)
        // and LoC's undated one, LoC's dated namesakes vetoed. In the second pass its own placeholder came back agreeing on the dates, and
        // the rule of findings 14 and 16 took it as the record that dates the two lone undated ones: both joined to it, two sources, a vote
        // onto a placeholder that carries nothing. The placeholder dates nothing; the string stays the desk's
        var ds = "pd" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Person(string uri, string source, string label, Lifespan? lifespan) => new(uri, source, label, [label, Qualified.Split(label).Name], "Person", lifespan, [], null, DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Person("http://vocab.getty.edu/ulan/500375601", "ulan", "Gibson, Jean", new Lifespan(new DateTimeOffset(1800, 1, 1, 0, 0, 0, TimeSpan.Zero), null, null, new DateTimeOffset(2100, 12, 31, 0, 0, 0, TimeSpan.Zero), "1800~-~2100")));
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/nb2010009063", "loc", "Gibson, Jean", null));
        await pg.Store.PutAuthorityAsync(Person("http://id.loc.gov/authorities/names/nb2010009060", "loc", "Gibson, Jean, 1935-1991", GettyVocabLoader.LifespanFromYears("1935", "1991")) with { Labels = ["Gibson, Jean, 1935-1991", "Gibson, Jean"] });
        await pg.Store.PutAuthorityAsync(Person("http://vocab.getty.edu/ulan/500001913", "ulan", "Gibson, Jean", GettyVocabLoader.LifespanFromYears("1927", "1991")));
        // (the dated namesakes make each source a source of several records of the name, as LoC and ULAN are, so finding 72 does not read the undated pair as two lone records)
        var gibson = await pg.Store.CollectAsync(ds, "persauthorfull", "Gibson, Jean, 1930-");
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var report = await reconciler.RunAsync(ds, runId: "run-test-placeholder-dates");
        Assert.Equal(1, report.Queued); Assert.Equal(0, report.Accepted); Assert.Equal(1, report.Reconsidered); Assert.Equal(0, report.Settled);
        var decision = (await pg.Store.DecisionForAsync(gibson.Id))!;
        Assert.Equal(DecisionMethod.Queued, decision.Method);
        var again = await reconciler.EvaluateAsync(ds, gibson, includeStore: true);
        Assert.Equal(Outcome.Queued, again.Outcome);
        var members = again.Groups.SelectMany(g => g.Members).ToList();
        Assert.DoesNotContain(members, m => m.Candidate.EntityId == decision.EntityId); // its own placeholder, not a candidate since finding 104
        Assert.DoesNotContain(members, m => m.Reason.Contains("which store dates") || m.JoinedTo == decision.EntityId);
        Assert.Equal(2, again.Groups.Count(g => !g.Vetoed && g.Members.Any(m => m.Candidate.EntityId is null))); // ULAN's and LoC's undated records, still two groups
    }

    [PostgresFact]
    public async Task A_queued_strings_own_placeholder_holds_no_record_for_it()
    {
        // finding 102 (2026-10-26, run u4ykeu): "Rug Chapel (Corwen, Wales)" was queued between LoC's two headings placed at Corwen, "Rug
        // Chapel" and "Capel y Rug"; in the second pass its own placeholder, labelled from the string and so placed at Corwen too, came back
        // as a store candidate, both headings were "placed through" it into one group, and the single was accepted onto the placeholder.
        // A placeholder awaiting review holds nothing; the string stays the desk's
        var ds = "pp" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Placed(string uri, string label, params string[] variants) => new(uri, "loc", label, [label, Qualified.Split(label).Name, .. variants], "Place", null, [], null, DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Placed("http://id.loc.gov/authorities/names/no99002269", "Rug Chapel (Corwen, Wales)"));
        await pg.Store.PutAuthorityAsync(Placed("http://id.loc.gov/authorities/names/no2019049001", "Capel y Rug (Corwen, Wales)", "Rug Chapel"));
        var rug = await pg.Store.CollectAsync(ds, "keywords", "Rug Chapel (Corwen, Wales)");
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "loc")], ProjectManifest.PmcDefault.ToOptions());
        var report = await reconciler.RunAsync(ds, runId: "run-test-placeholder-holds");
        Assert.Equal(1, report.Queued); Assert.Equal(0, report.Accepted); Assert.Equal(0, report.Settled);
        var decision = (await pg.Store.DecisionForAsync(rug.Id))!;
        Assert.Equal(DecisionMethod.Queued, decision.Method);
        var again = await reconciler.EvaluateAsync(ds, rug, includeStore: true);
        Assert.Equal(Outcome.Queued, again.Outcome);
        var members = again.Groups.SelectMany(g => g.Members).ToList();
        Assert.DoesNotContain(members, m => m.Candidate.EntityId == decision.EntityId); // its own placeholder, not a candidate since finding 104
        Assert.DoesNotContain(members, m => m.JoinedTo == decision.EntityId);
    }
}
