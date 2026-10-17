namespace LinkedArtNet.Dates.Tests;

/// <summary>Records that date several parts separately read as a set: the span is the hull, each part an alternative.</summary>
public class MultiPartTests
{
    [Theory]
    [InlineData("(1) 1980, (2) 1977, (3) ca. 1970s.", "[1980,1977,197X~]", 1970, 1980)]
    [InlineData("(1) and (2) ca. 1970s, (3) 1982.", "[197X~,1982]", 1970, 1982)]
    [InlineData("(1)1909. (2) Ca. 1900-10s.", "[1909,1900~/191X~]", 1900, 1919)] // "1900-10s" is the grammar's shared-prefix range, 1900 to the 1910s
    [InlineData("Sheet 1; 1964. Sheet 2 and 3; ca. 1930s.", "[1964,193X~]", 1930, 1964)]
    [InlineData("\"Flower\" in 1972. \"Lake in Forest\" in 1969. \"Volcano Lake\" ca. 1960s.", "[1972,1969,196X~]", 1960, 1972)]
    [InlineData("\"lamp\" made in 1956. Others ca. 1970s.", "[1956,197X~]", 1956, 1979)]
    [InlineData("Virgin, end of 5th century; Annunciation, 533-535; Saints, 705.", "[end century 5,0533/0535,0705]", 490, 705)]
    public void Enumerated_parts_become_a_set(string input, string canonical, long start, long end)
    {
        var result = TimeSpanParser.Default.Parse(input);
        Assert.Equal(ParseStatus.Parsed, result.Status);
        Assert.Equal(canonical, result.Canonical);
        Assert.Contains(DateFlag.MultiPart, result.Flags);
        Assert.Contains(DateFlag.Disjunction, result.Flags);
        Assert.Equal(start, result.TimeSpan!.BeginOfTheBegin!.Value.Year);
        Assert.Equal(end, result.TimeSpan.EndOfTheEnd!.Value.Year);
        Assert.Equal(canonical.Count(c => c == ',') + 1, result.Alternatives.Count(a => a.Note == "One member of the set"));
        Assert.Contains(result.Notes, n => n.Contains("parts separately"));
        Assert.Null(result.UnconsumedText);
        Assert.InRange(result.Confidence, 0.5, 0.85);
    }

    [Theory]
    [InlineData("1932. Printed 1963.", "1963")] // an edition sentence says which date is the object's; it is not a part
    [InlineData("Originally 1858, this re-carved edition was published probably in early 20th century.", "early century 20?")]
    [InlineData("1904 or 1905", "[1904,1905]")] // a set by "or" is a disjunction, not parts
    [InlineData("(1) 1980", "1980")] // one enumerated part is just a date
    [InlineData("Sheet 1; 1964", "1964")]
    [InlineData("b. 1606; d. 1669", "born 1606/died 1669")]
    [InlineData("July 1776 - August 1789; 1812 - 1820, 1900 and June 10-11, 1910", "1776-07/1789-08")] // a clause the grammar cannot finish is not a part; the old reading (first range, rest unconsumed) stands
    public void Other_clauses_keep_their_readings(string input, string canonical)
    {
        var result = TimeSpanParser.Default.Parse(input);
        Assert.Equal(canonical, result.Canonical);
        Assert.DoesNotContain(DateFlag.MultiPart, result.Flags);
    }
}
