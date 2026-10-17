using System.Security.Cryptography;
using System.Text;

namespace LinkedArtNet.Alignment;

/// <summary>
/// How a dataset's identifiers look: a base (one hostname per dataset, design decision 1) and a path segment per
/// class, so an entity's URI is <c>{base}/{segment}/{id}</c>. The PMC scheme keeps the paths its published
/// identifiers already use (person, group, place, concept). Ids are opaque, minted by <see cref="IdMinter"/>.
/// </summary>
public sealed record IdentifierScheme(string Base, IReadOnlyDictionary<EntityClass, string>? Segments = null, int IdLength = 8)
{
    public static readonly IReadOnlyDictionary<EntityClass, string> DefaultSegments = new Dictionary<EntityClass, string>
    {
        [EntityClass.Person] = "person", [EntityClass.Group] = "group", [EntityClass.Place] = "place",
        [EntityClass.Type] = "concept", [EntityClass.Material] = "concept", [EntityClass.Language] = "concept",
        [EntityClass.Period] = "period", [EntityClass.Event] = "event",
    };

    /// <summary>The Paul Mellon Centre's published scheme; its identifiers are already in LUX and must not change.</summary>
    public static IdentifierScheme Pmc { get; } = new("https://data.paul-mellon-centre.ac.uk/");

    public string Segment(EntityClass cls) => (Segments ?? DefaultSegments).TryGetValue(cls, out var s) ? s : cls.ToString().ToLowerInvariant();

    public string Uri(EntityClass cls, string id) => $"{Base.TrimEnd('/')}/{Segment(cls)}/{id}";

    /// <summary>A fresh URI for the class; the store checks it is unused before keeping it.</summary>
    public string Mint(EntityClass cls) => Uri(cls, IdMinter.Generate(IdLength));

    /// <summary>Whether a URI is one of ours under this base (any class).</summary>
    public bool Owns(string? uri) => uri is not null && uri.StartsWith(Base.TrimEnd('/') + "/", StringComparison.Ordinal);

    /// <summary>The class a URI of ours belongs to, from its path segment; null when the URI is not ours or the segment is unknown (concept is read as Type).</summary>
    public EntityClass? ClassOf(string? uri)
    {
        if (!Owns(uri)) return null;
        var rest = uri![(Base.TrimEnd('/').Length + 1)..];
        var slash = rest.IndexOf('/');
        if (slash <= 0) return null;
        var segment = rest[..slash];
        foreach (var (cls, seg) in Segments ?? DefaultSegments)
            if (seg == segment) return cls;
        return null;
    }
}

/// <summary>
/// Opaque ids: lower-case letters and digits without the confusable ones (no 0, 1, i, l, o), letter first so an id
/// is never read as a number. Eight characters give about 2.8 x 10^11 ids. This is the alphabet the PMC identifiers
/// were minted with, kept so new and old ids look alike.
/// </summary>
public static class IdMinter
{
    private static readonly char[] Letters = "abcdefghjkmnpqrstuvwxyz".ToCharArray();
    private static readonly char[] All = "23456789abcdefghjkmnpqrstuvwxyz".ToCharArray();

    public static string Generate(int length = 8)
    {
        if (length < 4) throw new ArgumentOutOfRangeException(nameof(length), "An id needs at least four characters.");
        var sb = new StringBuilder(length);
        sb.Append(Letters[RandomNumberGenerator.GetInt32(Letters.Length)]);
        for (var i = 1; i < length; i++) sb.Append(All[RandomNumberGenerator.GetInt32(All.Length)]);
        return sb.ToString();
    }

    /// <summary>Whether a string is an id this minter could have produced.</summary>
    public static bool Looks(string? id) => id is { Length: >= 4 } && Array.IndexOf(Letters, id[0]) >= 0 && id.All(c => Array.IndexOf(All, c) >= 0);
}
