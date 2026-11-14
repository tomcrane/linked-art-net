using System.Globalization;
using System.Text.Json;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Authorities;

/// <summary>Where something is for the map; <see cref="Near"/> names the TGN parent whose coordinates stand in when the record itself carries none (the owner, 2026-11-08: Windsor Castle in England has none in TGN, its Pennsylvanian namesake has).</summary>
public sealed record Placed(double Lat, double Long, string? Near = null, string? Via = null)
{
    /// <summary>Not the record's own coordinates: a parent's (<see cref="Near"/>) or another record's of the same name in the same place (<see cref="Via"/>).</summary>
    public bool Approximate => Near is not null || Via is not null;
}

/// <summary>
/// Where a record places something, for the map (plan section 7, "A map", the owner, 2026-11-08): TGN's <c>lat</c> and <c>long</c>,
/// loaded with the vocabulary into the record's <c>data</c>, or the coordinates a pull brought (Wikidata's P625, the centre of LUX's
/// polygon) under <c>pulled</c>. Null when the record places nothing.
/// </summary>
public static class Coordinates
{
    public static (double Lat, double Long)? Of(AuthorityRecord? record)
    {
        if (record?.DataJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(record.DataJson);
            var root = doc.RootElement;
            if (Number(root, "lat") is { } lat && Number(root, "long") is { } lon) return (lat, lon);
            if (root.TryGetProperty(SourcePull.Key, out var pulled) && Number(pulled, "lat") is { } plat && Number(pulled, "long") is { } plon) return (plat, plon);
        }
        catch (JsonException) { }
        return null;
    }

    /// <summary>
    /// The record's own coordinates, or failing those its TGN parent's (<c>broader</c>, up to two levels), marked approximate with the parent's
    /// name; null when nothing places it. About one TGN record in 160 carries no coordinates (2,972,689 of 2,991,010 do), castles and
    /// buildings among them.
    /// </summary>
    public static async Task<Placed?> ResolveAsync(IAlignmentStore store, AuthorityRecord? record, CancellationToken ct = default)
    {
        if (record is null) return null;
        if (Of(record) is { } own) return new Placed(own.Lat, own.Long);
        var current = record;
        for (var depth = 0; depth < 2; depth++)
        {
            if (Broader(current) is not { } parentUri || await store.GetAuthorityAsync(parentUri, ct) is not { } parent) return null;
            if (Of(parent) is { } c) return new Placed(c.Lat, c.Long, parent.Label ?? parentUri);
            current = parent;
        }
        return null;
    }

    /// <summary>
    /// The coordinates of another record of the same name whose TGN parents agree with this one's placement (the owner, 2026-11-08: the
    /// desk's chosen "Hamden (Conn.)", LoC's, had no marker while TGN's seven Hamdens had; LoC records carry no coordinates). Null when
    /// no candidate of the name is placed there.
    /// </summary>
    public static Placed? Borrow(string? placement, string? name, IEnumerable<AuthorityRecord> candidates)
    {
        if (string.IsNullOrWhiteSpace(placement) || string.IsNullOrWhiteSpace(name)) return null;
        var key = StringKey.Normalise(Reconciliation.Qualified.Split(name).Name);
        foreach (var r in candidates.Where(r => r.Source == "tgn"))
        {
            if (r.Label is null || StringKey.Normalise(r.Label) != key || Of(r) is not { } c) continue;
            var parents = ParentString(r);
            if (Reconciliation.Qualified.Agrees(placement, parents) != true) continue;
            var where = parents is null ? "" : " in " + string.Join(", ", parents.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Take(2));
            return new Placed(c.Lat, c.Long, Via: $"TGN's {r.Label}{where}");
        }
        return null;
    }

    private static string? ParentString(AuthorityRecord record)
    {
        if (record.DataJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(record.DataJson);
            return doc.RootElement.TryGetProperty("parentString", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string? Broader(AuthorityRecord record)
    {
        if (record.DataJson is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(record.DataJson);
            return doc.RootElement.TryGetProperty("broader", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>The distance between two points on the earth, in kilometres (the haversine formula; good to well under a kilometre at the distances asked of it).</summary>
    public static double Kilometres((double Lat, double Long) a, (double Lat, double Long) b)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var h = Math.Pow(Math.Sin(Rad(b.Lat - a.Lat) / 2), 2) + Math.Cos(Rad(a.Lat)) * Math.Cos(Rad(b.Lat)) * Math.Pow(Math.Sin(Rad(b.Long - a.Long) / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Sqrt(h));
    }

    /// <summary>The first record among several that places something.</summary>
    public static (double Lat, double Long)? Of(IEnumerable<AuthorityRecord?> records) => records.Select(Of).FirstOrDefault(c => c is not null);

    private static double? Number(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }
}
