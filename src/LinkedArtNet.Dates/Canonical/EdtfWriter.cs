using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Canonical;

/// <summary>
/// Strict EDTF (Level 2) for a result. Nodes EDTF can express keep their canonical text; keyword extensions
/// (bands, centuries, relations, lifecycle markers, named periods, deep time) become the interval of realised years,
/// with <c>..</c> for an open bound. Useful for systems that speak EDTF but not our extensions.
/// </summary>
public static class EdtfWriter
{
    public static string? Write(DateExpression expression, RealisedSpan span)
    {
        if (IsPureEdtf(expression)) return expression.Canonical;
        if (span.IsEmpty) return null;
        var start = span.Start is { } s ? CanonicalWriter.Year(s.Year) : "..";
        var end = span.End is { } e ? CanonicalWriter.Year(e.Year) : "..";
        return start == end ? start : $"{start}/{end}";
    }

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
