namespace LinkedArtNet.Vocabulary;

/// <summary>Getty ULAN: <c>Ulan.Person("500011051", "Rembrandt")</c>, <c>Ulan.Group("500246547", "Rijksmuseum")</c>. Actors are data, not vocabulary, so there are no constants.</summary>
public static class Ulan
{
    public const string Base = "http://vocab.getty.edu/ulan/";

    public static string Uri(string id) => id.StartsWith("http", StringComparison.Ordinal) ? id : Base + id;

    /// <summary>A <see cref="LinkedArtNet.Person"/> reference for a ULAN id (numeric or full URI).</summary>
    public static LinkedArtNet.Person Person(string id, string? label = null) => new LinkedArtNet.Person().WithId(Uri(id)).WithLabel(label);

    /// <summary>A <see cref="LinkedArtNet.Group"/> reference for a ULAN id (numeric or full URI).</summary>
    public static LinkedArtNet.Group Group(string id, string? label = null) => new LinkedArtNet.Group().WithId(Uri(id)).WithLabel(label);
}
