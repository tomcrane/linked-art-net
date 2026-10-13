using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Tests;

/// <summary>Canonical expression + profile -> datetimes. One row per rule; bounds written as the TimeSpan writes them.</summary>
public class RealiserTests
{
    private static readonly ParseProfile Strict = ParseProfile.Default with { Name = "strict", CenturyConvention = CenturyConvention.Strict };
    private static readonly ParseProfile Padded = ParseProfile.Default with { Name = "padded", CircaPaddingYears = 5, UncertainPaddingYears = 2 };
    private static readonly ParseProfile PaddedInner = Padded with { Name = "padded-inner", EmitInnerBounds = true };
    private static readonly ParseProfile South = ParseProfile.Default with { Name = "south", Hemisphere = Hemisphere.South };
    private static readonly ParseProfile ClampNow = ParseProfile.Default with { Name = "clamp", OpenEnd = new OpenEndPolicy(OpenEndMode.ClampToNow) };
    private static readonly ParseProfile Clamp1900 = ParseProfile.Default with { Name = "clamp1900", OpenEnd = new OpenEndPolicy(OpenEndMode.ClampToYear, 1900) };

    private static TimeSpanParseResult Realise(string canonical, ParseProfile? profile = null, Intent intent = Intent.Auto) =>
        new TimeSpanRealiser(profile, currentYear: () => 2026).Realise(DateExpression.Parse(canonical), canonical, intent);

    public static TheoryData<string, string?, string?> Singles() => new()
    {
        // points
        { "1984", "1984-01-01T00:00:00Z", "1984-12-31T23:59:59Z" },
        { "0079", "0079-01-01T00:00:00Z", "0079-12-31T23:59:59Z" },
        { "0000", "0000-01-01T00:00:00Z", "0000-12-31T23:59:59Z" },
        { "-0499", "-0499-01-01T00:00:00Z", "-0499-12-31T23:59:59Z" },
        { "1984-02", "1984-02-01T00:00:00Z", "1984-02-29T23:59:59Z" },
        { "1900-02", "1900-02-01T00:00:00Z", "1900-02-28T23:59:59Z" },
        { "1984-05-17", "1984-05-17T00:00:00Z", "1984-05-17T23:59:59Z" },
        { "1945-21", "1945-03-01T00:00:00Z", "1945-05-31T23:59:59Z" },
        { "1945-24", "1945-12-01T00:00:00Z", "1946-02-28T23:59:59Z" },
        { "1947-24", "1947-12-01T00:00:00Z", "1948-02-29T23:59:59Z" },
        // decades and unspecified digits
        { "195X", "1950-01-01T00:00:00Z", "1959-12-31T23:59:59Z" },
        { "-044X", "-0449-01-01T00:00:00Z", "-0440-12-31T23:59:59Z" },
        { "18XX", "1800-01-01T00:00:00Z", "1899-12-31T23:59:59Z" },
        { "1XXX", "1000-01-01T00:00:00Z", "1999-12-31T23:59:59Z" },
        { "1X50", "1050-01-01T00:00:00Z", "1950-12-31T23:59:59Z" },
        { "-04XX", "-0499-01-01T00:00:00Z", "-0400-12-31T23:59:59Z" },
        // centuries and millennia (popular convention by default)
        { "century 19", "1800-01-01T00:00:00Z", "1899-12-31T23:59:59Z" },
        { "century 1", "0000-01-01T00:00:00Z", "0099-12-31T23:59:59Z" },
        { "century -5", "-0499-01-01T00:00:00Z", "-0400-12-31T23:59:59Z" },
        { "century -1", "-0099-01-01T00:00:00Z", "0000-12-31T23:59:59Z" },
        { "millennium 2", "1000-01-01T00:00:00Z", "1999-12-31T23:59:59Z" },
        { "millennium -1", "-0999-01-01T00:00:00Z", "0000-12-31T23:59:59Z" },
        // bands
        { "early 195X", "1950-01-01T00:00:00Z", "1953-12-31T23:59:59Z" },
        { "mid 195X", "1953-01-01T00:00:00Z", "1956-12-31T23:59:59Z" },
        { "late 195X", "1957-01-01T00:00:00Z", "1959-12-31T23:59:59Z" },
        { "h1 195X", "1950-01-01T00:00:00Z", "1954-12-31T23:59:59Z" },
        { "early century 14", "1300-01-01T00:00:00Z", "1333-12-31T23:59:59Z" },
        { "mid century 14", "1334-01-01T00:00:00Z", "1366-12-31T23:59:59Z" },
        { "late century 14", "1367-01-01T00:00:00Z", "1399-12-31T23:59:59Z" },
        { "h2 century 14", "1350-01-01T00:00:00Z", "1399-12-31T23:59:59Z" },
        { "q2 century 14", "1325-01-01T00:00:00Z", "1349-12-31T23:59:59Z" },
        { "t3 century 14", "1366-01-01T00:00:00Z", "1399-12-31T23:59:59Z" },
        { "beginning century 19", "1800-01-01T00:00:00Z", "1809-12-31T23:59:59Z" },
        { "end century 19", "1890-01-01T00:00:00Z", "1899-12-31T23:59:59Z" },
        { "early century -5", "-0499-01-01T00:00:00Z", "-0466-12-31T23:59:59Z" },
        { "h1 millennium 2", "1000-01-01T00:00:00Z", "1499-12-31T23:59:59Z" },
        { "early 1850", "1850-01-01T00:00:00Z", "1850-04-30T23:59:59Z" },
        { "late 1850", "1850-09-01T00:00:00Z", "1850-12-31T23:59:59Z" },
        { "h2 1850", "1850-07-01T00:00:00Z", "1850-12-31T23:59:59Z" },
        { "q1 1850", "1850-01-01T00:00:00Z", "1850-03-31T23:59:59Z" },
        { "h1 1850-03", "1850-03-01T00:00:00Z", "1850-03-16T23:59:59Z" },
        // hedges are flags only by default
        { "1850~", "1850-01-01T00:00:00Z", "1850-12-31T23:59:59Z" },
        { "1850?", "1850-01-01T00:00:00Z", "1850-12-31T23:59:59Z" },
        { "-2499~", "-2499-01-01T00:00:00Z", "-2499-12-31T23:59:59Z" },
        // relations: one bound
        { "before 1850", null, "1849-12-31T23:59:59Z" },
        { "by 1850", null, "1850-12-31T23:59:59Z" },
        { "until 1850-06", null, "1850-06-30T23:59:59Z" },
        { "not-after 1850", null, "1850-12-31T23:59:59Z" },
        { "after 1850", "1851-01-01T00:00:00Z", null },
        { "not-before 1850", "1850-01-01T00:00:00Z", null },
        { "after 1850-05-17", "1850-05-18T00:00:00Z", null },
        { "before century 19", null, "1799-12-31T23:59:59Z" },
        // intervals
        { "1870/1890", "1870-01-01T00:00:00Z", "1890-12-31T23:59:59Z" },
        { "../1850", null, "1850-12-31T23:59:59Z" },
        { "1971/..", "1971-01-01T00:00:00Z", null },
        { "1850~/1900?", "1850-01-01T00:00:00Z", "1900-12-31T23:59:59Z" },
        { "late 195X/century 20", "1957-01-01T00:00:00Z", "1999-12-31T23:59:59Z" },
        { "1998/1902", "1902-01-01T00:00:00Z", "1998-12-31T23:59:59Z" },
        { "-0499/0079", "-0499-01-01T00:00:00Z", "0079-12-31T23:59:59Z" },
        // sets: the hull
        { "[1904,1905]", "1904-01-01T00:00:00Z", "1905-12-31T23:59:59Z" },
        { "[195X,1870/1890]", "1870-01-01T00:00:00Z", "1959-12-31T23:59:59Z" },
        // deep time, to the precision written
        { "4.5 Ga ago", "-4549997974-01-01T00:00:00Z", "-4449997974-12-31T23:59:59Z" },
        { "10 ka BP", "-8550-01-01T00:00:00Z", "-7550-12-31T23:59:59Z" },
        { "10.0 ka BP", "-8100-01-01T00:00:00Z", "-8000-12-31T23:59:59Z" },
        { "66 Ma ago", "-66497974-01-01T00:00:00Z", "-65497974-12-31T23:59:59Z" },
        { "300 years ago", "1726-01-01T00:00:00Z", "1726-12-31T23:59:59Z" },
    };

    [Theory]
    [MemberData(nameof(Singles))]
    public void Realises_a_single_span(string canonical, string? begin, string? end)
    {
        var result = Realise(canonical);
        Assert.Equal(ParseStatus.Parsed, result.Status);
        Assert.Equal(ResultKind.Single, result.Kind);
        var ts = Assert.IsType<LinkedArtTimeSpan>(result.TimeSpan);
        Assert.Equal(begin, ts.BeginOfTheBegin?.ToString());
        Assert.Equal(end, ts.EndOfTheEnd?.ToString());
        Assert.Null(ts.EndOfTheBegin);
        Assert.Null(ts.BeginOfTheEnd);
        Assert.Equal(canonical, ts.Label);
        Assert.Null(result.Begin);
        Assert.Null(result.End);
    }

    [Theory]
    [InlineData("century 19", "1801-01-01T00:00:00Z", "1900-12-31T23:59:59Z")]
    [InlineData("century 1", "0001-01-01T00:00:00Z", "0100-12-31T23:59:59Z")]
    [InlineData("century -5", "-0499-01-01T00:00:00Z", "-0400-12-31T23:59:59Z")]
    [InlineData("millennium 2", "1001-01-01T00:00:00Z", "2000-12-31T23:59:59Z")]
    [InlineData("late century 19", "1868-01-01T00:00:00Z", "1900-12-31T23:59:59Z")]
    public void Strict_century_convention(string canonical, string begin, string end)
    {
        var ts = Realise(canonical, Strict).TimeSpan!;
        Assert.Equal(begin, ts.BeginOfTheBegin?.ToString());
        Assert.Equal(end, ts.EndOfTheEnd?.ToString());
    }

    [Theory]
    [InlineData("1850~", "1845-01-01T00:00:00Z", "1855-12-31T23:59:59Z")]
    [InlineData("1850?", "1848-01-01T00:00:00Z", "1852-12-31T23:59:59Z")]
    [InlineData("1850%", "1845-01-01T00:00:00Z", "1855-12-31T23:59:59Z")]
    [InlineData("1850~/1900~", "1845-01-01T00:00:00Z", "1905-12-31T23:59:59Z")]
    [InlineData("late 195X~", "1952-01-01T00:00:00Z", "1964-12-31T23:59:59Z")]
    public void Padding_widens_hedged_dates(string canonical, string begin, string end)
    {
        var result = Realise(canonical, Padded);
        var ts = result.TimeSpan!;
        Assert.Equal(begin, ts.BeginOfTheBegin?.ToString());
        Assert.Equal(end, ts.EndOfTheEnd?.ToString());
        Assert.Null(ts.EndOfTheBegin);
        Assert.Contains(DateFlag.Padded, result.Flags);
    }

    [Fact]
    public void Padding_with_inner_bounds_emits_four_points()
    {
        var ts = Realise("1850~", PaddedInner).TimeSpan!;
        Assert.Equal("1845-01-01T00:00:00Z", ts.BeginOfTheBegin?.ToString());
        Assert.Equal("1850-01-01T00:00:00Z", ts.EndOfTheBegin?.ToString());
        Assert.Equal("1850-12-31T23:59:59Z", ts.BeginOfTheEnd?.ToString());
        Assert.Equal("1855-12-31T23:59:59Z", ts.EndOfTheEnd?.ToString());
        Assert.True(ts.IsOrdered());
    }

    [Fact]
    public void Southern_hemisphere_seasons()
    {
        var ts = Realise("1945-24", South).TimeSpan!;
        Assert.Equal("1945-06-01T00:00:00Z", ts.BeginOfTheBegin?.ToString());
        Assert.Equal("1945-08-31T23:59:59Z", ts.EndOfTheEnd?.ToString());
    }

    [Theory]
    [InlineData("after 1850", "1851-01-01T00:00:00Z", "2026-12-31T23:59:59Z")]
    [InlineData("1971/..", "1971-01-01T00:00:00Z", "2026-12-31T23:59:59Z")]
    [InlineData("before 1850", "-10000-01-01T00:00:00Z", "1849-12-31T23:59:59Z")]
    public void Open_bounds_can_be_clamped(string canonical, string begin, string end)
    {
        var ts = Realise(canonical, ClampNow).TimeSpan!;
        Assert.Equal(begin, ts.BeginOfTheBegin?.ToString());
        Assert.Equal(end, ts.EndOfTheEnd?.ToString());
        Assert.Equal("1900-12-31T23:59:59Z", Realise("after 1850", Clamp1900).TimeSpan!.EndOfTheEnd?.ToString());
    }

    // ---- flags and notes -------------------------------------------------------------------------------------

    [Theory]
    [InlineData("1850~", DateFlag.Approximate)]
    [InlineData("1850?", DateFlag.Uncertain)]
    [InlineData("1850%", DateFlag.Approximate, DateFlag.Uncertain)]
    [InlineData("before 1850", DateFlag.OpenStart)]
    [InlineData("after 1850", DateFlag.OpenEnd)]
    [InlineData("../1850", DateFlag.OpenStart)]
    [InlineData("[1904,1905]", DateFlag.Disjunction)]
    [InlineData("1998/1902", DateFlag.Inverted)]
    [InlineData("late 195X", DateFlag.Band)]
    [InlineData("1945-24", DateFlag.Season)]
    [InlineData("4.5 Ga ago", DateFlag.DeepTime)]
    [InlineData("born 1606", DateFlag.Lifecycle)]
    [InlineData("fl 1480/1510", DateFlag.Floruit)]
    public void Sets_flags(string canonical, params DateFlag[] expected)
    {
        var result = Realise(canonical);
        foreach (var flag in expected) Assert.Contains(flag, result.Flags);
        Assert.NotEmpty(result.Notes.Concat(result.Flags.Select(f => f.ToString())));
    }

    [Fact]
    public void A_set_lists_its_members_as_alternatives()
    {
        var result = Realise("[1904,1905]");
        Assert.Equal(2, result.Alternatives.Count);
        Assert.Equal("1904", result.Alternatives[0].Canonical);
        Assert.Equal("1904-12-31T23:59:59Z", result.Alternatives[0].TimeSpan!.EndOfTheEnd?.ToString());
    }

    [Fact]
    public void Undated_gives_no_span()
    {
        var result = Realise("undated");
        Assert.Equal(ParseStatus.Undated, result.Status);
        Assert.Empty(result.Spans());
    }

    [Fact]
    public void An_unresolved_period_is_unparsed_not_an_exception()
    {
        var result = Realise("period:edo");
        Assert.Equal(ParseStatus.Unparsed, result.Status);
        Assert.Contains(result.Notes, n => n.Contains("edo"));
    }

    [Fact]
    public void A_resolved_period_realises_its_bounds()
    {
        var edo = new NamedPeriod("edo", "Edo period", new Interval(new YearPoint(1603), new YearPoint(1868)));
        var result = new TimeSpanRealiser().Realise(edo, "Edo period");
        Assert.Equal("1603-01-01T00:00:00Z", result.TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1868-12-31T23:59:59Z", result.TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Contains(DateFlag.NamedPeriod, result.Flags);
        Assert.Equal("Edo period", result.TimeSpan.Label);
    }

    [Fact]
    public void A_correction_is_realised_and_explained()
    {
        var result = new TimeSpanRealiser().Realise(new Corrected(new YearPoint(1962), new YearPoint(1967)), "1962 (i.e. 1967)");
        Assert.Equal("1967-12-31T23:59:59Z", result.TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Contains(DateFlag.Corrected, result.Flags);
        Assert.Contains(result.Notes, n => n.Contains("1962"));
    }

    // ---- intent --------------------------------------------------------------------------------------------

    [Fact]
    public void Pair_intent_splits_an_interval_into_begin_and_end()
    {
        var result = Realise("1606/1669", intent: Intent.Pair);
        Assert.Equal(ResultKind.BirthDeath, result.Kind);
        Assert.Null(result.TimeSpan);
        Assert.Equal("1606-01-01T00:00:00Z", result.Begin!.BeginOfTheBegin?.ToString());
        Assert.Equal("1606-12-31T23:59:59Z", result.Begin!.EndOfTheEnd?.ToString());
        Assert.Equal("1669-01-01T00:00:00Z", result.End!.BeginOfTheBegin?.ToString());
        Assert.Equal("1669-12-31T23:59:59Z", result.End!.EndOfTheEnd?.ToString());
        Assert.Equal("1606/1669", result.Begin.Label);
    }

    [Fact]
    public void Pair_intent_with_open_bounds()
    {
        var result = Realise("1606/..", intent: Intent.Pair);
        Assert.NotNull(result.Begin);
        Assert.Null(result.End);
        Assert.Contains(DateFlag.OpenEnd, result.Flags);
    }

    [Fact]
    public void Born_and_died_markers_give_one_half_each()
    {
        var born = Realise("born 1606", intent: Intent.Pair);
        Assert.Equal(ResultKind.BirthDeath, born.Kind);
        Assert.NotNull(born.Begin);
        Assert.Null(born.End);

        var died = Realise("died 1669~");
        Assert.Equal(ResultKind.BirthDeath, died.Kind);
        Assert.Null(died.Begin);
        Assert.Equal("1669-12-31T23:59:59Z", died.End!.EndOfTheEnd?.ToString());
        Assert.Contains(DateFlag.Approximate, died.Flags);
    }

    [Fact]
    public void Floruit_gives_the_whole_span_and_both_halves()
    {
        var result = Realise("fl 1480/1510");
        Assert.Equal(ResultKind.Floruit, result.Kind);
        Assert.Equal("1480-01-01T00:00:00Z", result.TimeSpan!.BeginOfTheBegin?.ToString());
        Assert.Equal("1510-12-31T23:59:59Z", result.TimeSpan!.EndOfTheEnd?.ToString());
        Assert.Equal("1480-12-31T23:59:59Z", result.Begin!.EndOfTheEnd?.ToString());
        Assert.Equal("1510-01-01T00:00:00Z", result.End!.BeginOfTheBegin?.ToString());

        var single = Realise("fl 1480");
        Assert.Equal(ResultKind.Floruit, single.Kind);
        Assert.NotNull(single.TimeSpan);
        Assert.Null(single.Begin);
    }

    [Fact]
    public void Pair_intent_on_a_single_date_is_partial()
    {
        var result = Realise("1850~", intent: Intent.Pair);
        Assert.Equal(ParseStatus.Partial, result.Status);
        Assert.Equal(ResultKind.Single, result.Kind);
        Assert.NotNull(result.TimeSpan);
    }

    [Fact]
    public void Single_intent_keeps_an_interval_whole()
    {
        var result = Realise("1606/1669", intent: Intent.Single);
        Assert.Equal(ResultKind.Single, result.Kind);
        Assert.Equal("1669-12-31T23:59:59Z", result.TimeSpan!.EndOfTheEnd?.ToString());
    }

    [Fact]
    public void Inverted_pair_is_swapped()
    {
        var result = Realise("1669/1606", intent: Intent.Pair);
        Assert.Contains(DateFlag.Inverted, result.Flags);
        Assert.Equal("1606-01-01T00:00:00Z", result.Begin!.BeginOfTheBegin?.ToString());
    }

    // ---- labels --------------------------------------------------------------------------------------------

    [Fact]
    public void Label_policy()
    {
        var expression = DateExpression.Parse("1850");
        var label = new TimeSpanRealiser().Realise(expression, "c. 1850").TimeSpan!;
        Assert.Equal("c. 1850", label.Label);
        Assert.Null(label.IdentifiedBy);

        var both = new TimeSpanRealiser(ParseProfile.Default with { LabelPolicy = LabelPolicy.LabelAndName }).Realise(expression, "c. 1850").TimeSpan!;
        Assert.Equal("c. 1850", both.Label);
        Assert.Equal("c. 1850", Assert.IsType<Name>(Assert.Single(both.IdentifiedBy!)).Content);

        var name = new TimeSpanRealiser(ParseProfile.Default with { LabelPolicy = LabelPolicy.Name }).Realise(expression, "c. 1850").TimeSpan!;
        Assert.Null(name.Label);
        Assert.Equal("c. 1850", Assert.IsType<Name>(Assert.Single(name.IdentifiedBy!)).Content);
    }

    [Fact]
    public void Result_carries_canonical_and_edtf()
    {
        var result = Realise("late century 19");
        Assert.Equal("late century 19", result.Canonical);
        Assert.Equal("1867/1899", result.Edtf);
        Assert.Equal("default", result.Profile);
    }
}
