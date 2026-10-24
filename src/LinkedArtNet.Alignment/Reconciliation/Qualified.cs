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
    // one country under two names: LoC's headings say "(Great Britain)" where TGN's hierarchy says "United Kingdom" (2026-10-24, run t629cj:
    // the project's "National Maritime Museum (Great Britain)" against "(London)", placed in "..., England, United Kingdom, Europe, World")
    private static readonly (Regex Pattern, string Token)[] Synonyms =
    [
        (new Regex(@"(?<![a-z])(?:great britain|united kingdom|u\.k\.?|uk|britain)(?![a-z])", RegexOptions.Compiled), "unitedkingdom"),
    ];

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
        foreach (var (pattern, token) in Synonyms) folded = pattern.Replace(folded, token);
        return folded.Split([' ', ',', ';', '.', '(', ')', '/', ':', '-', '\'', '?', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
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
        return q.All(t => Has(p, t)) || p.All(t => Has(q, t));
    }

    private static readonly string[] Directions = ["north", "south", "east", "west", "greater", "central", "upper", "lower", "inner", "outer"];

    /// <summary>Whether a word is among the tokens, or a compound of it with a direction is ("west yorkshire" holds "yorkshire": finding 67); never the other way, since "york" is not "new york".</summary>
    private static bool Has(IReadOnlyList<string> tokens, string word) =>
        tokens.Contains(word) || tokens.Any(t => t.Length > word.Length && t.EndsWith(word, StringComparison.Ordinal) && Directions.Contains(t[..^word.Length]));

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
    /// <summary>
    /// The type a LoC heading gives a place after " : " ("Cornwall (England : County)" gives "County"; "New York (N.Y.)" none), or a
    /// parenthetical that is nothing but a level word ("(City)"). LoC adds the type only to tell a place from another of the
    /// name at a different level; the heading without one is the inhabited place (LoC practice, as the assistant knows it).
    /// </summary>
    public static string? TypeOf(IEnumerable<string?> labels)
    {
        foreach (var label in labels.OfType<string>())
            foreach (System.Text.RegularExpressions.Match m in Parenthetical.Matches(label))
            {
                var p = m.Groups[1].Value.Trim();
                var colon = p.IndexOf(" : ", StringComparison.Ordinal);
                if (colon > 0 && colon + 3 < p.Length) return p[(colon + 3)..].Trim();
                if (!p.Contains(' ') && LevelOf(p) is not null) return p;
            }
        return null;
    }

    /// <summary>
    /// A place's level from the words of its type, TGN's AAT placeType label ("inhabited places", "counties", "provinces",
    /// "nations") or LoC's type word ("City", "County", "Province"): 5 a nation, 4 a first-level division (state, province,
    /// region, département, Land, canton), 3 a second-level one (county, district, arrondissement, Kreis), 2 an inhabited place
    /// (city, town, village, borough, township, parish), null for anything else (rivers, islands, continents, regions of no
    /// administrative kind are not on the ladder). The word table is the assistant's own, from the AAT place-type labels TGN
    /// uses and LoC's type words; not a published list.
    /// </summary>
    public static int? LevelOf(string? type)
    {
        if (string.IsNullOrWhiteSpace(type)) return null;
        var lower = type.ToLowerInvariant();
        if (lower.Contains("county seat")) return 2;
        foreach (var token in lower.Split([' ', ',', '(', ')', '/', '-', ';'], StringSplitOptions.RemoveEmptyEntries))
            if (Levels.TryGetValue(token, out var level)) return level;
        return null;
    }

    public static string LevelName(int level) => level switch { 5 => "nation", 4 => "state or province", 3 => "county or district", 2 => "inhabited place", _ => "place" };

    private static readonly Dictionary<string, int> Levels = new(StringComparer.Ordinal)
    {
        ["nation"] = 5, ["nations"] = 5, ["country"] = 5, ["countries"] = 5, ["kingdom"] = 5, ["kingdoms"] = 5, ["republic"] = 5, ["republics"] = 5, ["empire"] = 5, ["empires"] = 5, ["sovereign"] = 5,
        ["state"] = 4, ["states"] = 4, ["province"] = 4, ["provinces"] = 4, ["region"] = 4, ["regions"] = 4, ["territory"] = 4, ["territories"] = 4,
        ["department"] = 4, ["departments"] = 4, ["département"] = 4, ["départements"] = 4, ["departement"] = 4, ["departements"] = 4, ["land"] = 4, ["länder"] = 4, ["lander"] = 4,
        ["canton"] = 4, ["cantons"] = 4, ["prefecture"] = 4, ["prefectures"] = 4, ["oblast"] = 4, ["oblasts"] = 4, ["emirate"] = 4, ["emirates"] = 4, ["autonomous"] = 4,
        ["county"] = 3, ["counties"] = 3, ["district"] = 3, ["districts"] = 3, ["arrondissement"] = 3, ["arrondissements"] = 3, ["kreis"] = 3, ["kreise"] = 3,
        ["shire"] = 3, ["shires"] = 3, ["municipality"] = 3, ["municipalities"] = 3, ["comarca"] = 3, ["comarcas"] = 3, ["unitary"] = 3,
        ["administrative"] = 3, // TGN's "former administrative divisions" (Dumfries, the old county; the owner's query, 2026-10-22)
        ["inhabited"] = 2, ["city"] = 2, ["cities"] = 2, ["town"] = 2, ["towns"] = 2, ["village"] = 2, ["villages"] = 2, ["hamlet"] = 2, ["hamlets"] = 2,
        ["borough"] = 2, ["boroughs"] = 2, ["township"] = 2, ["townships"] = 2, ["parish"] = 2, ["parishes"] = 2, ["commune"] = 2, ["communes"] = 2,
        ["neighborhood"] = 2, ["neighborhoods"] = 2, ["neighbourhood"] = 2, ["neighbourhoods"] = 2, ["suburb"] = 2, ["suburbs"] = 2,
        ["settlement"] = 2, ["settlements"] = 2, ["locality"] = 2, ["localities"] = 2, ["capital"] = 2, ["capitals"] = 2, ["metropolis"] = 2, ["metropolises"] = 2,
    };

    /// <param name="placesOnly">Keep only parentheticals of the "City, Country" shape (with a comma): a heading's type qualifier ("(Gallery)") or a lone country is left out.</param>
    public static string? PlacementOf(string? parentString, IEnumerable<string?> labels, bool placesOnly = false)
    {
        if (!string.IsNullOrWhiteSpace(parentString)) return parentString;
        var parts = labels.OfType<string>().SelectMany(l => Parenthetical.Matches(l).Select(m => Qualifying(m.Groups[1].Value))).Where(x => x.Length > 0 && (!placesOnly || x.Contains(','))).Distinct(StringComparer.Ordinal).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}
