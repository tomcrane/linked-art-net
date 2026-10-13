using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Canonical;

/// <summary>
/// Strict EDTF (Level 2) for a result, for systems that speak EDTF but not our keyword extensions. Nodes EDTF can
/// express keep their canonical text. Anything else (bands, centuries, relations, lifecycle markers, named periods,
/// deep time) becomes the realised years: a single node becomes <c>first/last</c>, an interval bound becomes the
/// year at that end (keeping a <c>~</c>/<c>?</c> qualifier), an open bound <c>..</c>.
/// </summary>
public static class EdtfWriter
{
    /// <param name="realise">Realises any sub-expression; the realiser supplies this.</param>
    public static string? Write(DateExpression expression, Func<DateExpression, RealisedSpan> realise)
    {
        switch (expression)
        {
            case var e when IsPureEdtf(e):
                return e.Canonical;
            case Corrected c:
                return Write(c.Correction, realise);
            case Lifecycle l:
                return Write(l.Inner, realise);
            case Interval i:
                return $"{Bound(i.Start, true, realise)}/{Bound(i.End, false, realise)}";
            case OneOf set:
            {
                // EDTF sets hold dates, not our extensions, so a set with an extension member becomes its hull.
                var span = realise(set);
                return span.IsEmpty ? null : $"{Year(span.Start)}/{Year(span.End)}";
            }
            case Relative r:
            {
                var span = realise(r);
                return span.IsEmpty ? null : $"{Year(span.Start)}/{Year(span.End)}";
            }
            default:
            {
                var span = realise(expression);
                if (span.IsEmpty) return null;
                var first = Year(span.Start);
                var last = Year(span.End);
                return first == last ? first : $"{first}/{last}";
            }
        }
    }

    private static string Bound(DateExpression? bound, bool isStart, Func<DateExpression, RealisedSpan> realise)
    {
        if (bound is null) return "..";
        if (IsPureEdtf(bound)) return bound.Canonical;
        var suffix = "";
        var core = bound;
        if (bound is Qualified q)
        {
            suffix = q.Qualification switch { Qualification.Approximate => "~", Qualification.Uncertain => "?", _ => "%" };
            core = q.Inner;
        }
        var span = realise(core);
        return (isStart ? Year(span.Start) : Year(span.End)) + suffix;
    }

    private static string Year(LinkedArtDate? date) => date is { } d ? CanonicalWriter.Year(d.Year) : "..";

    public static bool IsPureEdtf(DateExpression e) => e switch
    {
        YearPoint or MonthPoint or DayPoint or SeasonPoint or Decade or UnspecifiedDigits => true,
        Qualified q => IsPureEdtf(q.Inner),
        Interval i => (i.Start is null || IsPureEdtf(i.Start)) && (i.End is null || IsPureEdtf(i.End)),
        OneOf set => set.Members.All(IsPureEdtf),
        Corrected c => IsPureEdtf(c.Correction),
        _ => false,
    };
}
