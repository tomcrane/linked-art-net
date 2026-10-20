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
    public void Report_csv_cells_cannot_run_as_formulas()
    {
        var s = new SourceString(1, "ds", "ctx", "=HYPERLINK(\"http://evil\")", "=hyperlink", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1);
        var reading = new StringReading(s, s.Raw, [], [EntityClass.Person], null, [], [], null);
        var line = ShadowRun.CsvLine(new ShadowItem(s, new Decision(1, 1, null, "imported", null, null, DateTimeOffset.UtcNow, null, null, true), null, new Evaluation(Outcome.Minted, reading, []), Verdict.OldLocal));
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\")\"", line);
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
    [InlineData("185X", 1850, 1859)]
    [InlineData("18XX", 1800, 1899)]
    [InlineData("1471", 1471, 1471)]
    [InlineData("-0469", null, null)]
    [InlineData("19uu", 1900, 1999)]
    public void Edtf_years_read_as_ranges_and_bce_stays_text(string token, int? from, int? to)
    {
        var (f, t) = LocNamesLoader.ParseYear(token);
        Assert.Equal(from, f);
        Assert.Equal(to, t);
    }

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
        var decade = LocNamesLoader.LifespanFrom("185X", null)!;
        Assert.Equal(1850, decade.BeginEarliest!.Value.Year);
        Assert.Equal(1859, decade.BeginLatest!.Value.Year);
        var bce = LocNamesLoader.LifespanFrom("-0469", "-0399")!;
        Assert.True(bce.IsEmpty);
        Assert.Equal("-0469--0399", bce.Text);
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
        var groups = Reconciler.Groups(Reconciler.Score(reading, [ulan, loc, otherGroup, weak], 120));
        Assert.Equal(3, groups.Count);
        Assert.Equal(["ulan", "loc"], groups[0].Sources);
        Assert.Equal(1.0, groups[0].Score);
        Assert.Contains("http://id.loc.gov/authorities/names/n1", groups[0].Uris);
        Assert.Equal(["viaf"], groups[1].Sources);
        Assert.True(groups[2].Vetoed); // the Group-typed candidate: its group is vetoed and ranks last

        // a vetoed member that the authorities link to a live one takes the live one down with it: the undated LoC record is the
        // same person as the ULAN record whose dates conflict
        var dated = reading with { Lifespan = new Lifespan(new DateTimeOffset(1800, 1, 1, 0, 0, 0, TimeSpan.Zero), null, null, null, "1800-") };
        var ulanDated = ulan with { Lifespan = GettyVocabLoader.LifespanFromYears("1471", "1528") };
        var linked = Reconciler.Groups(Reconciler.Score(dated, [ulanDated, loc], 120));
        Assert.True(Assert.Single(linked).Vetoed);
        // the store's own entity does not count as a source
        var storeEntity = new Candidate("https://data.example.org/x/person/abc", "store", "Dürer, Albrecht", EntityClass.Person, null, ["http://vocab.getty.edu/ulan/1"], "store", 1.0, "https://data.example.org/x/person/abc");
        Assert.Equal(["ulan", "loc"], Reconciler.Groups(Reconciler.Score(reading, [storeEntity, ulan, loc], 120))[0].Sources);
    }

    [Fact]
    public void Dates_lift_or_veto_and_nationality_lifts()
    {
        var born1471 = new Lifespan(new DateTimeOffset(1471, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1471, 12, 31, 0, 0, 0, TimeSpan.Zero), null, null, "1471-");
        var reading = Reading("Dürer, Albrecht", EntityClass.Person) with { Lifespan = born1471, Nationalities = ["http://vocab.getty.edu/aat/300111192"] };
        var agrees = new Candidate("u1", "ulan", "Dürer, Albrecht", EntityClass.Person, GettyVocabLoader.LifespanFromYears("1471", "1528"), [], "local", 0.9, Nationalities: ["http://vocab.getty.edu/aat/300111192"]);
        var conflicts = new Candidate("u2", "ulan", "Dürer, Albrecht", EntityClass.Person, GettyVocabLoader.LifespanFromYears("1700", "1760"), [], "local", 1.0);
        var silent = new Candidate("u3", "ulan", "Dürer, Albrecht", EntityClass.Person, null, [], "local", 0.9);
        var scored = Reconciler.Score(reading, [agrees, conflicts, silent], 120);
        Assert.Equal(0.98, scored[0].Score); // 0.9 + dates + nationality
        Assert.True(scored[0].DatesAgree);
        Assert.True(scored[1].Veto);
        Assert.Equal(0.9, scored[2].Score);
        Assert.False(scored[2].DatesAgree);
        // dates on different sides are not comparable: no lift, no veto, and no single-source acceptance
        var diedOnly = new Candidate("u4", "loc", "Dürer, Albrecht", EntityClass.Person, new Lifespan(null, null, new DateTimeOffset(1520, 1, 1, 0, 0, 0, TimeSpan.Zero), null, "-1520"), [], "local", 1.0);
        var incomparable = Reconciler.Score(reading, [diedOnly], 120)[0];
        Assert.False(incomparable.Veto);
        Assert.False(incomparable.DatesAgree);
        Assert.Equal(1.0, incomparable.Score);
        Assert.Contains("not comparable", incomparable.Reason);
        var diedBefore = new Candidate("u5", "loc", "Dürer, Albrecht", EntityClass.Person, new Lifespan(null, null, new DateTimeOffset(1400, 1, 1, 0, 0, 0, TimeSpan.Zero), null, "-1400"), [], "local", 1.0);
        Assert.True(Reconciler.Score(reading, [diedBefore], 120)[0].Veto); // died before the string's birth
    }

    [Theory]
    [InlineData("Hammersmith, London", "Hammersmith", "London")]
    [InlineData("Piltown, County Kilkenny", "Piltown", "County Kilkenny")]
    [InlineData("Fountains Hall (Yorkshire)", "Fountains Hall", "Yorkshire")]
    [InlineData("Kedleston Hall (Derbyshire), England", "Kedleston Hall", "England, Derbyshire")]
    [InlineData("Moggerhanger", "Moggerhanger", null)]
    [InlineData("Mainz/Rhein", "Mainz/Rhein", null)]
    public void A_qualified_place_splits_into_name_and_qualifier(string raw, string name, string? qualifier)
    {
        var (n, q) = Qualified.Split(raw);
        Assert.Equal(name, n); Assert.Equal(qualifier, q);
    }

    [Fact]
    public void A_qualifier_places_a_candidate_by_its_parent_string_and_vetoes_the_namesakes_it_does_not_place()
    {
        Assert.True(Qualified.Agrees("Bradford", "Heaton, Bradford, West Yorkshire, England, United Kingdom, Europe, World"));
        Assert.True(Qualified.Agrees("Isle of Wight", "Shorwell, Isle of Wight, England, United Kingdom, Europe, World")); // "of" is noise, "wight" places it
        Assert.True(Qualified.Agrees("S. Australia", "Kent Town, South Australia, Australia, Oceania, World"));
        Assert.False(Qualified.Agrees("Bradford", "Heaton, Newcastle upon Tyne, England, United Kingdom, Europe, World"));
        Assert.Null(Qualified.Agrees("N.J.", "Princeton, New Jersey, United States, North and Central America, World")); // an abbreviation places nothing
        Assert.Null(Qualified.Agrees("Bradford", null));
        Assert.Equal("Bradford, England", Qualified.PlacementOf(null, ["Heaton (Bradford, England)"])); // a LoC heading's parenthetical stands in for a parent string

        var reading = Reading("Heaton", EntityClass.Place) with { Qualifier = "Bradford" };
        var bradford = new Candidate("t1", "tgn", "Heaton", EntityClass.Place, null, [], "local", 1.0, Placement: "Heaton, Bradford, West Yorkshire, England, United Kingdom, Europe, World");
        var newcastle = new Candidate("t2", "tgn", "Heaton", EntityClass.Place, null, [], "local", 1.0, Placement: "Heaton, Newcastle upon Tyne, England, United Kingdom, Europe, World");
        var unknown = new Candidate("t3", "tgn", "Heaton", EntityClass.Place, null, [], "local", 1.0);
        var scored = Reconciler.Score(reading, [bradford, newcastle, unknown], 120);
        Assert.True(scored[0].QualifierAgrees); Assert.False(scored[0].Veto); Assert.Equal(1.0, scored[0].Score);
        Assert.False(scored[1].QualifierAgrees); Assert.True(scored[1].Veto); Assert.Contains("places another", scored[1].Reason);
        Assert.Null(scored[2].QualifierAgrees); Assert.False(scored[2].Veto);
        // with nothing placed, namesakes stay rivals
        var open = Reconciler.Score(reading with { Qualifier = "N.J." }, [bradford, newcastle], 120);
        Assert.All(open, m => Assert.False(m.Veto));
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
        // a reload without a record retires it; a stale key is recomputed by reindexing
        await using (var cmd = pg.DataSource.CreateCommand("update authority_records set normalised_labels = '{stale}' where uri = 'http://vocab.getty.edu/ulan/500115493'")) await cmd.ExecuteNonQueryAsync();
        Assert.Empty(await pg.Store.FindAuthoritiesByLabelsAsync(["durer, albrecht"], "ulan", "Person"));
        var reindexed = await pg.Store.ReindexLabelsAsync("ulan");
        Assert.Equal(1, reindexed.AuthorityRecords); // only the stale row is rewritten; current rows are left alone
        Assert.Equal(0, (await pg.Store.ReindexLabelsAsync("loc")).AuthorityRecords); // another source: nothing to do
        Assert.Single(await pg.Store.FindAuthoritiesByLabelsAsync(["durer, albrecht"], "ulan", "Person"));
        var smaller = UlanSample[..UlanSample.IndexOf("<http://vocab.getty.edu/ulan/500301641>", StringComparison.Ordinal)]; // Dürer only
        await Task.Delay(50);
        // the second load takes the large-batch path, which rebuilds the label indexes around the insert
        pg.Store.RebuildIndexesAbove = 1;
        try { await new GettyVocabLoader(pg.Store).LoadAsync(NTriples.ReadText(smaller), GettyVocabulary.Ulan, "test-sample-2"); }
        finally { pg.Store.RebuildIndexesAbove = 500_000; }
        Assert.Single(await pg.Store.FindAuthoritiesByLabelsAsync(["durer, albrecht"], "ulan", "Person")); // the rebuilt index answers
        Assert.Contains(await pg.Store.AuthoritySourcesAsync(), src => src.Source == "ulan" && src.Records >= 1);
        // a lookup across sources is a union over the loaded sources; an unknown source finds nothing
        Assert.NotEmpty(await pg.Store.FindAuthoritiesByLabelsAsync(["durer, albrecht"]));
        Assert.Empty(await pg.Store.FindAuthoritiesByLabelsAsync(["durer, albrecht"], "viaf")); // not loaded: answered without a query
        await Assert.ThrowsAsync<ArgumentException>(() => pg.Store.FindAuthoritiesByLabelsAsync(["x"], "bad source"));
        Assert.NotNull(await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/ulan/500115493"));
        Assert.Null(await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/ulan/500301641")); // retired: not in the new dump
        Assert.Null(await pg.Store.GetAuthorityAsync("http://vocab.getty.edu/ulan/500301417")); // the hand-added Agnew's went with it
        Assert.Single(await pg.Store.FindAuthoritiesBySameAsAsync("http://www.wikidata.org/entity/Q5580"), a => a.Source == "ulan"); // the LoC sample links to the same Wikidata item
    }

    [Fact]
    public void Getty_bounds_are_tight_only_when_the_biography_states_the_year()
    {
        var durer = GettyVocabLoader.LifespanFromYears("1471", "1528", "German printmaker and painter, 1471-1528")!;
        Assert.True(durer.IsDated);
        Assert.Equal("1471-1528", durer.Text);
        var window = GettyVocabLoader.LifespanFromYears("1850", "2080", "American painter, active 20th century")!; // ULAN's window of possibility
        Assert.False(window.IsDated);
        Assert.Equal(1850, window.BeginEarliest!.Value.Year); Assert.Null(window.BeginLatest); Assert.Null(window.EndEarliest); Assert.Equal(2080, window.EndLatest!.Value.Year);
        Assert.Equal("1850~-~2080", window.Text);
        var living = GettyVocabLoader.LifespanFromYears("1950", "2050", "British sculptor, born 1950")!;
        Assert.True(Lifespan.Tight(living.BeginEarliest, living.BeginLatest)); Assert.Null(living.EndEarliest); // birth stated, end an outer bound
        Assert.Null(GettyVocabLoader.LifespanFromYears("1", "2090", "Italian painter")); // ULAN's "no dates"
        // a window agrees with nothing, but can still conflict
        var dated = new Lifespan(new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1900, 12, 31, 0, 0, 0, TimeSpan.Zero), null, null, "1900-");
        Assert.Equal(DateAgreement.Incomparable, dated.Compare(window));
        Assert.Equal(DateAgreement.Conflict, new Lifespan(new DateTimeOffset(1700, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1700, 12, 31, 0, 0, 0, TimeSpan.Zero), null, null, "1700-").Compare(window)); // born before the window opens
        Assert.Equal(DateAgreement.Conflict, dated.Compare(living)); // born 1900 is not born 1950
        Assert.Equal(DateAgreement.Agree, new Lifespan(new DateTimeOffset(1950, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1950, 12, 31, 0, 0, 0, TimeSpan.Zero), null, null, "1950-").Compare(living));
        Assert.Equal("Group", GettyVocabLoader.ClassFor("Person", "http://vocab.getty.edu/aat/300386361")); // a corporate body typed as a person
        Assert.Equal("Person", GettyVocabLoader.ClassFor("Person", "http://vocab.getty.edu/aat/300025103")); // an artist stays a person
        Assert.Equal("Place", GettyVocabLoader.ClassFor("Place", null));
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

    // A sliver of the LC Subject Headings file in its MADS/RDF shapes (read from the 2026 export on 2026-10-19): a topic, a
    // subdivision, the complex heading made of the two with its component list, and a geographic heading with a broader one.
    private const string LcshSample = """
        <http://id.loc.gov/authorities/subjects/sh00000074> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Topic> .
        <http://id.loc.gov/authorities/subjects/sh00000074> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Authority> .
        <http://id.loc.gov/authorities/subjects/sh00000074> <http://www.loc.gov/mads/rdf/v1#authoritativeLabel> "Angiotensin II"@en .
        <http://id.loc.gov/authorities/subjects/sh00000074> <http://www.loc.gov/mads/rdf/v1#hasVariant> _:v1 .
        _:v1 <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Topic> .
        _:v1 <http://www.loc.gov/mads/rdf/v1#variantLabel> "Angiotensin 2"@en .
        <http://id.loc.gov/authorities/subjects/sh99002348> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Topic> .
        <http://id.loc.gov/authorities/subjects/sh99002348> <http://www.loc.gov/mads/rdf/v1#authoritativeLabel> "Antagonists"@en .
        <http://id.loc.gov/authorities/subjects/sh00000071> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#ComplexSubject> .
        <http://id.loc.gov/authorities/subjects/sh00000071> <http://www.loc.gov/mads/rdf/v1#authoritativeLabel> "Angiotensin II--Antagonists"@en .
        <http://id.loc.gov/authorities/subjects/sh00000071> <http://www.loc.gov/mads/rdf/v1#componentList> _:l1 .
        _:l1 <http://www.w3.org/1999/02/22-rdf-syntax-ns#first> <http://id.loc.gov/authorities/subjects/sh00000074> .
        _:l1 <http://www.w3.org/1999/02/22-rdf-syntax-ns#rest> _:l2 .
        _:l2 <http://www.w3.org/1999/02/22-rdf-syntax-ns#first> <http://id.loc.gov/authorities/subjects/sh99002348> .
        _:l2 <http://www.w3.org/1999/02/22-rdf-syntax-ns#rest> <http://www.w3.org/1999/02/22-rdf-syntax-ns#nil> .
        <http://id.loc.gov/authorities/subjects/sh85056664> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://www.loc.gov/mads/rdf/v1#Geographic> .
        <http://id.loc.gov/authorities/subjects/sh85056664> <http://www.loc.gov/mads/rdf/v1#authoritativeLabel> "Great Britain"@en .
        <http://id.loc.gov/authorities/subjects/sh85056664> <http://www.loc.gov/mads/rdf/v1#hasBroaderAuthority> <http://id.loc.gov/authorities/subjects/sh85045631> .
        <http://id.loc.gov/authorities/subjects/sh85056664> <http://www.w3.org/2004/02/skos/core#closeMatch> <http://vocab.getty.edu/tgn/7002445> .
        """;

    [PostgresFact]
    public async Task Lcsh_subjects_load_as_types_places_and_complex_headings_with_components()
    {
        var result = await new LocNamesLoader(pg.Store).LoadAsync(NTriples.ReadText(LcshSample), LocFile.Subjects, "test-sample");
        Assert.Equal(4, result.Records);
        var topic = (await pg.Store.GetAuthorityAsync("http://id.loc.gov/authorities/subjects/sh00000074"))!;
        Assert.Equal("lcsh", topic.Source);
        Assert.Equal("Type", topic.Type);
        Assert.Contains("Angiotensin 2", topic.Labels);
        Assert.Null(topic.Lifespan);
        var complex = (await pg.Store.GetAuthorityAsync("http://id.loc.gov/authorities/subjects/sh00000071"))!;
        Assert.Equal("Angiotensin II--Antagonists", complex.Label);
        Assert.Contains("\"components\": [\"http://id.loc.gov/authorities/subjects/sh00000074\", \"http://id.loc.gov/authorities/subjects/sh99002348\"]", complex.DataJson); // jsonb spacing
        Assert.Contains("\"madsClass\": \"ComplexSubject\"", complex.DataJson);
        var gb = (await pg.Store.GetAuthorityAsync("http://id.loc.gov/authorities/subjects/sh85056664"))!;
        Assert.Equal("Place", gb.Type);
        Assert.Contains("sh85045631", gb.DataJson);
        Assert.Contains("http://vocab.getty.edu/tgn/7002445", gb.SameAs);
        Assert.Single(await pg.Store.FindAuthoritiesByLabelsAsync(["angiotensin ii"], "lcsh", "Type")); // its own index and source row
        Assert.Contains(await pg.Store.AuthoritySourcesAsync(), s => s.Source == "lcsh" && s.Records == 4);
    }

    [PostgresFact]
    public async Task A_list_string_is_decided_as_its_parts()
    {
        var ds = Dataset();
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Tgn(string id, string label, string parent) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label], "Place", null, [], $"{{\"parentString\":\"{parent}\"}}", DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Tgn("7011781", "Hammersmith", "London, Greater London, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7000005", "Lampeter", "Ceredigion, Wales, United Kingdom, Europe, World"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn")], ProjectManifest.PmcDefault.ToOptions());

        var list = await pg.Store.CollectAsync(ds, "place", "Hammersmith, London; Lampeter");
        var e = await reconciler.ReconcileOneDetailedAsync(ds, list);
        Assert.Equal(Outcome.Decomposed, e.Outcome);
        Assert.Equal(DecisionMethod.List, (await pg.Store.DecisionForAsync(list.Id))!.Method);
        var parts = await pg.Store.PartsAsync(list.Id);
        Assert.Equal(["Hammersmith, London", "Lampeter"], parts.Select(p => p.Raw).ToArray());
        Assert.Equal(2, e.Parts!.Count);
        Assert.Equal(Outcome.Accepted, e.Parts[0].Outcome); // placed by its qualifier, one source
        Assert.Equal(Outcome.Accepted, e.Parts[1].Outcome); // a one-word place is whole: the exact-single policy's two-word rule is for people and bodies
        Assert.Equal(DecisionMethod.ExactSingle, (await pg.Store.DecisionForAsync(parts[1].Id))!.Method);
        Assert.Contains((await pg.Store.ResolveAsync(ds, "place", "Lampeter"))!.Equivalents, q => q.Uri.EndsWith("7000005"));
        // a second list naming Lampeter shares the part string, already decided, and reconciles only the new one
        var again = await reconciler.ReconcileOneDetailedAsync(ds, await pg.Store.CollectAsync(ds, "place", "Lampeter; Moggerhanger"));
        Assert.Equal(Outcome.Decomposed, again.Outcome);
        Assert.Single(again.Parts!);
        Assert.Equal(Outcome.Minted, again.Parts![0].Outcome);
        Assert.Equal(2, (await pg.Store.ParentsAsync(parts[1].Id)).Count);
        Assert.Equal(1, (await pg.Store.CollectAsync(ds, "place", "Lampeter", ct: default, count: false)).Occurrences); // parts are not occurrences
        // a run counts the list and its new parts
        var report = await reconciler.RunAsync(ds);
        Assert.Equal(0, report.Lists); // both lists were decided above
        Assert.Equal(Verdict.Decomposed, ShadowRun.Judge(null, e));
    }

    [PostgresFact]
    public async Task Qualified_places_are_placed_by_tgn_parent_strings()
    {
        var ds = Dataset();
        var scheme = new IdentifierScheme("https://data.example.org/" + ds);
        static AuthorityRecord Tgn(string id, string label, string parent) => new($"http://vocab.getty.edu/tgn/{id}", "tgn", label, [label], "Place", null, [], $"{{\"parentString\":\"{parent}\"}}", DateTimeOffset.UtcNow, "test");
        await pg.Store.PutAuthorityAsync(Tgn("7011781", "Hammersmith", "London, Greater London, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7000001", "Heaton", "Bradford, West Yorkshire, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7000002", "Heaton", "Newcastle upon Tyne, Tyne and Wear, England, United Kingdom, Europe, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7000003", "Princeton", "Mercer, New Jersey, United States, North and Central America, World"));
        await pg.Store.PutAuthorityAsync(Tgn("7000004", "Princeton", "Gibson, Indiana, United States, North and Central America, World"));
        var reconciler = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "tgn")], ProjectManifest.PmcDefault.ToOptions());

        var hammersmith = await pg.Store.CollectAsync(ds, "place", "Hammersmith, London");
        Assert.Equal(Outcome.Accepted, await reconciler.ReconcileOneAsync(ds, hammersmith)); // one source, name exact, qualifier placed
        Assert.Contains((await pg.Store.ResolveAsync(ds, "place", "Hammersmith, London"))!.Equivalents, q => q.Uri.EndsWith("7011781"));
        var heaton = await pg.Store.CollectAsync(ds, "place", "Heaton, Bradford");
        Assert.Equal(Outcome.Accepted, await reconciler.ReconcileOneAsync(ds, heaton)); // the Newcastle namesake is vetoed by the qualifier
        Assert.Contains((await pg.Store.ResolveAsync(ds, "place", "Heaton, Bradford"))!.Equivalents, q => q.Uri.EndsWith("7000001"));
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "place", "Princeton, N.J."))); // an abbreviation places nothing: two rivals
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "place", "Heaton"))); // no qualifier, two namesakes: the aligned Bradford entity does not answer silently
        Assert.Equal("Heaton, Bradford", (await pg.Store.ResolveAsync(ds, "place", "Heaton, Bradford"))!.Label);
        Assert.Equal(Outcome.MatchedStore, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "place", "Heaton, Bradford, Yorkshire"))); // the same place again, by its qualifier
        Assert.Equal(Outcome.Accepted, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "place", "Heaton, Newcastle"))); // the other one, past the aligned Bradford entity
        Assert.Contains((await pg.Store.ResolveAsync(ds, "place", "Heaton, Newcastle"))!.Equivalents, q => q.Uri.EndsWith("7000002"));
        // a minted qualified place keeps its qualifier in the label, so it does not become a bare name that captures namesakes
        var minted = await pg.Store.CollectAsync(ds, "place", "Moggerhanger, Bedfordshire");
        Assert.Equal(Outcome.Minted, await reconciler.ReconcileOneAsync(ds, minted));
        Assert.Equal("Moggerhanger, Bedfordshire", (await pg.Store.ResolveAsync(ds, "place", "Moggerhanger, Bedfordshire"))!.Label);
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
        // Nobody (minted), the wrongly dated Dürer (ULAN vetoed by dates, and the LoC record it is linked to falls with it: minted)
        Assert.Equal(new ReconcileReport(Considered: 6, Overridden: 1, MatchedStore: 1, Accepted: 1, Queued: 1, Minted: 2, Deferred: 0, Skipped: 0), report);

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

        // minted: nothing found. The wrongly dated string: the ULAN candidate is vetoed by its dates and takes the linked LoC record with it
        Assert.Equal(DecisionMethod.Minted, (await pg.Store.DecisionForAsync(nobody.Id))!.Method);
        var minted = (await pg.Store.ResolveAsync(ds, "persauthorfull", "Nobody, Zed, 1900-1980"))!;
        Assert.Equal("Nobody, Zed", minted.Label);
        Assert.Equal(1900, minted.Lifespan!.BeginEarliest!.Value.Year);
        var conflict = (await pg.Store.DecisionForAsync(wrongDates.Id))!;
        Assert.Equal(DecisionMethod.Minted, conflict.Method);
        Assert.Contains("dates conflict", conflict.EvidenceJson);
        Assert.Contains("vetoed", conflict.EvidenceJson);
        Assert.NotEqual(durer.Id, conflict.EntityId); // a provisional entity of its own, not Dürer's

        // lookalikes: a sibling of a string awaiting review is queued too, never answered by the lookalike; a sibling of an
        // unknown minted name is consolidated onto it; two provisional lookalikes with different dates make an undated string ambiguous
        var smithAgain = await pg.Store.CollectAsync(ds, "persauthorfull", "Smith, John (editor)");
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, smithAgain));
        var nobodyAgain = await pg.Store.CollectAsync(ds, "persauthorfull", "Nobody, Zed (editor)");
        Assert.Equal(Outcome.MatchedStore, await reconciler.ReconcileOneAsync(ds, nobodyAgain));
        Assert.Equal(minted.Id, (await pg.Store.ResolveAsync(ds, "persauthorfull", "Nobody, Zed (editor)"))!.Id);
        await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Brown, Mary, 1800-1850"));
        await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Brown, Mary, 1900-1960"));
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Brown, Mary")));
        // a dated string whose only candidate has dates on the other side is not accepted on a single source
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n00000001", "loc", "Green, Ann", ["Green, Ann"], "Person", new Lifespan(null, null, new DateTimeOffset(1700, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1700, 12, 31, 0, 0, 0, TimeSpan.Zero), "-1700"), [], null, DateTimeOffset.UtcNow, "test"));
        Assert.Equal(Outcome.Minted, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Green, Ann, 1850-"))); // born after the record's death: conflict
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n00000002", "loc", "White, Bea", ["White, Bea"], "Person", new Lifespan(null, null, new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1900, 12, 31, 0, 0, 0, TimeSpan.Zero), "-1900"), [], null, DateTimeOffset.UtcNow, "test"));
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "White, Bea, 1850-"))); // not comparable: review, not acceptance
        // policy: an undated string whose one exact match in one source is the only candidate at all is accepted as exact-single
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n00000003", "loc", "Black, Cy", ["Black, Cy"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        var black = await pg.Store.CollectAsync(ds, "persauthorfull", "Black, Cy (author)");
        Assert.Equal(Outcome.Accepted, await reconciler.ReconcileOneAsync(ds, black));
        Assert.Equal(DecisionMethod.ExactSingle, (await pg.Store.DecisionForAsync(black.Id))!.Method);
        Assert.Contains((await pg.Store.ResolveAsync(ds, "persauthorfull", "Black, Cy (author)"))!.Equivalents, q => q.Uri.EndsWith("n00000003") && q.Method == DecisionMethod.ExactSingle);
        // not when a namesake exists anywhere, however it scores
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n00000004", "loc", "Grey, Di", ["Grey, Di"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n00000005", "loc", "Grey, Di, 1950-", ["Grey, Di, 1950-", "Grey, Di"], "Person", new Lifespan(new DateTimeOffset(1950, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1950, 12, 31, 0, 0, 0, TimeSpan.Zero), null, null, "1950-"), [], null, DateTimeOffset.UtcNow, "test"));
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Grey, Di (author)")));
        // nor for a heading that carries dates: LoC dated "Taylor, Desmond, 1930-" to tell him from other Desmond Taylors
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n81013508", "loc", "Taylor, Desmond, 1930-", ["Taylor, Desmond, 1930-", "Taylor, Desmond"], "Person", new Lifespan(new DateTimeOffset(1930, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(1930, 12, 31, 0, 0, 0, TimeSpan.Zero), null, null, "1930-"), [], "{\"headingDates\":\"1930-\"}", DateTimeOffset.UtcNow, "test"));
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Taylor, Desmond (author)")));
        // nor for a one-word name: an honorific and a surname is not an exact name
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500000777", "ulan", "Hoard, William Henry", ["Hoard, William Henry", "Hoard"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        Assert.Equal(Outcome.Queued, await reconciler.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Mr Hoard"))); // the test corpauthor context allows groups only
        // and not at all when the project turns the policy off
        var strict = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan"), new LocalIndexSource(pg.Store, "loc")], options with { AcceptUniqueExactSingleSource = false });
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n00000006", "loc", "Pink, Al", ["Pink, Al"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        Assert.Equal(Outcome.Queued, await strict.ReconcileOneAsync(ds, await pg.Store.CollectAsync(ds, "persauthorfull", "Pink, Al (author)")));
        // a truncated lookup never accepts: with room for one record, the dated Dürer string is queued although ULAN alone would have carried it
        var narrow = new Reconciler(pg.Store, scheme, [new LocalIndexSource(pg.Store, "ulan", limit: 1)], options);
        var narrowString = await pg.Store.CollectAsync(ds, "persauthorfull", "Smith, John, 1700-1750");
        var narrowEvaluation = await narrow.EvaluateAsync(ds, narrowString, includeStore: false);
        Assert.True(narrowEvaluation.Truncated);
        Assert.NotEqual(Outcome.Accepted, narrowEvaluation.Outcome);
        await narrow.CommitAsync(ds, narrowString, narrowEvaluation);

        // a later run: a new spelling of a known person is answered from the store, not the indexes
        var later = await pg.Store.CollectAsync(ds, "persauthorfull", "Durer, Albrecht (engraver)");
        var again = await reconciler.RunAsync(ds);
        Assert.Equal(1, again.Considered);
        Assert.Equal(1, again.MatchedStore);
        // an imported entity without dates is checked against its authority record's dates before the store answers for it
        var imported = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Dürer, Albrecht", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(imported.Id, new Equivalent("http://vocab.getty.edu/ulan/500115493", "ulan", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var modern = await pg.Store.CollectAsync(ds, "persauthorfull", "Dürer, Albrecht, 1900-1980");
        var modernEvaluation = await reconciler.EvaluateAsync(ds, modern);
        Assert.NotEqual(Outcome.MatchedStore, modernEvaluation.Outcome);
        Assert.Contains(modernEvaluation.Groups.SelectMany(g => g.Members), m => m.Candidate.EntityId == imported.Id && m.Veto);
        await reconciler.CommitAsync(ds, modern, modernEvaluation);
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
        Assert.Equal(3, audit.WithUlan); // Dürer, the impostor, and the undated import above
        Assert.True(audit.Clean >= 1);
        // an entity with only the LoC id is reached through ULAN's link and could gain the ULAN id
        var locOnly = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Durer, Albrecht", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(locOnly.Id, new Equivalent("http://id.loc.gov/authorities/names/n79118011", "loc", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var viaLink = new List<AuditItem>();
        var audit2 = await new IndexAudit(pg.Store).RunAsync(ds, item => { viaLink.Add(item); return Task.CompletedTask; });
        Assert.Equal(1, audit2.ViaLink);
        Assert.Contains(viaLink, f => f.Entity.Id == locOnly.Id && f.Flags.Contains(AuditFlag.UlanViaLink) && f.Detail.Contains("500115493"));
        // two ULAN ids on one entity are flagged, and both are checked
        var conflated = await pg.Store.AddEntityAsync(ds, new NewEntity(EntityClass.Person, "Dürer, Albrecht", Status: EntityStatus.Aligned), scheme);
        await pg.Store.AddEquivalentAsync(conflated.Id, new Equivalent("http://vocab.getty.edu/ulan/500115493", "ulan", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        await pg.Store.AddEquivalentAsync(conflated.Id, new Equivalent("http://vocab.getty.edu/ulan/500000001", "ulan", DecisionMethod.Imported, 1, DateTimeOffset.UtcNow));
        var multi = new List<AuditItem>();
        await new IndexAudit(pg.Store).RunAsync(ds, item => { multi.Add(item); return Task.CompletedTask; });
        var flaggedMulti = Assert.Single(multi, f => f.Entity.Id == conflated.Id);
        Assert.Contains(AuditFlag.MultipleUlan, flaggedMulti.Flags);
        Assert.Contains(AuditFlag.LabelMismatch, flaggedMulti.Flags); // Smith, John is not Dürer
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
        // a run that voted: its entity and its equivalents go too, so a rerun cannot find its own old answer
        var voteRun = await pg.Store.StartRunAsync(ds);
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://vocab.getty.edu/ulan/500999999", "ulan", "Voted, Vera", ["Voted, Vera"], "Person", GettyVocabLoader.LifespanFromYears("1900", "1980"), ["http://id.loc.gov/authorities/names/n99999999"], null, DateTimeOffset.UtcNow, "test"));
        await pg.Store.PutAuthorityAsync(new AuthorityRecord("http://id.loc.gov/authorities/names/n99999999", "loc", "Voted, Vera", ["Voted, Vera"], "Person", null, [], null, DateTimeOffset.UtcNow, "test"));
        var vera = await pg.Store.CollectAsync(ds, "persauthorfull", "Voted, Vera, 1900-1980");
        Assert.Equal(Outcome.Accepted, await reconciler.ReconcileOneAsync(ds, vera, voteRun.Id));
        var veraEntity = (await pg.Store.ResolveAsync(ds, "persauthorfull", "Voted, Vera, 1900-1980"))!;
        Assert.Equal(EntityStatus.Aligned, veraEntity.Status);
        var voteUndone = await pg.Store.UndoRunAsync(voteRun.Id);
        Assert.Equal(1, voteUndone.ProvisionalEntitiesRemoved);
        Assert.Null(await pg.Store.GetEntityAsync(veraEntity.Id));
        Assert.Empty(await pg.Store.FindEntitiesByEquivalentAsync("http://vocab.getty.edu/ulan/500999999"));
        Assert.Equal(1, (await reconciler.RunAsync(ds)).Considered); // the string is open again
    }
}
