using LinkedArtNet.Alignment.Authorities;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>The map's coordinates (the owner, 2026-11-08): TGN's strings in the record's data, a pull's numbers under "pulled", nothing otherwise.</summary>
public class CoordinatesTests
{
    private static AuthorityRecord Record(string? data) => new("http://vocab.getty.edu/tgn/1", "tgn", "x", [], "Place", null, [], data, DateTimeOffset.UtcNow, null);

    [Fact]
    public void Tgn_strings_and_pulled_numbers_are_read_and_nothing_else_is()
    {
        Assert.Equal((42.3584, -71.0598), Coordinates.Of(Record("""{"lat":"42.3584","long":"-71.0598","parentString":"Suffolk"}""")));
        Assert.Equal((51.5, -0.12), Coordinates.Of(Record("""{"ids":{},"pulled":{"lat":51.5,"long":-0.12}}""")));
        Assert.Equal((42.3584, -71.0598), Coordinates.Of(Record("""{"lat":"42.3584","long":"-71.0598","pulled":{"lat":51.5,"long":-0.12}}"""))); // the vocabulary's own first
        Assert.Null(Coordinates.Of(Record("""{"lat":"42.3584"}""")));
        Assert.Null(Coordinates.Of(Record("""{"pulled":{"lat":null,"long":null}}""")));
        Assert.Null(Coordinates.Of(Record("""{"ids":{}}""")));
        Assert.Null(Coordinates.Of(Record(null)));
        Assert.Null(Coordinates.Of(Record("not json")));
        Assert.Null(Coordinates.Of((AuthorityRecord?)null));
        Assert.Equal((51.5, -0.12), Coordinates.Of([null, Record("{}"), Record("""{"pulled":{"lat":51.5,"long":-0.12}}""")]));
    }

    /// <summary>The desk's chosen "Hamden (Conn.)" (LoC, no coordinates) borrows TGN's Hamden in Connecticut, not the six other Hamdens (the owner, 2026-11-08).</summary>
    [Fact]
    public void A_placed_candidate_without_coordinates_borrows_the_namesake_in_the_same_state()
    {
        AuthorityRecord Tgn(string id, string parents, double lat, double lon) => new("http://vocab.getty.edu/tgn/" + id, "tgn", "Hamden", ["Hamden"], "Place", null, [], $$$"""{"lat":"{{{lat}}}","long":"{{{lon}}}","parentString":"{{{parents}}}"}""", DateTimeOffset.UtcNow, null);
        var candidates = new[]
        {
            Tgn("1", "Delaware, Ohio, United States, North and Central America, World", 40.4, -82.9),
            Tgn("2", "New Haven, Connecticut, United States, North and Central America, World", 41.4, -72.9),
            Tgn("3", "Hamilton, New York, United States, North and Central America, World", 42.5, -75.5),
            new AuthorityRecord("http://vocab.getty.edu/tgn/4", "tgn", "Hampden", ["Hampden"], "Place", null, [], """{"lat":"42.1","long":"-72.4","parentString":"Hampden, Massachusetts, United States"}""", DateTimeOffset.UtcNow, null),
        };
        var borrowed = Coordinates.Borrow("Conn.", "Hamden", candidates);
        Assert.NotNull(borrowed);
        Assert.Equal(41.4, borrowed.Lat, 1); Assert.Equal(-72.9, borrowed.Long, 1);
        Assert.True(borrowed.Approximate);
        Assert.Equal("TGN's Hamden in New Haven, Connecticut", borrowed.Via);
        Assert.Equal(borrowed.Lat, Coordinates.Borrow("CT", "Hamden (Conn.)", candidates)!.Lat); // the postal code and a label with its parenthetical
        Assert.Null(Coordinates.Borrow("Mass.", "Hamden", candidates)); // Hampden is another name
        Assert.Null(Coordinates.Borrow(null, "Hamden", candidates));
        Assert.Null(Coordinates.Borrow("Conn.", "Hamden", []));
    }

    /// <summary>An identifier on the desk says what its record is (the owner, 2026-11-13: the two Dagenhams, a town and a constituency, shown as two numbers).</summary>
    [Fact]
    public void A_record_says_what_it_is_in_a_phrase()
    {
        AuthorityRecord R(string source, string? label, string? data) => new("u", source, label, [], "Place", null, [], data, DateTimeOffset.UtcNow, null);
        Assert.Equal("Dagenham: Parliamentary constituency in the United Kingdom, 1945-2010", RecordSummary.Says(R("wikidata", "Dagenham", """{"ids":{"viaf":"143706493"},"description":"Parliamentary constituency in the United Kingdom, 1945-2010"}""")));
        Assert.Equal("Dagenham, Barking and Dagenham, Greater London", RecordSummary.Says(R("tgn", "Dagenham", """{"lat":51.55,"parentString":"Barking and Dagenham, Greater London, England, United Kingdom, Europe, World"}""")));
        Assert.Equal("Dagenham (London, England)", RecordSummary.Says(R("loc", "Dagenham (London, England)", null)));
        Assert.Equal("Cozens: a painter", RecordSummary.Says(R("ulan", "Cozens", """{"pulled":{"description":"a painter"}}""")));
        Assert.Equal("Dagenham", RecordSummary.Says(R("loc", "Dagenham", "not json")));
        Assert.Null(RecordSummary.Says(null));
    }

    /// <summary>A field named to a reviewer in the manifest's plain words (the owner, 2026-11-13: "in the field place" on the entity page).</summary>
    [Fact]
    public void A_manifest_names_its_fields_in_plain_words_and_a_label_is_not_a_reading()
    {
        var pmc = LinkedArtNet.Alignment.Reconciliation.ProjectManifest.PmcDefault;
        Assert.Equal("place of publication", pmc.FieldName("place"));
        Assert.Equal("subject subdivision", pmc.FieldName("keywords:sub"));
        Assert.Equal("unknown", pmc.FieldName("unknown"));
        Assert.NotNull(pmc.FieldAbout("corpauthor"));
        Assert.Equal("place", new LinkedArtNet.Alignment.Reconciliation.ProjectManifest().FieldName("place"));
        var round = LinkedArtNet.Alignment.Reconciliation.ProjectManifest.FromJson(pmc.ToJson());
        Assert.Equal("place of publication", round.FieldName("place"));
        Assert.Empty(round.Differences(pmc));
        var unnamed = pmc with { Contexts = pmc.Contexts.ToDictionary(kv => kv.Key, kv => kv.Value with { Label = null, About = null }, StringComparer.OrdinalIgnoreCase) };
        Assert.All(unnamed.Differences(pmc), d => Assert.Contains("named or described differently", d));
        Assert.Equal(pmc.Contexts.Count, unnamed.Differences(pmc).Count);
    }

    /// <summary>An entity's identifiers in their sets (the owner, 2026-11-13: the merged Dagenham would hold a town's and a constituency's, and the VIAF numbers say nothing on viaf.org).</summary>
    [Fact]
    public void Identifiers_that_cite_one_another_are_one_set_and_a_number_is_described_by_what_gives_it()
    {
        AuthorityRecord R(string uri, string source, string? data, params string[] sameAs) => new(uri, source, "Dagenham", [], "Place", null, sameAs, data, DateTimeOffset.UtcNow, null);
        var town = R("http://www.wikidata.org/entity/Q1075299", "wikidata", """{"ids":{"loc":"n81024660","viaf":"143073557"},"description":"Town in east London, England"}""");
        var seat = R("http://www.wikidata.org/entity/Q3403624", "wikidata", """{"ids":{"viaf":"143706493"},"description":"Parliamentary constituency in the United Kingdom, 1945-2010"}""");
        var loc = R("http://id.loc.gov/authorities/names/n81024660", "loc", null);
        var lux = R("https://lux.collections.yale.edu/data/place/79b9", "lux", """{"pulled":{"facts":[{"key":"LUX says same as","url":"https://viaf.org/viaf/143073557/","value":"x"},{"key":"In LUX","url":"https://lux.collections.yale.edu/view/place/79b9","value":"y"}]}}""");
        var records = new[] { town, seat, loc, lux }.ToDictionary(r => r.Uri);
        string[] held = [loc.Uri, "http://viaf.org/viaf/143073557", "http://viaf.org/viaf/143706493", town.Uri, seat.Uri, lux.Uri, "https://lux.collections.yale.edu/data/place/a346"];
        var sets = IdentifierSets.Of(held, records);
        Assert.Equal(3, sets.Count);
        Assert.Equal([loc.Uri, "http://viaf.org/viaf/143073557", town.Uri, lux.Uri], sets[0]); // the town: LoC and VIAF through Wikidata's ids, LUX through its pull, the scheme and the slash no bar
        Assert.Equal(["http://viaf.org/viaf/143706493", seat.Uri], sets[1]);                    // the constituency
        Assert.Equal(["https://lux.collections.yale.edu/data/place/a346"], sets[2]);            // nothing says what it is: a set of its own
        Assert.Same(seat, IdentifierSets.CitedBy("http://viaf.org/viaf/143706493", records.Values));
        Assert.Null(IdentifierSets.CitedBy("http://viaf.org/viaf/1", records.Values));
        // two records that cite the same third identifier, held by neither, are one set
        var a = R("http://www.wikidata.org/entity/Q1", "wikidata", """{"ids":{"gnd":"1056799-9"}}""");
        var b = R("https://lux.collections.yale.edu/data/place/b", "lux", """{"pulled":{"facts":[{"key":"LUX says same as","url":"https://d-nb.info/gnd/1056799-9","value":"x"}]}}""");
        Assert.Single(IdentifierSets.Of([a.Uri, b.Uri], new[] { a, b }.ToDictionary(r => r.Uri)));
        Assert.Empty(IdentifierSets.Cites(R("u", "loc", "not json")));
    }
}
