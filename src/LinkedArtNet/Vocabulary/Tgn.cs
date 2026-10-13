namespace LinkedArtNet.Vocabulary;

/// <summary>Getty TGN: <c>Tgn.Place("7006952", "Amsterdam")</c>. Places are data, not vocabulary, so there are no constants.</summary>
public static class Tgn
{
    public const string Base = "http://vocab.getty.edu/tgn/";

    public static string Uri(string id) => id.StartsWith("http", StringComparison.Ordinal) ? id : Base + id;

    /// <summary>A <see cref="LinkedArtNet.Place"/> reference for a TGN id (numeric or full URI).</summary>
    public static LinkedArtNet.Place Place(string id, string? label = null) => new LinkedArtNet.Place().WithId(Uri(id)).WithLabel(label);
}
