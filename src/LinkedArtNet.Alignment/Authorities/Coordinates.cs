using System.Globalization;
using System.Text.Json;

namespace LinkedArtNet.Alignment.Authorities;

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
