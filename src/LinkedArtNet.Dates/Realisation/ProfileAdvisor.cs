using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LinkedArtNet.Dates.Text;

namespace LinkedArtNet.Dates.Realisation;

/// <summary>
/// Looks at a dataset's date strings before anyone parses them and says what conventions they use, with
/// profile recommendations and a suggested profile. Parsing itself never depends on other strings; this runs
/// beforehand and the owner pins the result as a profile. Counts are of strings containing the feature, not of
/// occurrences. Available as <c>tools/Corpus advise</c>, <c>POST /v1/advise</c> on the service and <c>/dates/advise</c> on the site.
/// </summary>
public static partial class ProfileAdvisor
{
    /// <summary>How many example strings are kept per feature.</summary>
    public const int ExamplesPerFeature = 3;

    public sealed record Report(
        int Strings,
        IReadOnlyDictionary<string, int> Bands,
        IReadOnlyDictionary<string, int> Hedges,
        IReadOnlyDictionary<string, int> Eras,
        IReadOnlyDictionary<string, int> CenturyNotations,
        IReadOnlyDictionary<string, int> Languages,
        int NumericDates, int NumericDayFirstEvidence, int NumericMonthFirstEvidence,
        int TwoDigitYears, int Brackets, int Seasons, int Undated, int OpenEnds, int Decades, int PeriodWords,
        IReadOnlyList<string> Recommendations,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Examples,
        ParseProfile Suggested)
    {
        /// <summary>
        /// The suggested profile as a profile file would state it: <c>inherits: default</c> plus only the settings
        /// that differ from the default, so the reader sees what the data changed. Save it under <c>profiles/</c>
        /// with a name of your own and review it; the advisor reads conventions, it does not know your collection.
        /// </summary>
        public JsonObject SuggestedProfileJson(string name = "suggested")
        {
            var suggested = JsonNode.Parse(Suggested.ToJson())!.AsObject();
            var baseline = JsonNode.Parse(ParseProfile.Default.ToJson())!.AsObject();
            var result = new JsonObject { ["inherits"] = "default", ["name"] = name };
            if (Suggested.Description is not null) result["description"] = Suggested.Description;
            foreach (var (key, value) in suggested)
            {
                if (key is "name" or "description") continue;
                if (baseline.TryGetPropertyValue(key, out var baseValue) && JsonNode.DeepEquals(baseValue, value)) continue;
                result[key] = value?.DeepClone();
            }
            return result;
        }

        /// <summary>True when the data suggested nothing beyond the default profile.</summary>
        public bool SuggestsDefault => SuggestedProfileJson().Count <= 3;

        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Profile advice for {Strings} strings");
            sb.AppendLine();
            void Table(string title, string prefix, IReadOnlyDictionary<string, int> counts)
            {
                if (counts.Count == 0) return;
                sb.AppendLine($"**{title}:** " + string.Join(", ", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}{Sample(prefix + kv.Key)}")));
                sb.AppendLine();
            }
            string Sample(string key) => Examples.TryGetValue(key, out var list) && list.Count > 0 ? $" (e.g. \"{list[0]}\")" : "";
            Table("Band words", "band:", Bands);
            Table("Hedges", "hedge:", Hedges);
            Table("Era words", "era:", Eras);
            Table("Century notations", "century:", CenturyNotations);
            Table("Languages (lexicon hits)", "lang:", Languages);
            sb.AppendLine($"**Shapes:** numeric dates {NumericDates} (day-first evidence {NumericDayFirstEvidence}{Sample("numeric:dayFirst")}, month-first evidence {NumericMonthFirstEvidence}{Sample("numeric:monthFirst")}), " +
                          $"two-digit years {TwoDigitYears}{Sample("twoDigitYears")}, bracketed {Brackets}{Sample("brackets")}, seasons {Seasons}{Sample("seasons")}, undated {Undated}{Sample("undated")}, " +
                          $"open ends {OpenEnds}{Sample("openEnds")}, decades {Decades}{Sample("decades")}, period words {PeriodWords}{Sample("periods")}");
            sb.AppendLine();
            sb.AppendLine("## Recommendations");
            sb.AppendLine();
            foreach (var r in Recommendations) sb.AppendLine("- " + r);
            sb.AppendLine();
            sb.AppendLine("## Suggested profile");
            sb.AppendLine();
            sb.AppendLine(SuggestsDefault
                ? "The default profile; nothing in the data called for a change."
                : "Only the settings that differ from `default` are stated. Save as `profiles/<name>.json`, set `name` to match the file, and review.");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine(SuggestedProfileJson().ToJsonString(ParseProfile.JsonOptions));
            sb.AppendLine("```");
            return sb.ToString();
        }
    }

    /// <summary>The language codes the advisor checks when none are given: every lexicon the library ships.</summary>
    public static IReadOnlyList<string> DefaultLanguages => Lexicon.Codes;

    public static Report Analyse(IEnumerable<string> strings, IEnumerable<string>? languageCodes = null)
    {
        var codes = (languageCodes ?? DefaultLanguages).ToList();
        var lexicons = codes.Select(c => (Code: c, Lexicon: Lexicon.Load(c))).ToList();
        var english = Lexicon.English;
        var bands = new Counter(); var hedges = new Counter(); var eras = new Counter(); var centuries = new Counter(); var languages = new Counter();
        var examples = new Examples();
        int n = 0, numeric = 0, dayFirst = 0, monthFirst = 0, twoDigit = 0, brackets = 0, seasons = 0, undated = 0, openEnds = 0, decades = 0, periods = 0;

        foreach (var raw in strings)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            n++;
            var text = Normaliser.Normalise(raw).Text;
            var lower = text.ToLowerInvariant();
            var words = Words().Matches(lower).Select(m => m.Value).ToList();

            foreach (var w in words.Where(w => w is "early" or "mid" or "middle" or "late" or "later" or "earlier"))
            {
                var key = w == "middle" ? "mid" : w;
                bands.Add(key); examples.Add("band:" + key, raw);
            }
            if (Halves().IsMatch(lower)) { bands.Add("half"); examples.Add("band:half", raw); }
            if (Quarters().IsMatch(lower)) { bands.Add("quarter"); examples.Add("band:quarter", raw); }
            if (Regex.IsMatch(lower, @"\b(beginning|start|end|turn) of\b")) { bands.Add("beginning/end of"); examples.Add("band:beginning/end of", raw); }

            var seasonHit = false; var undatedHit = false; var openEndHit = false;
            var hedgeKeys = new HashSet<string>(StringComparer.Ordinal); var eraKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (code, lexicon) in lexicons)
            {
                var hit = false;
                foreach (var w in words)
                {
                    var key = Lexicon.Normalise(w);
                    if (lexicon.Hedges.ContainsKey(key)) hedgeKeys.Add(key);
                    if (lexicon.Eras.ContainsKey(key)) eraKeys.Add(key);
                    if (lexicon.Seasons.ContainsKey(key)) seasonHit = true;
                    if (lexicon.Undated.Contains(key)) undatedHit = true;
                    if (lexicon.OpenEnd.Contains(key)) openEndHit = true;
                    // A word is evidence of another language only when English does not know it too ("ca.", "circa" and "winter" are
                    // shared); short words count only as hedges or eras ("um"), not as month abbreviations.
                    var evidence = lexicon.Hedges.ContainsKey(key) || lexicon.Eras.ContainsKey(key) || (key.Length > 2 && Knows(lexicon, key));
                    if (code != "en" && evidence && !Knows(english, key)) hit = true;
                }
                if (hit && code != "en") { languages.Add(code); examples.Add("lang:" + code, raw); }
            }
            foreach (var key in hedgeKeys) { hedges.Add(key); examples.Add("hedge:" + key, raw); }
            foreach (var key in eraKeys) { eras.Add(key); examples.Add("era:" + key, raw); }
            if (seasonHit) { seasons++; examples.Add("seasons", raw); }
            if (undatedHit) { undated++; examples.Add("undated", raw); }

            void Century(string kind) { centuries.Add(kind); examples.Add("century:" + kind, raw); }
            if (Regex.IsMatch(lower, @"\b(century|centuries|siècle|jahrhundert|secolo|siglo|eeuw|saec)\b")) Century("word");
            if (Regex.IsMatch(lower, @"\b\d{1,2}(st|nd|rd|th)\s*c\b")) Century("19th c.");
            if (Regex.IsMatch(lower, @"\bc\d{1,2}\b")) Century("C19");
            if (Regex.IsMatch(lower, @"\b\d{2}00s\b")) Century("1800s");
            if (Regex.IsMatch(lower, @"\b[xvi]{2,}(th|e|º)?\b")) Century("roman");

            var numericDate = NumericDate().Match(lower);
            var twoDigitHit = false;
            if (numericDate.Success)
            {
                numeric++; examples.Add("numeric", raw);
                var a = int.Parse(numericDate.Groups["a"].Value); var b = int.Parse(numericDate.Groups["b"].Value);
                if (a > 12 && b <= 12) { dayFirst++; examples.Add("numeric:dayFirst", raw); }
                if (b > 12 && a <= 12) { monthFirst++; examples.Add("numeric:monthFirst", raw); }
                if (numericDate.Groups["y"].Value.Length == 2) twoDigitHit = true;
            }
            // "1843-45" is an abbreviated range end, which the grammar reads without a year window, so only a numeric date
            // with a two-digit year or an apostrophe year ('08) counts here.
            if (Regex.IsMatch(lower, @"'\d{2}\b")) twoDigitHit = true;
            if (twoDigitHit) { twoDigit++; examples.Add("twoDigitYears", raw); }
            if (raw.Contains('[')) { brackets++; examples.Add("brackets", raw); }
            if (Regex.IsMatch(lower, @"\d{3}0'?s\b")) { decades++; examples.Add("decades", raw); }
            if (openEndHit || Regex.IsMatch(lower, @"-\s*$|\b(present|onwards|heute|aujourd'hui|heden|oggi|hoy)\b")) { openEnds++; examples.Add("openEnds", raw); }
            if (Regex.IsMatch(lower, @"\b(period|dynasty|era|reign|dynastie|epoche|periode|periodo)\b")) { periods++; examples.Add("periods", raw); }
        }

        var recommendations = new List<string>();
        var suggested = ParseProfile.Default with { Name = "suggested" };
        var changes = new List<string>();

        var early = bands["early"] + bands["earlier"]; var late = bands["late"] + bands["later"]; var mid = bands["mid"];
        if (early + late >= 10 && mid == 0)
        {
            recommendations.Add($"'early' ({early}) and 'late' ({late}) occur but 'mid' never does: the data may mean halves. Consider centuryBands early 0-49 / late 50-99 and decadeBands early 0-4 / late 5-9.");
            suggested = suggested with
            {
                CenturyBands = new BandTable(100, new(0, 49), new(25, 74), new(50, 99), new(0, 9), new(90, 99)),
                DecadeBands = new BandTable(10, new(0, 4), new(2, 7), new(5, 9), new(0, 0), new(9, 9)),
            };
            changes.Add("early and late read as halves");
        }
        else if (early + late + mid >= 10)
            recommendations.Add($"'early' ({early}), 'mid' ({mid}) and 'late' ({late}) all occur: the default thirds are plausible; check a sample against the source's own definitions (quarters are also common: see profiles/catalogue-quarters.json).");
        if (dayFirst > 0 && monthFirst == 0) recommendations.Add($"Numeric dates are day-first ({dayFirst} strings with a first number over 12): keep dayMonthOrder dayFirst.");
        if (monthFirst > 0 && dayFirst == 0)
        {
            recommendations.Add($"Numeric dates are month-first ({monthFirst} strings with a second number over 12): set dayMonthOrder monthFirst (profiles/archive-us.json).");
            suggested = suggested with { DayMonthOrder = DayMonthOrder.MonthFirst };
            changes.Add("month-first numeric dates");
        }
        if (dayFirst > 0 && monthFirst > 0) recommendations.Add($"Numeric dates are mixed ({dayFirst} day-first, {monthFirst} month-first): the source is inconsistent; parse with reduced confidence and review.");
        if (twoDigit > 0)
        {
            recommendations.Add($"{twoDigit} strings use two-digit years: set twoDigitYearWindow (1900 for library data; profiles/library.json).");
            suggested = suggested with { TwoDigitYearWindow = 1900 };
            changes.Add("two-digit years read within 1900-1999");
        }
        if (eras.Total > 0) recommendations.Add($"Era words occur ({eras.Total} strings): BCE dates are present; check that bare small numbers are not years without an era (minYearDigits).");
        var threshold = Math.Max(3, n / 50);
        var extraLanguages = languages.Items.Where(kv => kv.Value >= threshold).OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
        foreach (var code in extraLanguages)
            recommendations.Add($"Lexicon hits for '{code}' in {languages[code]} strings: add \"{code}\" to languages (profiles/multilingual.json has them all).");
        if (extraLanguages.Count > 0)
        {
            suggested = suggested with { Languages = ["en", .. extraLanguages] };
            changes.Add("lexicons " + string.Join(", ", extraLanguages));
        }
        if (centuries["roman"] >= threshold && extraLanguages.Contains("la"))
            recommendations.Add($"Roman-numeral centuries ({centuries["roman"]}) with Latin: manuscript cataloguing conventions; see profiles/medieval-manuscripts.json (strict centuries, 1301-1400).");
        if (brackets > n / 4) recommendations.Add($"{brackets} strings are bracketed: MARC-style inferred dates; the Bracketed flag will be common, and '[17_]' patterns are read as unspecified digits.");
        if (seasons > 0) recommendations.Add($"{seasons} strings name a season: decide hemisphere and whether 'Winter 1776' is Dec-Feb or Jan-Mar (winter policy; profiles/archive-us.json).");
        if (periods > n / 10) recommendations.Add($"{periods} strings name periods, dynasties or reigns: make sure the needed period tables are loaded (JsonNamedPeriodSource).");
        if (recommendations.Count == 0) recommendations.Add("Nothing stands out; the default profile should serve. Run the corpus tooling on a sample to confirm.");

        suggested = suggested with
        {
            Description = changes.Count == 0
                ? $"Suggested by the profile advisor from {n} strings: nothing differed from the default. Review before use."
                : $"Suggested by the profile advisor from {n} strings: {string.Join("; ", changes)}. Review before use.",
        };

        return new Report(n, bands.Items, hedges.Items, eras.Items, centuries.Items, languages.Items, numeric, dayFirst, monthFirst, twoDigit, brackets, seasons, undated, openEnds, decades, periods,
            recommendations, examples.Items, suggested);
    }

    /// <summary>Whether a lexicon has the word as a hedge, era, month, century word, band word or season.</summary>
    private static bool Knows(Lexicon l, string key) =>
        l.Hedges.ContainsKey(key) || l.Eras.ContainsKey(key) || l.Months.ContainsKey(key) || l.CenturyWords.Contains(key) || l.Bands.ContainsKey(key) || l.Seasons.ContainsKey(key);

    private sealed class Counter
    {
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
        public void Add(string key) => _counts[key] = this[key] + 1;
        public int this[string key] => _counts.GetValueOrDefault(key);
        public int Total => _counts.Values.Sum();
        public IReadOnlyDictionary<string, int> Items => _counts;
    }

    /// <summary>Up to <see cref="ExamplesPerFeature"/> distinct strings per feature, in first-seen order.</summary>
    private sealed class Examples
    {
        private readonly Dictionary<string, List<string>> _lists = new(StringComparer.Ordinal);
        public void Add(string key, string example)
        {
            if (!_lists.TryGetValue(key, out var list)) _lists[key] = list = [];
            var trimmed = example.Trim();
            if (list.Count < ExamplesPerFeature && !list.Contains(trimmed, StringComparer.Ordinal)) list.Add(trimmed);
        }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Items => _lists.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value, StringComparer.Ordinal);
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
