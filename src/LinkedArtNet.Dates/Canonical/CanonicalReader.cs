using System.Globalization;
using System.Text.RegularExpressions;

namespace LinkedArtNet.Dates.Canonical;

/// <summary>
/// Parses canonical expressions (and so strict ISO 8601 dates and EDTF Level 0-2 years, months, days, seasons,
/// qualifiers, unspecified digits, intervals and sets) into <see cref="DateExpression"/>s. This is strategy 1 of the
/// parser and the form an LLM interpreter or a human reviewer is asked to produce. Grammar: docs/canonical-expression.md.
/// </summary>
public static partial class CanonicalReader
{
    public static DateExpression Parse(string canonical)
    {
        if (!TryParse(canonical, out var expression, out var error)) throw new FormatException(error);
        return expression!;
    }

    public static bool TryParse(string canonical, out DateExpression? expression) => TryParse(canonical, out expression, out _);

    public static bool TryParse(string canonical, out DateExpression? expression, out string? error)
    {
        expression = null;
        error = null;
        if (string.IsNullOrWhiteSpace(canonical)) { error = "Empty expression."; return false; }
        try
        {
            var text = canonical.Trim();
            expression = ParseExpression(text);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            error = ex.Message;
            return false;
        }
    }

    // expression := 'undated' | lifecycle expression | set | interval | prefixed
    private static DateExpression ParseExpression(string text)
    {
        if (text == "undated") return Undated.Instance;
        // A leading lifecycle keyword marks the whole expression ("fl 1480/1510"), unless both bounds of an
        // interval are marked ("born 1850/died 1920"), which is a pair with explicit halves.
        var space = text.IndexOf(' ');
        if (space > 0 && Keywords.Lifecycle.TryGetValue(text[..space], out var marker) && !HasMarkedBounds(text))
            return new Lifecycle(marker, ParseExpression(text[(space + 1)..].TrimStart()));
        if (text.StartsWith('[') && text.EndsWith(']'))
            return new OneOf(SplitTopLevel(text[1..^1], ',').Select(ParseExpression));

        var slash = IndexOfTopLevel(text, '/');
        if (slash >= 0)
        {
            var left = text[..slash].Trim();
            var right = text[(slash + 1)..].Trim();
            return new Interval(left == ".." ? null : ParseBound(left), right == ".." ? null : ParseBound(right));
        }
        return ParsePrefixed(text);
    }

    private static bool HasMarkedBounds(string text)
    {
        var slash = IndexOfTopLevel(text, '/');
        if (slash < 0) return false;
        var right = text[(slash + 1)..].TrimStart();
        var space = right.IndexOf(' ');
        return space > 0 && Keywords.Lifecycle.ContainsKey(right[..space]);
    }

    // bound := [lifecycle] prefixed       ("born 1850/died 1920" is an explicit pair)
    private static DateExpression ParseBound(string text)
    {
        var space = text.IndexOf(' ');
        if (space > 0 && Keywords.Lifecycle.TryGetValue(text[..space], out var marker))
            return new Lifecycle(marker, ParseBound(text[(space + 1)..].TrimStart()));
        var bound = ParsePrefixed(text);
        return bound is Interval or OneOf or Undated or Relative ? throw new FormatException($"'{text}' cannot be an interval bound.") : bound;
    }

    // prefixed := (lifecycle | relation | band)* single
    private static DateExpression ParsePrefixed(string text)
    {
        var space = text.IndexOf(' ');
        if (space > 0)
        {
            var keyword = text[..space];
            var rest = text[(space + 1)..].TrimStart();
            if (Keywords.Lifecycle.TryGetValue(keyword, out var marker)) return new Lifecycle(marker, ParseExpression(rest));
            if (Keywords.Relation.TryGetValue(keyword, out var relation)) return new Relative(relation, ParsePrefixed(rest));
            if (Keywords.Band.TryGetValue(keyword, out var band))
            {
                // "late 195X~" hedges the whole band, so the qualifier moves outside it.
                var inner = ParsePrefixed(rest);
                return inner is Qualified q ? new Qualified(new Banded(q.Inner, band), q.Qualification) : new Banded(inner, band);
            }
            if (keyword is "century" or "millennium")
            {
                var (number, qualification) = ParseOrdinal(rest);
                var era = rest.StartsWith('-') ? Era.Bce : Era.Ce;
                DateExpression point = keyword == "century" ? new Century(Math.Abs(number), era) : new Millennium(Math.Abs(number), era);
                return qualification is { } qq ? new Qualified(point, qq) : point;
            }
        }
        return ParseSingle(text);
    }

    private static (int Number, Qualification? Qualification) ParseOrdinal(string text)
    {
        Qualification? q = text.Length > 0 && text[^1] is '~' or '?' or '%'
            ? text[^1] switch { '~' => Qualification.Approximate, '?' => Qualification.Uncertain, _ => Qualification.ApproximateAndUncertain }
            : null;
        return (ParseSmallInt(q is null ? text : text[..^1]), q);
    }

    // single := point ['~' | '?' | '%']
    private static DateExpression ParseSingle(string text)
    {
        if (text.Length == 0) throw new FormatException("Missing date.");
        var last = text[^1];
        if (last is '~' or '?' or '%')
        {
            var inner = ParseSingle(text[..^1]);
            var q = last switch { '~' => Qualification.Approximate, '?' => Qualification.Uncertain, _ => Qualification.ApproximateAndUncertain };
            return new Qualified(inner, q);
        }
        return ParsePoint(text);
    }

    private static DatePoint ParsePoint(string text)
    {
        if (text.StartsWith("period:", StringComparison.Ordinal)) return new NamedPeriod(text["period:".Length..]);

        var deep = DeepTimeRegex().Match(text);
        if (deep.Success)
        {
            var magnitude = deep.Groups["unit"].Value switch { "years" => Magnitude.Years, "ka" => Magnitude.Thousand, "Ma" => Magnitude.Million, "Ga" => Magnitude.Billion, _ => throw new FormatException(text) };
            var reference = deep.Groups["ref"].Value == "BP" ? DeepTimeReference.BeforePresent1950 : DeepTimeReference.Present;
            return new DeepTime(decimal.Parse(deep.Groups["value"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture), magnitude, reference);
        }

        var m = DateRegex().Match(text);
        if (!m.Success) throw new FormatException($"'{text}' is not a canonical date.");

        var yearText = m.Groups["year"].Value;
        if (yearText.Contains('X'))
        {
            if (m.Groups["month"].Success) throw new FormatException($"'{text}': unspecified digits are supported in the year only.");
            var pattern = yearText.StartsWith('Y') ? yearText[1..] : yearText;
            var digits = pattern.TrimStart('-');
            return digits.Length >= 4 && digits.EndsWith('X') && !digits[..^1].Contains('X')
                ? new Decade(long.Parse(pattern.Replace('X', '0'), CultureInfo.InvariantCulture))
                : new UnspecifiedDigits(pattern);
        }

        var year = ParseYear(yearText);
        if (!m.Groups["month"].Success) return new YearPoint(year);
        var month = int.Parse(m.Groups["month"].Value, CultureInfo.InvariantCulture);
        if (month is >= 21 and <= 24)
        {
            if (m.Groups["day"].Success) throw new FormatException($"'{text}': a season has no day.");
            return new SeasonPoint(year, (Season)month);
        }
        if (month is < 1 or > 12) throw new FormatException($"'{text}': month {month} is out of range.");
        if (!m.Groups["day"].Success) return new MonthPoint(year, month);
        return new DayPoint(year, month, int.Parse(m.Groups["day"].Value, CultureInfo.InvariantCulture));
    }

    private static long ParseYear(string text)
    {
        var digits = text.StartsWith('Y') ? text[1..] : text;
        var abs = digits.TrimStart('-');
        if (text.StartsWith('Y') ? abs.Length < 5 : abs.Length != 4)
            throw new FormatException($"'{text}': years are four digits, or 'Y' followed by five or more.");
        var value = long.Parse(abs, CultureInfo.InvariantCulture);
        if (value > LinkedArtDate.MaxAbsYear) throw new FormatException($"'{text}': year beyond the supported range.");
        return digits.StartsWith('-') ? -value : value;
    }

    private static int ParseSmallInt(string text)
    {
        if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) || n == 0)
            throw new FormatException($"'{text}' is not an ordinal number.");
        return n;
    }

    private static int IndexOfTopLevel(string text, char c)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']') depth--;
            else if (text[i] == c && depth == 0) return i;
        }
        return -1;
    }

    private static IEnumerable<string> SplitTopLevel(string text, char separator)
    {
        var depth = 0; var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']') depth--;
            else if (text[i] == separator && depth == 0) { yield return text[start..i].Trim(); start = i + 1; }
        }
        yield return text[start..].Trim();
    }

    [GeneratedRegex(@"^(?<year>Y?-?[0-9X]+)(?:-(?<month>[0-9]{2})(?:-(?<day>[0-9]{2}))?)?$")]
    private static partial Regex DateRegex();

    [GeneratedRegex(@"^(?<value>[0-9]+(?:\.[0-9]+)?) (?<unit>years|ka|Ma|Ga) (?<ref>ago|BP)$")]
    private static partial Regex DeepTimeRegex();

    internal static class Keywords
    {
        public static readonly Dictionary<string, Band> Band = Enum.GetValues<Dates.Band>().ToDictionary(CanonicalWriter.BandKeyword, b => b, StringComparer.Ordinal);
        public static readonly Dictionary<string, Relation> Relation = Enum.GetValues<Dates.Relation>().ToDictionary(CanonicalWriter.RelationKeyword, r => r, StringComparer.Ordinal);
        public static readonly Dictionary<string, LifecycleMarker> Lifecycle = Enum.GetValues<LifecycleMarker>().ToDictionary(CanonicalWriter.LifecycleKeyword, l => l, StringComparer.Ordinal);
    }
}
