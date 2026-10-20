using System.Text.RegularExpressions;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>
/// A qualified name: one thing named with where it is or what tells it from its namesakes, "Hammersmith, London",
/// "Kent Town, S. Australia", "Fountains Hall (Yorkshire)" (design doc 10, section 7.6). The name is what the
/// indexes are asked for; the qualifier is checked against what each candidate record says about its own place:
/// TGN's parent string ("Hammersmith, London, England, United Kingdom, Europe, World") or the parenthetical a LoC
/// heading carries ("Heaton (Bradford, England)"). Agreement is a token of the qualifier found among the record's
/// placement tokens; abbreviations ("N.J.") place nothing and leave the string to the desk.
/// </summary>
public static class Qualified
{
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal) { "county", "the", "of", "and", "near", "upon", "on", "in", "co", "st", "saint", "city", "town", "district" };
    private static readonly Regex Parenthetical = new(@"\(([^()]*)\)", RegexOptions.Compiled);

    /// <summary>Splits "Name, Qualifier" or "Name (Qualifier)" into the name and the qualifier; a name alone has no qualifier.</summary>
    public static (string Name, string? Qualifier) Split(string text)
    {
        var parts = new List<string>();
        var name = Parenthetical.Replace(text, m => { if (m.Groups[1].Value.Trim().Length > 0) parts.Add(m.Groups[1].Value.Trim()); return " "; });
        var comma = name.IndexOf(',');
        if (comma > 0)
        {
            var rest = name[(comma + 1)..].Trim();
            if (rest.Length > 0) parts.Insert(0, rest);
            name = name[..comma];
        }
        name = Regex.Replace(name, @"\s+", " ").Trim();
        if (name.Length == 0) return (text.Trim(), null);
        return (name, parts.Count == 0 ? null : string.Join(", ", parts));
    }

    /// <summary>The words a qualifier or a placement is compared by: folded, three letters or more, and not a generic word.</summary>
    public static IReadOnlyList<string> Tokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var folded = StringKey.Fold(StringKey.Normalise(text));
        return folded.Split([' ', ',', ';', '.', '(', ')', '/', ':', '-', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && !Noise.Contains(t)).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>Whether the qualifier places the candidate: null when either side has nothing to compare, true when a word of the qualifier is among the placement's words.</summary>
    public static bool? Agrees(string? qualifier, string? placement)
    {
        var q = Tokens(qualifier); var p = Tokens(placement);
        if (q.Count == 0 || p.Count == 0) return null;
        return q.Any(p.Contains);
    }

    /// <summary>What a record says about where it is: TGN's parent string when present, else the parentheticals of its labels.</summary>
    public static string? PlacementOf(string? parentString, IEnumerable<string?> labels)
    {
        if (!string.IsNullOrWhiteSpace(parentString)) return parentString;
        var parts = labels.OfType<string>().SelectMany(l => Parenthetical.Matches(l).Select(m => m.Groups[1].Value.Trim())).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}
