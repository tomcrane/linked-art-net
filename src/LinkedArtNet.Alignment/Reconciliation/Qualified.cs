using System.Text.RegularExpressions;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>
/// A qualified name: one thing named with where it is or what tells it from its namesakes, "Hammersmith, London",
/// "Kent Town, S. Australia", "Fountains Hall (Yorkshire)" (design doc 10, section 7.6). The name is what the
/// indexes are asked for; the qualifier is checked against what each candidate record says about its own place:
/// TGN's parent string ("Hammersmith, London, England, United Kingdom, Europe, World") or the parenthetical a LoC
/// heading carries ("Heaton (Bradford, England)"). Agreement is a token of the qualifier found among the record's
/// placement tokens. The cataloguers' abbreviations for states, provinces and territories ("N.J.", "Me.", "N.S.W.")
/// are read as their names, and a name of several words is one token, so "Jersey" is not "New Jersey".
/// </summary>
public static class Qualified
{
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal) { "county", "the", "of", "and", "near", "upon", "on", "in", "co", "st", "saint", "city", "town", "district" };
    private static readonly Regex Parenthetical = new(@"\(([^()]*)\)", RegexOptions.Compiled);

    // The abbreviations library cataloguers qualify place names with: the AACR2 Appendix B forms for the United States,
    // Canada and Australia, which LoC headings use ("Secaucus (N.J.)", "Tulita (N.W.T.)", "Kent Town (S. Aust.)"). Written
    // from the assistant's knowledge of that list, not loaded from a file; check a doubted form against the LoC's own
    // headings. Keys are as they appear after folding (lower case, periods kept).
    private static readonly (string Abbreviation, string Name)[] Regions =
    [
        ("ala.", "alabama"), ("ariz.", "arizona"), ("ark.", "arkansas"), ("calif.", "california"), ("colo.", "colorado"),
        ("conn.", "connecticut"), ("del.", "delaware"), ("d.c.", "district of columbia"), ("fla.", "florida"), ("ga.", "georgia"),
        ("ill.", "illinois"), ("ind.", "indiana"), ("kan.", "kansas"), ("ky.", "kentucky"), ("la.", "louisiana"), ("me.", "maine"),
        ("md.", "maryland"), ("mass.", "massachusetts"), ("mich.", "michigan"), ("minn.", "minnesota"), ("miss.", "mississippi"),
        ("mo.", "missouri"), ("mont.", "montana"), ("neb.", "nebraska"), ("nev.", "nevada"), ("n.h.", "new hampshire"),
        ("n.j.", "new jersey"), ("n.m.", "new mexico"), ("n.y.", "new york"), ("n.c.", "north carolina"), ("n.d.", "north dakota"),
        ("okla.", "oklahoma"), ("or.", "oregon"), ("pa.", "pennsylvania"), ("r.i.", "rhode island"), ("s.c.", "south carolina"),
        ("s.d.", "south dakota"), ("tenn.", "tennessee"), ("tex.", "texas"), ("vt.", "vermont"), ("va.", "virginia"),
        ("wash.", "washington"), ("w. va.", "west virginia"), ("w.va.", "west virginia"), ("wis.", "wisconsin"), ("wyo.", "wyoming"),
        ("alta.", "alberta"), ("b.c.", "british columbia"), ("man.", "manitoba"), ("n.b.", "new brunswick"), ("nfld.", "newfoundland"),
        ("n.s.", "nova scotia"), ("n.w.t.", "northwest territories"), ("ont.", "ontario"), ("p.e.i.", "prince edward island"),
        ("que.", "quebec"), ("sask.", "saskatchewan"),
        ("a.c.t.", "australian capital territory"), ("n.s.w.", "new south wales"), ("n.t.", "northern territory"), ("qld.", "queensland"),
        ("s. aust.", "south australia"), ("s.a.", "south australia"), ("tas.", "tasmania"), ("vic.", "victoria"), ("w.a.", "western australia"),
        ("s. australia", "south australia"), ("w. australia", "western australia"), // the cataloguers' own half-abbreviations ("Kent Town, S. Australia")
    ];
    // the normaliser drops a trailing period ("Princeton, N.J." folds to "princeton, n.j"), so the last one is optional at the end of
    // the text and required elsewhere: "La Paz" is not Louisiana, "Baton Rouge, La." is
    private static readonly (Regex Pattern, string Token)[] Expansions = Regions
        .Select(r => (new Regex(@"(?<![a-z])" + Regex.Escape(r.Abbreviation.TrimEnd('.')) + @"(?:\.(?![a-z])|$)", RegexOptions.Compiled), r.Name.Replace(" ", "")))
        .ToArray();
    private static readonly (Regex Pattern, string Token)[] Phrases = Regions.Select(r => r.Name).Where(n => n.Contains(' ')).Distinct()
        .Select(n => (new Regex(@"(?<![a-z])" + Regex.Escape(n) + @"(?![a-z])", RegexOptions.Compiled), n.Replace(" ", "")))
        .ToArray();

    /// <summary>Splits "Name, Qualifier" or "Name (Qualifier)" into the name and the qualifier; a name alone has no qualifier.</summary>
    public static (string Name, string? Qualifier) Split(string text)
    {
        var parts = new List<string>();
        var name = Parenthetical.Replace(text, m => { var q = Qualifying(m.Groups[1].Value); if (q.Length > 0) parts.Add(q); return " "; });
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

    /// <summary>The name and what its parentheticals say, commas left alone: "Cornwall (England : County)" is "Cornwall" placed by "England"; "Art, Modern" is itself.</summary>
    public static (string Name, string? Qualifier) SplitParenthetical(string text)
    {
        var parts = new List<string>();
        var name = Regex.Replace(Parenthetical.Replace(text, m => { var q = Qualifying(m.Groups[1].Value); if (q.Length > 0) parts.Add(q); return " "; }), @"\s+", " ").Trim();
        return name.Length == 0 || parts.Count == 0 ? (text.Trim(), null) : (name, string.Join(", ", parts));
    }

    /// <summary>What a parenthetical says about place: LoC's type after " : " ("London, England : Parish", "England : County") is not a place and is dropped.</summary>
    private static string Qualifying(string parenthetical)
    {
        var p = parenthetical.Trim();
        var colon = p.IndexOf(" : ", StringComparison.Ordinal);
        return (colon > 0 ? p[..colon] : p).Trim();
    }

    /// <summary>The words a qualifier or a placement is compared by: folded, three letters or more, and not a generic word.</summary>
    public static IReadOnlyList<string> Tokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var folded = StringKey.Fold(StringKey.Normalise(text));
        foreach (var (pattern, token) in Expansions) folded = pattern.Replace(folded, token);
        foreach (var (pattern, token) in Phrases) folded = pattern.Replace(folded, token);
        return folded.Split([' ', ',', ';', '.', '(', ')', '/', ':', '-', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3 && !Noise.Contains(t)).Distinct(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Whether the qualifier places the candidate: null when either side has nothing to compare; true when one side's words are all
    /// among the other's ("Bradford, Yorkshire" against TGN's "Heaton, Bradford, West Yorkshire, England, ..."; "Bradford, Yorkshire"
    /// against the project's coarser "Bradford"). A word on each side the other lacks is another place: "Lambeth, England" does not
    /// place a record in "Prestwich, England", nor "Turkey and Greece" one in Greece alone (finding 22; one shared word was enough
    /// before, and England placed every English parish).
    /// </summary>
    public static bool? Agrees(string? qualifier, string? placement)
    {
        var q = Tokens(qualifier); var p = Tokens(placement);
        if (q.Count == 0 || p.Count == 0) return null;
        return q.All(p.Contains) || p.All(q.Contains);
    }

    /// <summary>
    /// Whether a placement contains every one of the given words: a coarser placement ("England", or LoC's "London, England")
    /// inside a finer one (TGN's "Hammersmith, London, England, United Kingdom, Europe, World") names the same place at less
    /// depth; a word outside it ("Dumfries and Galloway") makes it another place.
    /// </summary>
    public static bool Contains(string? placement, IReadOnlyList<string> words)
    {
        if (words.Count == 0) return false;
        var p = Tokens(placement);
        return p.Count > 0 && words.All(p.Contains);
    }

    /// <summary>What a record says about where it is: TGN's parent string when present, else the parentheticals of its labels.</summary>
    public static string? PlacementOf(string? parentString, IEnumerable<string?> labels)
    {
        if (!string.IsNullOrWhiteSpace(parentString)) return parentString;
        var parts = labels.OfType<string>().SelectMany(l => Parenthetical.Matches(l).Select(m => Qualifying(m.Groups[1].Value))).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}
