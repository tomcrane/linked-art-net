namespace LinkedArtNet.Vocabulary;

/// <summary>The class a vocabulary term is instantiated as.</summary>
public enum VocabularyKind
{
    Type,
    Material,
    Language,
    Currency,
    MeasurementUnit,
    Place,
    Person,
    Group,
}

/// <summary>
/// One entry of a vocabulary registry (<c>vocabulary/*.json</c>): a C# name, the authority's identifier, the
/// label to write, and the class to instantiate. <paramref name="AliasOf"/> names another term with the same id
/// whose label differs (the site's examples are not consistent), so ids are unique among non-alias terms.
/// </summary>
public sealed record VocabularyTerm(string Name, string Id, string Label, VocabularyKind Kind, string? Notation, string? AliasOf);
