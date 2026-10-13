using System.Reflection;
using System.Text.Json;

namespace LinkedArtNet.Dates.Text;

/// <summary>
/// The words the tokenizer recognises for one language, loaded from an embedded <c>Lexicon/{code}.json</c>.
/// Keys are lower-case with abbreviation dots removed ("c." is looked up as "c"); multi-word phrases are matched
/// longest first. Several lexicons can be active at once (<see cref="Merge"/>).
/// </summary>
public sealed record Lexicon
{
    public string Language { get; private init; } = "";
    public IReadOnlyDictionary<string, int> Months { get; private init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, Season> Seasons { get; private init; } = new Dictionary<string, Season>();
    public IReadOnlySet<string> CenturyWords { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> MillenniumWords { get; private init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, Band> Bands { get; private init; } = new Dictionary<string, Band>();
    public IReadOnlyDictionary<string, Qualification> Hedges { get; private init; } = new Dictionary<string, Qualification>();
    /// <summary>"Bce", "Ce" or "Bp".</summary>
    public IReadOnlyDictionary<string, string> Eras { get; private init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, Relation> Relations { get; private init; } = new Dictionary<string, Relation>();
    public IReadOnlySet<string> OpenEnd { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> RangeWords { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> Between { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> Or { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> Floruit { get; private init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, LifecycleMarker> Lifecycle { get; private init; } = new Dictionary<string, LifecycleMarker>();
    public IReadOnlySet<string> Undated { get; private init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, Magnitude> Magnitudes { get; private init; } = new Dictionary<string, Magnitude>();
    public IReadOnlySet<string> Ago { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> Bp { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> Corrections { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> Noise { get; private init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, int> OrdinalWords { get; private init; } = new Dictionary<string, int>();

    /// <summary>The longest phrase (in words) in any table, so the tokenizer knows how far to look ahead.</summary>
    public int MaxPhraseWords { get; private init; }

    private static readonly Dictionary<string, Lexicon> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Lexicon English => Load("en");

    public static Lexicon Load(string languageCode)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(languageCode, out var cached)) return cached;
            var name = $"LinkedArtNet.Dates.Lexicon.{languageCode}.json";
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new FileNotFoundException($"No lexicon for language '{languageCode}'.", name);
            using var reader = new StreamReader(stream);
            return Cache[languageCode] = FromJson(reader.ReadToEnd());
        }
    }

    public static Lexicon FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        var root = doc.RootElement;
        var lexicon = new Lexicon
        {
            Language = root.GetProperty("language").GetString() ?? "",
            Months = Map(root, "months", e => e.GetInt32()),
            Seasons = Map(root, "seasons", e => Enum.Parse<Season>(e.GetString()!)),
            CenturyWords = Set(root, "centuryWords"),
            MillenniumWords = Set(root, "millenniumWords"),
            Bands = Map(root, "bands", e => Enum.Parse<Band>(e.GetString()!)),
            Hedges = Map(root, "hedges", e => Enum.Parse<Qualification>(e.GetString()!)),
            Eras = Map(root, "eras", e => e.GetString()!),
            Relations = Map(root, "relations", e => Enum.Parse<Relation>(e.GetString()!)),
            OpenEnd = Set(root, "openEnd"),
            RangeWords = Set(root, "rangeWords"),
            Between = Set(root, "between"),
            Or = Set(root, "or"),
            Floruit = Set(root, "floruit"),
            Lifecycle = Map(root, "lifecycle", e => Enum.Parse<LifecycleMarker>(e.GetString()!)),
            Undated = Set(root, "undated"),
            Magnitudes = Map(root, "magnitudes", e => Enum.Parse<Magnitude>(e.GetString()!)),
            Ago = Set(root, "ago"),
            Bp = Set(root, "bp"),
            Corrections = Set(root, "corrections"),
            Noise = Set(root, "noise"),
            OrdinalWords = Map(root, "ordinalWords", e => e.GetInt32()),
        };
        return lexicon with { MaxPhraseWords = lexicon.AllKeys().Max(k => k.Count(c => c == ' ') + 1) };
    }

    /// <summary>
    /// One lexicon answering for several languages; earlier lexicons win on conflicts. A word any table of an earlier
    /// language knows is left out of every table of a later language, so French "en" (noise) is not Dutch "en" (and).
    /// </summary>
    public static Lexicon Merge(IEnumerable<Lexicon> lexicons)
    {
        var list = lexicons.ToList();
        if (list.Count == 1) return list[0];
        var claimedBy = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < list.Count; i++)
            foreach (var key in list[i].AllKeys()) claimedBy.TryAdd(key, i);
        bool Owns(int i, string key) => claimedBy[key] == i;

        Dictionary<string, T> MergeMaps<T>(Func<Lexicon, IReadOnlyDictionary<string, T>> select)
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            for (var i = 0; i < list.Count; i++) foreach (var (k, v) in select(list[i])) if (Owns(i, k)) result.TryAdd(k, v);
            return result;
        }
        HashSet<string> MergeSets(Func<Lexicon, IReadOnlySet<string>> select)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < list.Count; i++) foreach (var k in select(list[i])) if (Owns(i, k)) result.Add(k);
            return result;
        }
        var merged = new Lexicon
        {
            Language = string.Join("+", list.Select(l => l.Language)),
            Months = MergeMaps(l => l.Months), Seasons = MergeMaps(l => l.Seasons),
            CenturyWords = MergeSets(l => l.CenturyWords), MillenniumWords = MergeSets(l => l.MillenniumWords),
            Bands = MergeMaps(l => l.Bands), Hedges = MergeMaps(l => l.Hedges), Eras = MergeMaps(l => l.Eras),
            Relations = MergeMaps(l => l.Relations), OpenEnd = MergeSets(l => l.OpenEnd), RangeWords = MergeSets(l => l.RangeWords),
            Between = MergeSets(l => l.Between), Or = MergeSets(l => l.Or), Floruit = MergeSets(l => l.Floruit),
            Lifecycle = MergeMaps(l => l.Lifecycle), Undated = MergeSets(l => l.Undated), Magnitudes = MergeMaps(l => l.Magnitudes),
            Ago = MergeSets(l => l.Ago), Bp = MergeSets(l => l.Bp), Corrections = MergeSets(l => l.Corrections),
            Noise = MergeSets(l => l.Noise), OrdinalWords = MergeMaps(l => l.OrdinalWords),
        };
        return merged with { MaxPhraseWords = list.Max(l => l.MaxPhraseWords) };
    }

    private Lexicon() { }

    private IEnumerable<string> AllKeys() => Months.Keys.Concat(Seasons.Keys).Concat(CenturyWords).Concat(MillenniumWords).Concat(Bands.Keys)
        .Concat(Hedges.Keys).Concat(Eras.Keys).Concat(Relations.Keys).Concat(OpenEnd).Concat(RangeWords).Concat(Between).Concat(Or)
        .Concat(Floruit).Concat(Lifecycle.Keys).Concat(Undated).Concat(Magnitudes.Keys).Concat(Ago).Concat(Bp).Concat(Corrections)
        .Concat(Noise).Concat(OrdinalWords.Keys);

    private static Dictionary<string, T> Map<T>(JsonElement root, string name, Func<JsonElement, T> convert)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        if (!root.TryGetProperty(name, out var obj)) return result;
        foreach (var p in obj.EnumerateObject()) result[Normalise(p.Name)] = convert(p.Value);
        return result;
    }

    private static HashSet<string> Set(JsonElement root, string name) =>
        root.TryGetProperty(name, out var arr) ? arr.EnumerateArray().Select(e => Normalise(e.GetString()!)).ToHashSet(StringComparer.Ordinal) : [];

    /// <summary>Lower-case, abbreviation dots removed, so "B.C." and "bc" meet in the table.</summary>
    public static string Normalise(string word) => word.ToLowerInvariant().Replace(".", "");
}
