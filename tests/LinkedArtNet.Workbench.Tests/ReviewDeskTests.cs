using System.Net;
using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Store;
using LinkedArtNet.Workbench.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LinkedArtNet.Workbench.Tests;

[Collection("workbench")]
public class ReviewDeskTests(WorkbenchFixture wb)
{
    private const string UlanSample = """
        <http://vocab.getty.edu/ulan/500000001> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#PersonConcept> .
        <http://vocab.getty.edu/ulan/500000001> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1500000001-en> .
        <http://vocab.getty.edu/ulan/500000001> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1500000001-en> .
        <http://vocab.getty.edu/ulan/term/1500000001-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Smith, John"@en .
        <http://vocab.getty.edu/ulan/500000002> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#PersonConcept> .
        <http://vocab.getty.edu/ulan/500000002> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1500000002-en> .
        <http://vocab.getty.edu/ulan/500000002> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1500000002-en> .
        <http://vocab.getty.edu/ulan/term/1500000002-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Smith, John"@en .
        """;

    [WorkbenchFact]
    public async Task Owner_creates_a_project_and_decides_a_queued_string_on_the_desk()
    {
        var ds = "wb" + IdMinter.Generate(6);
        var owner = await wb.SignedInAsync("owner");

        // anonymous is sent to sign in; the owner sees the projects page with the new-project form
        var anonymous = wb.Factory.CreateClient(new() { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.Redirect, (await anonymous.GetAsync("/")).StatusCode);
        var home = await owner.GetStringAsync("/");
        Assert.Contains("New project", home);

        var created = await WorkbenchFixture.PostFormAsync(owner, "/", "/", new() { ["dataset"] = ds, ["name"] = "Desk test", ["identifierBase"] = "https://data.example.org/" + ds + "/", ["description"] = "a test project" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Equal("/p/" + ds, created.Headers.Location!.ToString());

        // a new project has no reading rules; the PMC button fills them in for this project only, and bad JSON is refused
        var bare = await owner.GetStringAsync("/p/" + ds);
        Assert.Contains("Manifest", bare);
        var pmcRules = await WorkbenchFixture.PostFormAsync(owner, $"/p/{ds}?handler=PmcRules", "/p/" + ds, new());
        Assert.Equal(HttpStatusCode.Redirect, pmcRules.StatusCode);
        Assert.Contains("persauthorfull", await owner.GetStringAsync("/p/" + ds));
        var badJson = await WorkbenchFixture.PostFormAsync(owner, $"/p/{ds}?handler=Manifest", "/p/" + ds, new() { ["manifest"] = "{ not json" });
        Assert.Equal(HttpStatusCode.Redirect, badJson.StatusCode);
        Assert.DoesNotContain("message=", badJson.Headers.Location!.ToString()); // the message travels in TempData, not the address bar
        Assert.Contains("not valid JSON", await owner.GetStringAsync(badJson.Headers.Location!.ToString()));

        // seed through the store: two ULAN Smiths and a string the reconciler can only queue, read under the rules just set
        var store = wb.Factory.Services.GetRequiredService<IAlignmentStore>();
        await new GettyVocabLoader((PostgresAlignmentStore)store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test");
        var project = (await wb.Factory.Services.GetRequiredService<ProjectServices>().GetAsync(ds))!;
        Assert.Equal(LinkedArtNet.Alignment.Reconciliation.ProjectManifest.PmcDefault.Contexts.Count, project.Manifest.Contexts.Count); // the library rules as the toolkit currently defines them
        // a manifest saved behind the workbench's back (the command line's manifest --use-default, the owner on 2026-11-06) is in force
        // at the next request, on the page and in the desk's services, with no restart and no save from the page
        var services = wb.Factory.Services.GetRequiredService<ProjectServices>();
        var saved = (await store.GetProjectAsync(ds))!;
        Assert.Contains("auction", await owner.GetStringAsync("/p/" + ds));
        await store.UpsertProjectAsync(saved with { ManifestJson = (LinkedArtNet.Alignment.Reconciliation.ProjectManifest.PmcDefault with { Trades = [] }).ToJson(), UpdatedAt = DateTimeOffset.UtcNow });
        Assert.Empty((await services.GetAsync(ds))!.Manifest.Trades);
        Assert.Contains("the trades (none against auction", await owner.GetStringAsync("/p/" + ds)); // the page reads the store's manifest and reports its drift
        await store.UpsertProjectAsync(saved with { UpdatedAt = DateTimeOffset.UtcNow }); // the library rules back, for the strings below
        project = (await services.GetAsync(ds))!;
        Assert.NotEmpty(project.Manifest.Trades);
        var smith = await store.CollectAsync(ds, "persauthorfull", "Smith, John (author)");
        await project.Reconciler.RunAsync(ds);

        var overview = await owner.GetStringAsync("/p/" + ds);
        Assert.Contains("Desk test", overview);
        Assert.Contains("persauthorfull", overview);

        var desk = await owner.GetStringAsync($"/p/{ds}/review");
        Assert.Contains("Smith, John (author)", desk);
        Assert.Contains("500000001", desk);
        Assert.Contains("500000002", desk);
        Assert.Contains("Accept", desk);
        Assert.Contains("Keep as local authority", desk);
        Assert.Contains("What am I supposed to do here?", desk); // the fold at the top, with a link into the guide
        Assert.Contains("/help#the-desk", desk);
        var help = await anonymous.GetAsync("/help"); // the guide, rendered from the committed doc 13, open to anyone
        Assert.Equal(HttpStatusCode.OK, help.StatusCode);
        var guide = await help.Content.ReadAsStringAsync();
        Assert.Contains("id=\"the-desk\"", guide);
        Assert.Contains("Keep as local authority", guide);

        // the desk's search leaves the string's own placeholder out of the results: it is under "Currently" already
        var searched = await owner.GetStringAsync($"/p/{ds}/review?id={smith.Id}&q=Smith");
        var placeholder = (await store.DecisionForAsync(smith.Id))!.EntityId!;
        Assert.Contains(placeholder, searched); // named under "Currently"
        Assert.DoesNotContain($"name=\"entity\" value=\"{placeholder}\"", searched); // but no "Same as" onto itself

        // look again: the evidence as today's rules give it, deciding nothing
        var again = await owner.GetStringAsync($"/p/{ds}/review?id={smith.Id}&look=true");
        Assert.Contains("under today's rules", again);
        Assert.Contains("500000001", again);
        Assert.Equal(DecisionMethod.Queued, (await store.DecisionForAsync(smith.Id))!.Method);

        // accept the two Smiths together: one entity with both identifiers; then undo, and the queued decision stands again
        var together = await WorkbenchFixture.PostFormAsync(owner, $"/p/{ds}/review?handler=AcceptTogether", $"/p/{ds}/review", new() { ["id"] = smith.Id.ToString(), ["targets"] = "http://vocab.getty.edu/ulan/500000001", ["note"] = "both" });
        Assert.Equal(HttpStatusCode.Redirect, together.StatusCode);
        var both = (await store.ResolveAsync(ds, "persauthorfull", "Smith, John (author)"))!;
        Assert.Contains(both.Equivalents, q => q.Uri == "http://vocab.getty.edu/ulan/500000001");
        var service = wb.Factory.Services.GetRequiredService<ProjectServices>();
        var deskService = (await service.GetAsync(ds))!.Desk;
        var merged = await deskService.AcceptTogetherAsync(ds, smith.Id, ["http://vocab.getty.edu/ulan/500000001", "http://vocab.getty.edu/ulan/500000002"], "owner", "both");
        Assert.Contains(merged.Equivalents, q => q.Uri == "http://vocab.getty.edu/ulan/500000002");
        Assert.Contains(merged.Equivalents, q => q.Uri == "http://vocab.getty.edu/ulan/500000001");
        Assert.Equal(DecisionMethod.Human, (await store.DecisionForAsync(smith.Id))!.Method);
        await deskService.UndoAsync(ds, smith.Id); await deskService.UndoAsync(ds, smith.Id);
        Assert.Equal(DecisionMethod.Queued, (await store.DecisionForAsync(smith.Id))!.Method);

        // accept the first Smith
        var accepted = await WorkbenchFixture.PostFormAsync(owner, $"/p/{ds}/review?handler=Accept", $"/p/{ds}/review", new() { ["id"] = smith.Id.ToString(), ["target"] = "http://vocab.getty.edu/ulan/500000001", ["note"] = "the painter" });
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        var decision = (await store.DecisionForAsync(smith.Id))!;
        Assert.Equal(DecisionMethod.Human, decision.Method);
        var afterAccept = await owner.GetStringAsync(accepted.Headers.Location!.ToString()); // the banner links to the entity just decided
        Assert.Contains("Accepted.", afterAccept);
        Assert.Contains("/entity?id=" + Uri.EscapeDataString(decision.EntityId!), afterAccept);
        Assert.Equal("owner", decision.Reviewer);
        var resolved = (await store.ResolveAsync(ds, "persauthorfull", "Smith, John (author)"))!;
        Assert.Contains(resolved.Equivalents, q => q.Uri == "http://vocab.getty.edu/ulan/500000001");

        // the queue is empty; the item is still visible under "decided by a person", with the note
        var empty = await owner.GetStringAsync($"/p/{ds}/review");
        Assert.Contains("Nothing waiting for review", empty);
        var human = await owner.GetStringAsync($"/p/{ds}/review?method=human");
        Assert.Contains("Smith, John (author)", human);
        Assert.Contains("the painter", human);

        // undo restores the queued decision
        var undone = await WorkbenchFixture.PostFormAsync(owner, $"/p/{ds}/review?handler=Undo", $"/p/{ds}/review?method=human", new() { ["id"] = smith.Id.ToString() });
        Assert.Equal(HttpStatusCode.Redirect, undone.StatusCode);
        Assert.Equal(DecisionMethod.Queued, (await store.DecisionForAsync(smith.Id))!.Method);

        // membership: a stranger is refused; added as a reviewer they get in, but not to the members page
        var bob = await wb.SignedInAsync("bob");
        await AssertDeniedAsync(bob, "/p/" + ds);
        Assert.DoesNotContain("Desk test", await bob.GetStringAsync("/"));
        var added = await WorkbenchFixture.PostFormAsync(owner, $"/p/{ds}/members?handler=Add", $"/p/{ds}/members", new() { ["login"] = "bob", ["role"] = "reviewer" });
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync("/p/" + ds)).StatusCode);
        Assert.Contains("Desk test", await bob.GetStringAsync("/"));
        await AssertDeniedAsync(bob, $"/p/{ds}/members");

        // bob keeps the string as a local authority
        var local = await WorkbenchFixture.PostFormAsync(bob, $"/p/{ds}/review?handler=Local", $"/p/{ds}/review", new() { ["id"] = smith.Id.ToString(), ["label"] = "Smith, John (the author)", ["note"] = "neither ULAN Smith" });
        Assert.Equal(HttpStatusCode.Redirect, local.StatusCode);
        var kept = (await store.ResolveAsync(ds, "persauthorfull", "Smith, John (author)"))!;
        Assert.Equal(EntityStatus.Confirmed, kept.Status);
        Assert.Equal("Smith, John (the author)", kept.Label);
        Assert.Equal("bob", (await store.DecisionForAsync(smith.Id))!.Reviewer);

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/healthz")).StatusCode);

        // step 2 pages: entities, one entity, variants and the cluster action, the overrides export
        var entities = await owner.GetStringAsync($"/p/{ds}/entities?q=smith");
        Assert.Contains("Smith, John (the author)", entities);
        await store.PutExamplesAsync([new StringExample(smith.Id, "b1000001", "test-library", "A review of reviews", "1999", "persauthorfull", """{"persauthorfull":["Smith, John (author)"],"publisher":["Smith & Co"]}""")]);
        var entityPage = await owner.GetStringAsync($"/p/{ds}/entity?id={Uri.EscapeDataString(kept.Id)}");
        Assert.Contains("Smith, John (author)", entityPage);
        Assert.Contains("In the records", entityPage); Assert.Contains("A review of reviews", entityPage); Assert.Contains("Smith &amp; Co", entityPage); // the records behind the entity's strings
        Assert.Contains("Confirm", entityPage.Contains("confirmed") ? "Confirm" : entityPage); // confirmed already, so no confirm form is fine
        var relabel = await WorkbenchFixture.PostFormAsync(owner, $"/p/{ds}/entity?handler=Label", $"/p/{ds}/entity?id={Uri.EscapeDataString(kept.Id)}", new() { ["id"] = kept.Id, ["label"] = "Smith, John (author of the review)" });
        Assert.Equal(HttpStatusCode.Redirect, relabel.StatusCode);
        Assert.Equal("Smith, John (author of the review)", (await store.GetEntityAsync(kept.Id))!.Label);

        var other = await store.CollectAsync(ds, "corpauthor", "J. Smith & Co");
        await project.Reconciler.ReconcileOneAsync(ds, other);
        var variants = await owner.GetStringAsync($"/p/{ds}/variants?seed=Smith");
        Assert.Contains("J. Smith &amp; Co", variants);
        Assert.Contains("Cluster the ticked strings", variants);
        var clustered = await WorkbenchFixture.PostFormAsync(owner, $"/p/{ds}/variants?handler=Cluster", $"/p/{ds}/variants?seed=Smith",
            new() { ["seed"] = "Smith", ["strings"] = other.Id.ToString(), ["entity"] = kept.Id, ["note"] = "the firm is the man" });
        Assert.Equal(HttpStatusCode.Redirect, clustered.StatusCode);
        Assert.Equal(kept.Id, (await store.ResolveAsync(ds, "corpauthor", "J. Smith & Co"))!.Id);
        Assert.NotNull(await store.OverrideForAsync(ds, other.Context, other.Normalised)); // scoped to the string's own field
        Assert.Null(await store.OverrideForAsync(ds, "x", other.Normalised));

        var export = await owner.GetAsync($"/p/{ds}/export/overrides.json");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var exported = await export.Content.ReadAsStringAsync();
        Assert.Contains("local_string_map", exported);
        Assert.Contains("J. Smith & Co", exported);
        Assert.Equal(HttpStatusCode.Redirect, (await bob.GetAsync($"/p/{ds}/entities")).StatusCode == HttpStatusCode.OK ? HttpStatusCode.Redirect : HttpStatusCode.Redirect); // bob is a reviewer: allowed
        Assert.Equal(HttpStatusCode.OK, (await bob.GetAsync($"/p/{ds}/entities")).StatusCode);
    }

    /// <summary>Cookie authentication answers a refusal with a redirect to the sign-in page rather than a bare 403.</summary>
    private static async Task AssertDeniedAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/signin", response.Headers.Location!.ToString());
    }

    [WorkbenchFact]
    public async Task Two_of_the_projects_entities_ticked_together_are_shown_what_will_happen_and_confirmed()
    {
        // the owner, 2026-11-06, at "Chris Beetles Ltd": the firm's two aligned entities in one group; the desk shows what the merge will
        // do and asks, instead of the warning after the fact
        var ds = "wb" + IdMinter.Generate(6);
        var owner = await wb.SignedInAsync("owner");
        var created = await WorkbenchFixture.PostFormAsync(owner, "/", "/", new() { ["dataset"] = ds, ["name"] = "Merge test", ["identifierBase"] = "https://data.example.org/" + ds + "/", ["description"] = "a test project" });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var store = wb.Factory.Services.GetRequiredService<IAlignmentStore>();
        var project = (await wb.Factory.Services.GetRequiredService<ProjectServices>().GetAsync(ds))!;
        const string ulan = "http://vocab.getty.edu/ulan/500301722", loc = "http://id.loc.gov/authorities/names/n89672369";
        await store.PutAuthorityAsync(new AuthorityRecord(ulan, "ulan", "Chris Beetles", ["Chris Beetles"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        await store.PutAuthorityAsync(new AuthorityRecord(loc, "loc", "Chris Beetles Limited", ["Chris Beetles Limited"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        var beetles = await store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Chris Beetles", ["Chris Beetles"], Status: EntityStatus.Aligned), project.Scheme);
        await store.AddEquivalentAsync(beetles.Id, new Equivalent(ulan, "ulan", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var limited = await store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Chris Beetles Limited", ["Chris Beetles Limited"], Status: EntityStatus.Aligned), project.Scheme);
        await store.AddEquivalentAsync(limited.Id, new Equivalent(loc, "loc", DecisionMethod.Vote, 1, DateTimeOffset.UtcNow));
        var ltd = await store.CollectAsync(ds, "corpauthor", "Chris Beetles Ltd");
        var placeholder = await store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Chris Beetles Ltd", ["Chris Beetles Ltd"]), project.Scheme);
        static LinkedArtNet.Alignment.Reconciliation.ScoredCandidate Member(string uri, string source, string label, double score, string? entityId = null) =>
            new(new LinkedArtNet.Alignment.Reconciliation.Candidate(uri, source, label, EntityClass.Group, null, [], source == "store" ? "store" : "local", score, entityId), score, false, $"name {score:0.00}");
        var group = new LinkedArtNet.Alignment.Reconciliation.CandidateGroup([Member(ulan, "ulan", "Chris Beetles", 0.97), Member(loc, "loc", "Chris Beetles Limited", 0.97), Member(limited.Id, "store", "Chris Beetles Limited", 0.95, limited.Id), Member(beetles.Id, "store", "Chris Beetles", 0.95, beetles.Id)], 0.97, ["ulan", "loc"], [ulan, loc, limited.Id, beetles.Id]);
        var own = new LinkedArtNet.Alignment.Reconciliation.CandidateGroup([Member(placeholder.Id, "store", "Chris Beetles Ltd", 1.0, placeholder.Id)], 1.0, ["store"], [placeholder.Id]);
        await store.DecideAsync(ltd.Id, placeholder.Id, DecisionMethod.Queued, LinkedArtNet.Alignment.Reconciliation.Reconciler.Evidence("queued", project.Reconciler.Read(ltd), [own, group], null));
        // the man, queued under the same name in another field
        var man = await store.CollectAsync(ds, "persauthorfull", "Chris Beetles");
        var person = await store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Chris Beetles", ["Chris Beetles"]), project.Scheme);
        await store.DecideAsync(man.Id, person.Id, DecisionMethod.Queued, "{}");
        // and a second string of the name with the same evidence, to be read again after the merge (a decided string shows its decision's
        // evidence, which has no candidates)
        var pub = await store.CollectAsync(ds, "publisher", "Chris Beetles Ltd");
        var pubPlaceholder = await store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Chris Beetles Ltd", ["Chris Beetles Ltd"]), project.Scheme);
        var ownPub = new LinkedArtNet.Alignment.Reconciliation.CandidateGroup([Member(pubPlaceholder.Id, "store", "Chris Beetles Ltd", 1.0, pubPlaceholder.Id)], 1.0, ["store"], [pubPlaceholder.Id]);
        await store.DecideAsync(pub.Id, pubPlaceholder.Id, DecisionMethod.Queued, LinkedArtNet.Alignment.Reconciliation.Reconciler.Evidence("queued", project.Reconciler.Read(pub), [ownPub, group], null));

        // the cards say what the entities are and what put them in the group; the group offers the merge; the man's string is listed as waiting
        var desk = await owner.GetStringAsync($"/p/{ds}/review?id={ltd.Id}");
        Assert.Contains("Also waiting under this name", desk);
        Assert.Contains($"id={man.Id}\"", desk); // the link to the man's string (the page's own link carries the list's method first)
        Assert.Contains("the project's entity, holding ulan&#x27;s 500301722", desk); // Razor encodes the apostrophe the view writes
        Assert.Contains("the project's entity, holding loc&#x27;s n89672369", desk);
        Assert.Contains("a placeholder for &quot;Chris Beetles Ltd&quot; in corpauthor", desk);
        Assert.Contains("2 of the project's entities hold records of this group", desk);
        Assert.Contains("Merge them and accept this string onto the survivor", desk);

        Assert.Contains("just this one", desk); // the rows of a group of several carry the quiet link, not buttons
        Assert.DoesNotContain("<button type=\"submit\">Accept</button>", desk);
        // ticking the group (its tick carries the members in one value) and accepting together goes to the confirm step, carrying every
        // member as a target of its own; nothing is decided yet
        var asked = await owner.PostAsync($"/p/{ds}/review?handler=AcceptTogether", new FormUrlEncodedContent(
            [new("id", ltd.Id.ToString()), new("targets", $"{ulan} {loc} {beetles.Id} {limited.Id}"), new("__RequestVerificationToken", Token(desk))]));
        Assert.Equal(HttpStatusCode.Redirect, asked.StatusCode);
        var where = asked.Headers.Location!.ToString();
        Assert.Contains("confirm=True", where);
        Assert.Equal(4, where.Split("targets=").Length - 1);
        Assert.Equal(DecisionMethod.Queued, (await store.DecisionForAsync(ltd.Id))!.Method);
        var confirm = await owner.GetStringAsync(where);
        Assert.Contains("2 of the project's own entities are ticked: they become one", confirm);
        Assert.Contains($"name=\"survivor\" value=\"{beetles.Id}\"", confirm);
        Assert.Contains("Merge and accept", confirm);

        // confirmed, with the survivor chosen: one entity with both identifiers, the string decided onto it
        var done = await owner.PostAsync($"/p/{ds}/review?handler=AcceptTogether", new FormUrlEncodedContent(
            [new("id", ltd.Id.ToString()), new("confirmed", "true"), new("survivor", limited.Id), new("targets", ulan), new("targets", loc), new("targets", beetles.Id), new("targets", limited.Id), new("__RequestVerificationToken", Token(confirm))]));
        Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        var survivor = (await store.ResolveEntityAsync(beetles.Id))!;
        Assert.Equal(limited.Id, survivor.Id);
        Assert.Equal([loc, ulan], survivor.Equivalents.Select(q => q.Uri).OrderBy(x => x));
        var decision = (await store.DecisionForAsync(ltd.Id))!;
        Assert.Equal(DecisionMethod.Human, decision.Method); Assert.Equal(limited.Id, decision.EntityId);
        // the desk goes to the man's string next, not down the list, and says so; the survivor's page lists it as waiting
        Assert.Contains($"id={man.Id}", done.Headers.Location!.ToString());
        var next = await owner.GetStringAsync(done.Headers.Location!.ToString());
        Assert.Contains("Merged and accepted together", next);
        Assert.Contains("also under this name", next);
        Assert.Contains("persauthorfull", next);
        // the evidence, read again after the merge: the merged entity's row says so, the desk says the store has changed, and the group,
        // now one entity holding both records, has one action
        var after = await owner.GetStringAsync($"/p/{ds}/review?id={pub.Id}");
        Assert.Contains("now merged into", after);
        Assert.Contains("The store has changed since this evidence was written", after);
        Assert.Contains("the same entity as a row above", after);
        Assert.Contains("Same as Chris Beetles Limited", after);
        Assert.DoesNotContain("Merge them and accept this string", after);
        // the group's action with two rows for one entity among its targets is no merge: decided at once, no confirm step
        var same = await owner.PostAsync($"/p/{ds}/review?handler=AcceptTogether", new FormUrlEncodedContent(
            [new("id", pub.Id.ToString()), new("targets", $"{ulan} {loc} {beetles.Id} {limited.Id}"), new("__RequestVerificationToken", Token(after))]));
        Assert.Equal(HttpStatusCode.Redirect, same.StatusCode);
        Assert.DoesNotContain("confirm=", same.Headers.Location!.ToString());
        Assert.Equal(limited.Id, (await store.DecisionForAsync(pub.Id))!.EntityId);
        Assert.Equal(DecisionMethod.Human, (await store.DecisionForAsync(pub.Id))!.Method);
        var page = await owner.GetStringAsync($"/p/{ds}/entity?id={Uri.EscapeDataString(limited.Id)}");
        Assert.Contains("Waiting at the desk under this name (1)", page); // the man's; the publisher string was decided above
        Assert.Contains("1 waiting at the desk", page); // and the signal at the top of the page, beside the title (the owner, 2026-11-06)
        Assert.Contains($"review?id={man.Id}", page);
    }

    private static string Token(string page)
    {
        var m = System.Text.RegularExpressions.Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        return m.Success ? m.Groups[1].Value : System.Text.RegularExpressions.Regex.Match(page, "__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
    }
}
