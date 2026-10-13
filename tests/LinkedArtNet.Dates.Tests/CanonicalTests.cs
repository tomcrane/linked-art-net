using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LinkedArtNet.Dates.Canonical;

namespace LinkedArtNet.Dates.Tests;

public class CanonicalTests
{
    public static TheoryData<string, DateExpression> Cases() => new()
    {
        { "1984", new YearPoint(1984) },
        { "0079", new YearPoint(79) },
        { "0000", new YearPoint(0) },
        { "-0499", new YearPoint(-499) },
        { "Y12345", new YearPoint(12345) },
        { "Y-4500000000", new YearPoint(-4_500_000_000) },
        { "1984-05", new MonthPoint(1984, 5) },
        { "1984-05-17", new DayPoint(1984, 5, 17) },
        { "1945-24", new SeasonPoint(1945, Season.Winter) },
        { "195X", new Decade(1950) },
        { "-044X", new Decade(-440) },
        { "18XX", new UnspecifiedDigits("18XX") },
        { "1XXX", new UnspecifiedDigits("1XXX") },
        { "1X50", new UnspecifiedDigits("1X50") },
        { "century 19", new Century(19) },
        { "century -5", new Century(5, Era.Bce) },
        { "millennium 2", new Millennium(2) },
        { "millennium -1", new Millennium(1, Era.Bce) },
        { "period:edo", new NamedPeriod("edo") },
        { "4.5 Ga ago", new DeepTime(4.5m, Magnitude.Billion) },
        { "10 ka BP", new DeepTime(10m, Magnitude.Thousand, DeepTimeReference.BeforePresent1950) },
        { "undated", Undated.Instance },
        { "1850~", new Qualified(new YearPoint(1850), Qualification.Approximate) },
        { "1850?", new Qualified(new YearPoint(1850), Qualification.Uncertain) },
        { "1850%", new Qualified(new YearPoint(1850), Qualification.ApproximateAndUncertain) },
        { "-2499~", new Qualified(new YearPoint(-2499), Qualification.Approximate) },
        { "late 195X", new Banded(new Decade(1950), Band.Late) },
        { "early century 14", new Banded(new Century(14), Band.Early) },
        { "q2 century 14", new Banded(new Century(14), Band.SecondQuarter) },
        { "h2 18XX", new Banded(new UnspecifiedDigits("18XX"), Band.SecondHalf) },
        { "end century 19~", new Qualified(new Banded(new Century(19), Band.EndOf), Qualification.Approximate) },
        { "late 195X~", new Qualified(new Banded(new Decade(1950), Band.Late), Qualification.Approximate) },
        { "before late 195X~", new Relative(Relation.Before, new Qualified(new Banded(new Decade(1950), Band.Late), Qualification.Approximate)) },
        { "1850/1900~", new Interval(new YearPoint(1850), new Qualified(new YearPoint(1900), Qualification.Approximate)) },
        { "before 1850", new Relative(Relation.Before, new YearPoint(1850)) },
        { "not-after 1850~", new Relative(Relation.NotAfter, new Qualified(new YearPoint(1850), Qualification.Approximate)) },
        { "born 1606", new Lifecycle(LifecycleMarker.Born, new YearPoint(1606)) },
        { "fl 1480/1510", new Lifecycle(LifecycleMarker.Floruit, new Interval(new YearPoint(1480), new YearPoint(1510))) },
        { "born 1850/died 1920", new Interval(new Lifecycle(LifecycleMarker.Born, new YearPoint(1850)), new Lifecycle(LifecycleMarker.Died, new YearPoint(1920))) },
        { "born 1850/1860", new Lifecycle(LifecycleMarker.Born, new Interval(new YearPoint(1850), new YearPoint(1860))) },
        { "baptised 1606~/buried 1669", new Interval(new Lifecycle(LifecycleMarker.Baptised, new Qualified(new YearPoint(1606), Qualification.Approximate)), new Lifecycle(LifecycleMarker.Buried, new YearPoint(1669))) },
        { "1870/1890", new Interval(new YearPoint(1870), new YearPoint(1890)) },
        { "../1850", new Interval(null, new YearPoint(1850)) },
        { "1971/..", new Interval(new YearPoint(1971), null) },
        { "1850~/1900?", new Interval(new Qualified(new YearPoint(1850), Qualification.Approximate), new Qualified(new YearPoint(1900), Qualification.Uncertain)) },
        { "late 195X/century 20", new Interval(new Banded(new Decade(1950), Band.Late), new Century(20)) },
        { "[1904,1905]", new OneOf(new YearPoint(1904), new YearPoint(1905)) },
        { "[1904~,195X,1870/1890]", new OneOf(new Qualified(new YearPoint(1904), Qualification.Approximate), new Decade(1950), new Interval(new YearPoint(1870), new YearPoint(1890))) },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Reads_canonical_text(string canonical, DateExpression expected)
    {
        Assert.Equal(expected, DateExpression.Parse(canonical));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Writes_canonical_text(string canonical, DateExpression expression)
    {
        Assert.Equal(canonical, expression.Canonical);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1984-13")]
    [InlineData("1984-02-30")]
    [InlineData("84")]
    [InlineData("Y1984")]
    [InlineData("century 0")]
    [InlineData("century x")]
    [InlineData("late")]
    [InlineData("[1904]")]
    [InlineData("undated/1900")]
    [InlineData("../after 2015")]
    [InlineData("late 1850s")]
    [InlineData("19th century")]
    [InlineData("4,5 Ga ago")]
    public void Rejects_non_canonical_text(string text)
    {
        Assert.False(DateExpression.TryParse(text, out _));
        Assert.Throws<FormatException>(() => DateExpression.Parse(text));
    }

    [Fact]
    public void Corrected_writes_as_its_correction()
    {
        var corrected = new Corrected(new YearPoint(1962), new YearPoint(1967));
        Assert.Equal("1967", corrected.Canonical);
    }

    [Fact]
    public void Edtf_is_the_canonical_for_pure_edtf_and_the_realised_years_otherwise()
    {
        var realiser = new Realisation.TimeSpanRealiser();
        Assert.Equal("1850~", EdtfWriter.Write(new Qualified(new YearPoint(1850), Qualification.Approximate), realiser.RealiseSpan));
        var century = new Century(19);
        Assert.Equal("1800/1899", EdtfWriter.Write(century, realiser.RealiseSpan));
        var before = new Relative(Relation.Before, new YearPoint(1850));
        Assert.Equal("../1849", EdtfWriter.Write(before, realiser.RealiseSpan));
    }

    // ---- property: Read(Write(e)) == e for every expression the generator can build --------------------------

    [Property(Arbitrary = [typeof(ExpressionArbitrary)], MaxTest = 500)]
    public bool Canonical_round_trips(DateExpression expression)
    {
        var text = expression.Canonical;
        return DateExpression.Parse(text) == expression && DateExpression.Parse(text).Canonical == text;
    }

    [Property(Arbitrary = [typeof(ExpressionArbitrary)], MaxTest = 300)]
    public bool Realisation_never_throws_and_keeps_bounds_ordered(DateExpression expression)
    {
        var result = new Realisation.TimeSpanRealiser(currentYear: () => 2026).Realise(expression, expression.Canonical);
        foreach (var ts in result.Spans())
        {
            if (!ts.IsOrdered()) return false;
            if (ts.BeginOfTheBegin is null && ts.EndOfTheEnd is null) return false;
            if (ts.Label != expression.Canonical) return false;
        }
        return result.Status != ParseStatus.Unparsed || ContainsUnresolvedPeriod(expression)
            || (expression is OneOf && result.Flags.Contains(DateFlag.OpenStart) && result.Flags.Contains(DateFlag.OpenEnd));
    }

    private static bool ContainsUnresolvedPeriod(DateExpression e) => e switch
    {
        NamedPeriod { Resolved: null } => true,
        Qualified q => ContainsUnresolvedPeriod(q.Inner),
        Banded b => ContainsUnresolvedPeriod(b.Inner),
        Relative r => ContainsUnresolvedPeriod(r.Inner),
        Lifecycle l => ContainsUnresolvedPeriod(l.Inner),
        Interval i => (i.Start is not null && ContainsUnresolvedPeriod(i.Start)) || (i.End is not null && ContainsUnresolvedPeriod(i.End)),
        OneOf set => set.Members.Any(ContainsUnresolvedPeriod),
        _ => false,
    };
}

/// <summary>Random, structurally valid expressions (no Corrected, no resolved periods).</summary>
public static class ExpressionArbitrary
{
    public static Arbitrary<DateExpression> Expression() => Arb.From(Gen.Sized(Build));

    private static Gen<long> Year => Gen.Frequency(
        (6, Gen.Choose(1000, 2100).Select(i => (long)i)),
        (2, Gen.Choose(-9999, 999).Select(i => (long)i)),
        (1, Gen.Choose(10000, 99999999).Select(i => (long)i)),
        (1, Gen.Choose(10000, 99999999).Select(i => -(long)i)));

    private static Gen<DatePoint> Point => Gen.OneOf<DatePoint>(
        Year.Select(y => (DatePoint)new YearPoint(y)),
        Year.SelectMany(y => Gen.Choose(1, 12).Select(m => (DatePoint)new MonthPoint(y, m))),
        Year.SelectMany(y => Gen.Choose(1, 12).SelectMany(m => Gen.Choose(1, 28).Select(d => (DatePoint)new DayPoint(y, m, d)))),
        Year.SelectMany(y => Gen.Elements(Season.Spring, Season.Summer, Season.Autumn, Season.Winter).Select(s => (DatePoint)new SeasonPoint(y, s))),
        Gen.Choose(-500, 300).Select(i => (DatePoint)new Decade(i * 10L)),
        Gen.Choose(1, 30).SelectMany(n => Gen.Elements(Era.Ce, Era.Bce).Select(e => (DatePoint)new Century(n, e))),
        Gen.Choose(1, 5).SelectMany(n => Gen.Elements(Era.Ce, Era.Bce).Select(e => (DatePoint)new Millennium(n, e))),
        Gen.Elements("18XX", "1XXX", "1X50", "19X9", "-04XX").Select(p => (DatePoint)new UnspecifiedDigits(p)),
        Gen.Elements("edo", "meiji-27", "regency").Select(k => (DatePoint)new NamedPeriod(k)),
        Gen.Choose(1, 9999).SelectMany(v => Gen.Choose(0, 2).SelectMany(scale => Gen.Elements(Magnitude.Years, Magnitude.Thousand, Magnitude.Million, Magnitude.Billion).SelectMany(m => Gen.Elements(DeepTimeReference.Present, DeepTimeReference.BeforePresent1950).Select(r =>
            (DatePoint)new DeepTime(v / (decimal)Math.Pow(10, scale), m, r))))));

    private static Gen<DateExpression> Banded => Point.SelectMany(p => Gen.Frequency(
        (3, Gen.Constant((DateExpression)p)),
        (2, Gen.Elements(Enum.GetValues<Band>()).Select(b => (DateExpression)new Banded(p, b)))));

    private static Gen<DateExpression> Single => Banded.SelectMany(s => Gen.Frequency(
        (3, Gen.Constant(s)),
        (1, Gen.Elements(Qualification.Approximate, Qualification.Uncertain, Qualification.ApproximateAndUncertain).Select(q => (DateExpression)new Qualified(s, q)))));

    private static Gen<DateExpression> Prefixed => Single.SelectMany(s => Gen.Frequency(
        (5, Gen.Constant(s)),
        (1, Gen.Elements(Enum.GetValues<Relation>()).Select(r => (DateExpression)new Relative(r, s)))));

    private static Gen<DateExpression> Build(int size)
    {
        var interval = Gen.Frequency(
            (4, Single.SelectMany(a => Single.Select(b => (DateExpression)new Interval(a, b)))),
            (1, Single.Select(a => (DateExpression)new Interval(a, null))),
            (1, Single.Select(b => (DateExpression)new Interval(null, b))));
        var lifecycle = Gen.Elements(Enum.GetValues<LifecycleMarker>()).SelectMany(m =>
            Gen.OneOf(Prefixed, interval).Select(inner => (DateExpression)new Lifecycle(m, inner)));
        var markedPair = Gen.Elements(Enum.GetValues<LifecycleMarker>()).SelectMany(m1 => Gen.Elements(Enum.GetValues<LifecycleMarker>()).SelectMany(m2 =>
            Single.SelectMany(a => Single.Select(b => (DateExpression)new Interval(new Lifecycle(m1, a), new Lifecycle(m2, b))))));
        var set = Gen.Choose(2, 4).SelectMany(n => Gen.ArrayOf(Gen.OneOf(Prefixed, interval), n)).Select(ms => (DateExpression)new OneOf(ms));
        return Gen.Frequency(
            (5, Prefixed),
            (3, interval),
            (1, lifecycle),
            (1, markedPair),
            (1, set),
            (1, Gen.Constant((DateExpression)Undated.Instance)));
    }
}
