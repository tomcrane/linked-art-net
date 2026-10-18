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
    /// <summary>The lookup keys for a name: its normalised form, the folded form, and the inverted or natural order of the words when the name has a comma.</summary>
    public static IReadOnlyList<string> Keys(string name)
    {
        var key = StringKey.Normalise(name);
        if (key.Length == 0) return [];
        var keys = new List<string> { key };
        void Add(string k) { if (k.Length > 0 && !keys.Contains(k, StringComparer.Ordinal)) keys.Add(k); }
        Add(StringKey.Fold(key));
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
    public static double Score(string name, IEnumerable<string> labels)
    {
        var key = StringKey.Normalise(name);
        if (key.Length == 0) return 0;
        var best = 0.0;
        foreach (var label in labels)
        {
            var s = Score(key, StringKey.Normalise(label));
            if (s > best) best = s;
            if (best >= 1) break;
        }
        return best;
    }

    /// <summary>The score of two normalised keys.</summary>
    public static double Score(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1.0;
        var fa = StringKey.Fold(a); var fb = StringKey.Fold(b);
        if (fa == fb) return 0.97;
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
        // the surname must appear in full on both sides
        var surname = shorter.First(t => t.Length > 1);
        if (!longer.Contains(surname, StringComparer.Ordinal)) return false;
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
