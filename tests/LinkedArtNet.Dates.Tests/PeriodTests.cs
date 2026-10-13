using LinkedArtNet.Dates.Periods;

namespace LinkedArtNet.Dates.Tests;

public class PeriodTests
{
    private static readonly JsonNamedPeriodSource Source = JsonNamedPeriodSource.BuiltIn;

    [Theory]
    [InlineData("Edo period", "edo", "1603/1868")]
    [InlineData("edo", "edo", "1603/1868")]
    [InlineData("Tokugawa period", "edo", "1603/1868")]
    [InlineData("江戸時代", "edo", "1603/1868")]
    [InlineData("Meiji", "meiji", "1868-10-23/1912-07-30")]
    [InlineData("Shōwa", "showa", "1926-12-25/1989-01-07")]
    [InlineData("Showa era", "showa", "1926-12-25/1989-01-07")]
    [InlineData("Reiwa", "reiwa", "2019-05-01/..")]
    [InlineData("Kangxi", "kangxi", "1662/1722")]
    [InlineData("K'ang-hsi", "kangxi", "1662/1722")]
    [InlineData("Ming dynasty", "ming", "1368/1644")]
    [InlineData("Victorian", "victorian", "1837/1901")]
    [InlineData("Regency", "regency", "1811/1820")]
    [InlineData("George III", "george-iii", "1760-10-25/1820-01-29")]
    [InlineData("Roman Britain", "roman-britain", "0043/0410")]
    [InlineData("WWII", "second-world-war", "1939-09-01/1945-09-02")]
    public void Resolves_names(string text, string key, string resolved)
    {
        Assert.True(Source.TryResolve(text, out var period), text);
        Assert.Equal(key, period.Key);
        Assert.Equal(resolved, period.Resolved!.Canonical);
    }

    [Theory]
    [InlineData("Meiji 27", "meiji-27", 1894)]
    [InlineData("Meiji 1", "meiji-1", 1868)]
    [InlineData("Showa 45", "showa-45", 1970)]
    [InlineData("Heisei 31", "heisei-31", 2019)]
    [InlineData("Kangxi 12", "kangxi-12", 1673)]
    [InlineData("Qianlong 60th year", "qianlong-60", 1795)]
    [InlineData("Wanli 10", "wanli-10", 1582)]
    public void Counts_regnal_years(string text, string key, long year)
    {
        Assert.True(Source.TryResolve(text, out var period), text);
        Assert.Equal(key, period.Key);
        Assert.Equal(new YearPoint(year), period.Resolved);
        Assert.True(Source.TryResolveKey(key, out var byKey));
        Assert.Equal(period, byKey);
    }

    [Theory]
    [InlineData("Victoria 10")]      // reigns are not regnal-counted
    [InlineData("Baroque")]          // styles are not periods
    [InlineData("Renaissance")]
    [InlineData("1850")]
    [InlineData("")]
    public void Does_not_resolve(string text)
    {
        Assert.False(Source.TryResolve(text, out _));
    }

    [Fact]
    public void Tables_are_consistent()
    {
        foreach (var table in Source.Tables)
        foreach (var p in table.Periods)
        {
            var span = new Realisation.TimeSpanRealiser().RealiseSpan(p.Resolved);
            Assert.True(span.Start is not null, $"{p.Key} has no start");
            if (span.End is { } end) Assert.True(span.Start!.Value <= end, $"{p.Key} ends before it starts");
            Assert.NotEmpty(p.Names);
        }
        Assert.Equal(4, Source.Tables.Count);
    }

    [Fact]
    public void Canonical_keys_resolve_through_the_reader()
    {
        var e = DateExpression.Parse("early period:edo");
        Assert.Equal(new Banded(new NamedPeriod("edo"), Band.Early), e);

        var resolved = Canonical.CanonicalReader.Parse("early period:edo~", Source);
        var banded = Assert.IsType<Banded>(Assert.IsType<Qualified>(resolved).Inner);
        var period = Assert.IsType<NamedPeriod>(banded.Inner);
        Assert.Equal("1603/1868", period.Resolved!.Canonical);
        Assert.Equal("early period:edo~", resolved.Canonical);

        var unknown = Canonical.CanonicalReader.Parse("period:atlantis", Source);
        Assert.Null(Assert.IsType<NamedPeriod>(unknown).Resolved);
    }

    [Theory]
    [InlineData("Edo period", "period:edo", "1603-01-01T00:00:00Z", "1868-12-31T23:59:59Z")]
    [InlineData("early Edo period", "early period:edo", "1603-01-01T00:00:00Z", "1692-12-31T23:59:59Z")]
    [InlineData("late Edo", "late period:edo", "1781-01-01T00:00:00Z", "1868-12-31T23:59:59Z")]
    [InlineData("Meiji 27", "period:meiji-27", "1894-01-01T00:00:00Z", "1894-12-31T23:59:59Z")]
    [InlineData("Meiji 27 (1894)", "period:meiji-27", "1894-01-01T00:00:00Z", "1894-12-31T23:59:59Z")]
    [InlineData("c. Kangxi period", "period:kangxi~", "1662-01-01T00:00:00Z", "1722-12-31T23:59:59Z")]
    [InlineData("Victorian", "period:victorian", "1837-01-01T00:00:00Z", "1901-12-31T23:59:59Z")]
    [InlineData("Edo - Meiji", "period:edo/period:meiji", "1603-01-01T00:00:00Z", "1912-07-30T23:59:59Z")]
    [InlineData("Qianlong 60th year", "period:qianlong-60", "1795-01-01T00:00:00Z", "1795-12-31T23:59:59Z")]
    [InlineData("period:edo", "period:edo", "1603-01-01T00:00:00Z", "1868-12-31T23:59:59Z")]
    public void Parses_period_text_end_to_end(string text, string canonical, string begin, string end)
    {
        var r = TimeSpanParser.Default.Parse(text);
        Assert.Equal(ParseStatus.Parsed, r.Status);
        Assert.Equal(canonical, r.Canonical);
        Assert.Equal(begin, r.TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal(end, r.TimeSpan.EndOfTheEnd?.ToString());
        Assert.Contains(DateFlag.NamedPeriod, r.Flags);
        Assert.Equal(text, r.TimeSpan.Label);
    }

    [Fact]
    public void A_custom_table_extends_the_built_in_ones()
    {
        var custom = new JsonNamedPeriodSource().AddJson("""
            { "name": "test", "licence": "CC0-1.0", "periods": [
              { "key": "baroque", "label": "Baroque", "names": ["baroque"], "start": "1600", "end": "1750" } ] }
            """);
        var parser = new TimeSpanParser(periods: new CompositePeriodSource(custom, JsonNamedPeriodSource.BuiltIn));
        Assert.Equal("period:baroque", parser.Parse("Baroque").Canonical);
        Assert.Equal("period:edo", parser.Parse("Edo period").Canonical);
        Assert.Equal(ParseStatus.Unparsed, TimeSpanParser.Default.Parse("Baroque").Status);
    }
}
