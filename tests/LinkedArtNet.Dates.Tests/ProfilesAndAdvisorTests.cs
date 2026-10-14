using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Tests;

public class ProfilesAndAdvisorTests
{
    private static readonly ProfileCatalog Catalog = ProfileCatalog.Load(TestPaths.Profiles);

    [Fact]
    public void Every_profile_file_loads_and_inherits_the_default()
    {
        Assert.Contains("default", Catalog.Profiles.Keys);
        foreach (var name in new[] { "library", "archive-us", "catalogue-quarters", "medieval-manuscripts", "multilingual" })
        {
            var p = Catalog[name];
            Assert.Equal(name, p.Name);
            Assert.NotNull(p.Description);
            Assert.Equal(ParseProfile.Default.AcceptThreshold, p.AcceptThreshold); // inherited, not restated
        }
        Assert.Equal(1900, Catalog["library"].TwoDigitYearWindow);
        Assert.Equal(DayMonthOrder.MonthFirst, Catalog["archive-us"].DayMonthOrder);
        Assert.Equal(WinterPolicy.StartOfYear, Catalog["archive-us"].Winter);
        Assert.Equal(CenturyConvention.Strict, Catalog["medieval-manuscripts"].CenturyConvention);
        Assert.Equal(["en", "la"], Catalog["medieval-manuscripts"].Languages);
        Assert.Equal(7, Catalog["multilingual"].Languages.Count);
    }

    [Theory]
    [InlineData("archive-us", "7/4/1776", "1776-07-04")]
    [InlineData("default", "7/4/1776", "1776-04-07")]
    [InlineData("archive-us", "Winter 1776", "1776-24")]
    [InlineData("catalogue-quarters", "late 20th century", "late century 20")]
    [InlineData("library", "'08", "1908")]
    [InlineData("medieval-manuscripts", "saec. XIV", "century 14")]
    [InlineData("medieval-manuscripts", "s. xiv med.", "mid century 14")]
    public void Profiles_change_the_reading(string profile, string text, string canonical)
    {
        var parser = new TimeSpanParser(Catalog[profile]);
        Assert.Equal(canonical, parser.Parse(text).Canonical);
    }

    [Fact]
    public void Profiles_change_the_realisation()
    {
        // archive-us: timetwister's conventions. Winter opens the year and ends at the equinox; autumn runs to the year's end;
        // decade bands overlap as halves.
        Assert.Equal("1776-01-01T00:00:00Z", new TimeSpanParser(Catalog["archive-us"]).Parse("Winter 1776").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1776-03-20T23:59:59Z", new TimeSpanParser(Catalog["archive-us"]).Parse("Winter 1776").TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1776-03-20T00:00:00Z", new TimeSpanParser(Catalog["archive-us"]).Parse("1776 Spring").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1776-06-21T23:59:59Z", new TimeSpanParser(Catalog["archive-us"]).Parse("1776 Spring").TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1776-09-23T00:00:00Z", new TimeSpanParser(Catalog["archive-us"]).Parse("Fall 1776").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1776-12-31T23:59:59Z", new TimeSpanParser(Catalog["archive-us"]).Parse("Fall 1776").TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1775-12-31T23:59:59Z", new TimeSpanParser(Catalog["archive-us"]).Parse("early 1770's").TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1775-01-01T00:00:00Z", new TimeSpanParser(Catalog["archive-us"]).Parse("late 1770's").TimeSpan!.BeginOfTheBegin?.ToString());
        // catalogue-quarters: yearrange's conventions. 'mid' is the middle fifth of a century and the middle three years of a decade.
        Assert.Equal("1940-01-01T00:00:00Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("mid 20th century").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1959-12-31T23:59:59Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("mid 20th century").TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1834-01-01T00:00:00Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("mid 1830s").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1836-12-31T23:59:59Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("mid 1830s").TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1975-01-01T00:00:00Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("late 20th century").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1524-12-31T23:59:59Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("beginning of 16th century").TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1475-01-01T00:00:00Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("end of the 15th century").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1790-01-01T00:00:00Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("early 1790s").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1792-12-31T23:59:59Z", new TimeSpanParser(Catalog["catalogue-quarters"]).Parse("early 1790s").TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1301-01-01T00:00:00Z", new TimeSpanParser(Catalog["medieval-manuscripts"]).Parse("saec. XIV").TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1390-01-01T00:00:00Z", new TimeSpanParser(Catalog["medieval-manuscripts"]).Parse("c. 1400").TimeSpan!.BeginOfTheBegin?.ToString());
    }

    [Fact]
    public void Inheritance_cycles_and_name_mismatches_are_rejected()
    {
        var dir = Path.Combine(Path.GetTempPath(), "la-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.json"), """{ "inherits": "b", "name": "a" }""");
            File.WriteAllText(Path.Combine(dir, "b.json"), """{ "inherits": "a", "name": "b" }""");
            Assert.Throws<InvalidDataException>(() => ProfileCatalog.Load(dir));
            File.Delete(Path.Combine(dir, "b.json"));
            File.WriteAllText(Path.Combine(dir, "a.json"), """{ "inherits": "default", "name": "other" }""");
            Assert.Throws<InvalidDataException>(() => ProfileCatalog.Load(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Advisor_recommends_halves_when_mid_never_occurs()
    {
        var strings = Enumerable.Range(0, 12).Select(i => $"early {1500 + i * 10}s").Concat(Enumerable.Range(0, 12).Select(i => $"late 1{i + 5}th century"));
        var report = ProfileAdvisor.Analyse(strings);
        Assert.Equal(24, report.Strings);
        Assert.Equal(12, report.Bands["early"]);
        Assert.Contains(report.Recommendations, r => r.Contains("halves"));
        Assert.Contains("Profile advice", report.ToMarkdown());
    }

    [Fact]
    public void Advisor_spots_month_first_numeric_dates_two_digit_years_and_languages()
    {
        var strings = new[] { "7/4/1776", "12/25/1850", "1/31/08", "vers 1850", "XIVe siècle", "um 1850", "ca. 1850", "1990s" };
        var report = ProfileAdvisor.Analyse(strings);
        Assert.Equal(3, report.NumericDates);
        Assert.Equal(2, report.NumericMonthFirstEvidence);
        Assert.Equal(0, report.NumericDayFirstEvidence);
        Assert.Contains(report.Recommendations, r => r.Contains("monthFirst"));
        Assert.Contains(report.Recommendations, r => r.Contains("twoDigitYearWindow"));
        Assert.True(report.Languages.ContainsKey("fr"));
        Assert.True(report.Languages.ContainsKey("de"));
    }
}
