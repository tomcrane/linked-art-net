using System.Collections.Frozen;

namespace LinkedArtNet.Vocabulary;

/// <summary>
/// Getty AAT: the generated half (<c>Aat.g.cs</c>) holds one property per registry term; this half holds the
/// URI base, factories and lookups. Every property returns a new instance so callers cannot mutate a shared one.
/// </summary>
public static partial class Aat
{
    public const string Base = "http://vocab.getty.edu/aat/";

    // Lazy: static initialisers in a partial class run in an unspecified order across files, and Terms lives in Aat.g.cs.
    private static readonly Lazy<FrozenDictionary<string, VocabularyTerm>> ById = new(() =>
        Terms.Where(t => t.AliasOf is null).ToFrozenDictionary(t => t.Id, StringComparer.Ordinal));

    public static string Uri(string id) => id.StartsWith("http", StringComparison.Ordinal) ? id : Base + id;

    /// <summary>The registry term for an AAT id (numeric or full URI), or null if not registered.</summary>
    public static VocabularyTerm? TryGet(string id) => ById.Value.GetValueOrDefault(StripBase(id));

    /// <summary>A <see cref="Type"/> for any AAT id, using the registry label when none is given.</summary>
    public static Type Of(string id, string? label = null) => MakeType(StripBase(id), label ?? TryGet(id)?.Label);

    public static Material OfMaterial(string id, string? label = null) => MakeMaterial(StripBase(id), label ?? TryGet(id)?.Label);

    public static MeasurementUnit OfUnit(string id, string? label = null) => MakeUnit(StripBase(id), label ?? TryGet(id)?.Label);

    public static Currency OfCurrency(string id, string? label = null) => MakeCurrency(StripBase(id), label ?? TryGet(id)?.Label);

    public static Language OfLanguage(string id, string? label = null, string? notation = null)
    {
        var term = TryGet(id);
        return MakeLanguage(StripBase(id), label ?? term?.Label, notation ?? term?.Notation);
    }

    /// <summary>A nationality concept: a <see cref="Type"/> classified as AAT Nationality.</summary>
    public static Type NationalityOf(string id, string label) => Of(id, label).WithClassifiedAs(Nationality);

    private static Type MakeType(string id, string? label) => new(Base + id, label);
    private static Material MakeMaterial(string id, string? label) => new(Base + id, label);
    private static Currency MakeCurrency(string id, string? label) => new(Base + id, label);
    private static MeasurementUnit MakeUnit(string id, string? label) => new(Base + id, label);
    private static Language MakeLanguage(string id, string? label, string? notation) => new(Base + id, label, notation);

    private static string StripBase(string id) => id.StartsWith(Base, StringComparison.Ordinal) ? id[Base.Length..] : id;
}
