using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>A source citation against a record's title (2026-11-27): the variation allowed, and what is not; and the citations read from the Library of Congress bulk file.</summary>
[Collection("postgres")]
public class TitleCitationTests(PostgresFixture pg)
{
    [Fact]
    public void A_citation_cites_a_title_through_the_ways_cataloguers_differ()
    {
        // as LoC cites Lindsay Smith's book, against the record's title with its subtitle
        Assert.True(TitleCitation.Cites("Victorian photography, painting and poetry, 1995: CIP t.p. (Lindsay Smith, School of English and American Studies, University of Sussex)",
            "Victorian photography, painting and poetry : the enigma of visibility in Ruskin, Morris and the Pre-Raphaelites"));
        // case, accents, punctuation, an ampersand, a leading article, a statement of responsibility
        Assert.True(TitleCitation.Cites("His The life & works of Edwin Long, R.A., 1931: t.p.", "The life and works of Edwin Long, RA"));
        Assert.True(TitleCitation.Cites("Hoever, O. Handbuch der Kunstwissenschaft, 1923", "Handbuch der Kunstwissenschaft / Otto Höver"));
        Assert.True(TitleCitation.Cites("Les musées de France, 1950", "Musées de France"));
        // the abbreviations of a citation
        Assert.True(TitleCitation.Cites("His The Brit. sch. of miniature portrait painters, 1948: t.p. (Carl Winter)", "The British school of miniature portrait painters"));
        Assert.True(TitleCitation.Cites("Cat. of the pictures in the gallery of Alleyn's College, 1926", "Catalogue of the pictures in the Gallery of Alleyn's College of God's Gift at Dulwich : with biographical notices"));
        // not a title of one word of substance, nor words out of order, missing or of another book
        Assert.False(TitleCitation.Cites("Catalogue of an exhibition, 1921", "Catalogue"));
        Assert.False(TitleCitation.Cites("Paintings of the Prado, 1990", "Notes"));
        Assert.False(TitleCitation.Cites("Photography and painting in Victorian poetry, 1995", "Victorian photography, painting and poetry"));
        Assert.False(TitleCitation.Cites("Victorian photography, 1995", "Victorian photography, painting and poetry"));
        Assert.False(TitleCitation.Cites("Catalogue of the pictures at Petworth, 1920", "Catalogue of the pictures in the Gallery of Alleyn's College"));
        Assert.False(TitleCitation.Cites("Clothing in colonial Russian America, 1976", "John Middleton : Victorian, provincial architect"));
        Assert.False(TitleCitation.Cites(null, "Victorian photography, painting and poetry"));
    }

    [PostgresFact]
    public async Task Citations_are_read_from_the_bulk_file_for_the_records_wanted()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        string wanted = $"http://id.loc.gov/authorities/names/no{tag}", other = $"http://id.loc.gov/authorities/names/nx{tag}";
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(wanted, "loc", "Smith, Lindsay (Professor of English)", ["Smith, Lindsay (Professor of English)"], "Person", null, [], """{"occupations":["x"]}""", DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord(other, "loc", "Smith, Lindsay, 1984-", ["Smith, Lindsay"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        const string mads = "http://www.loc.gov/mads/rdf/v1#";
        // a citation's note before its record, its source after; and one of a record not wanted
        var triples = NTriples.ReadText($"""
            _:b1{tag} <{mads}citationNote> "CIP t.p. (Lindsay Smith, University of Sussex)" .
            <{wanted}> <{mads}hasSource> _:b1{tag} .
            _:b1{tag} <{mads}citationSource> "Victorian photography, painting and poetry, 1995:" .
            _:b1{tag} <{mads}citationStatus> "found" .
            <{wanted}> <{mads}hasSource> _:b2{tag} .
            _:b2{tag} <{mads}citationSource> "Smith, Lindsay. Pre-Raphaelitism, 2013:" .
            <{other}> <{mads}hasSource> _:b3{tag} .
            _:b3{tag} <{mads}citationSource> "Thai hawker food, 1993:" .
            """);
        var result = await new LocCitationsLoader(pg.Store).LoadAsync(triples, new HashSet<string> { wanted });
        Assert.Equal((1, 2), (result.Records, result.Citations));
        var record = (await pg.Store.GetAuthorityAsync(wanted))!;
        Assert.Contains("occupations", record.DataJson);                                   // the rest of its data is left
        var read = SourcePull.Read(record)!;
        Assert.Equal(2, read.Facts.Count);
        Assert.All(read.Facts, f => Assert.Equal("Found in", f.Key));
        Assert.Contains(read.Facts, f => f.Value == "Victorian photography, painting and poetry, 1995: CIP t.p. (Lindsay Smith, University of Sussex)");
        Assert.Contains(read.Facts, f => TitleCitation.Cites(f.Value, "Victorian photography, painting and poetry : the enigma of visibility"));
        Assert.Null(SourcePull.Read(await pg.Store.GetAuthorityAsync(other)));          // not wanted: untouched
    }
}
