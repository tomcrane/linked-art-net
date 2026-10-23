using LinkedArtNet.Alignment.Authorities;

namespace LinkedArtNet.Alignment.Tests;

[Collection("postgres")]
public class WikidataLoaderTests(PostgresFixture pg)
{
    // a sliver of the truthy dump in its shapes (checked 2026-10-23): a painter with VIAF, LoC and ULAN ids, dates, labels in several
    // languages and an alias; a museum with an inception; an engraver dated only by a work period; an item with no identifier, not kept
    private const string Sample = """
        <https://www.wikidata.org/wiki/Special:EntityData/Q5580> <http://schema.org/about> <http://www.wikidata.org/entity/Q5580> .
        <http://www.wikidata.org/entity/Q5580> <http://www.wikidata.org/prop/direct/P31> <http://www.wikidata.org/entity/Q5> .
        <http://www.wikidata.org/entity/Q5580> <http://www.wikidata.org/prop/direct/P214> "54146999" .
        <http://www.wikidata.org/entity/Q5580> <http://www.wikidata.org/prop/direct-normalized/P214> <http://viaf.org/viaf/54146999> .
        <http://www.wikidata.org/entity/Q5580> <http://www.wikidata.org/prop/direct/P244> "n79118011" .
        <http://www.wikidata.org/entity/Q5580> <http://www.wikidata.org/prop/direct/P245> "500115493" .
        <http://www.wikidata.org/entity/Q5580> <http://www.wikidata.org/prop/direct/P569> "1471-05-21T00:00:00Z"^^<http://www.w3.org/2001/XMLSchema#dateTime> .
        <http://www.wikidata.org/entity/Q5580> <http://www.wikidata.org/prop/direct/P570> "1528-04-06T00:00:00Z"^^<http://www.w3.org/2001/XMLSchema#dateTime> .
        <http://www.wikidata.org/entity/Q5580> <http://www.w3.org/2000/01/rdf-schema#label> "Albrecht Dürer"@de .
        <http://www.wikidata.org/entity/Q5580> <http://www.w3.org/2000/01/rdf-schema#label> "Albrecht Dürer"@en .
        <http://www.wikidata.org/entity/Q5580> <http://www.w3.org/2000/01/rdf-schema#label> "Альбрехт Дюрер"@ru .
        <http://www.wikidata.org/entity/Q5580> <http://www.w3.org/2004/02/skos/core#altLabel> "Dürer"@en .
        <http://www.wikidata.org/entity/Q5580> <http://www.w3.org/2004/02/skos/core#altLabel> "Albrecht Duerer"@de .
        <http://www.wikidata.org/entity/Q5580> <http://schema.org/description> "German painter, printmaker and theorist (1471-1528)"@en .
        <http://www.wikidata.org/entity/Q5580> <http://schema.org/description> "deutscher Maler"@de .
        <https://www.wikidata.org/wiki/Special:EntityData/Q180788> <http://schema.org/about> <http://www.wikidata.org/entity/Q180788> .
        <http://www.wikidata.org/entity/Q180788> <http://www.wikidata.org/prop/direct/P31> <http://www.wikidata.org/entity/Q207694> .
        <http://www.wikidata.org/entity/Q180788> <http://www.wikidata.org/prop/direct/P244> "n79065616" .
        <http://www.wikidata.org/entity/Q180788> <http://www.wikidata.org/prop/direct/P571> "1824-01-01T00:00:00Z"^^<http://www.w3.org/2001/XMLSchema#dateTime> .
        <http://www.wikidata.org/entity/Q180788> <http://www.w3.org/2000/01/rdf-schema#label> "National Gallery"@en .
        <http://www.wikidata.org/entity/Q180788> <http://www.w3.org/2000/01/rdf-schema#label> "National Gallery"@mul .
        <http://www.wikidata.org/entity/Q180788> <http://www.w3.org/2004/02/skos/core#altLabel> "National Gallery, London"@en-gb .
        <https://www.wikidata.org/wiki/Special:EntityData/Q99999901> <http://schema.org/about> <http://www.wikidata.org/entity/Q99999901> .
        <http://www.wikidata.org/entity/Q99999901> <http://www.wikidata.org/prop/direct/P31> <http://www.wikidata.org/entity/Q5> .
        <http://www.wikidata.org/entity/Q99999901> <http://www.wikidata.org/prop/direct/P1667> "7011781" .
        <http://www.wikidata.org/entity/Q99999901> <http://www.wikidata.org/prop/direct/P2031> "1773-01-01T00:00:00Z"^^<http://www.w3.org/2001/XMLSchema#dateTime> .
        <http://www.wikidata.org/entity/Q99999901> <http://www.wikidata.org/prop/direct/P2032> "1789-01-01T00:00:00Z"^^<http://www.w3.org/2001/XMLSchema#dateTime> .
        <http://www.wikidata.org/entity/Q99999901> <http://www.w3.org/2000/01/rdf-schema#label> "John Royce"@en .
        <https://www.wikidata.org/wiki/Special:EntityData/Q99999902> <http://schema.org/about> <http://www.wikidata.org/entity/Q99999902> .
        <http://www.wikidata.org/entity/Q99999902> <http://www.wikidata.org/prop/direct/P31> <http://www.wikidata.org/entity/Q5> .
        <http://www.wikidata.org/entity/Q99999902> <http://www.w3.org/2000/01/rdf-schema#label> "Nobody Kept"@en .
        <http://www.wikidata.org/entity/P214> <http://www.w3.org/2000/01/rdf-schema#label> "VIAF ID"@en .
        """;

    private const string Sitelinks = """
        -- MySQL dump
        INSERT INTO `wb_items_per_site` VALUES (1,5580,'enwiki','Albrecht Dürer'),(2,5580,'dewiki','Albrecht Dürer'),(3,5580,'frwiki','Albrecht Dürer'),(4,180788,'enwiki','National Gallery'),(5,99999902,'enwiki','Nobody');
        INSERT INTO `wb_items_per_site` VALUES (6,5580,'itwiki','Albrecht Dürer');
        """;

    [Fact]
    public void The_class_table_reads_the_settlement_and_body_types_the_first_load_left_untyped()
    {
        // the forty most frequent untyped instance-of values of the 2026-10-23 load, named through the Wikidata API: communes, Ortsteile,
        // lakes and streams are places; parties, clubs and learned societies are bodies; a conference is an event; works stay untyped
        Assert.Equal("Place", WikidataLoader.ClassOf(["Q484170"]));           // commune of France
        Assert.Equal("Place", WikidataLoader.ClassOf(["Q253019"]));           // Ortsteil
        Assert.Equal("Place", WikidataLoader.ClassOf(["Q23397", "Q47521"]));  // lake, stream
        Assert.Equal("Group", WikidataLoader.ClassOf(["Q955824"]));           // learned society
        Assert.Equal("Group", WikidataLoader.ClassOf(["Q47258130"]));         // academic conference series: a conference heading, as LoC has it
        Assert.Equal("Event", WikidataLoader.ClassOf(["Q2020153"]));          // academic conference
        Assert.Equal("Language", WikidataLoader.ClassOf(["Q34770"]));
        Assert.Equal("Place", WikidataLoader.ClassOf(["Q317557"]));           // parish church: a building is a place, as the project's houses are
        Assert.Equal("Group", WikidataLoader.ClassOf(["Q131186"]));           // choir
        Assert.Equal("Material", WikidataLoader.ClassOf(["Q113145171"]));     // type of chemical entity
        Assert.Null(WikidataLoader.ClassOf(["Q7725634", "Q11424"]));          // literary work, film
        Assert.Equal("Person", WikidataLoader.ClassOf(["Q7725634", "Q5"]));   // the first known class wins, whatever precedes it
    }

    [Fact]
    public void Dump_dates_give_years_and_sitelink_rows_count_per_kept_item()
    {
        Assert.Equal("1952", WikidataLoader.Year("1952-03-11T00:00:00Z"));
        Assert.Equal("1700", WikidataLoader.Year("1700-01-01T00:00:00Z"));
        Assert.Equal("-0500", WikidataLoader.Year("-0500-01-01T00:00:00Z"));
        Assert.Null(WikidataLoader.Year("not a date"));
        var counts = WikidataLoader.CountSitelinks(new StringReader(Sitelinks), [5580, 180788], out var rows);
        Assert.Equal(6, rows);
        Assert.Equal(4, counts[5580]); Assert.Equal(1, counts[180788]); Assert.False(counts.ContainsKey(99999902));
    }

    [PostgresFact]
    public async Task Items_with_identifiers_load_as_records_with_english_labels_links_dates_classes_and_sitelinks()
    {
        var result = await new WikidataLoader(pg.Store).LoadAsync(NTriples.ReadText(Sample), () => new StringReader(Sitelinks), "test-sample");
        Assert.Equal(3, result.Records); Assert.Equal(4, result.ItemsSeen); Assert.Equal(6, result.SitelinkRows);
        var durer = (await pg.Store.GetAuthorityAsync("http://www.wikidata.org/entity/Q5580"))!;
        Assert.Equal("wikidata", durer.Source);
        Assert.Equal("Albrecht Dürer", durer.Label);
        Assert.Equal("Person", durer.Type);
        Assert.Contains("Dürer", durer.Labels);
        Assert.DoesNotContain("Albrecht Duerer", durer.Labels); // a German alias is not a label here
        Assert.DoesNotContain(durer.Labels, l => l.Contains('А')); // nor the Russian one
        Assert.Equal(1471, durer.Lifespan!.BeginEarliest!.Value.Year); Assert.Equal(1528, durer.Lifespan.EndLatest!.Value.Year);
        Assert.Equal(["http://id.loc.gov/authorities/names/n79118011", "http://viaf.org/viaf/54146999", "http://vocab.getty.edu/ulan/500115493"], durer.SameAs.OrderBy(x => x, StringComparer.Ordinal));
        Assert.Contains("German painter", durer.DataJson); Assert.Contains("\"sitelinks\": 4", durer.DataJson); Assert.Contains("\"viaf\": \"54146999\"", durer.DataJson);
        Assert.Equal("test-sample", durer.SourceVersion);
        var gallery = (await pg.Store.GetAuthorityAsync("http://www.wikidata.org/entity/Q180788"))!;
        Assert.Equal("Group", gallery.Type); Assert.Equal(1824, gallery.Lifespan!.BeginEarliest!.Value.Year); Assert.Null(gallery.Lifespan.EndEarliest);
        Assert.Contains("National Gallery, London", gallery.Labels); Assert.Contains("\"sitelinks\": 1", gallery.DataJson);
        var royce = (await pg.Store.GetAuthorityAsync("http://www.wikidata.org/entity/Q99999901"))!;
        Assert.True(royce.Lifespan!.IsWorking); Assert.Equal(1773, royce.Lifespan.BeginLatest!.Value.Year); Assert.Equal(1789, royce.Lifespan.EndEarliest!.Value.Year);
        Assert.Equal(["http://vocab.getty.edu/tgn/7011781"], royce.SameAs);
        Assert.DoesNotContain("sitelinks", royce.DataJson);
        Assert.Null(await pg.Store.GetAuthorityAsync("http://www.wikidata.org/entity/Q99999902"));
        // the index finds them by label, folded
        Assert.Contains(await pg.Store.FindAuthoritiesByLabelAsync(StringKey.Normalise("Albrecht Durer"), "wikidata"), r => r.Uri.EndsWith("Q5580"));
    }
}
