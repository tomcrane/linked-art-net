using System.Text.Json;
using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Import;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class CurationServiceTests(PostgresFixture pg)
{
    private const string UlanSample = """
        <http://vocab.getty.edu/ulan/500301417> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#GroupConcept> .
        <http://vocab.getty.edu/ulan/500301417> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1-en> .
        <http://vocab.getty.edu/ulan/500301417> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1-en> .
        <http://vocab.getty.edu/ulan/term/1-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Thomas Agnew & Sons"@en .
        <http://vocab.getty.edu/ulan/500301417> <http://www.w3.org/2004/02/skos/core#exactMatch> <https://viaf.org/viaf/135146590> .
        """;

    [PostgresFact]
    public async Task Variants_cluster_into_one_entity_with_overrides_ids_and_an_export()
    {
        var ds = "cu-" + IdMinter.Generate(6);
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test");
        var manifest = ProjectManifest.PmcDefault;
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan")], manifest.ToOptions());
        var curation = new CurationService(pg.Store, scheme, reconciler);

        var spellings = new[] { "Thos. Agnew & Sons Ltd", "Agnew's", "Thos Agnew and Sons", "Agnew & Son", "Messrs Agnew" };
        var strings = new List<SourceString>();
        foreach (var s in spellings) strings.Add(await pg.Store.CollectAsync(ds, "corpauthor", s));
        strings.Add(await pg.Store.CollectAsync(ds, "corpauthor", "Agnew, Joan (editor)")); // not the firm
        await reconciler.RunAsync(ds);

        // the variants view: everything containing "agnew", grouped by what it resolves to
        Assert.Equal("agnew", CurationService.SeedWord("Thos. Agnew & Sons Ltd"));
        var groups = await curation.VariantsAsync(ds, "Thos. Agnew & Sons Ltd");
        Assert.Equal(6, groups.Sum(g => g.Strings.Count));

        // cluster the five firm spellings into a new confirmed local authority with the ULAN id attached
        var firm = await curation.ClusterAsync(ds, strings.Take(5).Select(s => s.Id).ToList(), null, "Thomas Agnew & Sons", ["http://vocab.getty.edu/ulan/500301417"], "tester", "London dealers");
        Assert.Equal(EntityStatus.Confirmed, firm.Status);
        Assert.Equal("Thomas Agnew & Sons", firm.Label);
        Assert.Contains("Agnew's", firm.Labels);
        Assert.Contains(firm.Equivalents, q => q.Uri == "http://vocab.getty.edu/ulan/500301417" && q.Method == DecisionMethod.Human);
        foreach (var s in strings.Take(5))
        {
            Assert.Equal(firm.Id, (await pg.Store.ResolveAsync(ds, "corpauthor", s.Raw))!.Id);
            Assert.Equal(DecisionMethod.Human, (await pg.Store.DecisionForAsync(s.Id))!.Method);
            Assert.Equal(firm.Id, (await pg.Store.OverrideForAsync(ds, "corpauthor", s.Normalised))!.Target);
            Assert.Null(await pg.Store.OverrideForAsync(ds, "publisher", s.Normalised)); // scoped to the field the strings came from
        }
        Assert.NotEqual(firm.Id, (await pg.Store.ResolveAsync(ds, "corpauthor", "Agnew, Joan (editor)"))!.Id);
        Assert.Single(await pg.Store.NotesAsync(ds, firm.Id));
        // the same spelling in another field is not caught until the rule is widened to any field
        var twin = await pg.Store.CollectAsync(ds, "publisher", "Agnew's");
        Assert.NotEqual(Outcome.Overridden, await reconciler.EvaluateAsync(ds, twin).ContinueWith(t => t.Result.Outcome));
        await curation.ClusterAsync(ds, [strings[1].Id], firm.Id, null, [], "tester", anyField: true);
        Assert.Equal(Outcome.Overridden, await reconciler.ReconcileOneAsync(ds, twin));

        // the entity page's view, label, identifiers, confirm, reopen
        var view = (await curation.EntityAsync(ds, firm.Id))!;
        Assert.Equal(6, view.Strings.Count);
        Assert.Single(view.Records); // the ULAN record behind the id
        await curation.SetLabelAsync(ds, firm.Id, "Agnew's (Thomas Agnew & Sons)");
        Assert.Contains("Thomas Agnew & Sons", (await pg.Store.GetEntityAsync(firm.Id))!.Labels);
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.AddEquivalentAsync(ds, firm.Id, "http://example.org/nowhere", "tester"));
        await curation.AddEquivalentAsync(ds, firm.Id, "http://viaf.org/viaf/135146590", "tester"); // a recognised namespace needs no cached record
        await curation.RemoveEquivalentAsync(ds, firm.Id, "http://viaf.org/viaf/135146590");
        Assert.Single((await pg.Store.GetEntityAsync(firm.Id))!.Equivalents);
        var reopened = await curation.ReopenAsync(ds, [strings[4].Id]);
        Assert.Equal(1, reopened);
        Assert.Null(await pg.Store.DecisionForAsync(strings[4].Id));
        Assert.Single(await pg.Store.UndecidedAsync(ds));

        // another project cannot touch the entity or its strings
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.SetLabelAsync("other", firm.Id, "x"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => curation.ClusterAsync("other", [strings[0].Id], null, "x", [], "tester"));

        // merge through the curation service, and the export in the pmc-lux shape
        var stray = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Agnew & Sons (stray)"), scheme);
        var merged = await curation.MergeAsync(ds, stray.Id, firm.Id, "tester");
        Assert.Equal(firm.Id, merged.Id);
        var json = await curation.ExportOverridesJsonAsync(ds);
        using var doc = JsonDocument.Parse(json);
        var map = doc.RootElement.GetProperty("local_string_map");
        Assert.Equal(firm.Id.Split('/')[^1], map.GetProperty("Agnew's").GetString());
        Assert.Equal(5, map.EnumerateObject().Count()); // one spelling per entry, whatever fields it is scoped to
        var authority = Assert.Single(doc.RootElement.GetProperty("authorities").EnumerateArray());
        Assert.Equal("500301417", authority.GetProperty("ulan").GetString());
        Assert.Equal("Group", authority.GetProperty("type").GetString());
        // and the exported file imports back as overrides
        var reimport = PmcLuxOverrides.Parse(json);
        Assert.Equal(5, reimport.LocalStringMap.Count);

        // orphan housekeeping: the provisional entities the cluster re-pointed away from were folded in or are gone
        Assert.Equal(0, await pg.Store.RemoveOrphanProvisionalsAsync(ds));
    }
}
