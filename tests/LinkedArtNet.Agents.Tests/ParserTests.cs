using LinkedArtNet.Dates;
using LinkedArtNet.Serialization;

namespace LinkedArtNet.Agents.Tests;

public class ParserTests
{
    private static ParsedAgent Parse(string text, string profile = "default") =>
        new AgentStringParser(AgentProfileCatalog.BuiltIn.Get(profile)).Parse(text);

    [Fact]
    public void An_ampersand_or_and_son_makes_a_firm()
    {
        Assert.Equal(AgentKind.Group, Parse("Crews & Son", "dscribe").Kind); // 2026-10-19 corpus run minted this as a person
        Assert.Equal(AgentKind.Group, Parse("Edward Jas Carter & Banks", "dscribe").Kind);
        Assert.Equal(AgentKind.Group, Parse("Thomas Agnew and Son", "dscribe").Kind);
        Assert.Equal(AgentKind.Person, Parse("Anderson, Edward", "dscribe").Kind); // "son" inside a word is not the word
    }

    [Fact]
    public void A_body_keeps_the_title_in_its_name()
    {
        var museum = Parse("Queen Victoria Museum", "dscribe"); // the 2026-10-20 shadow run read this as "Victoria Museum" and matched another museum
        Assert.Equal(AgentKind.Group, museum.Kind);
        Assert.Equal("Queen Victoria Museum", museum.Name);
        Assert.Empty(museum.Honorifics);
        Assert.Equal("Sir John Soane's Museum", Parse("Sir John Soane's Museum", "dscribe").Name);
        Assert.Equal("John Smith", Parse("Sir John Smith", "default").Name); // a person still gives up the title
    }

    [Fact]
    public void A_single_letter_is_an_initial_not_a_title()
    {
        Assert.Equal("Larsen, Ruth M", Parse("Larsen, Ruth M (editor)", "dscribe").Name); // "M" is Monsieur in the table, an initial here
        Assert.Equal("Bennett, Shelley M.", Parse("Bennett, Shelley M. (author)", "dscribe").Name);
        Assert.Equal("Smith, J. Robert", Parse("Smith, J. Robert (author)", "dscribe").Name);
        var direct = Parse("J. Smith", "default");
        Assert.Equal("J. Smith", direct.Name);
        Assert.Empty(direct.Honorifics);
        Assert.Equal(["Mr"], Parse("Smith, Mr John", "dscribe").Honorifics); // a real title still comes off
    }

    [Fact]
    public void DScribe_bracket_and_parenthesis_form()
    {
        var a = Parse("Grierson, Herbert John Clifford [Sir, 1866-1960] (preface by)", "dscribe");
        Assert.Equal(AgentKind.Person, a.Kind);
        Assert.Equal("Grierson, Herbert John Clifford", a.Name);
        Assert.Equal("Grierson", a.FamilyName);
        Assert.Equal("Herbert John Clifford", a.GivenNames);
        Assert.Equal("Herbert John Clifford Grierson", a.DisplayName);
        Assert.Equal(["Sir"], a.Honorifics);
        Assert.Equal("1866-1960", a.DateText);
        Assert.Equal("1866/1960", a.Dates!.Canonical);
        Assert.Equal(ResultKind.BirthDeath, a.Dates.Kind);
        Assert.NotNull(a.Dates.Begin); Assert.NotNull(a.Dates.End);
        Assert.Equal("writer of preface", Assert.Single(a.Roles).Label);
        Assert.Null(a.Unconsumed);
        Assert.True(a.Confidence >= 0.9);
    }

    [Fact]
    public void Open_ended_dates_and_a_known_role()
    {
        var a = Parse("Ashworth, Mary [1958-] (editor)", "dscribe");
        Assert.Equal("Ashworth, Mary", a.Name);
        Assert.Equal("1958-", a.DateText);
        Assert.NotNull(a.Dates!.Begin);
        Assert.Null(a.Dates.End);
        Assert.Equal("editor", Assert.Single(a.Roles).Label);
    }

    [Theory]
    [InlineData("Leconte de Lisle, Charles Marie, 1818-1894", "Leconte de Lisle, Charles Marie", "1818-1894", null)]
    [InlineData("Rembrandt Harmenszoon van Rijn, 1606-1669, artist", "Rembrandt Harmenszoon van Rijn", "1606-1669", "artist")]
    [InlineData("Smith, John, active 1640-1660", "Smith, John", "active 1640-1660", null)]
    [InlineData("Smith, John, approximately 1600-1650, ill.", "Smith, John", "approximately 1600-1650", "illustrator")]
    [InlineData("Smith, John, -1690", "Smith, John", "-1690", null)]
    public void Marc_headings_give_up_their_tail(string input, string name, string dateText, string? roleLabel)
    {
        var a = Parse(input, "marc");
        Assert.Equal(name, a.Name);
        Assert.Equal(dateText, a.DateText);
        Assert.True(a.HasDates, string.Join("; ", a.Dates?.Notes ?? []));
        if (roleLabel is null) Assert.Empty(a.Roles); else Assert.Equal(roleLabel, Assert.Single(a.Roles).Label);
    }

    [Theory]
    [InlineData("Milton, John 1608-1674", "Milton, John", "1608-1674")]
    [InlineData("Gander, Ryan 1976-", "Gander, Ryan", "1976-")]
    [InlineData("Starr, John W. 1925-", "Starr, John W.", "1925-")]
    [InlineData("Pelham, Henry 1695?-1754", "Pelham, Henry", "1695?-1754")]
    [InlineData("Smith, John 1866-1960, artist", "Smith, John", "1866-1960")]
    public void A_date_segment_opening_with_name_words_is_the_forenames_and_the_dates_with_their_comma_dropped(string input, string name, string dateText)
    {
        // 2026-10-23: the reconciler had matched "Milton, John 1608-1674" and eight headings like it on the surname alone, the dates
        // parser having swallowed "John 1608-1674" whole as a date segment
        var a = Parse(input, "marc");
        Assert.Equal(name, a.Name);
        Assert.Equal(dateText, a.DateText);
        Assert.Equal(name.Split(", ")[1], a.GivenNames);
        Assert.Null(a.Unconsumed);
        // a date segment that is only dates, or opens with a date word, is unchanged; "B. 1930" is "born 1930" whatever its case (run t629cj,
        // 2026-10-24: "Bell, Trevor, B. 1930" had lost its vote to a name "Bell, Trevor, B.")
        Assert.Equal("Bevan, Charles", Parse("Bevan, Charles, active 1865-1883", "marc").Name);
        Assert.Equal("James, Hugh", Parse("James, Hugh, d. 1695", "marc").Name);
        var bell = Parse("Bell, Trevor, B. 1930", "marc");
        Assert.Equal("Bell, Trevor", bell.Name); Assert.Equal("B. 1930", bell.DateText);
    }

    [Fact]
    public void Marc_honorific_sits_between_name_and_dates()
    {
        var a = Parse("Smith, John, Sir, 1866-1960", "marc");
        Assert.Equal("Smith, John", a.Name);
        Assert.Equal(["Sir"], a.Honorifics);
        Assert.Equal("1866-1960", a.DateText);
    }

    [Theory]
    [InlineData("Dutch painter, 1606-1669", "Dutch", "painter", "1606-1669")]
    [InlineData("French illuminator, active mid-15th century", "French", "illuminator", "active mid-15th century")]
    public void Ulan_display_strings_have_no_name(string input, string nationality, string occupation, string dateText)
    {
        var a = Parse(input, "ulan");
        Assert.Null(a.Name);
        Assert.Equal(nationality, Assert.Single(a.Nationalities).Label);
        Assert.Equal(occupation, Assert.Single(a.Roles).Label);
        Assert.Equal(dateText, a.DateText);
        Assert.True(a.HasDates);
        Assert.Equal(AgentKind.Person, a.Kind);
    }

    [Fact]
    public void Ulan_two_occupations()
    {
        var a = Parse("Italian painter and architect, 1483-1520", "ulan");
        Assert.Equal("Italian", Assert.Single(a.Nationalities).Label);
        Assert.Equal(["painter", "architect"], a.Roles.Select(r => r.Label));
        Assert.Contains(a.Roles, r => r.Label == "architect" && r.AatUri == "http://vocab.getty.edu/aat/300024987");
        Assert.Contains(a.Nationalities, n => n.AatUri == "http://vocab.getty.edu/aat/300111198");
    }

    [Fact]
    public void Ead_semicolon_form()
    {
        var a = Parse("Smith; John (1900-1980); artist", "ead");
        Assert.Equal("Smith, John", a.Name);
        Assert.Equal("1900-1980", a.DateText);
        Assert.Equal("artist", Assert.Single(a.Roles).Label);
    }

    [Theory]
    [InlineData("attributed to Rembrandt", "Rembrandt", AgentQualifier.AttributedTo)]
    [InlineData("after Rembrandt", "Rembrandt", AgentQualifier.After)]
    [InlineData("Circle of Rembrandt", "Rembrandt", AgentQualifier.CircleOf)]
    [InlineData("Rembrandt and others", "Rembrandt", AgentQualifier.AndOthers)]
    [InlineData("Rembrandt (attributed)", "Rembrandt", AgentQualifier.AttributedTo)]
    [InlineData("Rembrandt?", "Rembrandt", AgentQualifier.Possibly)]
    public void Qualifiers_come_off_the_name(string input, string name, AgentQualifier qualifier)
    {
        var a = Parse(input);
        Assert.Equal(name, a.Name);
        Assert.Contains(qualifier, a.Qualifiers);
        Assert.Null(a.Unconsumed);
    }

    [Theory]
    [InlineData("Anonymous")]
    [InlineData("Unknown artist")]
    [InlineData("anon.")]
    public void Anonymous_has_no_name(string input)
    {
        var a = Parse(input);
        Assert.Null(a.Name);
        Assert.Equal(AgentKind.Unknown, a.Kind);
        Assert.True(a.Qualifiers.Contains(AgentQualifier.Anonymous) || a.Qualifiers.Contains(AgentQualifier.Unknown));
    }

    [Theory]
    [InlineData("Royal Academy of Arts [1768-] (publisher)", "Royal Academy of Arts", "1768-")]
    [InlineData("Thames & Hudson Ltd (publisher)", "Thames & Hudson Ltd", null)]
    [InlineData("Society of Antiquaries of London", "Society of Antiquaries of London", null)]
    public void Corporate_bodies_are_groups(string input, string name, string? dateText)
    {
        var a = Parse(input, "dscribe");
        Assert.Equal(AgentKind.Group, a.Kind);
        Assert.Equal(name, a.Name);
        Assert.Equal(dateText, a.DateText);
        Assert.Null(a.FamilyName);
    }

    [Fact]
    public void Direct_order_honorific_and_a_role_the_table_does_not_know()
    {
        var a = Parse("Sir Joshua Reynolds (contributor of two plates)", "dscribe");
        Assert.Equal("Joshua Reynolds", a.Name);
        Assert.Equal(["Sir"], a.Honorifics);
        var role = Assert.Single(a.Roles);
        Assert.Equal("contributor of two plates", role.Text);
        Assert.Null(role.AatUri);
        Assert.Contains(a.Notes, n => n.Contains("not in the role table"));
        Assert.InRange(a.Confidence, 0.85, 0.95);
    }

    [Fact]
    public void A_date_with_a_comma_inside_brackets_stays_one_date()
    {
        var a = Parse("Smith, John [b. 12 May, 1866]", "dscribe");
        Assert.Equal("Smith, John", a.Name);
        Assert.Equal("b. 12 May, 1866", a.DateText);
        Assert.True(a.Dates!.Begin is not null || a.Dates.TimeSpan is not null); // the dates parser's reading of the comma form is its own matter
    }

    [Fact]
    public void Unplaceable_text_is_reported_not_dropped()
    {
        var a = Parse("Smith, John [see also Jones]", "dscribe");
        Assert.Equal("Smith, John", a.Name);
        Assert.Equal("see also Jones", a.Unconsumed);
        Assert.True(a.Confidence < 0.9);
    }

    [Fact]
    public void Empty_and_nonsense_inputs_do_not_throw()
    {
        Assert.Equal(AgentKind.Unknown, Parse("").Kind);
        Assert.Equal(0, Parse("   ").Confidence);
        var a = Parse("[[[", "dscribe");
        Assert.NotNull(a);
    }

    [Fact]
    public void Builder_makes_a_person_with_name_parts_lifespan_nationality_and_occupation()
    {
        var a = Parse("Dutch painter, 1606-1669", "ulan") with { Name = "Rembrandt van Rijn, Rembrandt Harmenszoon", FamilyName = "Rembrandt van Rijn", GivenNames = "Rembrandt Harmenszoon", SortName = "Rembrandt van Rijn, Rembrandt Harmenszoon", DisplayName = "Rembrandt Harmenszoon Rembrandt van Rijn" };
        var person = a.ToPerson("https://example.org/person/rembrandt");
        var json = LinkedArtJson.Serialize(person);
        Assert.Contains("\"born\"", json);
        Assert.Contains("1606-01-01T00:00:00Z", json);
        Assert.Contains("1669-12-31T23:59:59Z", json);
        Assert.Contains("http://vocab.getty.edu/aat/300111175", json); // Dutch
        Assert.Contains("http://vocab.getty.edu/aat/300404651", json); // given name
        Assert.Contains("http://vocab.getty.edu/aat/300404652", json); // family name
        Assert.Contains("Rembrandt Harmenszoon Rembrandt van Rijn", json);
    }

    [Fact]
    public void Builder_makes_a_group_with_formation()
    {
        var a = Parse("Royal Academy of Arts [1768-]", "dscribe");
        var group = a.ToGroup();
        var json = LinkedArtJson.Serialize(group);
        Assert.Contains("\"type\": \"Group\"", json);
        Assert.Contains("formed_by", json);
        Assert.Contains("1768-01-01T00:00:00Z", json);
        Assert.DoesNotContain("dissolved_by", json);
    }

    [Fact]
    public void Dto_carries_the_parts_and_the_linked_art_json()
    {
        var dto = AgentResultDto.From(Parse("Grierson, Herbert John Clifford [Sir, 1866-1960] (preface by)", "dscribe"));
        Assert.Equal("person", dto.Kind);
        Assert.Equal("birthDeath", dto.Dates!.Kind);
        Assert.Equal("writer of preface", dto.Roles[0].Label);
        Assert.NotNull(dto.Agent);
        Assert.Equal("Person", dto.Agent!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Profiles_load_and_inherit()
    {
        var catalog = AgentProfileCatalog.BuiltIn;
        Assert.Equal(AgentProfileCatalog.BuiltInNames.Length, catalog.Profiles.Count);
        Assert.False(catalog.Get("ulan").NameRequired);
        Assert.True(catalog.Get("ead").SemicolonForm);
        Assert.Equal("library", catalog.Get("wellcome").DatesProfile); // inherited from marc
        Assert.Equal(["qualifier", "dates", "honorific"], catalog.Get("marc").Parentheses.Select(m => m.ToString().ToLowerInvariant()));
    }

    [Fact]
    public void Tables_resolve_registry_terms_and_never_invent_ids()
    {
        var t = AgentTables.Default;
        Assert.True(t.TryRole("preface by", out var preface));
        Assert.Equal("writer of preface", preface.Label);
        Assert.Null(preface.AatUri);
        Assert.True(t.TryRole("Author.", out var author));
        Assert.Equal("http://vocab.getty.edu/aat/300025492", author.AatUri);
        Assert.True(t.TryNationality("dutch", out var dutch));
        Assert.Equal("http://vocab.getty.edu/aat/300111175", dutch.AatUri);
        Assert.True(t.TryNationality("English", out var english));
        Assert.Null(english.AatUri);
        Assert.True(t.IsHonorific("Rev."));
        Assert.True(t.TryQualifier("et al.", out var q) && q == AgentQualifier.AndOthers);
    }
}
