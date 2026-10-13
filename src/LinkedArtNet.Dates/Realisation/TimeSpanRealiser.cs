using System.Globalization;
using LinkedArtNet.Dates.Canonical;

namespace LinkedArtNet.Dates.Realisation;

/// <summary>
/// Turns a <see cref="DateExpression"/> into Linked Art TimeSpans under a <see cref="ParseProfile"/>. Parsing decides
/// what a string means; this decides which datetimes that meaning covers, so every rule here is a profile policy.
/// Each node type is realised by a small pure function on <see cref="RealisedSpan"/>; the public entry point adds
/// intent handling, labels and the result envelope.
/// </summary>
public sealed class TimeSpanRealiser
{
    private readonly ParseProfile _profile;
    private readonly Func<long> _currentYear;

    public TimeSpanRealiser(ParseProfile? profile = null, Func<long>? currentYear = null)
    {
        _profile = profile ?? ParseProfile.Default;
        _currentYear = currentYear ?? (() => DateTime.UtcNow.Year);
    }

    public ParseProfile Profile => _profile;

    /// <summary>Realise a parsed expression. <paramref name="input"/> becomes every TimeSpan's label.</summary>
    public TimeSpanParseResult Realise(DateExpression expression, string input, Intent intent = Intent.Auto, double confidence = 1.0, string strategy = "canonical")
    {
        ArgumentNullException.ThrowIfNull(expression);
        var notes = new List<string>();
        var flags = new HashSet<DateFlag>();
        var alternatives = new List<Alternative>();

        if (expression is Undated)
        {
            return new TimeSpanParseResult
            {
                Input = input, Status = ParseStatus.Undated, Kind = ResultKind.None, Confidence = confidence,
                Expression = expression, Canonical = expression.Canonical, Strategy = strategy, Profile = _profile.Name,
                Notes = ["The text says the date is unknown."],
            };
        }

        var core = expression is Corrected c ? Note(c, notes, flags) : expression;
        var kind = DecideKind(core, intent, notes);

        RealisedSpan whole;
        LinkedArtTimeSpan? begin = null, end = null, timeSpan = null;
        try
        {
            switch (kind)
            {
                case ResultKind.BirthDeath or ResultKind.FormationDissolution:
                {
                    var (first, second) = SplitPair(core, flags, notes);
                    whole = RealisedSpan.Hull(first, second);
                    begin = first is null ? null : ToTimeSpan(first.Value, input);
                    end = second is null ? null : ToTimeSpan(second.Value, input);
                    break;
                }
                case ResultKind.Floruit:
                {
                    var inner = ((Lifecycle)Unwrap(core)).Inner;
                    whole = Realise(inner, flags, notes, alternatives);
                    flags.Add(DateFlag.Floruit);
                    timeSpan = ToTimeSpan(whole, input);
                    if (inner is Interval { Start: not null, End: not null } iv)
                    {
                        begin = ToTimeSpan(Realise(iv.Start, flags, notes, alternatives), input);
                        end = ToTimeSpan(Realise(iv.End, flags, notes, alternatives), input);
                    }
                    break;
                }
                default:
                    whole = Realise(core, flags, notes, alternatives);
                    timeSpan = ToTimeSpan(whole, input);
                    break;
            }
        }
        catch (RealisationException ex)
        {
            notes.Add(ex.Message);
            return new TimeSpanParseResult
            {
                Input = input, Status = ParseStatus.Unparsed, Kind = ResultKind.None, Confidence = 0,
                Expression = expression, Canonical = expression.Canonical, Flags = flags, Notes = notes,
                Strategy = strategy, Profile = _profile.Name,
            };
        }

        var status = ParseStatus.Parsed;
        if (intent == Intent.Pair && kind == ResultKind.Single)
        {
            status = ParseStatus.Partial;
            notes.Add("A pair was asked for but the text gives one date; it is returned as a single span.");
        }
        if (whole.IsEmpty)
        {
            notes.Add("The readings leave both ends open, so no span can be given.");
            return new TimeSpanParseResult
            {
                Input = input, Status = ParseStatus.Unparsed, Kind = ResultKind.None, Confidence = confidence,
                Expression = expression, Canonical = expression.Canonical, Flags = flags, Alternatives = alternatives, Notes = notes,
                Strategy = strategy, Profile = _profile.Name,
            };
        }

        return new TimeSpanParseResult
        {
            Input = input, Status = status, Kind = kind, Confidence = confidence,
            TimeSpan = timeSpan, Begin = begin, End = end,
            Expression = expression, Canonical = expression.Canonical,
            Edtf = EdtfWriter.Write(core, e => Realise(e, [], [], [])),
            Flags = flags, Alternatives = alternatives, Notes = notes,
            Strategy = strategy, Profile = _profile.Name,
        };
    }

    // ---- intent --------------------------------------------------------------------------------------------

    private static DateExpression Unwrap(DateExpression e) => e is Corrected c ? c.Correction : e;

    private static DateExpression Note(Corrected c, List<string> notes, HashSet<DateFlag> flags)
    {
        flags.Add(DateFlag.Corrected);
        notes.Add($"Read as the corrected date {c.Correction.Canonical}; the text also gives {c.Original.Canonical}.");
        return c.Correction;
    }

    private static ResultKind DecideKind(DateExpression e, Intent intent, List<string> notes)
    {
        if (e is Lifecycle { Marker: LifecycleMarker.Floruit }) return ResultKind.Floruit;
        var hasLifecycle = e is Lifecycle || e is Interval { Start: Lifecycle } || e is Interval { End: Lifecycle };
        return intent switch
        {
            Intent.Single => ResultKind.Single,
            Intent.Pair when e is Interval || hasLifecycle => ResultKind.BirthDeath,
            Intent.Pair => ResultKind.Single,
            _ when hasLifecycle => ResultKind.BirthDeath,
            _ => ResultKind.Single,
        };
    }

    private (RealisedSpan? First, RealisedSpan? Second) SplitPair(DateExpression e, HashSet<DateFlag> flags, List<string> notes)
    {
        var alternatives = new List<Alternative>();
        switch (e)
        {
            case Interval iv:
            {
                RealisedSpan? first = iv.Start is null ? null : Realise(iv.Start, flags, notes, alternatives);
                RealisedSpan? second = iv.End is null ? null : Realise(iv.End, flags, notes, alternatives);
                if (iv.Start is null) flags.Add(DateFlag.OpenStart);
                if (iv.End is null) flags.Add(DateFlag.OpenEnd);
                if (first is { } f && second is { } s && f.Start is { } fs && s.End is { } se && fs > se)
                {
                    flags.Add(DateFlag.Inverted);
                    notes.Add("The bounds were given in the wrong order and have been swapped.");
                    return (second, first);
                }
                return (first, second);
            }
            case Lifecycle { Marker: LifecycleMarker.Born or LifecycleMarker.Baptised } l:
                flags.Add(DateFlag.Lifecycle);
                notes.Add($"'{CanonicalWriter.LifecycleKeyword(l.Marker)}' gives only the first date of the pair.");
                return (Realise(l.Inner, flags, notes, alternatives), null);
            case Lifecycle { Marker: LifecycleMarker.Died or LifecycleMarker.Buried } l:
                flags.Add(DateFlag.Lifecycle);
                notes.Add($"'{CanonicalWriter.LifecycleKeyword(l.Marker)}' gives only the second date of the pair.");
                return (null, Realise(l.Inner, flags, notes, alternatives));
            default:
                return (Realise(e, flags, notes, alternatives), null);
        }
    }

    // ---- node rules ----------------------------------------------------------------------------------------

    /// <summary>The span an expression covers, with no label or intent applied.</summary>
    public RealisedSpan RealiseSpan(DateExpression expression) => Realise(expression, [], [], []);

    private RealisedSpan Realise(DateExpression e, HashSet<DateFlag> flags, List<string> notes, List<Alternative> alternatives)
    {
        switch (e)
        {
            case YearPoint y: return Years(y.Year, y.Year);
            case MonthPoint m: return new(LinkedArtDate.StartOfMonth(m.Year, m.Month), LinkedArtDate.EndOfMonth(m.Year, m.Month));
            case DayPoint d: return new(new LinkedArtDate(d.Year, d.Month, d.Day), new LinkedArtDate(d.Year, d.Month, d.Day).EndOfDay());
            case SeasonPoint s: flags.Add(DateFlag.Season); return SeasonSpan(s, notes);
            case Decade dec: return Years(dec.FirstYear, dec.LastYear);
            case Century c: return CenturySpan(c, notes);
            case Millennium ml: return MillenniumSpan(ml, notes);
            case UnspecifiedDigits u: return Years(Math.Min(u.Minimum, u.Maximum), Math.Max(u.Minimum, u.Maximum));
            case NamedPeriod p:
                if (p.Resolved is null) throw new RealisationException($"The period '{p.Key}' is not known to any period source.");
                flags.Add(DateFlag.NamedPeriod);
                notes.Add($"'{p.Label ?? p.Key}' is taken as {p.Resolved.Canonical}.");
                return Realise(p.Resolved, flags, notes, alternatives);
            case DeepTime dt: flags.Add(DateFlag.DeepTime); return DeepTimeSpan(dt, notes);
            case Undated: throw new RealisationException("An undated expression has no span.");

            case Qualified q:
            {
                var inner = Realise(q.Inner, flags, notes, alternatives);
                var padding = 0;
                if (q.Qualification is Qualification.Approximate or Qualification.ApproximateAndUncertain) { flags.Add(DateFlag.Approximate); padding = Math.Max(padding, _profile.CircaPaddingYears); }
                if (q.Qualification is Qualification.Uncertain or Qualification.ApproximateAndUncertain) { flags.Add(DateFlag.Uncertain); padding = Math.Max(padding, _profile.UncertainPaddingYears); }
                if (padding == 0) return inner;
                flags.Add(DateFlag.Padded);
                notes.Add($"The profile widens hedged dates by {padding} year(s).");
                return inner.Pad(padding, _profile.EmitInnerBounds);
            }
            case Banded b:
            {
                flags.Add(DateFlag.Band);
                var inner = Realise(b.Inner, flags, notes, alternatives);
                return BandSpan(inner, b.Band, b.Inner, notes);
            }
            case Relative r:
            {
                var inner = Realise(r.Inner, flags, notes, alternatives);
                return RelativeSpan(r.Relation, inner, flags, notes);
            }
            case Lifecycle l:
                flags.Add(l.Marker == LifecycleMarker.Floruit ? DateFlag.Floruit : DateFlag.Lifecycle);
                return Realise(l.Inner, flags, notes, alternatives);
            case Interval iv:
            {
                RealisedSpan? start = iv.Start is null ? null : Realise(iv.Start, flags, notes, alternatives);
                RealisedSpan? end = iv.End is null ? null : Realise(iv.End, flags, notes, alternatives);
                if (start is null) flags.Add(DateFlag.OpenStart);
                if (end is null) flags.Add(DateFlag.OpenEnd);
                var s = start?.Start; var en = end?.End;
                if (s is { } sv && en is { } ev && sv > ev)
                {
                    flags.Add(DateFlag.Inverted);
                    notes.Add("The bounds were given in the wrong order and have been swapped.");
                    (s, en) = (end!.Value.Start, start!.Value.End);
                }
                return Clamp(new RealisedSpan(s, en), flags);
            }
            case OneOf set:
            {
                flags.Add(DateFlag.Disjunction);
                var spans = set.Members.Select(m => Realise(m, flags, notes, alternatives)).ToList();
                foreach (var (member, span) in set.Members.Zip(spans))
                    alternatives.Add(new Alternative(member.Canonical, 1.0 / set.Members.Count, "One member of the set", ToTimeSpan(span, member.Canonical)));
                notes.Add("The text offers alternatives; the span covers all of them.");
                return spans.Aggregate(RealisedSpan.Hull);
            }
            case Corrected c: return Realise(Note(c, notes, flags), flags, notes, alternatives);
            default: throw new ArgumentOutOfRangeException(nameof(e), e.GetType().Name);
        }
    }

    private static RealisedSpan Years(long first, long last) => new(LinkedArtDate.StartOfYear(first), LinkedArtDate.EndOfYear(last));

    private RealisedSpan CenturySpan(Century c, List<string> notes)
    {
        long first, last;
        if (c.Era == Era.Bce)
        {
            // nth century BCE = (100n) BCE .. (100n-99) BCE, i.e. astronomical 1-100n .. 100-100n.
            first = 1 - 100L * c.Number;
            last = 100 - 100L * c.Number;
        }
        else if (_profile.CenturyConvention == CenturyConvention.Strict)
        {
            first = 100L * (c.Number - 1) + 1;
            last = 100L * c.Number;
        }
        else
        {
            first = 100L * (c.Number - 1);
            last = 100L * c.Number - 1;
        }
        notes.Add($"Century {c.Number}{(c.Era == Era.Bce ? " BCE" : "")} = {first} to {last} ({_profile.CenturyConvention} convention).");
        return Years(first, last);
    }

    private RealisedSpan MillenniumSpan(Millennium m, List<string> notes)
    {
        long first, last;
        if (m.Era == Era.Bce) { first = 1 - 1000L * m.Number; last = 1000 - 1000L * m.Number; }
        else if (_profile.CenturyConvention == CenturyConvention.Strict) { first = 1000L * (m.Number - 1) + 1; last = 1000L * m.Number; }
        else { first = 1000L * (m.Number - 1); last = 1000L * m.Number - 1; }
        notes.Add($"Millennium {m.Number}{(m.Era == Era.Bce ? " BCE" : "")} = {first} to {last}.");
        return Years(first, last);
    }

    private RealisedSpan SeasonSpan(SeasonPoint s, List<string> notes)
    {
        // Northern meteorological seasons; the south is shifted by six months. Winter runs into the next year.
        var south = _profile.Hemisphere == Hemisphere.South;
        var (startMonth, endMonth) = s.Season switch
        {
            Season.Spring => south ? (9, 11) : (3, 5),
            Season.Summer => south ? (12, 2) : (6, 8),
            Season.Autumn => south ? (3, 5) : (9, 11),
            _ => south ? (6, 8) : (12, 2),
        };
        var endYear = endMonth < startMonth ? s.Year + 1 : s.Year;
        notes.Add($"{s.Season} {CanonicalWriter.Year(s.Year)} = months {startMonth} to {endMonth} ({_profile.Hemisphere}ern hemisphere).");
        return new(LinkedArtDate.StartOfMonth(s.Year, startMonth), LinkedArtDate.EndOfMonth(endYear, endMonth));
    }

    private RealisedSpan DeepTimeSpan(DeepTime dt, List<string> notes)
    {
        var reference = dt.Reference == DeepTimeReference.BeforePresent1950 ? 1950 : _currentYear();
        // Precision from the written value: "4.5" is +-0.05, "4500" is +-0.5, "10" (as "10 ka") is +-0.5 ka.
        var halfUnit = HalfUnit(dt.Value) * DeepTime.Factor(dt.Magnitude);
        var yearsAgo = dt.YearsAgo;
        var first = ClampYear((long)Math.Floor(reference - yearsAgo - halfUnit));
        var last = ClampYear((long)Math.Floor(reference - yearsAgo + halfUnit));
        notes.Add($"{dt.Value} {dt.Magnitude.ToString().ToLowerInvariant()} years {(dt.Reference == DeepTimeReference.BeforePresent1950 ? "before 1950" : "before the present")}, to the precision written: {first} to {last}.");
        return Years(first, last);
    }

    private static decimal HalfUnit(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        var dot = text.IndexOf('.');
        var decimals = dot < 0 ? 0 : text.Length - dot - 1;
        return 0.5m / (decimal)Math.Pow(10, decimals);
    }

    private static long ClampYear(long year) => Math.Clamp(year, -LinkedArtDate.MaxAbsYear, LinkedArtDate.MaxAbsYear);

    private RealisedSpan BandSpan(RealisedSpan inner, Band band, DateExpression innerExpression, List<string> notes)
    {
        if (inner.Start is not { } start || inner.End is not { } end) throw new RealisationException("A band needs a closed span.");
        var firstYear = start.Year; var lastYear = end.Year;
        var years = lastYear - firstYear + 1;

        if (innerExpression is YearPoint)
        {
            // Within a single year, bands are months.
            var (m0, m1) = Positions(band, _profile.YearBands, 12);
            notes.Add($"'{CanonicalWriter.BandKeyword(band)}' of a year = months {m0 + 1} to {m1 + 1}.");
            return new(LinkedArtDate.StartOfMonth(firstYear, m0 + 1), LinkedArtDate.EndOfMonth(firstYear, m1 + 1));
        }
        if (years >= 10)
        {
            var table = years switch { < 100 => _profile.DecadeBands, < 1000 => _profile.CenturyBands, _ => _profile.CenturyBands };
            var (p0, p1) = Positions(band, table, (int)Math.Min(years, int.MaxValue));
            var scale = years / (double)table.Length;
            long y0 = firstYear + (long)Math.Round(p0 * scale, MidpointRounding.ToZero);
            long y1 = firstYear + (long)Math.Round((p1 + 1) * scale, MidpointRounding.ToZero) - 1;
            if (years == table.Length) { y0 = firstYear + p0; y1 = firstYear + p1; }
            notes.Add($"'{CanonicalWriter.BandKeyword(band)}' of {firstYear}-{lastYear} = {y0} to {y1}.");
            return Years(y0, y1);
        }
        // Short spans (months, days, a few years): proportional by days.
        var days0 = LinkedArtDate.DaysFromCivil(start.Year, start.Month, start.Day);
        var days1 = LinkedArtDate.DaysFromCivil(end.Year, end.Month, end.Day);
        var length = days1 - days0 + 1;
        var (f0, f1) = Fractions(band);
        var d0 = days0 + (long)Math.Floor(length * f0);
        var d1 = days0 + (long)Math.Ceiling(length * f1) - 1;
        var (sy, sm, sd) = LinkedArtDate.CivilFromDays(d0);
        var (ey, em, ed) = LinkedArtDate.CivilFromDays(Math.Max(d0, d1));
        return new(new LinkedArtDate(sy, sm, sd), new LinkedArtDate(ey, em, ed).EndOfDay());
    }

    /// <summary>Inclusive positions (0-based) of a band within a span of <paramref name="length"/> units.</summary>
    private static (int Start, int End) Positions(Band band, BandTable table, int length)
    {
        var n = table.Length;
        (int, int) Exact(int parts, int index) => (n * index / parts, n * (index + 1) / parts - 1);
        var (s, e) = band switch
        {
            Band.Early => (table.Early.Start, table.Early.End),
            Band.Mid => (table.Mid.Start, table.Mid.End),
            Band.Late => (table.Late.Start, table.Late.End),
            Band.BeginningOf => (table.BeginningOf.Start, table.BeginningOf.End),
            Band.EndOf => (table.EndOf.Start, table.EndOf.End),
            Band.FirstHalf => Exact(2, 0), Band.SecondHalf => Exact(2, 1),
            Band.FirstQuarter => Exact(4, 0), Band.SecondQuarter => Exact(4, 1), Band.ThirdQuarter => Exact(4, 2), Band.FourthQuarter => Exact(4, 3),
            Band.FirstThird => Exact(3, 0), Band.SecondThird => Exact(3, 1), Band.LastThird => Exact(3, 2),
            _ => throw new ArgumentOutOfRangeException(nameof(band)),
        };
        return (s, e);
    }

    private static (double, double) Fractions(Band band) => band switch
    {
        Band.Early or Band.FirstThird => (0, 1 / 3.0), Band.Mid or Band.SecondThird => (1 / 3.0, 2 / 3.0), Band.Late or Band.LastThird => (2 / 3.0, 1),
        Band.FirstHalf => (0, 0.5), Band.SecondHalf => (0.5, 1),
        Band.FirstQuarter => (0, 0.25), Band.SecondQuarter => (0.25, 0.5), Band.ThirdQuarter => (0.5, 0.75), Band.FourthQuarter => (0.75, 1),
        Band.BeginningOf => (0, 0.1), Band.EndOf => (0.9, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    private RealisedSpan RelativeSpan(Relation relation, RealisedSpan inner, HashSet<DateFlag> flags, List<string> notes)
    {
        switch (relation)
        {
            case Relation.Before:
                flags.Add(DateFlag.OpenStart);
                notes.Add("'before' ends just before the date begins; the start is open.");
                return Clamp(new RealisedSpan(null, inner.Start!.Value.AddSeconds(-1)), flags);
            case Relation.By or Relation.Until or Relation.NotAfter:
                flags.Add(DateFlag.OpenStart);
                notes.Add($"'{CanonicalWriter.RelationKeyword(relation)}' includes the date; the start is open.");
                return Clamp(new RealisedSpan(null, inner.End), flags);
            case Relation.After:
                flags.Add(DateFlag.OpenEnd);
                notes.Add("'after' begins just after the date ends; the end is open.");
                return Clamp(new RealisedSpan(inner.End!.Value.AddSeconds(1), null), flags);
            case Relation.NotBefore:
                flags.Add(DateFlag.OpenEnd);
                notes.Add("'not before' includes the date; the end is open.");
                return Clamp(new RealisedSpan(inner.Start, null), flags);
            default: throw new ArgumentOutOfRangeException(nameof(relation));
        }
    }

    private RealisedSpan Clamp(RealisedSpan span, HashSet<DateFlag> flags)
    {
        var policy = _profile.OpenEnd;
        if (policy.Mode == OpenEndMode.OmitBound) return span;
        var year = policy.Mode == OpenEndMode.ClampToYear ? policy.Year ?? _currentYear() : _currentYear();
        var start = span.Start ?? (span.End is null ? null : LinkedArtDate.StartOfYear(_profile.PlausibleYearMin));
        var end = span.End ?? LinkedArtDate.EndOfYear(year);
        return new RealisedSpan(start, end) { InnerStart = span.InnerStart, InnerEnd = span.InnerEnd };
    }

    // ---- output ----------------------------------------------------------------------------------------------

    private LinkedArtTimeSpan ToTimeSpan(RealisedSpan span, string label)
    {
        var ts = new LinkedArtTimeSpan
        {
            BeginOfTheBegin = span.Start,
            EndOfTheBegin = span.InnerStart,
            BeginOfTheEnd = span.InnerEnd,
            EndOfTheEnd = span.End,
        };
        if (_profile.LabelPolicy is LabelPolicy.Label or LabelPolicy.LabelAndName) ts.Label = label;
        if (_profile.LabelPolicy is LabelPolicy.Name or LabelPolicy.LabelAndName) ts.IdentifiedBy = [new Name(label)];
        return ts;
    }
}

/// <summary>The datetimes an expression covers: outer bounds, and optional inner bounds when a hedge was padded.</summary>
public readonly record struct RealisedSpan(LinkedArtDate? Start, LinkedArtDate? End)
{
    public LinkedArtDate? InnerStart { get; init; }
    public LinkedArtDate? InnerEnd { get; init; }

    public bool IsEmpty => Start is null && End is null;

    public RealisedSpan Pad(int years, bool keepInner)
    {
        var start = Start?.AddYears(-years);
        var end = End?.AddYears(years);
        return new RealisedSpan(start, end) { InnerStart = keepInner ? Start : null, InnerEnd = keepInner ? End : null };
    }

    public static RealisedSpan Hull(RealisedSpan? a, RealisedSpan? b)
    {
        if (a is null) return b ?? default;
        if (b is null) return a.Value;
        return Hull(a.Value, b.Value);
    }

    public static RealisedSpan Hull(RealisedSpan a, RealisedSpan b)
    {
        var start = a.Start is null || b.Start is null ? null : (LinkedArtDate?)(a.Start.Value < b.Start.Value ? a.Start.Value : b.Start.Value);
        var end = a.End is null || b.End is null ? null : (LinkedArtDate?)(a.End.Value > b.End.Value ? a.End.Value : b.End.Value);
        return new RealisedSpan(start, end);
    }
}

/// <summary>An expression that cannot be realised (an unresolved named period, a band on an open interval).</summary>
public sealed class RealisationException(string message) : Exception(message);
