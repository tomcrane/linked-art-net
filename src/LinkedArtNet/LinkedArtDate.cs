using System.Globalization;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// A point in time at second precision, in UTC, on the proleptic Gregorian calendar, for use in
/// Linked Art <c>TimeSpan</c> properties (<c>begin_of_the_begin</c> and friends).
/// </summary>
/// <remarks>
/// <para>
/// Years use ISO 8601 / XSD 1.1 astronomical numbering: year <c>0</c> is 1 BCE, year <c>-1</c> is 2 BCE.
/// Use <see cref="FromBce"/> and <see cref="BceYear"/> to avoid doing that arithmetic by hand.
/// Years may be as large as ±10<sup>15</sup>, far beyond <see cref="DateTime"/> and enough for geological time.
/// </para>
/// <para>
/// The lexical form (<see cref="ToString()"/>) is the <c>xsd:dateTime</c> profile Linked Art requires:
/// <c>yyyy-MM-ddTHH:mm:ssZ</c>, with a leading <c>-</c> for negative years and as many year digits as needed
/// (at least four). Parsing is tolerant of the forms found in the wild: no timezone (assumed UTC), numeric
/// offsets (converted to UTC), fractional seconds (dropped), <c>T24:00:00</c> (the last second of the day),
/// a leading <c>+</c>, and date-only or year-month or year-only values (the start of that period).
/// </para>
/// </remarks>
[JsonConverter(typeof(LinkedArtDateConverter))]
public readonly record struct LinkedArtDate : IComparable<LinkedArtDate>, IComparable, IParsable<LinkedArtDate>
{
    /// <summary>The largest magnitude of year supported (10^15).</summary>
    public const long MaxAbsYear = 1_000_000_000_000_000L;

    private const long SecondsPerDay = 86_400;

    /// <summary>Astronomical year: 0 is 1 BCE, -1 is 2 BCE.</summary>
    public long Year { get; }
    public int Month { get; }
    public int Day { get; }
    public int Hour { get; }
    public int Minute { get; }
    public int Second { get; }

    public LinkedArtDate(long year, int month, int day) : this(year, month, day, 0, 0, 0)
    {
    }

    public LinkedArtDate(long year, int month, int day, int hour, int minute, int second)
    {
        if (year < -MaxAbsYear || year > MaxAbsYear)
            throw new ArgumentOutOfRangeException(nameof(year), year, $"Year must be within ±{MaxAbsYear}.");
        if (month < 1 || month > 12)
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be 1-12.");
        var daysInMonth = DaysInMonth(year, month);
        if (day < 1 || day > daysInMonth)
            throw new ArgumentOutOfRangeException(nameof(day), day, $"Day must be 1-{daysInMonth} for {year:D4}-{month:D2}.");
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "Hour must be 0-23.");
        if (minute < 0 || minute > 59)
            throw new ArgumentOutOfRangeException(nameof(minute), minute, "Minute must be 0-59.");
        if (second < 0 || second > 59)
            throw new ArgumentOutOfRangeException(nameof(second), second, "Second must be 0-59.");

        Year = year;
        Month = month;
        Day = day;
        Hour = hour;
        Minute = minute;
        Second = second;
    }

    /// <summary>Creates a date from a <see cref="DateTimeOffset"/>, converting to UTC.</summary>
    public LinkedArtDate(DateTimeOffset dateTimeOffset)
        : this(dateTimeOffset.UtcDateTime)
    {
    }

    /// <summary>
    /// Creates a date from a <see cref="DateTime"/>. <see cref="DateTimeKind.Local"/> values are converted to
    /// UTC; <see cref="DateTimeKind.Unspecified"/> values are taken to be UTC already.
    /// </summary>
    public LinkedArtDate(DateTime dateTime)
    {
        var utc = dateTime.Kind == DateTimeKind.Local ? dateTime.ToUniversalTime() : dateTime;
        Year = utc.Year;
        Month = utc.Month;
        Day = utc.Day;
        Hour = utc.Hour;
        Minute = utc.Minute;
        Second = utc.Second;
    }

    /// <summary>Creates a date in a BCE year (1 BCE = astronomical year 0).</summary>
    public static LinkedArtDate FromBce(long bceYear, int month = 1, int day = 1, int hour = 0, int minute = 0, int second = 0)
    {
        if (bceYear < 1)
            throw new ArgumentOutOfRangeException(nameof(bceYear), bceYear, "BCE years start at 1.");
        return new LinkedArtDate(1 - bceYear, month, day, hour, minute, second);
    }

    /// <summary>True when the year is 1 BCE or earlier (astronomical year &lt;= 0).</summary>
    public bool IsBce => Year <= 0;

    /// <summary>The year as a BCE count (1 BCE = 1), or null for CE years.</summary>
    public long? BceYear => Year <= 0 ? 1 - Year : null;

    // ---- calendar arithmetic ---------------------------------------------------------------------

    /// <summary>Proleptic Gregorian leap-year rule, valid for negative years.</summary>
    public static bool IsLeapYear(long year) => (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;

    public static int DaysInMonth(long year, int month) => month switch
    {
        1 or 3 or 5 or 7 or 8 or 10 or 12 => 31,
        4 or 6 or 9 or 11 => 30,
        2 => IsLeapYear(year) ? 29 : 28,
        _ => throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be 1-12."),
    };

    /// <summary>
    /// Days since 1970-01-01 for a proleptic Gregorian civil date (Howard Hinnant's algorithm).
    /// </summary>
    public static long DaysFromCivil(long year, int month, int day)
    {
        year -= month <= 2 ? 1 : 0;
        var era = (year >= 0 ? year : year - 399) / 400;
        var yoe = year - era * 400;                                        // [0, 399]
        var doy = (153 * (month + (month > 2 ? -3 : 9)) + 2) / 5 + day - 1; // [0, 365]
        var doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;                   // [0, 146096]
        return era * 146097 + doe - 719468;
    }

    /// <summary>Inverse of <see cref="DaysFromCivil"/>.</summary>
    public static (long Year, int Month, int Day) CivilFromDays(long days)
    {
        days += 719468;
        var era = (days >= 0 ? days : days - 146096) / 146097;
        var doe = days - era * 146097;                                          // [0, 146096]
        var yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;       // [0, 399]
        var y = yoe + era * 400;
        var doy = doe - (365 * yoe + yoe / 4 - yoe / 100);                      // [0, 365]
        var mp = (5 * doy + 2) / 153;                                           // [0, 11]
        var d = (int)(doy - (153 * mp + 2) / 5 + 1);                            // [1, 31]
        var m = (int)(mp < 10 ? mp + 3 : mp - 9);                               // [1, 12]
        return (y + (m <= 2 ? 1 : 0), m, d);
    }

    public LinkedArtDate AddDays(long days)
    {
        if (days == 0) return this;
        var (y, m, d) = CivilFromDays(DaysFromCivil(Year, Month, Day) + days);
        return new LinkedArtDate(y, m, d, Hour, Minute, Second);
    }

    public LinkedArtDate AddSeconds(long seconds)
    {
        if (seconds == 0) return this;
        var secondOfDay = Hour * 3600L + Minute * 60L + Second;
        var total = secondOfDay + seconds;
        var dayDelta = FloorDiv(total, SecondsPerDay);
        var newSecondOfDay = (int)(total - dayDelta * SecondsPerDay);
        var (y, m, d) = dayDelta == 0
            ? (Year, Month, Day)
            : CivilFromDays(DaysFromCivil(Year, Month, Day) + dayDelta);
        return new LinkedArtDate(y, m, d, newSecondOfDay / 3600, newSecondOfDay / 60 % 60, newSecondOfDay % 60);
    }

    /// <summary>Adds years, clamping 29 February to 28 February in non-leap years.</summary>
    public LinkedArtDate AddYears(long years)
    {
        if (years == 0) return this;
        var y = Year + years;
        var d = Math.Min(Day, DaysInMonth(y, Month));
        return new LinkedArtDate(y, Month, d, Hour, Minute, Second);
    }

    /// <summary>Adds months, clamping the day to the end of the resulting month.</summary>
    public LinkedArtDate AddMonths(long months)
    {
        if (months == 0) return this;
        var index = Year * 12 + (Month - 1) + months;
        var y = FloorDiv(index, 12);
        var m = (int)(index - y * 12) + 1;
        var d = Math.Min(Day, DaysInMonth(y, m));
        return new LinkedArtDate(y, m, d, Hour, Minute, Second);
    }

    private static long FloorDiv(long a, long b)
    {
        var q = a / b;
        if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
        return q;
    }

    // ---- period boundaries -----------------------------------------------------------------------

    public static LinkedArtDate StartOfYear(long year) => new(year, 1, 1);
    public static LinkedArtDate EndOfYear(long year) => new(year, 12, 31, 23, 59, 59);
    public static LinkedArtDate StartOfMonth(long year, int month) => new(year, month, 1);
    public static LinkedArtDate EndOfMonth(long year, int month) => new(year, month, DaysInMonth(year, month), 23, 59, 59);

    public LinkedArtDate StartOfDay() => new(Year, Month, Day);
    public LinkedArtDate EndOfDay() => new(Year, Month, Day, 23, 59, 59);
    /// <summary>Alias of <see cref="EndOfDay"/>: the Linked Art convention for inclusive span ends.</summary>
    public LinkedArtDate LastSecondOfDay() => EndOfDay();
    public LinkedArtDate StartOfMonth() => StartOfMonth(Year, Month);
    public LinkedArtDate EndOfMonth() => EndOfMonth(Year, Month);
    public LinkedArtDate StartOfYear() => StartOfYear(Year);
    public LinkedArtDate EndOfYear() => EndOfYear(Year);

    // ---- conversions -----------------------------------------------------------------------------

    /// <summary>Converts to a UTC <see cref="DateTimeOffset"/>; throws when the year is outside 1-9999.</summary>
    public DateTimeOffset ToDateTimeOffset()
    {
        if (!TryToDateTimeOffset(out var result))
            throw new InvalidOperationException($"{this} cannot be represented as a DateTimeOffset (year must be 1-9999).");
        return result;
    }

    public bool TryToDateTimeOffset(out DateTimeOffset result)
    {
        if (Year < 1 || Year > 9999)
        {
            result = default;
            return false;
        }
        result = new DateTimeOffset((int)Year, Month, Day, Hour, Minute, Second, TimeSpan.Zero);
        return true;
    }

    // ---- comparison ------------------------------------------------------------------------------

    public int CompareTo(LinkedArtDate other)
    {
        var c = Year.CompareTo(other.Year);
        if (c != 0) return c;
        c = Month.CompareTo(other.Month);
        if (c != 0) return c;
        c = Day.CompareTo(other.Day);
        if (c != 0) return c;
        c = Hour.CompareTo(other.Hour);
        if (c != 0) return c;
        c = Minute.CompareTo(other.Minute);
        if (c != 0) return c;
        return Second.CompareTo(other.Second);
    }

    int IComparable.CompareTo(object? obj) => obj switch
    {
        null => 1,
        LinkedArtDate other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(LinkedArtDate)}.", nameof(obj)),
    };

    public static bool operator <(LinkedArtDate left, LinkedArtDate right) => left.CompareTo(right) < 0;
    public static bool operator >(LinkedArtDate left, LinkedArtDate right) => left.CompareTo(right) > 0;
    public static bool operator <=(LinkedArtDate left, LinkedArtDate right) => left.CompareTo(right) <= 0;
    public static bool operator >=(LinkedArtDate left, LinkedArtDate right) => left.CompareTo(right) >= 0;

    // ---- lexical form ----------------------------------------------------------------------------

    /// <summary>The <c>xsd:dateTime</c> lexical form, always in UTC with a <c>Z</c> designator.</summary>
    public override string ToString()
    {
        var yearPart = Year < 0
            ? "-" + (-Year).ToString("D4", CultureInfo.InvariantCulture)
            : Year.ToString("D4", CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture,
            $"{yearPart}-{Month:D2}-{Day:D2}T{Hour:D2}:{Minute:D2}:{Second:D2}Z");
    }

    public static LinkedArtDate Parse(string s, IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!TryParse(s.AsSpan(), out var result))
            throw new FormatException($"'{s}' is not a valid Linked Art dateTime.");
        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out LinkedArtDate result)
    {
        if (s is null)
        {
            result = default;
            return false;
        }
        return TryParse(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out LinkedArtDate result)
    {
        result = default;
        s = s.Trim();
        if (s.IsEmpty) return false;

        var i = 0;
        var negative = false;
        if (s[0] == '-') { negative = true; i++; }
        else if (s[0] == '+') { i++; }

        var yearStart = i;
        while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
        var yearDigits = i - yearStart;
        if (yearDigits < 4 || yearDigits > 18) return false;
        if (!long.TryParse(s[yearStart..i], NumberStyles.None, CultureInfo.InvariantCulture, out var year)) return false;
        if (negative) year = -year;
        if (year < -MaxAbsYear || year > MaxAbsYear) return false;

        int month = 1, day = 1, hour = 0, minute = 0, second = 0;
        long offsetSeconds = 0;

        if (i < s.Length && s[i] == '-')
        {
            i++;
            if (!TryRead2(s, ref i, out month)) return false;

            if (i < s.Length && s[i] == '-')
            {
                i++;
                if (!TryRead2(s, ref i, out day)) return false;

                if (i < s.Length && (s[i] == 'T' || s[i] == ' '))
                {
                    i++;
                    if (!TryRead2(s, ref i, out hour)) return false;
                    if (i >= s.Length || s[i] != ':') return false;
                    i++;
                    if (!TryRead2(s, ref i, out minute)) return false;
                    if (i >= s.Length || s[i] != ':') return false;
                    i++;
                    if (!TryRead2(s, ref i, out second)) return false;

                    if (i < s.Length && s[i] == '.')
                    {
                        i++;
                        var fracStart = i;
                        while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
                        if (i == fracStart) return false;
                    }

                    if (i < s.Length)
                    {
                        if (s[i] == 'Z' || s[i] == 'z')
                        {
                            i++;
                        }
                        else if (s[i] == '+' || s[i] == '-')
                        {
                            var sign = s[i] == '-' ? -1 : 1;
                            i++;
                            if (!TryRead2(s, ref i, out var offH)) return false;
                            if (i < s.Length && s[i] == ':') i++;
                            if (!TryRead2(s, ref i, out var offM)) return false;
                            if (offH > 14 || offM > 59) return false;
                            offsetSeconds = sign * (offH * 3600L + offM * 60L);
                        }
                    }
                }
            }
        }

        if (i != s.Length) return false;
        if (month < 1 || month > 12) return false;
        if (day < 1 || day > DaysInMonth(year, month)) return false;

        if (hour == 24)
        {
            if (minute != 0 || second != 0) return false;
            hour = 23; minute = 59; second = 59;
        }
        else if (hour > 23 || minute > 59 || second > 59)
        {
            return false;
        }

        var date = new LinkedArtDate(year, month, day, hour, minute, second);
        result = offsetSeconds == 0 ? date : date.AddSeconds(-offsetSeconds);
        return true;
    }

    private static bool TryRead2(ReadOnlySpan<char> s, ref int i, out int value)
    {
        value = 0;
        if (i + 2 > s.Length) return false;
        if (!char.IsAsciiDigit(s[i]) || !char.IsAsciiDigit(s[i + 1])) return false;
        value = (s[i] - '0') * 10 + (s[i + 1] - '0');
        i += 2;
        return true;
    }
}
