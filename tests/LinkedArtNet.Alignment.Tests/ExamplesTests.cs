using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Import;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class ExamplesTests(PostgresFixture pg)
{
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
        }
        finally { File.Delete(path); File.Delete(dump); }
    }
}
