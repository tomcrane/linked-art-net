using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Import;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class ExamplesTests(PostgresFixture pg)
{
    [PostgresFact]
    public async Task A_publishers_place_is_hinted_from_the_table_the_projects_records_and_its_entity()
    {
        // the owner, 2026-11-15, at "Cambridge": 276 of the string's 525 records are Cambridge University Press, four are American
        Assert.Equal("Cambridge, England", PublisherPlaces.Find("Cambridge University Press")!.Place);
        Assert.Equal("http://vocab.getty.edu/tgn/7013527", PublisherPlaces.Find("the MIT press")!.Uri);
        Assert.Equal("Stanford, California", PublisherPlaces.Find("Stanford University Press")!.Place);
        Assert.Null(PublisherPlaces.Find("Yale University Press")); // two offices: absent until an entry can carry several
        Assert.Contains("general knowledge", PublisherPlaces.Notes);

        var ds = "ph" + Guid.NewGuid().ToString("N")[..6];
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        var cambridge = await pg.Store.CollectAsync(ds, "place", "Cambridge");
        var cupString = await pg.Store.CollectAsync(ds, "publisher", "Cambridge University Press");
        var cup = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Group, "Cambridge University Press"), scheme);
        await pg.Store.DecideAsync(cupString.Id, cup.Id, DecisionMethod.Human, null, "tester");
        static string Fields(string place, string publisher) => $"{{\"place\":[\"{place}\"],\"publisher\":[\"{publisher}\"]}}";
        await pg.Store.PutRecordsAsync(ds, "c1", [new SourceRecord("library", "R3", "Elsewhere", "2001", Fields("Cambridge [England]; New York", "Cambridge University Press"), null)]);
        await pg.Store.PutExamplesAsync([
            new StringExample(cambridge.Id, "R1", "library", "Ten books", "1999", "place", Fields("Cambridge", "Cambridge University Press")),
            new StringExample(cambridge.Id, "R2", "library", "Digital", "2012", "place", Fields("Cambridge", "Kettle's Yard"))]);
        var desk = new ReviewService(pg.Store, scheme, new Reconciler(pg.Store, scheme, [], ProjectManifest.PmcDefault.ToOptions()));
        var recs = await desk.RecordsForAsync(cambridge, null, ProjectManifest.PmcDefault);
        Assert.Equal("publisher", recs.By);
        var hints = recs.Hints["Cambridge University Press"];
        Assert.Equal(["table", "records", "entity"], hints.Select(h => h.From));
        Assert.Equal("Cambridge, England", hints[0].Place); Assert.Equal("http://vocab.getty.edu/tgn/7010874", hints[0].Uri);
        Assert.Equal("Cambridge [England]", hints[1].Place); Assert.Contains("1 record", hints[1].Text);
        Assert.Null(hints[2].Place); Assert.Equal(cup.Id, hints[2].EntityId);
        Assert.False(recs.Hints.ContainsKey("Kettle's Yard")); // nothing known: no hint
    }

    // four Heritage records in the export's shape: three name Langford's auction house (two kept at two per string), one is Hodgkin's
    // catalogue with a publisher, a place, keywords and a compound keyword; the notes' (HIE) place is the hierarchical one
    private const string Xml = """
        <?xml version="1.0"?>
        <dataexport xmlns="x-schema:EF-34074-Export.dtd" file="MAIN_CAT">
          <record ID="0951249363">
            <stdno>0951249363</stdno>
            <title>Eliot Hodgkin 1905-1987 : painter and collector</title>
            <persauthorfull>Hodgkin, Eliot (author)</persauthorfull>
            <corpauthor>Hazlitt, Gooden &amp; Fox.</corpauthor>
            <medium>Text</medium>
            <place>London</place>
            <publisher>Hazlitt, Gooden &amp; Fox</publisher>
            <year>c1990</year>
            <keywords>Hodgkin, Eliot, 1905-1987</keywords>
            <keywords>Paintings</keywords>
            <keywords>Hodgkin, Eliot, 1905-1987 -- Catalogs</keywords>
            <keywords>Arts, Modern-20th century-Scotland</keywords>
            <notescsvx>(ACN) ||(GEN) Catalogue of an exhibition, 14 March to 10 April 1990.</notescsvx>
          </record>
          <record ID="L1">
            <stdno>L1</stdno>
            <title>A catalogue of the genuine and capital collection of pictures ... which will be sold by auction by Messrs Langford</title>
            <persauthorfull></persauthorfull>
            <corpauthor>Messrs Langford</corpauthor>
            <medium>Text</medium>
            <place>[London]</place>
            <publisher></publisher>
            <year>1760</year>
            <notescsvx>(HIE) London, England||(GEN) Sale catalogue.</notescsvx>
          </record>
          <record ID="L2">
            <stdno>L2</stdno>
            <title>A catalogue of the ... prints and drawings ... sold by auction by Messrs Langford</title>
            <corpauthor>Messrs Langford</corpauthor>
            <medium>Text</medium>
            <year>1761</year>
          </record>
          <record ID="L3">
            <stdno>L3</stdno>
            <title>A third Langford sale</title>
            <corpauthor>Messrs Langford</corpauthor>
            <medium>Text</medium>
            <year>1762</year>
            <series>Les contemporains : BADBYTEuvres et portraits</series>
          </record>
        </dataexport>
        """;

    [PostgresFact]
    public async Task The_records_behind_a_string_are_collected_from_the_library_export()
    {
        var ds = "ex" + Guid.NewGuid().ToString("N")[..6];
        var langford = await pg.Store.CollectAsync(ds, "corpauthor", "Messrs Langford");
        var hodgkin = await pg.Store.CollectAsync(ds, "persauthorfull", "Hodgkin, Eliot (author)");
        var publisher = await pg.Store.CollectAsync(ds, "publisher", "Hazlitt, Gooden & Fox");
        var london = await pg.Store.CollectAsync(ds, "place", "London");
        var londonEngland = await pg.Store.CollectAsync(ds, "place", "London, England");
        var paintings = await pg.Store.CollectAsync(ds, "keywords", "Paintings");
        var compound = await pg.Store.CollectAsync(ds, "keywords", "Hodgkin, Eliot, 1905-1987 -- Catalogs");
        var head = await pg.Store.CollectAsync(ds, "keywords:head", "Hodgkin, Eliot, 1905-1987");
        var cleaned = await pg.Store.CollectAsync(ds, "keywords:sub", "Exhibition catalogues"); // a cleaned form the export does not carry
        var modern = await pg.Store.CollectAsync(ds, "keywords", "Arts, Modern--20th century--Scotland");   // the 2024 process's cleaned form of a raw keyword
        var modernHead = await pg.Store.CollectAsync(ds, "keywords:head", "Arts, Modern");
        var scotland = await pg.Store.CollectAsync(ds, "keywords:sub", "Scotland");
        await pg.Store.LinkPartAsync(compound.Id, 0, head.Id);
        await pg.Store.LinkPartAsync(compound.Id, 1, cleaned.Id);
        var unseen = await pg.Store.CollectAsync(ds, "corpauthor", "Nobody at All");

        var path = Path.Combine(Path.GetTempPath(), ds + ".xml");
        var dump = Path.Combine(Path.GetTempPath(), ds + ".sql");
        await File.WriteAllTextAsync(dump, """
            INSERT INTO public.pmc_subjects_csv (record_id, record_keywords, keywords_cleaned, aat, tgn, ulan) VALUES ('0951249363', 'Arts, Modern-20th century-Scotland', 'Arts, Modern--20th century--Scotland', null, null, null);
            INSERT INTO public.pmc_subjects_csv (record_id, record_keywords, keywords_cleaned, aat, tgn, ulan) VALUES ('X', 'O''Neill, Hugh-Biography', 'O''Neill, Hugh--Biography', null, null, '500000001');
            """);
        var map = HeritageExampleCollector.ReadCleanedKeywords(dump);
        Assert.Equal("Arts, Modern--20th century--Scotland", map["Arts, Modern-20th century-Scotland"]);
        Assert.Equal("O'Neill, Hugh--Biography", map["O'Neill, Hugh-Biography"]); // the doubled quote undone
        // the March 2024 export carries one byte that is not UTF-8 (0x9D in a series field); the collector reads past it
        var bytes = System.Text.Encoding.UTF8.GetBytes(Xml);
        var marker = System.Text.Encoding.UTF8.GetBytes("BADBYTE");
        var at = bytes.AsSpan().IndexOf(marker);
        Assert.True(at > 0);
        await File.WriteAllBytesAsync(path, [.. bytes[..at], (byte)0x9D, .. bytes[(at + marker.Length)..]]);
        try
        {
            var report = await new HeritageExampleCollector(pg.Store, map).CollectAsync(ds, path, "test-export", perString: 2);
            Assert.Equal(4, report.Records);
            Assert.Equal((1, 2), report.ByContext["corpauthor"]); // Langford covered, Nobody not
            Assert.Equal((2, 2), report.ByContext["place"]);     // the element and the notes' hierarchical place

            var langfords = await pg.Store.ExamplesForAsync(langford.Id, 3);
            Assert.Equal(2, langfords.Count); // three records name the house; two kept
            Assert.All(langfords, e => { Assert.Equal("corpauthor", e.Field); Assert.Equal("test-export", e.Source); });
            Assert.Contains(langfords, e => e.RecordId == "L1" && e.Date == "1760" && e.Title!.Contains("sold by auction"));
            var (name, values) = langfords.First(e => e.RecordId == "L1").NamingFields().First();
            Assert.Equal("corpauthor", name); Assert.Equal(["Messrs Langford"], values); // the string's own field leads the card

            var h = Assert.Single(await pg.Store.ExamplesForAsync(hodgkin.Id, 3));
            Assert.Equal("0951249363", h.RecordId); Assert.Equal("c1990", h.Date); Assert.Equal("persauthorfull", h.Field);
            Assert.Single(await pg.Store.ExamplesForAsync(publisher.Id, 3));
            Assert.Equal(2, (await pg.Store.ExamplesForAsync(london.Id, 3)).Count); // Hodgkin's "London" and Langford's "[London]", the brackets trimmed
            Assert.Single(await pg.Store.ExamplesForAsync(londonEngland.Id, 3)); // the (HIE) place
            Assert.Single(await pg.Store.ExamplesForAsync(paintings.Id, 3));
            Assert.Single(await pg.Store.ExamplesForAsync(head.Id, 3));          // the head of the compound keyword, matched as a part
            Assert.Empty(await pg.Store.ExamplesForAsync(cleaned.Id, 3));
            Assert.Single(await pg.Store.ExamplesForAsync(modern.Id, 3));     // reached through the cleaned form
            Assert.Single(await pg.Store.ExamplesForAsync(modernHead.Id, 3)); // and its parts
            Assert.Single(await pg.Store.ExamplesForAsync(scotland.Id, 3));
            Assert.Empty(await pg.Store.ExamplesForAsync(unseen.Id, 3));

            // the desk: a part with no examples of its own shows its parents'
            var desk = new ReviewService(pg.Store, new IdentifierScheme("https://data.example.org/" + ds), new Reconciler(pg.Store, new IdentifierScheme("https://data.example.org/" + ds), [], ProjectManifest.PmcDefault.ToOptions()));
            var viaParent = await desk.ExamplesForAsync(cleaned.Id);
            Assert.Equal("0951249363", Assert.Single(viaParent).RecordId);

            // collected again, the source's examples are replaced, not doubled
            Assert.Equal(13, await pg.Store.ClearExamplesAsync(ds, "test-export"));
            Assert.Empty(await pg.Store.ExamplesForAsync(langford.Id, 3));
            var again = await new HeritageExampleCollector(pg.Store, map).CollectAsync(ds, path, "test-export", perString: 3);
            Assert.Equal(3, (await pg.Store.ExamplesForAsync(langford.Id, 3)).Count);
            Assert.Equal(14, again.Examples);

            // records and occurrences (doc 12 section 14.1 and 14.2): every record has its row, whether or not a string of the store's
            // is in it; a string's records are counted and grouped by a field; a part knows the whole value it is a part of
            var systems = Assert.Single(await pg.Store.RecordSystemsAsync(ds));
            Assert.Equal(("test-export", 4, 0), (systems.System, systems.Records, systems.Gone));
            Assert.Equal(3, await pg.Store.RecordCountAsync(langford.Id));
            var groups = await pg.Store.RecordGroupsAsync(langford.Id, "year", groups: 5, perGroup: 2);
            Assert.Equal(3, groups.Sum(g => g.Records));
            Assert.All(groups, g => Assert.InRange(g.First.Count, 1, 2));
            Assert.Equal(modern.Id, Assert.Single(await pg.Store.ExamplesForAsync(modernHead.Id, 3)).ParentStringId);
            Assert.Null(Assert.Single(await pg.Store.ExamplesForAsync(modern.Id, 3)).ParentStringId);
            // a later export without a record: the record is marked gone and kept
            var fewer = path + ".fewer.xml";
            var xml = await File.ReadAllTextAsync(path, System.Text.Encoding.Latin1);
            var cut = xml.IndexOf("<record ID=\"L1\"", StringComparison.Ordinal);
            var end = xml.IndexOf("</record>", cut, StringComparison.Ordinal) + "</record>".Length;
            await File.WriteAllTextAsync(fewer, xml[..cut] + xml[end..], System.Text.Encoding.Latin1);
            try
            {
                await pg.Store.ClearExamplesAsync(ds, "test-export");
                var collector = new HeritageExampleCollector(pg.Store, map);
                var later = await collector.CollectAsync(ds, fewer, "test-export", collect: "later");
                Assert.Equal(3, later.Records); Assert.Equal(1, collector.Gone);
                var after = Assert.Single(await pg.Store.RecordSystemsAsync(ds));
                Assert.Equal((4, 1, "later"), (after.Records, after.Gone, after.LastCollect));
                Assert.Equal(2, await pg.Store.RecordCountAsync(langford.Id));
            }
            finally { File.Delete(fewer); }
        }
        finally { File.Delete(path); File.Delete(dump); }
    }
}
