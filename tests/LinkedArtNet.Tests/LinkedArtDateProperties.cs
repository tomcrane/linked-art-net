using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace LinkedArtNet.Tests;

/// <summary>
/// Property-based tests for <see cref="LinkedArtDate"/> over a wide range of years, including deep time
/// and BCE, which example-based tests cannot cover exhaustively.
/// </summary>
public class LinkedArtDateProperties
{
    // Years from about -5 billion to +5 billion, biased to include small magnitudes too.
    private static readonly Gen<long> Years = Gen.OneOf(
        Gen.Choose(-10000, 10000).Select(i => (long)i),
        from hi in Gen.Choose(-5000, 5000)
        from lo in Gen.Choose(0, 999_999)
        select (long)hi * 1_000_000 + lo);

    private static readonly Gen<LinkedArtDate> Dates =
        from y in Years
        from m in Gen.Choose(1, 12)
        from d in Gen.Choose(1, 31)
        from h in Gen.Choose(0, 23)
        from mi in Gen.Choose(0, 59)
        from s in Gen.Choose(0, 59)
        select new LinkedArtDate(y, m, Math.Min(d, LinkedArtDate.DaysInMonth(y, m)), h, mi, s);

    private static readonly Arbitrary<LinkedArtDate> DateArb = Arb.From(Dates);

    [Property(MaxTest = 1000)]
    public Property Format_then_parse_round_trips() =>
        Prop.ForAll(DateArb, date => LinkedArtDate.Parse(date.ToString()) == date);

    [Property(MaxTest = 1000)]
    public Property Days_from_civil_round_trips() =>
        Prop.ForAll(DateArb, date =>
        {
            var days = LinkedArtDate.DaysFromCivil(date.Year, date.Month, date.Day);
            var (y, m, d) = LinkedArtDate.CivilFromDays(days);
            return y == date.Year && m == date.Month && d == date.Day;
        });

    [Property(MaxTest = 1000)]
    public Property Adding_and_subtracting_days_is_identity() =>
        Prop.ForAll(DateArb, Arb.From(Gen.Choose(-1_000_000, 1_000_000)),
            (date, n) => date.AddDays(n).AddDays(-n) == date);

    [Property(MaxTest = 1000)]
    public Property Adding_one_day_is_86400_seconds() =>
        Prop.ForAll(DateArb, date => date.AddDays(1) == date.AddSeconds(86_400));

    [Property(MaxTest = 1000)]
    public Property Later_dates_compare_greater() =>
        Prop.ForAll(DateArb, Arb.From(Gen.Choose(1, 1_000_000)),
            (date, n) => date.AddSeconds(n) > date && date < date.AddSeconds(n));

    [Property(MaxTest = 1000)]
    public Property Day_components_are_always_valid_after_arithmetic() =>
        Prop.ForAll(DateArb, Arb.From(Gen.Choose(-100_000, 100_000)), (date, n) =>
        {
            var moved = date.AddDays(n);
            return moved.Month is >= 1 and <= 12
                && moved.Day >= 1
                && moved.Day <= LinkedArtDate.DaysInMonth(moved.Year, moved.Month);
        });

    [Property(MaxTest = 500)]
    public Property Last_second_of_day_is_the_latest_instant_of_that_day() =>
        Prop.ForAll(DateArb, date =>
        {
            var last = date.LastSecondOfDay();
            return last.Year == date.Year && last.Month == date.Month && last.Day == date.Day
                && last >= date
                && last.AddSeconds(1).Day != date.Day;
        });
}
