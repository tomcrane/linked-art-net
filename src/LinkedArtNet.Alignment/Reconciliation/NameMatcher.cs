using System.Text.RegularExpressions;
namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>
/// Name comparison for candidate generation and scoring (design doc 10, section 7.2). Everything works on keys from
/// <see cref="StringKey.Normalise"/>: exact first, then the diacritic-folded form, then the same words in another
/// order ("smith, john" and "john smith"), then a surname with initials against the full given names, then word
/// overlap with a bounded edit distance for the rest. The result is a score in 0..1; what counts as acceptable is the
/// reconciler's threshold, not this class's.
/// </summary>
public static class NameMatcher
{
    /// <summary>The lookup keys for a name: its normalised, folded, bare and canonical forms (<see cref="StringKey.Variants"/>), and the inverted or natural order of the words when the name has a comma.</summary>
    public static IReadOnlyList<string> Keys(string name)
    {
        var key = StringKey.Normalise(name);
        if (key.Length == 0) return [];
        var keys = new List<string>(StringKey.Variants(name));
        void Add(string k) { if (k.Length > 0 && !keys.Contains(k, StringComparer.Ordinal)) keys.Add(k); }
        var comma = key.IndexOf(',');
        if (comma > 0 && comma < key.Length - 1)
        {
            // "smith, john" -> "john smith"; a second comma holds a qualifier and is left alone
            var family = key[..comma].Trim();
            var rest = key[(comma + 1)..];
            var second = rest.IndexOf(',');
            var given = (second >= 0 ? rest[..second] : rest).Trim();
            if (given.Length > 0 && family.Length > 0) { Add(given + " " + family); Add(StringKey.Fold(given + " " + family)); }
        }
        else if (comma < 0 && key.Contains(' '))
        {
            // "john smith" -> "smith, john"
            var last = key.LastIndexOf(' ');
            var inverted = key[(last + 1)..] + ", " + key[..last];
            Add(inverted); Add(StringKey.Fold(inverted));
        }
        return keys;
    }

    /// <summary>The best score of the name against any of the labels.</summary>
    public static double Score(string name, IEnumerable<string> labels) => Best(name, labels).Score;

    /// <summary>The best score of the name against any of the labels, and the label that gave it.</summary>
    public static (double Score, string? Label) Best(string name, IEnumerable<string> labels)
    {
        var key = StringKey.Normalise(name);
        if (key.Length == 0) return (0, null);
        var best = 0.0; string? bestLabel = null;
        foreach (var label in labels)
        {
            var s = Score(key, StringKey.Normalise(label));
            if (s > best) { best = s; bestLabel = label; }
            if (best >= 1) break;
        }
        return (best, bestLabel);
    }

    /// <summary>Whether a body's name and one of a record's labels are the same once legal suffixes, a courtesy prefix, connectives and a type qualifier without a comma ("(Firm)") go; abbreviations are not expanded, so "Thos." is still not "Thomas".</summary>
    public static bool SameBody(string name, IEnumerable<string> labels)
    {
        var a = BodyForm(name);
        return a.Length > 0 && labels.Any(l => BodyForm(l) == a);
    }

    /// <summary>LoC's qualifiers that say what kind of body a heading names, which go when a body's name is compared; a place ("(London, England)"), an institution ("(University of Cambridge)") or a vessel ("(Destroyer)") stays. The assistant's own list of LoC's usual type qualifiers.</summary>
    private static readonly HashSet<string> BodyTypes = new(StringComparer.Ordinal)
    {
        "firm", "firms", "publisher", "publishers", "publishing company", "publishing firm", "organization", "organisation", "association", "society",
        "gallery", "galleries", "art gallery", "museum", "company", "agency", "foundation", "trust", "institute", "institution", "press",
        "shop", "store", "studio", "workshop", "manufacturer", "manufacturers", "printer", "printers", "bookseller", "booksellers",
        "auctioneers", "auction house", "dealer", "dealers", "art dealers", "corporation", "partnership",
    };

    /// <summary>Whether a record's own label says it is a body: a type qualifier ("Remondini (Firm)") or a legal suffix ("Macmillan, Inc.").</summary>
    public static bool PlainlyABody(string? label) => label is not null && (WithoutBodyType(label) != label || NameForms.HasLegalSuffix(StringKey.Normalise(label)));

    /// <summary>The label without a parenthetical that names a kind of body ("(Firm)", "(Gallery)"); a place or an institution in parentheses stays.</summary>
    public static string WithoutBodyType(string label) =>
        System.Text.RegularExpressions.Regex.Replace(label, @"\s*\(([^()]*)\)", m => BodyTypes.Contains(m.Groups[1].Value.Trim().ToLowerInvariant()) ? "" : m.Value);

    /// <summary>
    /// Whether a label is a firm named for the man the string names: the string one word ("Dowell", from "Mr Dowell"; "Christie" from
    /// "Messrs Christie") and the label's body form that word in the possessive ("Dowell's Ltd", "Christie's") (finding 105, 2026-10-26,
    /// the desk: the project's own "Dowell's Ltd" never met "Mr Dowell", the keys "dowell" and "dowells" being two).
    /// </summary>
    public static bool PossessiveOf(string name, IEnumerable<string> labels)
    {
        var a = BodyForm(name);
        if (a.Length == 0 || a.Contains(' ')) return false;
        return labels.Any(l => BodyForm(l) == a + "s");
    }

    /// <summary>The bare possessive of a one-word name as a lookup key ("dowells" for "Dowell"), the form a possessive firm name is indexed under once its apostrophe and legal suffix are gone; null for a name of several words.</summary>
    public static string? PossessiveKey(string name)
    {
        var a = BodyForm(name);
        return a.Length == 0 || a.Contains(' ') ? null : a + "s";
    }

    private static string BodyForm(string raw)
    {
        var key = StringKey.Normalise(WithoutBodyType(raw));
        return NameForms.Canonical(Saints(StringKey.Fold(key)), dropQualifiers: false, expandAbbreviations: false);
    }

    // "St." and "Saint", "Ste." and "Sainte", "Mt." and "Mount" are one word in a place or church name ("St. Mary" is LoC's
    // "Saint Mary (Lambeth, England : Parish)"): expanded before the folded forms are compared (finding 25, 2026-10-21)
    private static readonly Regex SaintAbbreviation = new(@"\bst\.?(?=\s|$)", RegexOptions.Compiled);
    private static readonly Regex SainteAbbreviation = new(@"\bste\.?(?=\s|$)", RegexOptions.Compiled);
    private static readonly Regex MountAbbreviation = new(@"\bmt\.?(?=\s|$)", RegexOptions.Compiled);
    private static string Saints(string folded) => MountAbbreviation.Replace(SainteAbbreviation.Replace(SaintAbbreviation.Replace(folded, "saint"), "sainte"), "mount");

    /// <summary>The score of two normalised keys.</summary>
    public static double Score(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1.0;
        var fa = Saints(StringKey.Fold(a)); var fb = Saints(StringKey.Fold(b));
        if (fa == fb) return 0.97;
        // the same letters with punctuation or spacing alone between them ("Watson, J.N.P." and "Watson, J. N. P.", "C Paul" and "C. Paul") are one name (finding 34, 2026-10-22)
        if (StringKey.Bare(fa) == StringKey.Bare(fb)) return a.Contains('&') != b.Contains('&') ? 0.93 : 0.97; // "Adams & Dart" is a firm, "Adams, Dart" a person (finding 73)
        var ca = NameForms.Canonical(fa, dropQualifiers: false); var cb = NameForms.Canonical(fb, dropQualifiers: false);
        if (ca.Length > 0 && ca == cb) return 0.95;
        // a parenthetical right after initials is the fuller form of the name, not a qualifier ("Oppé, A. P. (Adolf Paul)", MARC 100 $q):
        // with the initials expanded ("Carwitham, J. (John)" is "Carwitham, John"; finding 78, 2026-10-23), or without it, the two are the
        // same name at the folded level
        var ea = FullerFormExpanded(fa); var eb = FullerFormExpanded(fb);
        if ((ea != fa || eb != fb) && (ea == eb || StringKey.Bare(ea) == StringKey.Bare(eb))) return 0.97;
        var ua = FullerFormDropped(fa); var ub = FullerFormDropped(fb);
        if ((ua != fa || ub != fb) && (ua == ub || StringKey.Bare(ua) == StringKey.Bare(ub))) return 0.97; // "Jope, E M" and "Jope, E. M. (Edward Martyn)" (finding 57)
        // equal only once parenthesised qualifiers go: in LoC those are the disambiguators ("Smith, John (Clergyman)"), so this is review territory, not acceptance
        var qa = NameForms.Canonical(fa); var qb = NameForms.Canonical(fb);
        if (qa.Length > 0 && qa == qb) return 0.85;
        var ta = Tokens(fa); var tb = Tokens(fb);
        if (ta.Length == 0 || tb.Length == 0) return 0;
        if (SameMultiset(ta, tb)) return 0.93;
        if (InitialsMatch(ta, tb)) return 0.85;
        var overlap = Overlap(ta, tb);
        var edit = EditSimilarity(fa, fb);
        var score = Math.Max(overlap * 0.8, edit >= 0.85 ? edit * 0.9 : 0);
        return Math.Round(score, 3);
    }

    private static string[] Tokens(string key) => key.Split([' ', ',', '.', '-', '\'', '(', ')', '/', ';', ':'], StringSplitOptions.RemoveEmptyEntries);

    private static readonly System.Text.RegularExpressions.Regex FullerForm = new(@"(?<=(?:^|[\s,])[a-z]\.?)\s*\([^()]*\)", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static string FullerFormDropped(string key) => FullerForm.IsMatch(key) ? FullerForm.Replace(key, "").TrimEnd('.', ' ') : key;

    private static readonly System.Text.RegularExpressions.Regex InitialsThenFullerForm = new(@"(?<=^|[\s,])(?<initials>(?:[a-z]\.?\s*)+)\((?<words>[^()]+)\)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>The initials replaced by the fuller form that follows them when each word begins with its initial: "robertson, g. h. (george harcourt), 1943-" is "robertson, george harcourt, 1943-"; anything else is left as it is.</summary>
    private static string FullerFormExpanded(string key)
    {
        var m = InitialsThenFullerForm.Match(key);
        if (!m.Success) return key;
        var initials = m.Groups["initials"].Value.Split([' ', '.'], StringSplitOptions.RemoveEmptyEntries);
        var words = m.Groups["words"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (initials.Length == 0 || initials.Length != words.Length || !initials.Zip(words).All(p => p.Second.StartsWith(p.First, StringComparison.Ordinal))) return key;
        return (key[..m.Index] + string.Join(' ', words) + key[(m.Index + m.Length)..]).Trim();
    }

    /// <summary>Whether the name is an abbreviated form of one of the labels, initials against the words ("Horlor, George W" of "Horlor, George William"; "Carwitham, John" of "Carwitham, J. (John)"): the same name written shorter, not another name (finding 78).</summary>
    public static bool Abbreviates(string name, IEnumerable<string> labels)
    {
        var a = Tokens(Saints(StringKey.Fold(StringKey.Normalise(name))));
        if (a.Length == 0) return false;
        return labels.Any(l =>
        {
            var f = Saints(StringKey.Fold(StringKey.Normalise(l)));
            var expanded = Tokens(FullerFormExpanded(f)); var dropped = Tokens(FullerFormDropped(f));
            return expanded.SequenceEqual(a, StringComparer.Ordinal) || InitialsMatch(a, Tokens(f)) || InitialsMatch(a, expanded) || InitialsMatch(dropped, a);
        });
    }

    private static bool SameMultiset(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        var sa = a.OrderBy(x => x, StringComparer.Ordinal); var sb = b.OrderBy(x => x, StringComparer.Ordinal);
        return sa.SequenceEqual(sb, StringComparer.Ordinal);
    }

    /// <summary>"smith j" or "smith j a" against "smith john" or "smith john arthur": every short token is the other side's token or its initial, in order after the shared surname.</summary>
    private static bool InitialsMatch(string[] a, string[] b)
    {
        var (shorter, longer) = a.Length <= b.Length ? (a, b) : (b, a);
        if (shorter.Length < 2 || !shorter.Any(t => t.Length == 1)) return false;
        // the surname must appear in full on both sides; a side that is all initials has nothing to anchor on
        var surname = shorter.FirstOrDefault(t => t.Length > 1);
        if (surname is null || !longer.Contains(surname, StringComparer.Ordinal)) return false;
        var rest = longer.Where(t => t != surname).ToList();
        foreach (var t in shorter.Where(t => t != surname))
        {
            var i = rest.FindIndex(x => x == t || (t.Length == 1 && x.StartsWith(t, StringComparison.Ordinal)));
            if (i < 0) return false;
            rest.RemoveAt(i);
        }
        return true;
    }

    private static double Overlap(string[] a, string[] b)
    {
        var sa = new HashSet<string>(a, StringComparer.Ordinal); var sb = new HashSet<string>(b, StringComparer.Ordinal);
        var inter = sa.Intersect(sb).Count();
        var union = sa.Union(sb).Count();
        return union == 0 ? 0 : (double)inter / union;
    }

    /// <summary>1 minus the Levenshtein distance over the longer length; 0 for very different lengths without computing.</summary>
    public static double EditSimilarity(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var max = Math.Max(a.Length, b.Length);
        if (Math.Abs(a.Length - b.Length) > max * 0.3 || max > 200) return 0;
        var prev = new int[b.Length + 1]; var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return 1.0 - (double)prev[b.Length] / max;
    }
}
