using System.Text.Json;

namespace LinkedArtNet.Tests;

public class LinkedArtDateTests
{
    [Theory]
    [InlineData(1881, 1, 1, 0, 0, 0, "1881-01-01T00:00:00Z")]
    [InlineData(1881, 12, 31, 23, 59, 59, "1881-12-31T23:59:59Z")]
    [InlineData(0, 1, 1, 0, 0, 0, "0000-01-01T00:00:00Z")]
    [InlineData(-1, 1, 1, 0, 0, 0, "-0001-01-01T00:00:00Z")]
    [InlineData(-499, 7, 4, 0, 0, 0, "-0499-07-04T00:00:00Z")]
    [InlineData(9999, 12, 31, 23, 59, 59, "9999-12-31T23:59:59Z")]
    [InlineData(12345, 1, 1, 0, 0, 0, "12345-01-01T00:00:00Z")]
    [InlineData(-4500000000L, 1, 1, 0, 0, 0, "-4500000000-01-01T00:00:00Z")]
    [InlineData(4500000000L, 6, 15, 12, 30, 45, "4500000000-06-15T12:30:45Z")]
    public void Formats_as_xsd_dateTime(long y, int m, int d, int h, int mi, int s, string expected)
    {
        var date = new LinkedArtDate(y, m, d, h, mi, s);
        Assert.Equal(expected, date.ToString());
    }

    [Theory]
    [InlineData("1881-01-01T00:00:00Z", 1881, 1, 1, 0, 0, 0)]
    [InlineData("1848-09-30T23:59:59", 1848, 9, 30, 23, 59, 59)]          // no zone: assumed Z
    [InlineData("2019-07-19T10:00:00+01:00", 2019, 7, 19, 9, 0, 0)]       // offset converted to UTC
    [InlineData("2019-07-19T10:00:00-0500", 2019, 7, 19, 15, 0, 0)]
    [InlineData("1998-09-02T22:20:00.500Z", 1998, 9, 2, 22, 20, 0)]       // fraction dropped
    [InlineData("1500-12-31T24:00:00Z", 1500, 12, 31, 23, 59, 59)]        // end-of-day form
    [InlineData("1881-12-31", 1881, 12, 31, 0, 0, 0)]
    [InlineData("1881-12", 1881, 12, 1, 0, 0, 0)]
    [InlineData("1881", 1881, 1, 1, 0, 0, 0)]
    [InlineData("0000-01-01T00:00:00Z", 0, 1, 1, 0, 0, 0)]
    [InlineData("-0001-01-01T00:00:00Z", -1, 1, 1, 0, 0, 0)]
    [InlineData("-9999-01-01T00:00:00", -9999, 1, 1, 0, 0, 0)]
    [InlineData("+12345-01-01T00:00:00Z", 12345, 1, 1, 0, 0, 0)]          // ISO expanded form accepted on input
    [InlineData("-4500000000-01-01T00:00:00Z", -4500000000L, 1, 1, 0, 0, 0)]
    [InlineData("1881-01-01 00:00:00Z", 1881, 1, 1, 0, 0, 0)]             // space separator tolerated
    public void Parses_xsd_dateTime_and_tolerant_forms(string input, long y, int m, int d, int h, int mi, int s)
    {
        var date = LinkedArtDate.Parse(input);
        Assert.Equal(new LinkedArtDate(y, m, d, h, mi, s), date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("198")]                     // fewer than four year digits
    [InlineData("1881-13-01")]
    [InlineData("1881-02-29")]             // not a leap year
    [InlineData("1881-01-01T25:00:00Z")]
    [InlineData("1881-01-01T24:00:01Z")]   // 24:00:00 is the only permitted hour-24 form
    [InlineData("1881-1-1")]
    [InlineData("1881-01-01T00:00Z")]      // seconds are required when a time is present
    [InlineData("1881-01-01T00:00:00Q")]
    public void Rejects_malformed_input(string input)
    {
        Assert.False(LinkedArtDate.TryParse(input, null, out _));
        Assert.Throws<FormatException>(() => LinkedArtDate.Parse(input));
    }

    [Fact]
    public void Bce_helpers_use_astronomical_numbering()
    {
        Assert.Equal(0, LinkedArtDate.FromBce(1).Year);
        Assert.Equal(-499, LinkedArtDate.FromBce(500).Year);
        Assert.Equal(500, LinkedArtDate.FromBce(500).BceYear);
        Assert.True(LinkedArtDate.FromBce(500).IsBce);
        Assert.True(new LinkedArtDate(0, 6, 1).IsBce);
        Assert.False(new LinkedArtDate(1, 1, 1).IsBce);
        Assert.Null(new LinkedArtDate(1, 1, 1).BceYear);
        Assert.Throws<ArgumentOutOfRangeException>(() => LinkedArtDate.FromBce(0));
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(1900, false)]
    [InlineData(2024, true)]
    [InlineData(2023, false)]
    [InlineData(0, true)]        // 1 BCE is a leap year in the proleptic Gregorian calendar
    [InlineData(-1, false)]
    [InlineData(-4, true)]
    [InlineData(-100, false)]
    [InlineData(-400, true)]
    [InlineData(4500000000L, true)]      // divisible by 400
    [InlineData(-4500000000L, true)]
    [InlineData(4500000100L, false)]     // divisible by 100 but not 400
    [InlineData(4500000004L, true)]      // divisible by 4 but not 100
    [InlineData(-4500000100L, false)]
    public void Leap_years_follow_proleptic_gregorian_rules(long year, bool expected)
    {
        Assert.Equal(expected, LinkedArtDate.IsLeapYear(year));
    }

    [Theory]
    [InlineData(2024, 2, 29)]
    [InlineData(2023, 2, 28)]
    [InlineData(0, 2, 29)]
    [InlineData(-1, 2, 28)]
    [InlineData(1881, 4, 30)]
    [InlineData(1881, 12, 31)]
    public void Days_in_month(long year, int month, int expected)
    {
        Assert.Equal(expected, LinkedArtDate.DaysInMonth(year, month));
    }

    [Fact]
    public void Constructor_rejects_invalid_components()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedArtDate(1881, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedArtDate(1881, 13, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedArtDate(1881, 2, 29));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedArtDate(1881, 1, 1, 24, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedArtDate(1881, 1, 1, 0, 60, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LinkedArtDate(1881, 1, 1, 0, 0, -1));
    }

    [Fact]
    public void Period_boundaries()
    {
        Assert.Equal("1499-12-31T23:59:59Z", LinkedArtDate.EndOfYear(1499).ToString());
        Assert.Equal("1400-01-01T00:00:00Z", LinkedArtDate.StartOfYear(1400).ToString());
        Assert.Equal("1848-02-29T23:59:59Z", LinkedArtDate.EndOfMonth(1848, 2).ToString());
        Assert.Equal("1848-09-30T23:59:59Z", new LinkedArtDate(1848, 9, 30).LastSecondOfDay().ToString());
        Assert.Equal("1848-09-30T00:00:00Z", new LinkedArtDate(1848, 9, 30, 12, 0, 0).StartOfDay().ToString());
        Assert.Equal("1848-09-30T23:59:59Z", new LinkedArtDate(1848, 9, 30, 12, 0, 0).EndOfDay().ToString());
        Assert.Equal("1848-12-31T23:59:59Z", new LinkedArtDate(1848, 9, 30).EndOfYear().ToString());
        Assert.Equal("1848-09-30T23:59:59Z", new LinkedArtDate(1848, 9, 2).EndOfMonth().ToString());
    }

    [Fact]
    public void Day_arithmetic_crosses_year_and_era_boundaries()
    {
        Assert.Equal(new LinkedArtDate(1, 1, 1), new LinkedArtDate(0, 12, 31).AddDays(1));
        Assert.Equal(new LinkedArtDate(0, 12, 31), new LinkedArtDate(1, 1, 1).AddDays(-1));
        Assert.Equal(new LinkedArtDate(-1, 12, 31), new LinkedArtDate(0, 1, 1).AddDays(-1));
        Assert.Equal(new LinkedArtDate(2024, 3, 1), new LinkedArtDate(2024, 2, 28).AddDays(2));
        Assert.Equal(new LinkedArtDate(2023, 3, 1), new LinkedArtDate(2023, 2, 28).AddDays(1));
        Assert.Equal(new LinkedArtDate(1882, 1, 1), new LinkedArtDate(1881, 1, 1).AddDays(365));
        Assert.Equal(new LinkedArtDate(-4500000000L, 1, 2), new LinkedArtDate(-4500000000L, 1, 1).AddDays(1));
    }

    [Fact]
    public void Second_arithmetic()
    {
        Assert.Equal(new LinkedArtDate(1881, 12, 31, 23, 59, 59), new LinkedArtDate(1882, 1, 1).AddSeconds(-1));
        Assert.Equal(new LinkedArtDate(1882, 1, 1), new LinkedArtDate(1881, 12, 31, 23, 59, 59).AddSeconds(1));
        Assert.Equal(new LinkedArtDate(1881, 1, 2, 0, 0, 0), new LinkedArtDate(1881, 1, 1).AddSeconds(86400));
        Assert.Equal(new LinkedArtDate(0, 12, 31, 23, 59, 59), new LinkedArtDate(1, 1, 1).AddSeconds(-1));
    }

    [Fact]
    public void Year_and_month_arithmetic_clamps_to_month_end()
    {
        Assert.Equal(new LinkedArtDate(2025, 2, 28), new LinkedArtDate(2024, 2, 29).AddYears(1));
        Assert.Equal(new LinkedArtDate(2028, 2, 29), new LinkedArtDate(2024, 2, 29).AddYears(4));
        Assert.Equal(new LinkedArtDate(1881, 2, 28), new LinkedArtDate(1881, 1, 31).AddMonths(1));
        Assert.Equal(new LinkedArtDate(1882, 1, 31), new LinkedArtDate(1881, 12, 31).AddMonths(1));
        Assert.Equal(new LinkedArtDate(1880, 11, 30), new LinkedArtDate(1881, 1, 31).AddMonths(-2));
        Assert.Equal(new LinkedArtDate(0, 6, 1), new LinkedArtDate(1, 6, 1).AddYears(-1));
    }

    [Fact]
    public void Comparison_is_chronological()
    {
        var bce = new LinkedArtDate(-1, 1, 1);
        var yearZero = new LinkedArtDate(0, 1, 1);
        var ce = new LinkedArtDate(1, 1, 1);
        var later = new LinkedArtDate(1, 1, 1, 0, 0, 1);
        Assert.True(bce < yearZero);
        Assert.True(yearZero < ce);
        Assert.True(ce < later);
        Assert.True(later > bce);
        Assert.True(ce <= new LinkedArtDate(1, 1, 1));
        Assert.True(ce >= new LinkedArtDate(1, 1, 1));
        Assert.Equal(0, ce.CompareTo(new LinkedArtDate(1, 1, 1)));
        var sorted = new[] { later, bce, ce, yearZero }.OrderBy(x => x).ToArray();
        Assert.Equal(new[] { bce, yearZero, ce, later }, sorted);
    }

    [Fact]
    public void Converts_to_and_from_DateTimeOffset_within_range()
    {
        var dto = new DateTimeOffset(1998, 9, 2, 22, 20, 0, TimeSpan.FromHours(2));
        var date = new LinkedArtDate(dto);
        Assert.Equal("1998-09-02T20:20:00Z", date.ToString());
        Assert.Equal(dto, date.ToDateTimeOffset());

        Assert.Throws<InvalidOperationException>(() => new LinkedArtDate(0, 1, 1).ToDateTimeOffset());
        Assert.Throws<InvalidOperationException>(() => new LinkedArtDate(10000, 1, 1).ToDateTimeOffset());
        Assert.False(new LinkedArtDate(-500, 1, 1).TryToDateTimeOffset(out _));
        Assert.True(new LinkedArtDate(1, 1, 1).TryToDateTimeOffset(out var min));
        Assert.Equal(DateTimeOffset.MinValue, min);
    }

    [Fact]
    public void DateTime_constructor_treats_unspecified_kind_as_utc()
    {
        var unspecified = new DateTime(1998, 9, 2, 22, 20, 0, DateTimeKind.Unspecified);
        Assert.Equal("1998-09-02T22:20:00Z", new LinkedArtDate(unspecified).ToString());
        var utc = new DateTime(1998, 9, 2, 22, 20, 0, DateTimeKind.Utc);
        Assert.Equal("1998-09-02T22:20:00Z", new LinkedArtDate(utc).ToString());
    }

    [Fact]
    public void Json_round_trips_through_the_converter()
    {
        var ts = new LinkedArtTimeSpan
        {
            BeginOfTheBegin = LinkedArtDate.FromBce(540),
            EndOfTheEnd = LinkedArtDate.EndOfYear(-519),
            EndOfTheBegin = new LinkedArtDate(12345, 6, 7, 8, 9, 10),
        };
        var json = JsonSerializer.Serialize(ts);
        Assert.Contains("\"begin_of_the_begin\":\"-0539-01-01T00:00:00Z\"", json);
        Assert.Contains("\"end_of_the_end\":\"-0519-12-31T23:59:59Z\"", json);
        Assert.Contains("\"end_of_the_begin\":\"12345-06-07T08:09:10Z\"", json);
        Assert.DoesNotContain("begin_of_the_end", json);

        var back = JsonSerializer.Deserialize<LinkedArtTimeSpan>(json)!;
        Assert.Equal(ts.BeginOfTheBegin, back.BeginOfTheBegin);
        Assert.Equal(ts.EndOfTheEnd, back.EndOfTheEnd);
        Assert.Equal(ts.EndOfTheBegin, back.EndOfTheBegin);
        Assert.Null(back.BeginOfTheEnd);
    }

    [Fact]
    public void Json_reader_accepts_site_and_lux_forms()
    {
        var json = """{"type":"TimeSpan","begin_of_the_begin":"1500-01-01T00:00:00","end_of_the_end":"1599-12-31T24:00:00"}""";
        var ts = JsonSerializer.Deserialize<LinkedArtTimeSpan>(json)!;
        Assert.Equal(new LinkedArtDate(1500, 1, 1), ts.BeginOfTheBegin);
        Assert.Equal(new LinkedArtDate(1599, 12, 31, 23, 59, 59), ts.EndOfTheEnd);

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<LinkedArtTimeSpan>("""{"type":"TimeSpan","begin_of_the_begin":"circa 1500"}"""));
    }

    [Fact]
    public void TimeSpan_factories()
    {
        var year = LinkedArtTimeSpan.FromYear(1881);
        Assert.Null(year.Label); // factories set no label; use WithLabel
        Assert.Equal(new LinkedArtDate(1881, 1, 1), year.BeginOfTheBegin);
        Assert.Equal(new LinkedArtDate(1881, 12, 31, 23, 59, 59), year.EndOfTheEnd);

        var bce = LinkedArtTimeSpan.FromYear(-499);
        Assert.Equal(new LinkedArtDate(-499, 12, 31, 23, 59, 59), bce.EndOfTheEnd);

        var day = LinkedArtTimeSpan.FromDay(1606, 7, 15);
        Assert.Equal(new LinkedArtDate(1606, 7, 15), day.BeginOfTheBegin);
        Assert.Equal(new LinkedArtDate(1606, 7, 15, 23, 59, 59), day.EndOfTheEnd);

        var withId = LinkedArtTimeSpan.FromYear(1905, "https://example.org/ts");
        Assert.Equal("https://example.org/ts", withId.Id);
    }
}
