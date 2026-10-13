namespace LinkedArtNet.Vocabulary;

/// <summary>Getty TGN: generated place constants in <c>Tgn.g.cs</c>; factories here.</summary>
public static partial class Tgn
{
    public const string Base = "http://vocab.getty.edu/tgn/";

    public static string Uri(string id) => id.StartsWith("http", StringComparison.Ordinal) ? id : Base + id;

    /// <summary>A <see cref="Place"/> reference for any TGN id.</summary>
    public static Place Of(string id, string? label = null) => MakePlace(id.StartsWith(Base, StringComparison.Ordinal) ? id[Base.Length..] : id, label);

    private static Place MakePlace(string id, string? label) => new Place().WithId(Base + id).WithLabel(label);
}
