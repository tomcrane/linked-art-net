using System.Globalization;
using System.Text;

namespace LinkedArtNet.Dates.Canonical;

/// <summary>
/// Writes the canonical text of a <see cref="DateExpression"/>: EDTF Level 0-2 wherever EDTF can express the node,
/// lower-case keyword prefixes where it cannot (<c>late 195X</c>, <c>century 19</c>, <c>before 1850</c>,
/// <c>born 1606</c>, <c>fl 1480/1510</c>, <c>period:edo</c>, <c>4.5 Ga ago</c>, <c>undated</c>).
/// The grammar is in docs/canonical-expression.md; <see cref="CanonicalReader"/> parses it back.
/// </summary>
public static class CanonicalWriter
{
    public static string Write(DateExpression expression)
    {
        var sb = new StringBuilder();
        Append(sb, expression);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, DateExpression e)
    {
        switch (e)
        {
            case YearPoint y: sb.Append(Year(y.Year)); break;
            case MonthPoint m: sb.Append(Year(m.Year)).Append('-').Append(m.Month.ToString("00", CultureInfo.InvariantCulture)); break;
            case DayPoint d: sb.Append(Year(d.Year)).Append('-').Append(d.Month.ToString("00", CultureInfo.InvariantCulture)).Append('-').Append(d.Day.ToString("00", CultureInfo.InvariantCulture)); break;
            case SeasonPoint s: sb.Append(Year(s.Year)).Append('-').Append((int)s.Season); break;
            case Decade dec: sb.Append(Year(dec.Base)[..^1]).Append('X'); break;
            case Century c: sb.Append("century ").Append(c.Era == Era.Bce ? "-" : "").Append(c.Number); break;
            case Millennium ml: sb.Append("millennium ").Append(ml.Era == Era.Bce ? "-" : "").Append(ml.Number); break;
            case UnspecifiedDigits u: sb.Append(u.Pattern); break;
            case NamedPeriod p: sb.Append("period:").Append(p.Key); break;
            case DeepTime dt:
                sb.Append(dt.Value.ToString(CultureInfo.InvariantCulture)).Append(' ')
                  .Append(dt.Magnitude switch { Magnitude.Years => "years", Magnitude.Thousand => "ka", Magnitude.Million => "Ma", _ => "Ga" })
                  .Append(dt.Reference == DeepTimeReference.BeforePresent1950 ? " BP" : " ago");
                break;
            case Undated: sb.Append("undated"); break;

            case Qualified q:
                Append(sb, q.Inner);
                sb.Append(q.Qualification switch { Qualification.Approximate => '~', Qualification.Uncertain => '?', _ => '%' });
                break;
            case Banded b: sb.Append(BandKeyword(b.Band)).Append(' '); Append(sb, b.Inner); break;
            case Relative r: sb.Append(RelationKeyword(r.Relation)).Append(' '); Append(sb, r.Inner); break;
            case Lifecycle l: sb.Append(LifecycleKeyword(l.Marker)).Append(' '); Append(sb, l.Inner); break;
            case Interval i:
                if (i.Start is null) sb.Append(".."); else Append(sb, i.Start);
                sb.Append('/');
                if (i.End is null) sb.Append(".."); else Append(sb, i.End);
                break;
            case OneOf set:
                sb.Append('[');
                for (var k = 0; k < set.Members.Count; k++)
                {
                    if (k > 0) sb.Append(',');
                    Append(sb, set.Members[k]);
                }
                sb.Append(']');
                break;
            case Corrected c: Append(sb, c.Correction); break;
            default: throw new ArgumentOutOfRangeException(nameof(e), e.GetType().Name);
        }
    }

    /// <summary>EDTF year: at least four digits, <c>-</c> for BCE (astronomical), <c>Y</c> prefix beyond four digits.</summary>
    public static string Year(long year)
    {
        var abs = Math.Abs(year);
        var digits = abs.ToString(CultureInfo.InvariantCulture);
        if (digits.Length > 4) return (year < 0 ? "Y-" : "Y") + digits;
        return (year < 0 ? "-" : "") + digits.PadLeft(4, '0');
    }

    public static string BandKeyword(Band band) => band switch
    {
        Band.Early => "early", Band.Mid => "mid", Band.Late => "late",
        Band.FirstHalf => "h1", Band.SecondHalf => "h2",
        Band.FirstQuarter => "q1", Band.SecondQuarter => "q2", Band.ThirdQuarter => "q3", Band.FourthQuarter => "q4",
        Band.FirstThird => "t1", Band.SecondThird => "t2", Band.LastThird => "t3",
        Band.BeginningOf => "beginning", Band.EndOf => "end",
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    public static string RelationKeyword(Relation relation) => relation switch
    {
        Relation.Before => "before", Relation.After => "after", Relation.By => "by", Relation.Until => "until",
        Relation.NotBefore => "not-before", Relation.NotAfter => "not-after",
        _ => throw new ArgumentOutOfRangeException(nameof(relation)),
    };

    public static string LifecycleKeyword(LifecycleMarker marker) => marker switch
    {
        LifecycleMarker.Born => "born", LifecycleMarker.Died => "died", LifecycleMarker.Baptised => "baptised",
        LifecycleMarker.Buried => "buried", LifecycleMarker.Floruit => "fl",
        _ => throw new ArgumentOutOfRangeException(nameof(marker)),
    };
}
