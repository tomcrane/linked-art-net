namespace LinkedArtNet.Vocabulary;

/// <summary>Getty ULAN: generated actor constants in <c>Ulan.g.cs</c>; factories here.</summary>
public static partial class Ulan
{
    public const string Base = "http://vocab.getty.edu/ulan/";

    public static string Uri(string id) => id.StartsWith("http", StringComparison.Ordinal) ? id : Base + id;

    /// <summary>A <see cref="Person"/> reference for any ULAN id.</summary>
    public static Person PersonOf(string id, string? label = null) => MakePerson(Strip(id), label);

    /// <summary>A <see cref="Group"/> reference for any ULAN id.</summary>
    public static Group GroupOf(string id, string? label = null) => MakeGroup(Strip(id), label);

    private static Person MakePerson(string id, string? label) => new Person().WithId(Base + id).WithLabel(label);
    private static Group MakeGroup(string id, string? label) => new Group().WithId(Base + id).WithLabel(label);
    private static string Strip(string id) => id.StartsWith(Base, StringComparison.Ordinal) ? id[Base.Length..] : id;
}
