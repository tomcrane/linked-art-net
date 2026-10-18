using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Alignment.Tests;

public class NameMatcherTests
{
    [Fact]
    public void Keys_cover_folded_and_reordered_forms()
    {
        var keys = NameMatcher.Keys("Dürer, Albrecht");
        Assert.Contains("dürer, albrecht", keys);
        Assert.Contains("durer, albrecht", keys);
        Assert.Contains("albrecht dürer", keys);
        Assert.Contains("albrecht durer", keys);
        Assert.Contains("smith, john", NameMatcher.Keys("John Smith"));
        Assert.Contains("knight frank rutley", NameMatcher.Keys("Knight, Frank & Rutley"));
        Assert.Contains("thomas agnew sons", NameMatcher.Keys("Thos. Agnew & Sons Ltd"));
        Assert.Contains("langford", NameMatcher.Keys("Messrs Langford"));
        Assert.Equal(["tate gallery"], NameMatcher.Keys("Tate Gallery").Where(k => k == "tate gallery"));
        Assert.Empty(NameMatcher.Keys("  "));
    }

    [Theory]
    [InlineData("smith, john", "smith, john", 1.0)]
    [InlineData("dürer, albrecht", "durer, albrecht", 0.97)]
    [InlineData("smith, john", "john smith", 0.93)]
    [InlineData("smith, j.", "smith, john", 0.85)]
    [InlineData("smith, j. a.", "smith, john arthur", 0.85)]
    [InlineData("thos. agnew & sons ltd", "thomas agnew & sons", 0.95)]
    [InlineData("landmark trust (great britain)", "landmark trust", 0.85)] // the qualifier is a disambiguator: review, not acceptance
    [InlineData("smith, john (clergyman)", "smith, john (painter)", 0.85)]
    [InlineData("messrs. langford", "langford", 0.95)]
    [InlineData("knight, frank & rutley", "knight frank and rutley", 0.95)] // "&" and "and" compare equal in the canonical form
    [InlineData("smith, john", "smyth, john", 0.82)] // one letter off: edit similarity, below acceptance and above review
    [InlineData("tate gallery", "national gallery", 0.267)]
    [InlineData("rembrandt", "vermeer", 0.0)]
    [InlineData("w e h", "hurcomb, w e", 0.4)] // all initials on one side: nothing to anchor on (word overlap only), and no crash
    public void Scores_rank_exact_folded_reordered_initials_then_fuzzy(string a, string b, double expected) =>
        Assert.Equal(expected, NameMatcher.Score(a, b), 2);

    [Theory]
    [InlineData("Thos. Agnew & Sons Ltd", "thomas agnew sons")]
    [InlineData("Messrs Waterer & Sons", "waterer sons")]
    [InlineData("Marlborough Galerie AG", "marlborough galerie")]
    [InlineData("Pedro Sá", "pedro sá")] // a suffix that is also a surname is not stripped
    [InlineData("Jon Smith", "jon smith")] // a short form that is also a name is not expanded
    [InlineData("England & Co.", "england co")]
    [InlineData("Hazlitt, Gooden and Fox", "hazlitt gooden fox")]
    [InlineData("Lisson Gallery (New York, N.Y.)", "lisson gallery")]
    [InlineData("Christie's East (Firm)", "christies east")]
    [InlineData("(Firm)", "firm")]
    [InlineData("Ltd", "ltd")] // a name that is only a suffix is left alone
    public void Canonical_forms_expand_abbreviations_and_drop_firm_noise(string raw, string canonical) => Assert.Equal(canonical, NameForms.Canonical(StringKey.Normalise(raw)));

    [Theory]
    [InlineData("dürer", "durer")]
    [InlineData("straße", "strasse")]
    [InlineData("søren", "soren")]
    [InlineData("łódź", "lodz")]
    [InlineData("plain", "plain")]
    public void Folding_strips_diacritics(string key, string folded) => Assert.Equal(folded, StringKey.Fold(key));
}

public class NTriplesTests
{
    [Fact]
    public void Lines_parse_with_escapes_languages_datatypes_and_blank_nodes()
    {
        var text = """
            # a comment
            <http://a/s> <http://a/p> <http://a/o> .
            <http://a/s> <http://a/label> "Dürer, \"Albrecht\""@en .
            <http://a/s> <http://a/start> "1471"^^<http://www.w3.org/2001/XMLSchema#gYear> .
            _:b1 <http://a/p> "plain" .
            this line is broken
            <http://a/s> <http://a/p> <http://a/missing-dot>
            """;
        var triples = NTriples.ReadText(text).ToList();
        Assert.Equal(4, triples.Count);
        Assert.Equal(new Triple("http://a/s", "http://a/p", "http://a/o", false), triples[0]);
        Assert.Equal("Dürer, \"Albrecht\"", triples[1].Object);
        Assert.Equal("en", triples[1].Lang);
        Assert.True(triples[1].IsLiteral);
        Assert.Equal("http://www.w3.org/2001/XMLSchema#gYear", triples[2].Datatype);
        Assert.Equal("_:b1", triples[3].Subject);
    }

    [Fact]
    public void A_bad_escape_fails_its_line_and_not_the_load()
    {
        var triples = NTriples.ReadText("<http://a/s> <http://a/p> \"bad \\U0011FFFF\" .\n<http://a/s> <http://a/p> \"surrogate \\U0000D800\" .\n<http://a/s> <http://a/p> \"fine \\U0001F600\" .").ToList();
        Assert.Single(triples);
        Assert.Equal("fine \U0001F600", triples[0].Object);
    }

    [Fact]
    public void Getty_helpers_canonicalise_links_and_read_year_bounds()
    {
        Assert.Equal("http://www.wikidata.org/entity/Q5580", GettyVocabLoader.CanonicalSameAs("https://wikidata.org/wiki/Q5580"));
        Assert.Equal("http://id.loc.gov/authorities/names/n79118011", GettyVocabLoader.CanonicalSameAs("https://id.loc.gov/authorities/names/n79118011"));
        Assert.Equal("http://viaf.org/viaf/98031201", GettyVocabLoader.CanonicalSameAs("https://viaf.org/viaf/98031201")); // as the ULAN VIAF alignment file writes them
        var l = GettyVocabLoader.LifespanFromYears("1471", "1528")!;
        Assert.Equal(1471, l.BeginEarliest!.Value.Year);
        Assert.Equal(1528, l.EndLatest!.Value.Year);
        Assert.Equal("1471-1528", l.Text);
        var bce = GettyVocabLoader.LifespanFromYears("-500", null)!;
        Assert.True(bce.IsEmpty);
        Assert.Equal("-500", bce.Text);
        Assert.Null(GettyVocabLoader.LifespanFromYears(null, null));
    }
}

public class LocHeadingTests
{
    [Theory]
    [InlineData("Dürer, Albrecht, 1471-1528", "Dürer, Albrecht", "1471-1528", 1471, 1528)]
    [InlineData("Smith, John, 1900-", "Smith, John", "1900-", 1900, null)]
    [InlineData("Manson, John, -1812", "Manson, John", "-1812", null, 1812)]
    [InlineData("Grierson, Herbert John Clifford, Sir, 1866-1960", "Grierson, Herbert John Clifford, Sir", "1866-1960", 1866, 1960)]
    [InlineData("Lely, Peter, Sir, 1618-1680.", "Lely, Peter, Sir", "1618-1680.", 1618, 1680)]
    [InlineData("Unknown, Artist, approximately 1500-1560", "Unknown, Artist", "approximately 1500-1560", 1500, 1560)]
    [InlineData("Tate Gallery", "Tate Gallery", null, null, null)]
    [InlineData("Paixão, Geraldo Ferreira da", "Paixão, Geraldo Ferreira da", null, null, null)]
    public void Personal_headings_split_into_name_and_dates(string label, string name, string? dates, int? begin, int? end)
    {
        var (n, d, l) = LocNamesLoader.SplitHeading(label);
        Assert.Equal(name, n);
        Assert.Equal(dates, d);
        Assert.Equal(begin, l?.BeginEarliest?.Year);
        Assert.Equal(end, l?.EndLatest?.Year);
    }

    [Fact]
    public void Active_dates_are_not_a_lifespan_and_rwo_dates_win()
    {
        var (n, d, l) = LocNamesLoader.SplitHeading("Smith, John, active 1640-1660");
        Assert.Equal("Smith, John", n);
        Assert.Equal("active 1640-1660", d);
        Assert.Null(l);
        var rwo = LocNamesLoader.LifespanFrom("1471-05-21", "approximately 1528")!;
        Assert.Equal(1471, rwo.BeginEarliest!.Value.Year);
        Assert.Equal(1528, rwo.EndLatest!.Value.Year);
        Assert.True(LocNamesLoader.UsableLink("http://viaf.org/viaf/12345"));
        Assert.True(LocNamesLoader.UsableLink("http://www.wikidata.org/entity/Q5580"));
        Assert.False(LocNamesLoader.UsableLink("http://viaf.org/viaf/sourceID/LC%7Cn79118011#skos:Concept"));
    }
}

public class GroupingTests
{
    private static StringReading Reading(string text, params EntityClass[] classes) =>
        new(new SourceString(1, "ds", "ctx", text, StringKey.Normalise(text), null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1), text, NameMatcher.Keys(text), classes, null, [], [], null);

    [Fact]
    public void Candidates_join_through_same_as_and_vetoes_drop_out()
    {
        var reading = Reading("Dürer, Albrecht", EntityClass.Person);
        var ulan = new Candidate("http://vocab.getty.edu/ulan/1", "ulan", "Dürer, Albrecht", EntityClass.Person, null, ["http://id.loc.gov/authorities/names/n1"], "local", 1.0);
        var loc = new Candidate("http://id.loc.gov/authorities/names/n1", "loc", "Dürer, Albrecht", EntityClass.Person, null, [], "local", 1.0);
        var otherGroup = new Candidate("http://vocab.getty.edu/ulan/2", "ulan", "Dürer, Albrecht", EntityClass.Group, null, [], "local", 1.0);
        var weak = new Candidate("http://viaf.org/viaf/9", "viaf", "Durer, Albert", EntityClass.Person, null, [], "remote", 0.9);
        var groups = Reconciler.Groups(Reconciler.Score(reading, [ulan, loc, otherGroup, weak]));
        Assert.Equal(2, groups.Count); // the Group-typed candidate is vetoed and its group dropped
        Assert.Equal(["ulan", "loc"], groups[0].Sources);
        Assert.Equal(1.0, groups[0].Score);
        Assert.Contains("http://id.loc.gov/authorities/names/n1", groups[0].Uris);
        Assert.Equal(["viaf"], groups[1].Sources);
    }

    [Fact]
    public void Dates_lift_or_veto_and_nationality_lifts()
    {
        var born1471 = new Lifespan(new DateTimeOffset(1471, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1471, 12, 31, 0, 0, 0, TimeSpan.Zero), null, null, "1471-");
        var reading = Reading("Dürer, Albrecht", EntityClass.Person) with { Lifespan = born1471, Nationalities = ["http://vocab.getty.edu/aat/300111192"] };
        var agrees = new Candidate("u1", "ulan", "Dürer, Albrecht", EntityClass.Person, GettyVocabLoader.LifespanFromYears("1471", "1528"), [], "local", 0.9, Nationalities: ["http://vocab.getty.edu/aat/300111192"]);
        var conflicts = new Candidate("u2", "ulan", "Dürer, Albrecht", EntityClass.Person, GettyVocabLoader.LifespanFromYears("1700", "1760"), [], "local", 1.0);
        var silent = new Candidate("u3", "ulan", "Dürer, Albrecht", EntityClass.Person, null, [], "local", 0.9);
        var scored = Reconciler.Score(reading, [agrees, conflicts, silent]);
        Assert.Equal(0.98, scored[0].Score); // 0.9 + dates + nationality
        Assert.True(scored[1].Veto);
        Assert.Equal(0.9, scored[2].Score);
    }
}

[Collection("postgres")]
public class ReconcilerStoreTests(PostgresFixture pg)
{
    private static string Dataset() => "ds-" + IdMinter.Generate(6);

    // A sliver of ULAN in the shapes the Getty N-Triples use (checked against vocab.getty.edu 2026-10-18).
    private const string UlanSample = """
        <http://vocab.getty.edu/ulan/500115493> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#PersonConcept> .
        <http://vocab.getty.edu/ulan/500115493> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1500065078-en> .
        <http://vocab.getty.edu/ulan/500115493> <http://www.w3.org/2008/05/skos-xl#altLabel> <http://vocab.getty.edu/ulan/term/1500065079-en> .
        <http://vocab.getty.edu/ulan/500115493> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1500065078-en> .
        <http://vocab.getty.edu/ulan/term/1500065078-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Dürer, Albrecht"@en .
        <http://vocab.getty.edu/ulan/term/1500065079-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Albrecht Dürer"@en .
        <http://vocab.getty.edu/ulan/500115493> <http://www.w3.org/2004/02/skos/core#exactMatch> <http://id.loc.gov/authorities/names/n79118011> .
        <http://vocab.getty.edu/ulan/500115493> <http://www.w3.org/2004/02/skos/core#exactMatch> <https://wikidata.org/wiki/Q5580> .
        <http://vocab.getty.edu/ulan/500115493> <http://xmlns.com/foaf/0.1/focus> <http://vocab.getty.edu/ulan/500115493-agent> .
        <http://vocab.getty.edu/ulan/500115493> <http://vocab.getty.edu/ontology#agentTypePreferred> <http://vocab.getty.edu/aat/300025103> .
        <http://vocab.getty.edu/ulan/500115493-agent> <http://vocab.getty.edu/ontology#biographyPreferred> <http://vocab.getty.edu/ulan/bio/4000168516> .
        <http://vocab.getty.edu/ulan/500115493-agent> <http://vocab.getty.edu/ontology#nationalityPreferred> <http://vocab.getty.edu/aat/300111192> .
        <http://vocab.getty.edu/ulan/bio/4000168516> <http://vocab.getty.edu/ontology#estStart> "1471"^^<http://www.w3.org/2001/XMLSchema#gYear> .
        <http://vocab.getty.edu/ulan/bio/4000168516> <http://vocab.getty.edu/ontology#estEnd> "1528"^^<http://www.w3.org/2001/XMLSchema#gYear> .
        <http://vocab.getty.edu/ulan/bio/4000168516> <http://schema.org/description> "German printmaker and painter, 1471-1528" .
        <http://vocab.getty.edu/ulan/500301641> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#GroupConcept> .
        <http://vocab.getty.edu/ulan/500301641> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1500101-en> .
        <http://vocab.getty.edu/ulan/500301641> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1500101-en> .
        <http://vocab.getty.edu/ulan/term/1500101-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Tate Gallery"@en .
        <http://vocab.getty.edu/ulan/500000001> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#PersonConcept> .
        <http://vocab.getty.edu/ulan/500000001> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1500000001-en> .
        <http://vocab.getty.edu/ulan/500000001> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1500000001-en> .
        <http://vocab.getty.edu/ulan/term/1500000001-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Smith, John"@en .
        <http://vocab.getty.edu/ulan/500000002> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#PersonConcept> .
        <http://vocab.getty.edu/ulan/500000002> <http://vocab.getty.edu/ontology#prefLabelGVP> <http://vocab.getty.edu/ulan/term/1500000002-en> .
        <http://vocab.getty.edu/ulan/500000002> <http://www.w3.org/2008/05/skos-xl#prefLabel> <http://vocab.getty.edu/ulan/term/1500000002-en> .
        <http://vocab.getty.edu/ulan/term/1500000002-en> <http://www.w3.org/2008/05/skos-xl#literalForm> "Smith, John"@en .
        <http://vocab.getty.edu/ulan/300000000> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://vocab.getty.edu/ontology#Facet> .
        <http://vocab.getty.edu/ulan/300000000> <http://vocab.getty.edu/ontology#parentString> "ignored" .
        """;

    [PostgresFact]
    public async Task Getty_triples_load_as_authority_records_with_labels_dates_links_and_nationality()
    {
        var result = await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test-sample");
        Assert.Equal(4, result.Records);
        var durer = (await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/ulan/500115493"))!;
        Assert.Equal("ulan", durer.Source);
        Assert.Equal("Dürer, Albrecht", durer.Label);
        Assert.Equal("Person", durer.Type);
        Assert.Contains("Albrecht Dürer", durer.Labels);
        Assert.Equal(1471, durer.Lifespan!.BeginEarliest!.Value.Year);
        Assert.Equal(1528, durer.Lifespan.EndLatest!.Value.Year);
        Assert.Equal(["http://id.loc.gov/authorities/names/n79118011", "http://www.wikidata.org/entity/Q5580"], durer.SameAs.OrderBy(x => x));
        Assert.Contains("300111192", durer.DataJson);
        Assert.Contains("German printmaker", durer.DataJson);
        Assert.Equal("test-sample", durer.SourceVersion);
        Assert.Equal("Group", (await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/ulan/500301641"))!.Type);
        Assert.Null(await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/ulan/300000000"));
        // found by the folded key and by the inverted order
        Assert.Single(await pg.Store.FindAuthoritiesByLabelsAsync(["durer, albrecht"], "ulan", "Person"));
        Assert.Single(await pg.Store.FindAuthoritiesByLabelsAsync(["albrecht durer"], "ulan"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500301417", "ulan", "Thomas Agnew & Sons", ["Agnew's"], "Group", null, [], null, DateTimeOffset.UtcNow, "test"));
        Assert.Single(await pg.Store.FindAuthoritiesByLabelsAsync(NameMatcher.Keys("Thos. Agnew & Sons Ltd"), "ulan", "Group"));
        Assert.Single(await pg.Store.FindAuthoritiesBySameAsAsync("http://www.wikidata.org/entity/Q5580"), a => a.Source == "ulan"); // the LoC sample links to the same Wikidata item
    }

    // A sliver of the LC Name Authority File in the MADS/RDF shapes (checked against a 2024 export on 2026-10-18).
    private const string LocSample = """
        <http://id.loc.gov/authorities/names/n79118011> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Authority> .
        <http://id.loc.gov/authorities/names/n79118011> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#PersonalName> .
        <http://id.loc.gov/authorities/names/n79118011> <http://www.loc.gov/mads/rdf/v1#authoritativeLabel> "D\u00FCrer, Albrecht, 1471-1528" .
        <http://id.loc.gov/authorities/names/n79118011> <http://www.loc.gov/mads/rdf/v1#hasVariant> _:v1 .
        _:v1 <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#PersonalName> .
        _:v1 <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Variant> .
        _:v1 <http://www.loc.gov/mads/rdf/v1#variantLabel> "Durero, Alberto, 1471-1528" .
        <http://id.loc.gov/authorities/names/n79118011> <http://www.loc.gov/mads/rdf/v1#hasExactExternalAuthority> <http://viaf.org/viaf/54146219> .
        <http://id.loc.gov/authorities/names/n79118011> <http://www.loc.gov/mads/rdf/v1#hasExactExternalAuthority> <http://viaf.org/viaf/sourceID/LC%7Cn79118011#skos:Concept> .
        <http://id.loc.gov/authorities/names/n79118011> <http://www.loc.gov/mads/rdf/v1#hasCloseExternalAuthority> <http://www.wikidata.org/entity/Q5580> .
        <http://id.loc.gov/authorities/names/n79118011> <http://www.loc.gov/mads/rdf/v1#identifiesRWO> <http://id.loc.gov/rwo/agents/n79118011> .
        <http://id.loc.gov/rwo/agents/n79118011> <http://www.loc.gov/mads/rdf/v1#birthDate> "1471-05-21" .
        <http://id.loc.gov/rwo/agents/n79118011> <http://www.loc.gov/mads/rdf/v1#deathDate> "1528-04-06" .
        <http://id.loc.gov/rwo/agents/n79118011> <http://www.loc.gov/mads/rdf/v1#occupation> <http://id.loc.gov/authorities/subjects/sh85104291> .
        <http://id.loc.gov/authorities/names/n50063091> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#CorporateName> .
        <http://id.loc.gov/authorities/names/n50063091> <http://www.loc.gov/mads/rdf/v1#authoritativeLabel> "Tate Gallery" .
        <http://id.loc.gov/authorities/names/n79021164> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Geographic> .
        <http://id.loc.gov/authorities/names/n79021164> <http://www.loc.gov/mads/rdf/v1#authoritativeLabel> "Siena (Italy)" .
        <http://id.loc.gov/authorities/names/n80000000> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Title> .
        <http://id.loc.gov/authorities/names/n80000000> <http://www.loc.gov/mads/rdf/v1#authoritativeLabel> "Some anonymous work" .
        """;

    [PostgresFact]
    public async Task Loc_names_load_with_headings_split_variants_dates_and_usable_links()
    {
        var result = await new LocNamesLoader(pg.Store).LoadAsync(NTriples.ReadText(LocSample), "test-sample");
        Assert.Equal(3, result.Records); // the title heading is left out
        var durer = (await pg.Store.GetAuthorityAsync("http://id.loc.gov/authorities/names/n79118011"))!;
        Assert.Equal("loc", durer.Source);
        Assert.Equal("Person", durer.Type);
        Assert.Equal("Dürer, Albrecht, 1471-1528", durer.Label);
        Assert.Contains("Dürer, Albrecht", durer.Labels);
        Assert.Contains("Durero, Alberto", durer.Labels);
        Assert.Equal(1471, durer.Lifespan!.BeginEarliest!.Value.Year);
        Assert.Equal(1528, durer.Lifespan.EndLatest!.Value.Year);
        Assert.Equal(["http://viaf.org/viaf/54146219", "http://www.wikidata.org/entity/Q5580"], durer.SameAs.OrderBy(x => x));
        Assert.Contains("sh85104291", durer.DataJson);
        Assert.Equal("Group", (await pg.Store.GetAuthorityAsync("http://id.loc.gov/authorities/names/n50063091"))!.Type);
        Assert.Equal("Place", (await pg.Store.GetAuthorityAsync("http://id.loc.gov/authorities/names/n79021164"))!.Type);
        Assert.Null(await pg.Store.GetAuthorityAsync("http://id.loc.gov/authorities/names/n80000000"));
        Assert.Single(await pg.Store.FindAuthoritiesByLabelsAsync(["durer, albrecht"], "loc", "Person"));
        Assert.Single(await pg.Store.FindAuthoritiesByLabelsAsync(["siena"], "loc", "Place")); // the qualifier drops out of the comparison key
    }

    [PostgresFact]
    public async Task Strings_are_overridden_matched_in_store_accepted_queued_or_minted()
    {
        var ds = Dataset();
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(UlanSample), GettyVocabulary.Ulan, "test-sample");
        // a second source that agrees with ULAN about Dürer through the sameAs link
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n79118011", "loc", "Dürer, Albrecht", ["Dürer, Albrecht", "Durero, Alberto"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));

        var options = new ReconcilerOptions
        {
            Contexts = new Dictionary<string, ContextRule>(StringComparer.OrdinalIgnoreCase)
            {
                ["persauthorfull"] = new([EntityClass.Person], "default"),
                ["corpauthor"] = new([EntityClass.Group], "default"),
            },
        };
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], options);

        var tateOverride = scheme.Uri(EntityClass.Group, "pmc-tate");
        await pg.Store.PutOverrideAsync(new Override(ds, null, "tate", tateOverride, "test"));
        var tate = await pg.Store.CollectAsync(ds, "corpauthor", "Tate");
        var durerDated = await pg.Store.CollectAsync(ds, "persauthorfull", "Dürer, Albrecht, 1471-1528");
        var durerInverted = await pg.Store.CollectAsync(ds, "persauthorfull", "Albrecht Durer");
        var smith = await pg.Store.CollectAsync(ds, "persauthorfull", "Smith, John");
        var nobody = await pg.Store.CollectAsync(ds, "persauthorfull", "Nobody, Zed, 1900-1980");
        var wrongDates = await pg.Store.CollectAsync(ds, "persauthorfull", "Dürer, Albrecht, 1800-1870");

        var report = await reconciler.RunAsync(ds);
        // oldest first: Tate (override), the dated Dürer (vote), the inverted Dürer (now in the store), Smith (two exact Smiths: review),
        // Nobody (minted), the wrongly dated Dürer (ULAN vetoed by dates; the undated LoC record alone sends it to review)
        Assert.Equal(new ReconcileReport(Considered: 6, Overridden: 1, MatchedStore: 1, Accepted: 1, Queued: 2, Minted: 1, Deferred: 0, Skipped: 0), report);

        // override: the entity was created under the curated identifier
        var tateEntity = (await pg.Store.ResolveAsync(ds, "corpauthor", "Tate"))!;
        Assert.Equal(tateOverride, tateEntity.Id);
        Assert.Equal(EntityStatus.Confirmed, tateEntity.Status);

        // accepted: ULAN and LoC agree through the sameAs link, and the string's dates agree with ULAN's; the inverted spelling then matched the new entity in the store
        var durer = (await pg.Store.ResolveAsync(ds, "persauthorfull", "Dürer, Albrecht, 1471-1528"))!;
        Assert.Equal(EntityStatus.Aligned, durer.Status);
        Assert.Equal("Dürer, Albrecht", durer.Label);
        Assert.Equal(1471, durer.Lifespan!.BeginEarliest!.Value.Year);
        Assert.Equal(["http://id.loc.gov/authorities/names/n79118011", "http://vocab.getty.edu/ulan/500115493", "http://www.wikidata.org/entity/Q5580"], durer.Equivalents.Select(e => e.Uri).OrderBy(x => x));
        Assert.Equal(durer.Id, (await pg.Store.ResolveAsync(ds, "persauthorfull", "Albrecht Durer"))!.Id);
        var decision = (await pg.Store.DecisionForAsync(durerDated.Id))!;
        Assert.Equal(DecisionMethod.Vote, decision.Method);
        Assert.Contains("dates agree", decision.EvidenceJson);

        // queued: two ULAN Smiths, both exact, nothing to choose; a provisional entity meanwhile
        var smithDecision = (await pg.Store.DecisionForAsync(smith.Id))!;
        Assert.Equal(DecisionMethod.Queued, smithDecision.Method);
        Assert.Equal(EntityStatus.Provisional, (await pg.Store.GetEntityAsync(smithDecision.EntityId!))!.Status);
        Assert.Contains("500000001", smithDecision.EvidenceJson);
        Assert.Contains("500000002", smithDecision.EvidenceJson);

        // minted: nothing found. The wrongly dated string: the ULAN candidate is vetoed by its dates and the undated LoC record alone is not enough
        Assert.Equal(DecisionMethod.Minted, (await pg.Store.DecisionForAsync(nobody.Id))!.Method);
        var minted = (await pg.Store.ResolveAsync(ds, "persauthorfull", "Nobody, Zed, 1900-1980"))!;
        Assert.Equal("Nobody, Zed", minted.Label);
        Assert.Equal(1900, minted.Lifespan!.BeginEarliest!.Value.Year);
        var conflict = (await pg.Store.DecisionForAsync(wrongDates.Id))!;
        Assert.Equal(DecisionMethod.Queued, conflict.Method);
        Assert.Contains("dates conflict", conflict.EvidenceJson);
        Assert.NotEqual(durer.Id, conflict.EntityId); // a provisional entity of its own, not Dürer's

        // a later run: a new spelling of a known person is answered from the store, not the indexes
        var later = await pg.Store.CollectAsync(ds, "persauthorfull", "Durer, Albrecht (engraver)");
        var again = await reconciler.RunAsync(ds);
        Assert.Equal(1, again.Considered);
        Assert.Equal(1, again.MatchedStore);
        Assert.Equal(durer.Id, (await pg.Store.ResolveAsync(ds, "persauthorfull", "Durer, Albrecht (engraver)"))!.Id);
        Assert.Equal(DecisionMethod.ExactName, (await pg.Store.DecisionForAsync(later.Id))!.Method);
        Assert.Equal(0, (await reconciler.RunAsync(ds)).Considered);

        // the shadow run re-judges the decisions with the store hidden: the voted Dürer agrees with itself through its ULAN id,
        // the override agrees through its target, the minted ones have nothing to compare
        var shadow = await new ShadowRun(pg.Store, reconciler).RunAsync(ds, method: DecisionMethod.Vote);
        Assert.Equal(1, shadow[Verdict.Agree]);
        Assert.Equal(1, shadow.Considered);
        var shadowAll = await new ShadowRun(pg.Store, reconciler).RunAsync(ds, method: DecisionMethod.Minted);
        Assert.Equal(shadowAll.Considered, shadowAll[Verdict.OldLocal]);
        // a wrong old answer is caught: give Dürer's string a decision to an entity with a different ULAN id
        var impostor = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Dürer, Albrecht", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(impostor.Id, new Equivalent("http://vocab.getty.edu/ulan/500000099", "ulan", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        await pg.Store.DecideAsync(durerDated.Id, impostor.Id, DecisionMethod.Imported);
        var caught = await new ShadowRun(pg.Store, reconciler).RunAsync(ds, method: DecisionMethod.Imported);
        Assert.Equal(1, caught[Verdict.Disagree]);
        await pg.Store.DecideAsync(durerDated.Id, durer.Id, DecisionMethod.Vote); // put it back

        // the audit: Dürer's entity agrees with its ULAN record; the impostor's ULAN id is not in the index
        var flagged = new List<AuditItem>();
        var audit = await new IndexAudit(pg.Store).RunAsync(ds, item => { flagged.Add(item); return Task.CompletedTask; });
        Assert.Equal(2, audit.WithUlan);
        Assert.True(audit.Clean >= 1);
        // an entity with only the LoC id is reached through ULAN's link and could gain the ULAN id
        var locOnly = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Durer, Albrecht", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(locOnly.Id, new Equivalent("http://id.loc.gov/authorities/names/n79118011", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var viaLink = new List<AuditItem>();
        var audit2 = await new IndexAudit(pg.Store).RunAsync(ds, item => { viaLink.Add(item); return Task.CompletedTask; });
        Assert.Equal(1, audit2.ViaLink);
        Assert.Contains(viaLink, f => f.Entity.Id == locOnly.Id && f.Flags.Contains(AuditFlag.UlanViaLink) && f.Detail.Contains("500115493"));
        Assert.Contains(flagged, f => f.Entity.Id == impostor.Id && f.Flags.Contains(AuditFlag.UlanMissing));
        var linkConflict = IndexAudit.Check(durer with { Equivalents = [.. durer.Equivalents.Where(q => q.Source != "loc"), new Equivalent("http://id.loc.gov/authorities/names/n00000000", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow)] },
            "http://vocab.getty.edu/ulan/500115493", await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/ulan/500115493"));
        Assert.Contains(AuditFlag.LinkConflict, linkConflict.Flags);
        Assert.Contains("n79118011", linkConflict.Detail);

        // undoing a run: its strings are undecided again, the provisional entities it minted go, the aligned one stays
        var run = await pg.Store.StartRunAsync(ds);
        var extra = await pg.Store.CollectAsync(ds, "persauthorfull", "Nobody Else, 1901-1981");
        await reconciler.RunAsync(ds, runId: run.Id);
        var provisional = (await pg.Store.DecisionForAsync(extra.Id))!.EntityId!;
        var undone = await pg.Store.UndoRunAsync(run.Id);
        Assert.Equal(new UndoResult(DecisionsInvalidated: 1, ProvisionalEntitiesRemoved: 1), undone);
        Assert.Null(await pg.Store.DecisionForAsync(extra.Id));
        Assert.Null(await pg.Store.GetEntityAsync(provisional));
        Assert.NotNull(await pg.Store.GetEntityAsync(durer.Id));
        Assert.Equal(1, (await reconciler.RunAsync(ds)).Considered);
    }
}
