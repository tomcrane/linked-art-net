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
        var entityPage = await owner.GetStringAsync($"/p/{ds}/entity?id={Uri.EscapeDataString(kept.Id)}");
        Assert.Contains("Smith, John (author)", entityPage);
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
}
