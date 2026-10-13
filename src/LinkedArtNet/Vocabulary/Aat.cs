using System.Collections.Frozen;

namespace LinkedArtNet.Vocabulary;

/// <summary>
/// Getty AAT. The generated half (<c>Aat.g.cs</c>) holds one property per registry term (<c>Aat.Painting</c>);
/// this half holds the URI base, lookups, and factories for terms that are not in the registry:
/// <c>Aat.Term("300046218", "Parasol")</c>, <c>Aat.Material(...)</c>, <c>Aat.Unit(...)</c>, <c>Aat.Currency(...)</c>,
/// <c>Aat.Language(...)</c>. Every call returns a new instance so callers cannot mutate a shared one.
/// </summary>
/// <remarks>Factory methods share their names with model classes, so types are written fully qualified inside this class.</remarks>
public static partial class Aat
{
    public const string Base = "http://vocab.getty.edu/aat/";

    // Lazy: static initialisers in a partial class run in an unspecified order across files, and Terms lives in Aat.g.cs.
    private static readonly Lazy<FrozenDictionary<string, VocabularyTerm>> ById = new(() =>
        Terms.Where(t => t.AliasOf is null).ToFrozenDictionary(t => t.Id, StringComparer.Ordinal));

    public static string Uri(string id) => id.StartsWith("http", StringComparison.Ordinal) ? id : Base + id;

    /// <summary>The registry term for an AAT id (numeric or full URI), or null if not registered.</summary>
    public static VocabularyTerm? TryGet(string id) => ById.Value.GetValueOrDefault(Strip(id));

    /// <summary>A <see cref="LinkedArtNet.Type"/> for any AAT id, using the registry label when none is given.</summary>
    public static LinkedArtNet.Type Term(string id, string? label = null) => MakeType(Strip(id), label ?? TryGet(id)?.Label);

    public static LinkedArtNet.Material Material(string id, string? label = null) => MakeMaterial(Strip(id), label ?? TryGet(id)?.Label);

    public static LinkedArtNet.MeasurementUnit Unit(string id, string? label = null) => MakeUnit(Strip(id), label ?? TryGet(id)?.Label);

    public static LinkedArtNet.Currency Currency(string id, string? label = null) => MakeCurrency(Strip(id), label ?? TryGet(id)?.Label);

    public static LinkedArtNet.Language Language(string id, string? label = null, string? notation = null)
    {
        var term = TryGet(id);
        return MakeLanguage(Strip(id), label ?? term?.Label, notation ?? term?.Notation);
    }

    /// <summary>A nationality concept: a <see cref="LinkedArtNet.Type"/> classified as AAT Nationality.</summary>
    public static LinkedArtNet.Type NationalityTerm(string id, string label) => Term(id, label).WithClassifiedAs(Nationality);

    private static LinkedArtNet.Type MakeType(string id, string? label) => new(Base + id, label);
    private static LinkedArtNet.Material MakeMaterial(string id, string? label) => new(Base + id, label);
    private static LinkedArtNet.Currency MakeCurrency(string id, string? label) => new(Base + id, label);
    private static LinkedArtNet.MeasurementUnit MakeUnit(string id, string? label) => new(Base + id, label);
    private static LinkedArtNet.Language MakeLanguage(string id, string? label, string? notation) => new(Base + id, label, notation);

    private static string Strip(string id) => id.StartsWith(Base, StringComparison.Ordinal) ? id[Base.Length..] : id;
}
