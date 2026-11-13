using System.Text.Json;

namespace LinkedArtNet.Alignment.Authorities;

/// <summary>
/// Which of an entity's identifiers belong together, read from what the store holds about each (the owner, 2026-11-13, at the bare
/// "Dagenham": Wikidata's parliamentary constituency, its VIAF number and a LUX record, to be removed as one, and "it's hard to tell
/// what the VIAF one actually is when you go to it on VIAF.org"). A record cites another identifier through its <c>sameAs</c>, the
/// <c>ids</c> of the Wikidata extract (<c>"viaf": "143706493"</c>) and the "same as" facts of a pull; identifiers that cite one
/// another, or cite a common third, are one set. A VIAF number has no record here, and is described by the record that gives it.
/// </summary>
public static class IdentifierSets
{
    /// <summary>The identifiers a record gives for the same thing, as URIs.</summary>
    public static IReadOnlyList<string> Cites(AuthorityRecord? record)
    {
        if (record is null) return [];
        var cited = new List<string>(record.SameAs);
        if (record.DataJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(record.DataJson);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("ids", out var ids) && ids.ValueKind == JsonValueKind.Object)
                    foreach (var p in ids.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() is { Length: > 0 } id && Uri(p.Name, id) is { } uri) cited.Add(uri);
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(SourcePull.Key, out var pulled) && pulled.ValueKind == JsonValueKind.Object
                    && pulled.TryGetProperty("facts", out var facts) && facts.ValueKind == JsonValueKind.Array)
                    foreach (var f in facts.EnumerateArray())
                        if (f.ValueKind == JsonValueKind.Object && f.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String && k.GetString()!.Contains("same as", StringComparison.OrdinalIgnoreCase)
                            && f.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String && u.GetString() is { Length: > 0 } url) cited.Add(url);
            }
            catch (JsonException) { }
        }
        return cited;
    }

    /// <summary>The held record that gives this identifier as its own, if one does: Wikidata's Q3403624 for the VIAF number it carries.</summary>
    public static AuthorityRecord? CitedBy(string uri, IEnumerable<AuthorityRecord> records)
    {
        var key = Key(uri);
        return records.FirstOrDefault(r => Key(r.Uri) != key && Cites(r).Any(c => Key(c) == key));
    }

    /// <summary>
    /// The identifiers in the order given, gathered into sets: two are in one set when the record of one cites the other, or both
    /// cite the same identifier. An identifier nothing connects is a set of its own.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Of(IReadOnlyList<string> held, IReadOnlyDictionary<string, AuthorityRecord> records)
    {
        var parent = held.ToDictionary(Key, Key, StringComparer.Ordinal);
        string Find(string x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        void Union(string a, string b) { a = Find(a); b = Find(b); if (a != b) parent[b] = a; }
        var citedBy = new Dictionary<string, string>(StringComparer.Ordinal); // a cited identifier not held, by the first held one that cites it
        foreach (var uri in held)
        {
            if (!records.TryGetValue(uri, out var record)) continue;
            foreach (var c in Cites(record).Select(Key).Distinct(StringComparer.Ordinal))
            {
                if (c == Key(uri)) continue;
                if (parent.ContainsKey(c)) Union(Key(uri), c);
                else if (citedBy.TryGetValue(c, out var other)) Union(other, Key(uri));
                else citedBy[c] = Key(uri);
            }
        }
        return held.GroupBy(u => Find(Key(u)), StringComparer.Ordinal).Select(g => (IReadOnlyList<string>)g.ToList()).ToList();
    }

    /// <summary>An identifier compared without its scheme, "www." or a trailing slash: VIAF and others are written both ways.</summary>
    public static string Key(string uri)
    {
        var u = uri.Trim();
        var at = u.IndexOf("://", StringComparison.Ordinal);
        if (at >= 0) u = u[(at + 3)..];
        if (u.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) u = u[4..];
        return u.TrimEnd('/');
    }

    private static string? Uri(string source, string id) => source switch
    {
        "viaf" => AuthorityUris.Viaf + id,
        "loc" => AuthorityUris.Loc(id),
        "ulan" => AuthorityUris.Ulan + id,
        "aat" => AuthorityUris.Aat + id,
        "tgn" => AuthorityUris.Tgn + id,
        "gnd" => AuthorityUris.Gnd + id,
        _ => null,
    };
}
