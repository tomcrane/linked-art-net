using LinkedArtNet.Dates.Text;

namespace LinkedArtNet.Dates.Tests;

/// <summary>Repairs the normaliser makes to corrupt or machine-shaped input before the grammar sees it.</summary>
public class NormaliserTests
{
    [Theory]
    [InlineData("late 1750âs - early 1760âs", "late 1750's - early 1760's")]
    [InlineData("1730âs early", "1730's early")]
    [InlineData("1750â€™s", "1750's")]
    [InlineData("1750â€˜s", "1750's")]
    [InlineData("1750â€“1760", "1750-1760")]
    [InlineData("late 1750\u00e2\u0080\u0099s - early 1760\u00e2\u0080\u0099s", "late 1750's - early 1760's")] // Latin-1 reading of the same bytes, as in yearrange
    [InlineData("1750\u00e2\u0080\u00931760", "1750-1760")]
    [InlineData("Pâques 1750", "Pâques 1750")] // an â that is a letter of a word is left alone
    public void Mojibake_apostrophes_and_dashes_are_repaired(string input, string expected)
    {
        var result = Normaliser.Normalise(input);
        Assert.Equal(expected, result.Text);
        if (input != expected) Assert.Contains(result.Notes, n => n.Contains("Mojibake"));
    }

    [Theory]
    [InlineData("18690000", "1869")]
    [InlineData("18690500", "1869-05")]
    [InlineData("18690512", "1869-05-12")]
    [InlineData("18691300", "18691300")] // month 13: not a date column value
    [InlineData("18690032", "18690032")] // day without a month
    [InlineData("1869000", "1869000")]   // seven digits are something else
    public void Eight_digit_database_values_become_iso_dates(string input, string expected)
    {
        var result = Normaliser.Normalise(input);
        Assert.Equal(expected, result.Text);
        if (input != expected) Assert.Contains(result.Notes, n => n.Contains("YYYYMMDD"));
    }

    [Theory]
    [InlineData("late 1750âs - early 1760âs", "late 175X/early 176X")]
    [InlineData("1730âs early", "early 173X")]
    [InlineData("18690000", "1869")]
    [InlineData("18690512", "1869-05-12")]
    public void The_parser_reads_the_repaired_text(string input, string canonical)
    {
        var result = TimeSpanParser.Default.Parse(input);
        Assert.Equal(ParseStatus.Parsed, result.Status);
        Assert.Equal(canonical, result.Canonical);
        Assert.Equal(input, result.TimeSpan?.Label ?? result.Begin?.Label); // the label is always the text as written
    }
}
