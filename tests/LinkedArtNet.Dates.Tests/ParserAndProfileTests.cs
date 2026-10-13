using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Tests;

public class ParserAndProfileTests
{
    [Fact]
    public void Parses_canonical_input_end_to_end()
    {
        var result = TimeSpanParser.Default.Parse("late 195X");
        Assert.Equal(ParseStatus.Parsed, result.Status);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal("canonical", result.Strategy);
        Assert.Equal("late 195X", result.TimeSpan!.Label);
        Assert.Equal("1957-01-01T00:00:00Z", result.TimeSpan.BeginOfTheBegin?.ToString());
        Assert.Equal(TimeSpanParser.Version, result.ParserVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello world")]
    [InlineData("not a date at all")]
    [InlineData(null)]
    public void Never_throws_for_unreadable_input(string? input)
    {
        var result = TimeSpanParser.Default.Parse(input!);
        Assert.Equal(ParseStatus.Unparsed, result.Status);
        Assert.Equal(ResultKind.None, result.Kind);
        Assert.Empty(result.Spans());
        Assert.NotEmpty(result.Notes);
    }

    [Fact]
    public void Pair_intent_through_the_parser()
    {
        var life = TimeSpanParser.Default.Parse("1606/1669", Intent.Pair);
        var person = new Person { Born = new Birth { TimeSpan = life.Begin }, Died = new Death { TimeSpan = life.End } };
        Assert.Equal("1606/1669", person.Born.TimeSpan!.Label);
        Assert.Equal("1669-12-31T23:59:59Z", person.Died.TimeSpan!.EndOfTheEnd?.ToString());
    }

    [Fact]
    public void Options_profile_overrides_the_parser_profile()
    {
        var strict = ParseProfile.Default with { Name = "strict", CenturyConvention = CenturyConvention.Strict };
        var result = TimeSpanParser.Default.Parse("century 19", new ParseOptions { Profile = strict });
        Assert.Equal("strict", result.Profile);
        Assert.Equal("1801-01-01T00:00:00Z", result.TimeSpan!.BeginOfTheBegin?.ToString());
    }

    [Fact]
    public void Very_long_input_is_truncated_not_fatal()
    {
        var result = TimeSpanParser.Default.Parse(new string('1', 10_000));
        Assert.Equal(ParseStatus.Unparsed, result.Status);
    }

    [Fact]
    public void Default_profile_json_matches_the_built_in_default()
    {
        var path = Path.Combine(TestPaths.Profiles, "default.json");
        if (Environment.GetEnvironmentVariable("LINKEDARTNET_WRITE_PROFILES") == "1")
            File.WriteAllText(path, ParseProfile.Default.ToJson() + "\n");
        var loaded = ParseProfile.Load(path);
        Assert.Equal(ParseProfile.Default, loaded);
        Assert.Equal(Normalise(ParseProfile.Default.ToJson()), Normalise(File.ReadAllText(path)));
    }

    [Fact]
    public void Profiles_round_trip_through_json()
    {
        var profile = ParseProfile.Default with
        {
            Name = "archive", CenturyConvention = CenturyConvention.Strict, CircaPaddingYears = 5,
            OpenEnd = new OpenEndPolicy(OpenEndMode.ClampToYear, 1950), Hemisphere = Hemisphere.South,
            TwoDigitYearWindow = 1900, Languages = ["en", "fr"], LabelPolicy = LabelPolicy.LabelAndName,
        };
        Assert.Equal(profile, ParseProfile.FromJson(profile.ToJson()));
    }

    private static string Normalise(string json) => json.Replace("\r\n", "\n").TrimEnd();
}
