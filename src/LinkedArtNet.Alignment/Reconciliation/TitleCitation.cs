using System.Globalization;
using System.Text;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>
/// Whether an authority's source citation ("Smith, Lindsay. Victorian photography, painting and poetry, 1995: CIP t.p. ...") cites a
/// work by a title as a catalogue record gives it. The match allows what differs between a cataloguer's citation and a record's title
/// (the owner, 2026-11-27: "we need to allow some variation"): case, accents and punctuation; "&amp;" for "and"; a leading article; the
/// subtitle (the title up to its first colon or slash is what is looked for); initials with or without their stops ("R.A.", "RA"); and the abbreviations citations are full of
/// ("Brit. sch. of miniature portrait painters" for "The British school of miniature portrait painters": a cited word that is the
/// beginning of the title's word, or the other way about, three letters at least). A citation may stop short of a long title after four words, three of them of substance. It does not allow words
/// missing from the middle, added or out of order, nor a title of fewer than two words of substance: "Catalogue", "Paintings" and "Notes" are in every citation.
/// </summary>
public static class TitleCitation
{
    private static readonly HashSet<string> Articles = new(StringComparer.Ordinal) { "the", "a", "an", "le", "la", "les", "der", "die", "das", "il", "el" };
    private static readonly HashSet<string> Small = new(StringComparer.Ordinal) { "of", "and", "in", "on", "the", "a", "an", "to", "for", "by", "at", "from", "with", "de", "du", "des", "la", "le", "et", "und", "von" };

    /// <summary>The words of a text as they are compared: lower case, without accents, "&amp;" as "and", letters and digits only.</summary>
    public static IReadOnlyList<string> Words(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Replace("&", " and ", StringComparison.Ordinal).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark || ch is '\'' or '’') continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }
        // initials run together, so that "R.A." and "RA", "J.M.W." and "J M W" are one word each
        var words = new List<string>();
        var run = false;
        foreach (var w in sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (w.Length == 1 && char.IsLetter(w[0]) && run) words[^1] += w; else words.Add(w);
            run = w.Length == 1 && char.IsLetter(w[0]);
        }
        return words;
    }

    /// <summary>The title as it is looked for: up to its statement of responsibility and its subtitle, without a leading article.</summary>
    public static IReadOnlyList<string> Sought(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return [];
        var main = title.Split(" / ")[0];
        var cut = main.IndexOfAny([':', ';']);
        if (cut > 0) main = main[..cut];
        // a full stop ends the title proper only when followed by a space and more ("Thel. Thelyphthora"), not in "J.M.W. Turner"
        var words = Words(main).ToList();
        if (words.Count > 1 && Articles.Contains(words[0])) words.RemoveAt(0);
        return words;
    }

    private static bool Same(string cited, string titled) =>
        cited == titled || (cited.Length >= 3 && titled.StartsWith(cited, StringComparison.Ordinal)) || (titled.Length >= 3 && cited.StartsWith(titled, StringComparison.Ordinal) && cited.Length - titled.Length <= 2);

    /// <summary>Whether the citation holds the title's words, in order and together, allowing the variation described on the class.</summary>
    public static bool Cites(string? citation, string? title)
    {
        var sought = Sought(title);
        // enough to be a title: two words of substance, and a dozen letters between them all
        if (sought.Count(w => w.Length >= 4 && !Small.Contains(w)) < 2 || sought.Sum(w => w.Length) < 12) return false;
        var cited = Words(citation);
        for (var at = 0; at < cited.Count; at++)
        {
            var i = 0;
            while (i < sought.Count && at + i < cited.Count && Same(cited[at + i], sought[i])) i++;
            if (i == sought.Count) return true;
            // a citation that stops short of a long title: its first words, four at least with three of substance among them
            if (i >= 4 && sought.Take(i).Count(w => w.Length >= 4 && !Small.Contains(w)) >= 3) return true;
        }
        return false;
    }
}
