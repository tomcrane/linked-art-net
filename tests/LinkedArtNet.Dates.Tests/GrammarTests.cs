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
    // abbreviated ends within the decade, ISO fragments, open ends written with a slash
    [InlineData("1843-5", "1843/1845")]
    [InlineData("c. 1797-8", "1797~/1798~")]
    [InlineData("c. 1767/8", "1767~/1768~")]
    [InlineData("1985-04-12T23:20:30", "1985-04-12")]
    [InlineData("1985-04-12/", "1985-04-12/..")]
    [InlineData("/1985", "../1985")]
    [InlineData("2004?-06-11", "2004?")]
    [InlineData("156X-12-25", "156X")]
    [InlineData("1984-1X", "1984")]
    // shared-suffix century ranges, joined band ranges
    [InlineData("18-19th century", "century 18/century 19")]
    [InlineData("mid-late 20th century", "mid century 20/late century 20")]
    // edition numbers and spaced suffixes
    [InlineData("62/200, 1983", "1983")]
    [InlineData("Edition of 100, 1980", "1980")]
    [InlineData("1500 s c. early", "early 15XX~")]
    [InlineData("1720 s late", "late 172X")]
    [InlineData("178-, printed later", "178X")]
    // year-first archival forms and seasons
    [InlineData("1776 4-10 July", "1776-07-04/1776-07-10")]
    [InlineData("1776 Spring", "1776-21")]
    [InlineData("July 4?, 1776", "1776-07-04?")]
    [InlineData("Spring - July 2005", "2005-21/2005-07")]
    [InlineData("1777 Spring - 1778 Summer", "1777-21/1778-22")]
    [InlineData("1776 Summ", "1776-22")]
    [InlineData("July 21st, 1776", "1776-07-21")]
    [InlineData("July 21st 1776", "1776-07-21")]
    [InlineData("ca.1610-aft.1635", "1610~/1635")]
    // parentheticals: bounds, bands, editions
    [InlineData("Early 20th century (before 1923)", "early century 20/1922")]
    [InlineData("1932 (printed between 1963 and 1981)", "1963/1981")]
    [InlineData("1877 (1890's reproduction in reduced size)", "189X")]
    [InlineData("1830s (mid.)", "mid 183X")]
    [InlineData("1950's (early printing)", "195X")]
    [InlineData("18th century (later reproduction)", "century 18")]
    [InlineData("19thC(late)", "late century 19")]
    [InlineData("c. 1801-02", "1801~/1802~")]
    [InlineData("Original circa 1830's, reproduction 1930's", "193X")]
    [InlineData("Original album was printed in 1914. This is the later edition printed in 1972.", "1972")]
    [InlineData("19th century, late to early 20th century", "late century 19/early century 20")]
    // prose around relations: dates from, based on, counts, alternatives, a probable date inside an open one
    [InlineData("This re-carved print is from 1927", "1927")]
    [InlineData("1936 (early post-1945 printing)", "after 1945")]
    [InlineData("5 of 100, not dated (1930)", "1930")]
    [InlineData("94/300 (accually 94/3000), not dated (1978)", "1978")]
    [InlineData("between 1783 or 1784", "[1783,1784]")]
    [InlineData("After 1580, probably 1584.", "1584?")]
    // ULAN abbreviations: op. (active), m. (died), p. (post)
    [InlineData("op.1430-m.1462", "fl 1430/died 1462")]
    [InlineData("c.1610-p.1635", "1610~/1635")]
    [InlineData("op.1730-, d.1772", "fl 1730/died 1772")]
    [InlineData("A.P., 2005", "2005")]
    // yearrange leftovers: hedged band ranges, postfix relations, month over year, cardinal century pairs, slips in "century"
    [InlineData("Ca. early-mid 20th century.", "early century 20~/mid century 20")]
    [InlineData("Ca. 1936 or earlier.", "not-after 1936~")]
    [InlineData("ca. 1910 or later", "not-before 1910~")]
    [InlineData("4 & 5 july 1776", "1776-07-04/1776-07-05")]
    [InlineData("10/1890", "1890-10")]
    [InlineData("18-19c", "century 18/century 19")]
    [InlineData("Late 20th centuly.", "late century 20")]
    [InlineData("Ca. mid 20th centurry.", "mid century 20~")]
    [InlineData("Ca. early 20th centruy.", "early century 20~")]
    [InlineData("probably in 1890 - 1900s", "1890?/190X")]
    [InlineData("c. 1953 (this item: 1970s/80s)", "197X/198X")]
    // prose biographies: an alternative birth year, a repeated marker, a place before the date
    [InlineData("born 1600 or 1604, died 1687", "born [1600,1604]/died 1687")]
    [InlineData("active 1730, died d1772", "fl 1730/died 1772")]
    [InlineData("born Rome (Italy), 1700; died Rome (Italy), 1773", "born 1700/died 1773")]
    [InlineData("born 1600 or 1604", "born [1600,1604]")]
    [InlineData("P 1957", "1957")]
    // negated relations keep their direction
    [InlineData("not earlier than 1450", "not-before 1450")]
    [InlineData("not later than 1450", "not-after 1450")]
    [InlineData("no earlier than 1450", "not-before 1450")]
    [InlineData("Cut as a jigsaw print in 1997 and printed in January 2005.", "2005-01")]
    [InlineData("Originally 1858, this re-carved edition was published probably in early 20th century.", "early century 20?")]
    [InlineData("1940s(early)", "early 194X")]
    // a second lifecycle marker after a comma
    [InlineData("active 1430, died 1462", "fl 1430/died 1462")]
    [InlineData("born 1700, died after 1776", "born 1700/died after 1776")]
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

        var salvageOnly = new TimeSpanParser(strategies: [new SalvageStrategy()]);
        var salvaged = salvageOnly.Parse("Not dated, but the 1780s and 1792 are mentioned somewhere");
        Assert.Equal("salvage", salvaged.Strategy);
        Assert.Contains(DateFlag.Salvaged, salvaged.Flags);
        Assert.Equal("178X/1792", salvaged.Canonical);

        // The grammar now skips leading prose, so the full chain reads this without salvage.
        var prose = TimeSpanParser.Default.Parse("Italian painter, active 1451-1473");
        Assert.Equal("grammar", prose.Strategy);
        Assert.Equal("fl 1451/1473", prose.Canonical);
        Assert.True(prose.Confidence < 0.95);
    }

    [Fact]
    public void A_bare_2000s_is_a_century_alone_but_a_decade_when_the_other_end_of_the_range_is_one()
    {
        var alone = TimeSpanParser.Default.Parse("2000s");
        Assert.Equal("20XX", alone.Canonical);
        Assert.Contains(alone.Notes, n => n.Contains("read as the century 2000-2099"));

        var ranged = TimeSpanParser.Default.Parse("[1950s-2000s]");
        Assert.Equal("195X/200X", ranged.Canonical);
        Assert.Equal("2009-12-31T23:59:59Z", ranged.TimeSpan!.EndOfTheEnd!.ToString());
        Assert.DoesNotContain(ranged.Notes, n => n.Contains("century"));
        Assert.Contains(ranged.Notes, n => n.Contains("read as the decade 2000-2009"));
    }

    [Fact]
    public void A_following_or_parenthesised_date_that_overlaps_narrows_the_span()
    {
        // the era range is primary; the century phrase clarifies it for a reader who does not know the eras
        Assert.Equal("period:taisho/early century 20", TimeSpanParser.Default.Parse("Japanese, Taishô era–Shôwa era, early 20th century").Canonical);
        Assert.Equal("period:taisho/period:showa", TimeSpanParser.Default.Parse("Japanese, Taishô era–Shôwa era, 20th century").Canonical);
        Assert.Equal("early century 20/period:meiji", TimeSpanParser.Default.Parse("Meiji era, early 20th century").Canonical);
        // a parenthesised date inside the span narrows it; outside, it still widens it
        Assert.Equal("1705~/1715", TimeSpanParser.Default.Parse("Edo period (1615-1868), c. 1705-1715").Canonical);
        Assert.Equal("1782?", TimeSpanParser.Default.Parse("c. 1780s (1782?)").Canonical);
        Assert.Equal("1977/1978", TimeSpanParser.Default.Parse("1977 (1978)").Canonical);
        Assert.Equal("1886", TimeSpanParser.Default.Parse("明治19年(1886)").Canonical);
        // explicit years right after a period name date the period as the cataloguer does
        Assert.Equal("1568/1615", TimeSpanParser.Default.Parse("Momoyama period, 1568-1615").Canonical);
        Assert.Equal("1867/1912", TimeSpanParser.Default.Parse("Meiji Period (1867-1912)").Canonical);
        Assert.Equal("1886/1912", TimeSpanParser.Default.Parse("Late Meiji Period (1886-1912)").Canonical);
        Assert.Equal("1890/191X", TimeSpanParser.Default.Parse("Later Meiji period (1890 - 1910s).").Canonical);
        // "made after a painting in ..." names the source; the later clause dates the copy
        Assert.Equal("193X~", TimeSpanParser.Default.Parse("Made after a painting in Edo era. Ca. 1930s.").Canonical);
    }

    [Fact]
    public void A_band_after_a_comma_belongs_to_the_period_before_it_when_a_range_follows()
    {
        Assert.Equal("late period:edo/early period:meiji", TimeSpanParser.Default.Parse("Edo period, Late to early Meiji period").Canonical);
        Assert.Equal("beginning century 20", TimeSpanParser.Default.Parse("Japanese, Meiji era, beginning of 20th century").Canonical);
    }
}
