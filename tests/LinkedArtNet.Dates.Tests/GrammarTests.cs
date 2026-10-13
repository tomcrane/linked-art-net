using LinkedArtNet.Dates.Grammar;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Tests;

/// <summary>Free text -> canonical expression. The corpus has the breadth; these rows pin the grammar's own rules.</summary>
public class GrammarTests
{
    private static Candidate? Grammar(string text) => new GrammarStrategy().TryParse(Text.Normaliser.Normalise(text).Text, ParseProfile.Default);

    private static string Canonical(string text) => Grammar(text)?.Expression.Canonical ?? "(none)";

    [Theory]
    // years, eras
    [InlineData("1984", "1984")]
    [InlineData("500 BC", "-0499")]
    [InlineData("1 BC", "0000")]
    [InlineData("AD 79", "0079")]
    [InlineData("0 AD", "0000")]
    [InlineData("27 BC - AD 14", "-0026/0014")]
    [InlineData("480-479 BC", "-0479/-0478")]
    // hedges
    [InlineData("c. 1850", "1850~")]
    [InlineData("c1543", "1543~")]
    [InlineData("about 1850", "1850~")]
    [InlineData("probably 1850", "1850?")]
    [InlineData("1850?", "1850?")]
    [InlineData("c. 1850?", "1850%")]
    [InlineData("1861(?)", "1861?")]
    // decades and unspecified digits
    [InlineData("1950s", "195X")]
    [InlineData("the 1950's", "195X")]
    [InlineData("1800s", "18XX")]
    [InlineData("1950s-2000s", "195X/200X")]
    [InlineData("17_", "17XX")]
    [InlineData("189_", "189X")]
    [InlineData("18--", "18XX")]
    // centuries
    [InlineData("19th century", "century 19")]
    [InlineData("nineteenth century", "century 19")]
    [InlineData("XIXth century", "century 19")]
    [InlineData("C19", "century 19")]
    [InlineData("19th c.", "century 19")]
    [InlineData("s. XIV", "century 14")]
    [InlineData("5th century BC", "century -5")]
    [InlineData("2nd millennium BC", "millennium -2")]
    // bands
    [InlineData("early 14th century", "early century 14")]
    [InlineData("mid-14th century", "mid century 14")]
    [InlineData("first half of the 14th century", "h1 century 14")]
    [InlineData("2nd half 14th century", "h2 century 14")]
    [InlineData("last quarter of the 19th century", "q4 century 19")]
    [InlineData("beginning of the 19th century", "beginning century 19")]
    [InlineData("end of the 19th century", "end century 19")]
    [InlineData("late 1950s", "late 195X")]
    [InlineData("c. late 15th century", "late century 15~")]
    [InlineData("early - mid 13th century", "early century 13/mid century 13")]
    [InlineData("late 13th - early 14th century", "late century 13/early century 14")]
    [InlineData("17th-18th century", "century 17/century 18")]
    [InlineData("18th or 19th century", "[century 18,century 19]")]
    // ranges and abbreviation
    [InlineData("1870-1890", "1870/1890")]
    [InlineData("between 1870 and 1890", "1870/1890")]
    [InlineData("1870 to 1890", "1870/1890")]
    [InlineData("1847-52", "1847/1852")]
    [InlineData("c.1928 - 32", "1928~/1932~")]
    [InlineData("c.1878-79", "1878~/1879~")]
    [InlineData("1900-10s", "1900/191X")]
    [InlineData("c. 1606-1669", "1606~/1669")]
    [InlineData("[c 1960s-c 1970s]", "196X~/197X~")]
    [InlineData("[1500] - [1520]", "1500/1520")]
    [InlineData("1998-1902", "1998/1902")]
    // open ends and relations
    [InlineData("1971-", "1971/..")]
    [InlineData("-1850", "../1850")]
    [InlineData("1971 - present", "1971/..")]
    [InlineData("1971 onwards", "1971/..")]
    [InlineData("before 1850", "before 1850")]
    [InlineData("after 1850", "after 1850")]
    [InlineData("by 1850", "by 1850")]
    [InlineData("until 1850", "until 1850")]
    [InlineData("since 1850", "not-before 1850")]
    [InlineData("not after 1850", "not-after 1850")]
    [InlineData("post-1945", "after 1945")]
    [InlineData("ante 1850", "before 1850")]
    // sets
    [InlineData("1904 or 1905", "[1904,1905]")]
    [InlineData("1904 or 1905 or 1906", "[1904,1905,1906]")]
    // months, days, seasons
    [InlineData("July 2010", "2010-07")]
    [InlineData("1902 October 27", "1902-10-27")]
    [InlineData("8 May 1944-19 Aug 1946", "1944-05-08/1946-08-19")]
    [InlineData("October 27, 1902", "1902-10-27")]
    [InlineData("Apr-May 1903", "1903-04/1903-05")]
    [InlineData("Winter 1945", "1945-24")]
    // lifecycle and floruit
    [InlineData("b. 1850", "born 1850")]
    [InlineData("d. 1920", "died 1920")]
    [InlineData("died c. 1920", "died 1920~")]
    [InlineData("† 1920", "died 1920")]
    [InlineData("* 1850 † 1920", "born 1850/died 1920")]
    [InlineData("bapt. 1606", "baptised 1606")]
    [InlineData("fl. 1480-1510", "fl 1480/1510")]
    [InlineData("active 1920s", "fl 192X")]
    [InlineData("active c. 1500-1530", "fl 1500~/1530")]
    [InlineData("fl. C14", "fl century 14")]
    // deep time
    [InlineData("4.5 billion years ago", "4.5 Ga ago")]
    [InlineData("66 million years ago", "66 Ma ago")]
    [InlineData("10,000 BP", "10000 years BP")]
    [InlineData("c. 10 ka BP", "10 ka BP~")]
    // noise, parentheticals, corrections, repeats
    [InlineData("1493 (first edition published)", "1493")]
    [InlineData("1980s (when photocopies made)", "198X")]
    [InlineData("[1988], ©1988", "1988")]
    [InlineData("cop. 1980", "1980")]
    [InlineData("MDCCLXXV [1775]", "1775")]
    [InlineData("1977 (1978)", "1977/1978")]
    [InlineData("1962, (i. e. 1967)", "1967")]
    [InlineData("1898 (original drawing by Lethaby) c.1900 (traced by John Brandon-Jones)", "1898/1900~")]
    [InlineData("1 August 1876-[c 2016]", "1876-08-01/2016~")]
    // undated
    [InlineData("n.d.", "undated")]
    [InlineData("no date", "undated")]
    [InlineData("undated", "undated")]
    public void Reads_free_text(string text, string canonical)
    {
        Assert.Equal(canonical, Canonical(text));
    }

    [Theory]
    [InlineData("turn of the century")]
    [InlineData("pre-war")]
    [InlineData("Late Pleistocene")]
    [InlineData("?")]
    [InlineData("13/11/08")]
    [InlineData("hello world")]
    public void Refuses_what_it_cannot_read(string text)
    {
        var candidate = Grammar(text);
        Assert.True(candidate is null || candidate.Expression is Undated == false && candidate.UnconsumedText is not null,
            $"'{text}' read as {candidate?.Expression.Canonical}");
    }

    [Fact]
    public void Confidence_falls_with_noise_and_multiple_dates()
    {
        Assert.Equal(0.95, Grammar("1850")!.Confidence);
        Assert.True(Grammar("1898 (original drawing by Lethaby) c.1900 (traced by John Brandon-Jones)")!.Confidence <= 0.7);
        Assert.True(Grammar("1977 (1978)")!.Confidence < 0.95);
    }

    [Fact]
    public void Unconsumed_text_is_reported()
    {
        var c = Grammar("1850 something else")!;
        Assert.Equal("1850", c.Expression.Canonical);
        Assert.Equal("something else", c.UnconsumedText);
        Assert.True(c.Confidence < 0.9);
    }

    [Fact]
    public void Two_digit_years_need_a_window()
    {
        Assert.Null(Grammar("'08"));
        var windowed = new GrammarStrategy().TryParse("13/11/08", ParseProfile.Default with { TwoDigitYearWindow = 1950 });
        Assert.Equal("2008-11-13", windowed!.Expression.Canonical);
        Assert.Contains(DateFlag.TwoDigitYear, windowed.Flags!);
    }

    [Fact]
    public void Roman_numeral_years_are_flagged()
    {
        var c = Grammar("MDCCLXXV")!;
        Assert.Equal("1775", c.Expression.Canonical);
        Assert.Contains(DateFlag.RomanNumeral, c.Flags!);
    }

    [Fact]
    public void End_to_end_through_the_parser()
    {
        var r = TimeSpanParser.Default.Parse("late 1950s");
        Assert.Equal(ParseStatus.Parsed, r.Status);
        Assert.Equal("grammar", r.Strategy);
        Assert.Equal("late 195X", r.Canonical);
        Assert.Equal("1957-01-01T00:00:00Z", r.TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("late 1950s", r.TimeSpan.Label);

        var salvaged = TimeSpanParser.Default.Parse("Not dated, but the 1780s and 1792 are mentioned somewhere");
        Assert.Equal("salvage", salvaged.Strategy);
        Assert.Contains(DateFlag.Salvaged, salvaged.Flags);
        Assert.Equal("178X/1792", salvaged.Canonical);
    }
}
