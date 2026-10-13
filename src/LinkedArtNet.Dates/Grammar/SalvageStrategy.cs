using System.Globalization;
using System.Text.RegularExpressions;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Grammar;

/// <summary>
/// Strategy 4: when nothing else can read the text, pull plausible years out of it ("Not dated (1780's)",
/// "72/127, 2005") and offer their hull at low confidence with the <see cref="DateFlag.Salvaged"/> flag.
/// </summary>
public sealed partial class SalvageStrategy : IParseStrategy
{
    public string Name => "salvage";

    public Candidate? TryParse(string text, ParseProfile profile)
    {
        var years = new List<(long Year, bool Decade)>();
        foreach (Match m in YearPattern().Matches(text))
        {
            var year = long.Parse(m.Groups["y"].Value, CultureInfo.InvariantCulture);
            if (m.Groups["bc"].Success) year = 1 - year;
            if (year < profile.PlausibleYearMin || year > profile.PlausibleYearMax) continue;
            years.Add((year, m.Groups["s"].Success));
        }
        if (years.Count == 0) return null;

        DateExpression Point((long Year, bool Decade) y) => y.Decade && y.Year % 10 == 0 ? new Decade(y.Year) : new YearPoint(y.Year);
        var first = years.MinBy(y => y.Year);
        var last = years.MaxBy(y => y.Year);
        var expression = years.Count == 1 || first.Year == last.Year ? Point(first) : new Interval(Point(first), Point(last));
        var note = years.Count == 1
            ? $"Only the year {years[0].Year} could be read from the text."
            : $"{years.Count} years were found in the text; the span covers them all.";
        return new Candidate(expression, years.Count == 1 ? 0.5 : 0.35, Name, UnconsumedText: text, Notes: [note]);
    }

    [GeneratedRegex(@"(?<!\d)(?<y>\d{3,4})(?<s>'?s)?(?!\d)(?:\s*(?<bc>B\.?C\.?E?\.?)(?![A-Za-z]))?", RegexOptions.IgnoreCase)]
    private static partial Regex YearPattern();
}
