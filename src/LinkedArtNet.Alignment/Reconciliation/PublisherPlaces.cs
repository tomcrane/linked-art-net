using System.Reflection;
using System.Text.Json;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>A publisher and where it is, from the shipped table.</summary>
public sealed record PublisherPlace(IReadOnlyList<string> Names, string Place, string? Uri, string? Source);

/// <summary>
/// Background knowledge shipped with the library (Tables/publisher-places.json; the owner, 2026-11-15, at "Cambridge": "the app having a
/// set of common background data would help here"): where a publisher is, for reading a bare place of publication. A skeleton of a
/// handful of entries, its provenance stated in the file, grown by pull request. It is a hint at the desk and decides nothing.
/// </summary>
public static class PublisherPlaces
{
    private static readonly Lazy<(string Notes, List<PublisherPlace> All, Dictionary<string, PublisherPlace> ByKey)> Table = new(Load);

    public static string Notes => Table.Value.Notes;
    public static IReadOnlyList<PublisherPlace> All => Table.Value.All;

    /// <summary>The entry for a publisher as a record names it, compared as names are (case, punctuation, a leading article in the table's own names); null when the table has none.</summary>
    public static PublisherPlace? Find(string? publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher)) return null;
        foreach (var key in StringKey.Variants(publisher))
            if (Table.Value.ByKey.TryGetValue(key, out var found)) return found;
        return null;
    }

    private static (string, List<PublisherPlace>, Dictionary<string, PublisherPlace>) Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LinkedArtNet.Alignment.Tables.publisher-places.json") ?? throw new InvalidOperationException("publisher-places.json is not embedded.");
        using var doc = JsonDocument.Parse(stream);
        var all = new List<PublisherPlace>();
        var byKey = new Dictionary<string, PublisherPlace>(StringComparer.Ordinal);
        foreach (var e in doc.RootElement.GetProperty("publishers").EnumerateArray())
        {
            var tgn = e.TryGetProperty("tgn", out var t) ? t.GetString() : null;
            var entry = new PublisherPlace(e.GetProperty("names").EnumerateArray().Select(n => n.GetString()!).ToList(), e.GetProperty("place").GetString()!,
                tgn is null ? null : "http://vocab.getty.edu/tgn/" + tgn, e.TryGetProperty("source", out var s) ? s.GetString() : null);
            all.Add(entry);
            foreach (var name in entry.Names) foreach (var key in StringKey.Variants(name)) byKey.TryAdd(key, entry);
        }
        return (doc.RootElement.GetProperty("notes").GetString() ?? "", all, byKey);
    }
}
