using System.Text;
using System.Text.RegularExpressions;
using LinkedArtNet.Dates.Text;

namespace LinkedArtNet.Dates.Realisation;

/// <summary>
/// Looks at a dataset's date strings before anyone parses them and says what conventions they use, with
/// profile recommendations. Parsing itself never depends on other strings; this runs beforehand and the owner
/// pins the result as a profile. Counts are of strings containing the feature, not of occurrences.
/// </summary>
public static partial class ProfileAdvisor
{
    public sealed record Report(
        int Strings,
        IReadOnlyDictionary<string, int> Bands,
        IReadOnlyDictionary<string, int> Hedges,
        IReadOnlyDictionary<string, int> Eras,
        IReadOnlyDictionary<string, int> CenturyNotations,
        IReadOnlyDictionary<string, int> Languages,
        int NumericDates, int NumericDayFirstEvidence, int NumericMonthFirstEvidence,
        int TwoDigitYears, int Brackets, int Seasons, int Undated, int OpenEnds, int Decades, int PeriodWords,
        IReadOnlyList<string> Recommendations)
    {
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Profile advice for {Strings} strings");
            sb.AppendLine();
            void Table(string title, IReadOnlyDictionary<string, int> counts)
            {
                if (counts.Count == 0) return;
                sb.AppendLine($"**{title}:** " + string.Join(", ", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
                sb.AppendLine();
            }
            Table("Band words", Bands);
            Table("Hedges", Hedges);
            Table("Era words", Eras);
            Table("Century notations", CenturyNotations);
            Table("Languages (lexicon hits)", Languages);
            sb.AppendLine($"**Shapes:** numeric dates {NumericDates} (day-first evidence {NumericDayFirstEvidence}, month-first evidence {NumericMonthFirstEvidence}), " +
                          $"two-digit years {TwoDigitYears}, bracketed {Brackets}, seasons {Seasons}, undated {Undated}, open ends {OpenEnds}, decades {Decades}, period words {PeriodWords}");
            sb.AppendLine();
            sb.AppendLine("## Recommendations");
            sb.AppendLine();
            foreach (var r in Recommendations) sb.AppendLine("- " + r);
            return sb.ToString();
        }
    }

    public static Report Analyse(IEnumerable<string> strings, IEnumerable<string>? languageCodes = null)
    {
        var codes = (languageCodes ?? ["en", "fr", "de", "it", "es", "nl", "la"]).ToList();
        var lexicons = codes.Select(c => (Code: c, Lexicon: Lexicon.Load(c))).ToList();
        var bands = new Counter(); var hedges = new Counter(); var eras = new Counter(); var centuries = new Counter(); var languages = new Counter();
        int n = 0, numeric = 0, dayFirst = 0, monthFirst = 0, twoDigit = 0, brackets = 0, seasons = 0, undated = 0, openEnds = 0, decades = 0, periods = 0;

        foreach (var raw in strings)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            n++;
            var text = Normaliser.Normalise(raw).Text;
            var lower = text.ToLowerInvariant();
            var words = Words().Matches(lower).Select(m => m.Value).ToList();

            foreach (var w in words.Where(w => w is "early" or "mid" or "middle" or "late" or "later" or "earlier")) bands.Add(w == "middle" ? "mid" : w);
            if (Halves().IsMatch(lower)) bands.Add("half");
            if (Quarters().IsMatch(lower)) bands.Add("quarter");
            if (Regex.IsMatch(lower, @"\b(beginning|start|end|turn) of\b")) bands.Add("beginning/end of");

            foreach (var (code, lexicon) in lexicons)
            {
                var hit = false;
                foreach (var w in words)
                {
                    var key = Lexicon.Normalise(w);
                    if (lexicon.Hedges.ContainsKey(key)) { hedges.Add(key); hit = true; }
                    if (lexicon.Eras.ContainsKey(key)) { eras.Add(key); hit = true; }
                    if (code != "en" && (lexicon.Months.ContainsKey(key) || lexicon.CenturyWords.Contains(key) || lexicon.Bands.ContainsKey(key) || lexicon.Seasons.ContainsKey(key)) && key.Length > 2) hit = true;
                    if (lexicon.Seasons.ContainsKey(key)) seasons++;
                    if (lexicon.Undated.Contains(key)) undated++;
                    if (lexicon.OpenEnd.Contains(key)) openEnds++;
                }
                if (hit && code != "en") languages.Add(code);
            }
            if (Regex.IsMatch(lower, @"\b(century|centuries|siècle|jahrhundert|secolo|siglo|eeuw|saec)\b")) centuries.Add("word");
            if (Regex.IsMatch(lower, @"\b\d{1,2}(st|nd|rd|th)\s*c\b")) centuries.Add("19th c.");
            if (Regex.IsMatch(lower, @"\bc\d{1,2}\b")) centuries.Add("C19");
            if (Regex.IsMatch(lower, @"\b\d{2}00s\b")) centuries.Add("1800s");
            if (Regex.IsMatch(lower, @"\b[xvi]{2,}(th|e|º)?\b")) centuries.Add("roman");

            var numericDate = NumericDate().Match(lower);
            if (numericDate.Success)
            {
                numeric++;
                var a = int.Parse(numericDate.Groups["a"].Value); var b = int.Parse(numericDate.Groups["b"].Value);
                if (a > 12 && b <= 12) dayFirst++;
                if (b > 12 && a <= 12) monthFirst++;
                if (numericDate.Groups["y"].Value.Length == 2) twoDigit++;
            }
            if (Regex.IsMatch(lower, @"\b\d{4}\s*-\s*\d{2}\b(?!\d)") || Regex.IsMatch(lower, @"'\d{2}\b")) twoDigit++;
            if (raw.Contains('[')) brackets++;
            if (Regex.IsMatch(lower, @"\d{3}0'?s\b")) decades++;
            if (Regex.IsMatch(lower, @"-\s*$|\b(present|onwards|heute|aujourd'hui|heden|oggi|hoy)\b")) openEnds++;
            if (Regex.IsMatch(lower, @"\b(period|dynasty|era|reign|dynastie|epoche|periode|periodo)\b")) periods++;
        }

        var recommendations = new List<string>();
        var early = bands["early"] + bands["earlier"]; var late = bands["late"] + bands["later"]; var mid = bands["mid"];
        if (early + late >= 10 && mid == 0)
            recommendations.Add($"'early' ({early}) and 'late' ({late}) occur but 'mid' never does: the data may mean halves. Consider centuryBands early 0-49 / late 50-99 and decadeBands early 0-4 / late 5-9.");
        else if (early + late + mid >= 10)
            recommendations.Add($"'early' ({early}), 'mid' ({mid}) and 'late' ({late}) all occur: the default thirds are plausible; check a sample against the source's own definitions (quarters are also common: see profiles/catalogue-quarters.json).");
        if (dayFirst > 0 && monthFirst == 0) recommendations.Add($"Numeric dates are day-first ({dayFirst} strings with a first number over 12): keep dayMonthOrder dayFirst.");
        if (monthFirst > 0 && dayFirst == 0) recommendations.Add($"Numeric dates are month-first ({monthFirst} strings with a second number over 12): set dayMonthOrder monthFirst (profiles/archive-us.json).");
        if (dayFirst > 0 && monthFirst > 0) recommendations.Add($"Numeric dates are mixed ({dayFirst} day-first, {monthFirst} month-first): the source is inconsistent; parse with reduced confidence and review.");
        if (twoDigit > 0) recommendations.Add($"{twoDigit} strings use two-digit years: set twoDigitYearWindow (1900 for library data; profiles/library.json).");
        if (eras.Total > 0) recommendations.Add($"Era words occur ({eras.Total} strings): BCE dates are present; check that bare small numbers are not years without an era (minYearDigits).");
        foreach (var (code, count) in languages.Items.OrderByDescending(kv => kv.Value))
            if (count >= Math.Max(3, n / 50)) recommendations.Add($"Lexicon hits for '{code}' in {count} strings: add \"{code}\" to languages (profiles/multilingual.json has them all).");
        if (brackets > n / 4) recommendations.Add($"{brackets} strings are bracketed: MARC-style inferred dates; the Bracketed flag will be common, and '[17_]' patterns are read as unspecified digits.");
        if (seasons > 0) recommendations.Add($"{seasons} strings name a season: decide hemisphere and whether 'Winter 1776' is Dec-Feb or Jan-Mar (winter policy; profiles/archive-us.json).");
        if (periods > n / 10) recommendations.Add($"{periods} strings name periods, dynasties or reigns: make sure the needed period tables are loaded (JsonNamedPeriodSource).");
        if (recommendations.Count == 0) recommendations.Add("Nothing stands out; the default profile should serve. Run the corpus tooling on a sample to confirm.");

        return new Report(n, bands.Items, hedges.Items, eras.Items, centuries.Items, languages.Items, numeric, dayFirst, monthFirst, twoDigit, brackets, seasons, undated, openEnds, decades, periods, recommendations);
    }

    private sealed class Counter
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
        public void Add(string key) => _counts[key] = this[key] + 1;
        public int this[string key] => _counts.GetValueOrDefault(key);
        public int Total => _counts.Values.Sum();
        public IReadOnlyDictionary<string, int> Items => _counts;
    }

    [GeneratedRegex(@"[\p{L}][\p{L}'\.]*")]
    private static partial Regex Words();

    [GeneratedRegex(@"\b(first|second|latter|1st|2nd|première|seconde|erste|zweite|prima|seconda|primera|segunda|eerste|tweede)\s+(half|moitié|hälfte|metà|mitad|helft)\b")]
    private static partial Regex Halves();

    [GeneratedRegex(@"\b(quarter|quart|viertel|quarto|cuarto|kwart)\b")]
    private static partial Regex Quarters();

    [GeneratedRegex(@"(?<!\d)(?<a>\d{1,2})[/.-](?<b>\d{1,2})[/.-](?<y>\d{2}|\d{4})(?!\d)")]
    private static partial Regex NumericDate();
}
