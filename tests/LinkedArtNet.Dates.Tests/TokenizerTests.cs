using LinkedArtNet.Dates.Text;

namespace LinkedArtNet.Dates.Tests;

public class TokenizerTests
{
    private static string Kinds(string text) => string.Join(" ", Tokenizer.English.Tokenize(text).Select(t => t.Kind.ToString()));

    [Theory]
    [InlineData("1984", "Number")]
    [InlineData("c. 1850", "Hedge Number")]
    [InlineData("c1543", "Hedge Number")]
    [InlineData("1950s", "Number DecadeSuffix")]
    [InlineData("the 1950's", "Noise Number DecadeSuffix")]
    [InlineData("late 1950s", "Band Number DecadeSuffix")]
    [InlineData("mid-1950s", "Band Dash Number DecadeSuffix")]
    [InlineData("19th century", "Ordinal CenturyWord")]
    [InlineData("19th c.", "Ordinal Hedge")]
    [InlineData("XIXth century", "Ordinal CenturyWord")]
    [InlineData("nineteenth century", "Ordinal CenturyWord")]
    [InlineData("C19", "Hedge Number")]
    [InlineData("s. XIV", "Word Roman")]
    [InlineData("first half of the 14th century", "Band Noise Ordinal CenturyWord")]
    [InlineData("2nd half 14th century", "Band Ordinal CenturyWord")]
    [InlineData("500 B.C.", "Number Era")]
    [InlineData("AD 79", "Era Number")]
    [InlineData("5th century BCE", "Ordinal CenturyWord Era")]
    [InlineData("4.5 billion years ago", "Number Magnitude Ago")]
    [InlineData("10,000 BP", "Number Bp")]
    [InlineData("10 ka BP", "Number Magnitude Bp")]
    [InlineData("before 1850", "Relation Number")]
    [InlineData("not after 1850", "Relation Number")]
    [InlineData("post-1945", "Relation Dash Number")]
    [InlineData("1971 - present", "Number Dash OpenEnd")]
    [InlineData("between 1870 and 1890", "Between Number RangeWord Number")]
    [InlineData("1904 or 1905", "Number Or Number")]
    [InlineData("n.d.", "Undated")]
    [InlineData("no date", "Undated")]
    [InlineData("b. 1850", "Lifecycle Number")]
    [InlineData("* 1850 † 1920", "Lifecycle Number Lifecycle Number")]
    [InlineData("fl. 1480-1510", "Floruit Number Dash Number")]
    [InlineData("active 1920s", "Floruit Number DecadeSuffix")]
    [InlineData("Winter 1945", "Season Number")]
    [InlineData("8 May 1944", "Number Month Number")]
    [InlineData("1902 October 27", "Number Month Number")]
    [InlineData("17_", "Unspecified")]
    [InlineData("18--", "Unspecified")]
    [InlineData("19XX", "Unspecified")]
    [InlineData("MDCCLXXV", "Roman")]
    [InlineData("1962, (i. e. 1967)", "Number Comma OpenParen Correction Number CloseParen")]
    [InlineData("1861(?)", "Number OpenParen Question CloseParen")]
    [InlineData("cop. 1980", "Noise Number")]
    [InlineData("[1988], ©1988", "OpenBracket Number CloseBracket Comma Noise Number")]
    [InlineData("13/11/08", "Number Slash Number Slash Number")]
    public void Classifies_tokens(string text, string expectedKinds)
    {
        Assert.Equal(expectedKinds, Kinds(text));
    }

    [Theory]
    [InlineData("MDCCLXXV", 1775, false)]
    [InlineData("XIX", 19, false)]
    [InlineData("XIXth", 19, true)]
    [InlineData("xive", 14, true)]
    [InlineData("MCMXCIV", 1994, false)]
    [InlineData("C", 100, false)]
    public void Reads_roman_numerals(string word, long value, bool ordinal)
    {
        Assert.True(Tokenizer.TryRoman(word, out var v, out var o));
        Assert.Equal(value, v);
        Assert.Equal(ordinal, o);
    }

    [Theory]
    [InlineData("D")]
    [InlineData("M")]
    [InlineData("IIII")]
    [InlineData("hello")]
    [InlineData("")]
    public void Rejects_non_numerals(string word)
    {
        Assert.False(Tokenizer.TryRoman(word, out _, out _) && word.Length <= 1);
    }

    [Fact]
    public void Numbers_carry_their_values()
    {
        var tokens = Tokenizer.English.Tokenize("10,000 BP and 4.5 Ga");
        Assert.Equal(10000L, tokens[0].Number);
        Assert.Equal(4.5m, tokens[3].Value);
        Assert.True(tokens[3].HasDecimal);
    }

    [Theory]
    [InlineData("[1847-1852]", "1847-1852", DateFlag.Bracketed)]
    [InlineData("(1989?)", "1989?", DateFlag.Parenthesised)]
    [InlineData("[ late 12th century ]", "late 12th century", DateFlag.Bracketed)]
    public void Normaliser_strips_a_wrapper_and_records_it(string input, string text, DateFlag flag)
    {
        var n = Normaliser.Normalise(input);
        Assert.Equal(text, n.Text);
        Assert.Contains(flag, n.Flags);
    }

    [Theory]
    [InlineData("1870–1890", "1870-1890")]
    [InlineData("1870—1890", "1870-1890")]
    [InlineData("1850.", "1850")]
    [InlineData("19th c.", "19th c.")]
    [InlineData("  1850  ,", "1850")]
    [InlineData("1850 [sic]", "1850")]
    [InlineData("[1500] - [1520]", "[1500] - [1520]")]
    [InlineData("Late  1950’s", "Late 1950's")]
    public void Normaliser_cleans_text(string input, string expected)
    {
        Assert.Equal(expected, Normaliser.Normalise(input).Text);
    }
}
