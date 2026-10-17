using LinkedArtNet.Alignment.Import;

namespace LinkedArtNet.Alignment.Tests;

public class SchemeTests
{
    [Fact]
    public void Pmc_scheme_keeps_the_published_paths()
    {
        var s = IdentifierScheme.Pmc;
        Assert.Equal("https://data.paul-mellon-centre.ac.uk/person/abcd2345", s.Uri(EntityClass.Person, "abcd2345"));
        Assert.Equal("https://data.paul-mellon-centre.ac.uk/group/pmc-tate-gallery", s.Uri(EntityClass.Group, "pmc-tate-gallery"));
        Assert.Equal("https://data.paul-mellon-centre.ac.uk/concept/x", s.Uri(EntityClass.Type, "x"));
        Assert.Equal("https://data.paul-mellon-centre.ac.uk/concept/x", s.Uri(EntityClass.Material, "x"));
        Assert.Equal("https://data.paul-mellon-centre.ac.uk/place/x", s.Uri(EntityClass.Place, "x"));
        Assert.True(s.Owns("https://data.paul-mellon-centre.ac.uk/person/abcd2345"));
        Assert.False(s.Owns("https://lux.collections.yale.edu/data/person/1"));
        Assert.False(s.Owns(null));
        Assert.Equal(EntityClass.Person, s.ClassOf("https://data.paul-mellon-centre.ac.uk/person/abcd2345"));
        Assert.Equal(EntityClass.Type, s.ClassOf("https://data.paul-mellon-centre.ac.uk/concept/x"));
        Assert.Null(s.ClassOf("https://data.paul-mellon-centre.ac.uk/nonesuch/x"));
        Assert.Null(s.ClassOf("https://elsewhere.org/person/x"));
    }

    [Fact]
    public void Another_dataset_gets_its_own_base_and_segments()
    {
        var s = new IdentifierScheme("https://data.example.org/wellcome", new Dictionary<EntityClass, string> { [EntityClass.Person] = "agents", [EntityClass.Group] = "agents" });
        Assert.Equal("https://data.example.org/wellcome/agents/k7", s.Uri(EntityClass.Group, "k7"));
        Assert.Equal("https://data.example.org/wellcome/place/k7", s.Uri(EntityClass.Place, "k7")); // unlisted classes fall back to their name
        Assert.StartsWith("https://data.example.org/wellcome/agents/", s.Mint(EntityClass.Person));
    }

    [Fact]
    public void Minted_ids_use_the_unambiguous_alphabet_letter_first()
    {
        for (var i = 0; i < 200; i++)
        {
            var id = IdMinter.Generate();
            Assert.Equal(8, id.Length);
            Assert.True(IdMinter.Looks(id), id);
            Assert.DoesNotContain(id, c => "01ilo".Contains(c));
            Assert.True(char.IsLetter(id[0]));
        }
        Assert.Equal(12, IdMinter.Generate(12).Length);
        Assert.False(IdMinter.Looks("1abc"));
        Assert.False(IdMinter.Looks("abc"));
        Assert.False(IdMinter.Looks("abco2345"));
        Assert.Throws<ArgumentOutOfRangeException>(() => IdMinter.Generate(3));
    }

    [Theory]
    [InlineData("  Tate   Gallery ", "tate gallery")]
    [InlineData("Smith, John.", "smith, john")]
    [InlineData("O’Keeffe, Georgia", "o'keeffe, georgia")]
    [InlineData("Leconte de Lisle, Charles Marie, 1818–1894", "leconte de lisle, charles marie, 1818-1894")]
    [InlineData("ＡＢＣ", "abc")]
    [InlineData("Rembrandt;", "rembrandt")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalisation_folds_case_space_quotes_and_dashes(string? raw, string expected) => Assert.Equal(expected, StringKey.Normalise(raw));

    [Fact]
    public void Lifespans_conflict_only_when_both_sides_know_and_disagree()
    {
        var rembrandt = new Lifespan(Y(1606), Y(1606, end: true), Y(1669), Y(1669, end: true), "1606-1669");
        var sameBirthUnknownDeath = new Lifespan(Y(1606), Y(1606, end: true), null, null);
        var later = new Lifespan(Y(1700), Y(1700, end: true), Y(1760), Y(1760, end: true));
        var silent = new Lifespan(null, null, null, null);
        Assert.True(rembrandt.Compatible(sameBirthUnknownDeath));
        Assert.True(rembrandt.Compatible(silent));
        Assert.False(rembrandt.Compatible(later));
        Assert.False(rembrandt.IsEmpty);
        Assert.True(silent.IsEmpty);
    }

    [Fact]
    public void Lifespan_from_linked_art_timespans_reads_the_bounds()
    {
        var born = new LinkedArtTimeSpan { BeginOfTheBegin = new LinkedArtDate(1866, 1, 1), EndOfTheEnd = new LinkedArtDate(1866, 12, 31, 23, 59, 59) };
        var died = new LinkedArtTimeSpan { BeginOfTheBegin = new LinkedArtDate(1960, 1, 1), EndOfTheEnd = new LinkedArtDate(1960, 12, 31, 23, 59, 59) };
        var l = Lifespan.From(born, died, "1866-1960")!;
        Assert.Equal(1866, l.BeginEarliest!.Value.Year);
        Assert.Equal(1960, l.EndLatest!.Value.Year);
        Assert.Equal("1866-1960", l.Text);
        Assert.Null(Lifespan.From(null, null));
        var bce = new LinkedArtTimeSpan { BeginOfTheBegin = LinkedArtDate.FromBce(500), EndOfTheEnd = LinkedArtDate.FromBce(400) };
        Assert.Null(Lifespan.From(bce, null)); // outside what the store's timestamps can hold; recorded as text only when text is given
        Assert.Equal("c. 500 BCE", Lifespan.From(bce, null, "c. 500 BCE")!.Text);
    }

    [Fact]
    public void Authority_uris_are_recognised_and_loc_picks_names_or_subjects()
    {
        Assert.Equal("ulan", AuthorityUris.SourceOf("http://vocab.getty.edu/ulan/500011051"));
        Assert.Equal("loc", AuthorityUris.SourceOf("http://id.loc.gov/authorities/names/n79142935"));
        Assert.Equal("lux", AuthorityUris.SourceOf("https://lux.collections.yale.edu/data/person/abc"));
        Assert.Equal("other", AuthorityUris.SourceOf("https://example.org/x"));
        Assert.Equal("http://id.loc.gov/authorities/names/n79142935", AuthorityUris.Loc("n79142935"));
        Assert.Equal("http://id.loc.gov/authorities/subjects/sh85038258", AuthorityUris.Loc("sh85038258"));
    }

    [Fact]
    public void Pmc_lux_file_parses_and_lists_its_equivalents()
    {
        var file = PmcLuxOverrides.Parse("""
            { "local_string_map": { "Tate": "pmc-tate-gallery", "Tate Gallery": "pmc-tate-gallery" },
              "authorities": [ { "identifier": "pmc-tate-gallery", "type": "Group", "label": "Tate Gallery", "viaf": "155600523", "ulan": "500301641", "loc": "n50063091" } ] }
            """);
        Assert.Equal(2, file.LocalStringMap.Count);
        var tate = Assert.Single(file.Authorities);
        Assert.Equal(["http://vocab.getty.edu/ulan/500301641", "http://viaf.org/viaf/155600523", "http://id.loc.gov/authorities/names/n50063091"], tate.EquivalentUris());
        Assert.Equal(EntityClass.Group, PmcLuxOverrides.ClassOf("Group"));
        Assert.Equal(EntityClass.Type, PmcLuxOverrides.ClassOf("Type"));
        Assert.Null(PmcLuxOverrides.ClassOf("Mystery"));
    }

    private static DateTimeOffset Y(int year, bool end = false) => end ? new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero) : new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
}
