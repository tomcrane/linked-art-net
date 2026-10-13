using LinkedArtNet.Dates.Canonical;

namespace LinkedArtNet.Dates;

/// <summary>
/// What a date string <em>means</em>, with the surface form stripped away: "late 1950s", "Late 1950's" and
/// "1950s (late)" are all <c>Banded(Decade(1950), Band.Late)</c>. Parsing produces one of these; the
/// <see cref="Realisation.TimeSpanRealiser"/> turns it into datetimes under a <see cref="Realisation.ParseProfile"/>.
/// Every expression has a <see cref="Canonical"/> text form (EDTF where EDTF can say it, keyword extensions where it
/// cannot) that <see cref="CanonicalReader"/> parses back: <c>Read(Write(e)) == e</c>.
/// </summary>
public abstract record DateExpression
{
    /// <summary>The canonical text form; see docs/canonical-expression.md.</summary>
    public string Canonical => CanonicalWriter.Write(this);

    public sealed override string ToString() => Canonical;

    /// <summary>Parses a canonical expression; throws <see cref="FormatException"/> if the text is not canonical.</summary>
    public static DateExpression Parse(string canonical) => CanonicalReader.Parse(canonical);

    public static bool TryParse(string canonical, out DateExpression? expression) => CanonicalReader.TryParse(canonical, out expression);
}

/// <summary>A node that denotes a span of time directly, as opposed to a wrapper that modifies another node.</summary>
public abstract record DatePoint : DateExpression;

public enum Era { Ce, Bce }

/// <summary>Seasons, numbered as EDTF does (<c>1945-24</c> is winter 1945).</summary>
public enum Season { Spring = 21, Summer = 22, Autumn = 23, Winter = 24 }

/// <summary>EDTF qualification: <c>~</c> approximate, <c>?</c> uncertain, <c>%</c> both.</summary>
public enum Qualification { Approximate, Uncertain, ApproximateAndUncertain }

/// <summary>A named part of a span: "early 1950s", "second quarter of the 14th century", "end of the 1800s".</summary>
public enum Band
{
    Early, Mid, Late,
    FirstHalf, SecondHalf,
    FirstQuarter, SecondQuarter, ThirdQuarter, FourthQuarter,
    FirstThird, SecondThird, LastThird,
    BeginningOf, EndOf,
}

/// <summary>
/// How a bound relates to the inner date. <see cref="Before"/> and <see cref="After"/> exclude the inner date;
/// <see cref="By"/>, <see cref="Until"/>, <see cref="NotAfter"/> and <see cref="NotBefore"/> include it.
/// </summary>
public enum Relation { Before, After, By, Until, NotBefore, NotAfter }

public enum LifecycleMarker { Born, Died, Baptised, Buried, Floruit }

/// <summary>Units for deep time: years, thousand years (ka), million years (Ma), billion years (Ga).</summary>
public enum Magnitude { Years, Thousand, Million, Billion }

/// <summary>What "ago" counts back from: the present, or 1950 as radiocarbon "BP" does.</summary>
public enum DeepTimeReference { Present, BeforePresent1950 }

/// <summary>A calendar year in astronomical numbering (0 is 1 BCE, -1 is 2 BCE).</summary>
public sealed record YearPoint(long Year) : DatePoint;

public sealed record MonthPoint(long Year, int Month) : DatePoint
{
    public int Month { get; } = Month is >= 1 and <= 12 ? Month : throw new ArgumentOutOfRangeException(nameof(Month));
}

public sealed record DayPoint(long Year, int Month, int Day) : DatePoint
{
    public int Month { get; } = Month is >= 1 and <= 12 ? Month : throw new ArgumentOutOfRangeException(nameof(Month));
    public int Day { get; } = Day >= 1 && Day <= LinkedArtDate.DaysInMonth(Year, Month) ? Day : throw new ArgumentOutOfRangeException(nameof(Day));
}

public sealed record SeasonPoint(long Year, Season Season) : DatePoint;

/// <summary>
/// The ten years an EDTF <c>X</c> pattern covers: <c>195X</c> is <c>Decade(1950)</c> = 1950..1959. For a negative
/// base the pattern runs the other way: <c>-044X</c> is <c>Decade(-440)</c> = -449..-440.
/// </summary>
public sealed record Decade(long Base) : DatePoint
{
    public long Base { get; } = Base % 10 == 0 ? Base : throw new ArgumentException("A decade base is a multiple of ten.", nameof(Base));
    public long FirstYear => Base < 0 ? Base - 9 : Base;
    public long LastYear => Base < 0 ? Base : Base + 9;
}

/// <summary>An ordinal century ("19th century", "5th century BCE"); which years it covers is a realisation policy.</summary>
public sealed record Century(int Number, Era Era = Era.Ce) : DatePoint
{
    public int Number { get; } = Number >= 1 ? Number : throw new ArgumentOutOfRangeException(nameof(Number));
}

public sealed record Millennium(int Number, Era Era = Era.Ce) : DatePoint
{
    public int Number { get; } = Number >= 1 ? Number : throw new ArgumentOutOfRangeException(nameof(Number));
}

/// <summary>An EDTF year with unspecified digits that is not a plain decade: <c>18XX</c>, <c>1XXX</c>, <c>1X50</c>.</summary>
public sealed record UnspecifiedDigits(string Pattern) : DatePoint
{
    public string Pattern { get; } = IsValid(Pattern) ? Pattern : throw new ArgumentException($"'{Pattern}' is not a year pattern of digits and X.", nameof(Pattern));

    public static bool IsValid(string pattern)
    {
        var digits = pattern.StartsWith('-') ? pattern[1..] : pattern;
        return digits.Length >= 2 && digits.Contains('X') && digits.All(c => char.IsAsciiDigit(c) || c == 'X');
    }

    public long Minimum => long.Parse(Pattern.Replace('X', Pattern.StartsWith('-') ? '9' : '0'));
    public long Maximum => long.Parse(Pattern.Replace('X', Pattern.StartsWith('-') ? '0' : '9'));
}

/// <summary>A period known by name ("Edo period", "Regency"); <paramref name="Resolved"/> is null until a period source supplies the bounds.</summary>
public sealed record NamedPeriod(string Key, string? Label = null, DateExpression? Resolved = null) : DatePoint
{
    public string Key { get; } = Key.Length > 0 && Key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.') ? Key : throw new ArgumentException("A period key is letters, digits, '-', '_' or '.'.", nameof(Key));
}

/// <summary>"4.5 billion years ago", "10,000 BP". <paramref name="Value"/> keeps its written precision so the realiser can widen by significant digits.</summary>
public sealed record DeepTime(decimal Value, Magnitude Magnitude, DeepTimeReference Reference = DeepTimeReference.Present) : DatePoint
{
    public decimal Value { get; } = Value > 0 ? Value : throw new ArgumentOutOfRangeException(nameof(Value));

    public static long Factor(Magnitude m) => m switch
    {
        Magnitude.Years => 1,
        Magnitude.Thousand => 1_000,
        Magnitude.Million => 1_000_000,
        Magnitude.Billion => 1_000_000_000,
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    public decimal YearsAgo => Value * Factor(Magnitude);
}

/// <summary>"n.d.", "undated", "s.d.".</summary>
public sealed record Undated : DatePoint
{
    public static readonly Undated Instance = new();
}

/// <summary>EDTF <c>~</c>, <c>?</c>, <c>%</c> on a single date; intervals and sets qualify their members instead.</summary>
public sealed record Qualified(DateExpression Inner, Qualification Qualification) : DateExpression
{
    public DateExpression Inner { get; } = Inner is Interval or OneOf or Undated
        ? throw new ArgumentException("Qualify the members of an interval or set, not the whole.", nameof(Inner)) : Inner;
}

/// <summary>A band of a single date. A hedge goes outside the band (<c>Qualified(Banded(...))</c>, written <c>late 195X~</c>).</summary>
public sealed record Banded(DateExpression Inner, Band Band) : DateExpression
{
    public DateExpression Inner { get; } = Inner is Interval or OneOf or Undated or Relative or Lifecycle or Qualified
        ? throw new ArgumentException("A band applies to a single, unqualified date; qualify the band instead.", nameof(Inner)) : Inner;
}

public sealed record Relative(Relation Relation, DateExpression Inner) : DateExpression
{
    public DateExpression Inner { get; } = Inner is Interval or OneOf or Undated or Relative or Lifecycle
        ? throw new ArgumentException("A relation applies to a single date.", nameof(Inner)) : Inner;
}

public sealed record Lifecycle(LifecycleMarker Marker, DateExpression Inner) : DateExpression
{
    public DateExpression Inner { get; } = Inner is Undated or Lifecycle
        ? throw new ArgumentException("A lifecycle marker applies to a date or interval.", nameof(Inner)) : Inner;
}

/// <summary>A span between two bounds, either of which may be open (<c>../1850</c>, <c>1971/..</c>).</summary>
public sealed record Interval(DateExpression? Start, DateExpression? End) : DateExpression
{
    public DateExpression? Start { get; } = Check(Start);
    public DateExpression? End { get; } = Check(End);

    private static DateExpression? Check(DateExpression? bound) => bound is Interval or OneOf or Undated or Lifecycle or Relative
        ? throw new ArgumentException("An interval bound is a single date; relations apply to the whole expression.", nameof(bound))
        : bound;
}

/// <summary>"1904 or 1905": one of the members, as EDTF <c>[1904,1905]</c>.</summary>
public sealed record OneOf : DateExpression
{
    public OneOf(IEnumerable<DateExpression> members)
    {
        Members = members.ToList();
        if (Members.Count < 2) throw new ArgumentException("A set has at least two members.", nameof(members));
        if (Members.Any(m => m is OneOf or Undated or Lifecycle)) throw new ArgumentException("Set members are single dates or intervals.", nameof(members));
    }

    public OneOf(params DateExpression[] members) : this((IEnumerable<DateExpression>)members) { }

    public IReadOnlyList<DateExpression> Members { get; }

    public bool Equals(OneOf? other) => other is not null && Members.SequenceEqual(other.Members);

    public override int GetHashCode() => Members.Aggregate(17, (h, m) => h * 31 + m.GetHashCode());
}

/// <summary>"1962 (i.e. 1967)": the corrected reading is used; the original is kept for the explanation.</summary>
public sealed record Corrected(DateExpression Original, DateExpression Correction) : DateExpression;
