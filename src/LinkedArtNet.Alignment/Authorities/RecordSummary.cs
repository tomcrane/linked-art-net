using System.Text.Json;

namespace LinkedArtNet.Alignment.Authorities;

/// <summary>
/// What a record is, in a phrase, for wherever an identifier would otherwise stand as a bare number (the owner, 2026-11-13, at the two
/// Dagenhams: one entity held Wikidata's town, the other its parliamentary constituency of the same name, and the merge's confirm step
/// showed "wikidata's Q3403624" and "wikidata's Q1075299"). The record's label, then the most telling thing the store holds about it:
/// the description (Wikidata's own, or one a pull brought), TGN's first two parents, or its dates.
/// </summary>
public static class RecordSummary
{
    public static string? Says(AuthorityRecord? record)
    {
        if (record is null) return null;
        var label = string.IsNullOrWhiteSpace(record.Label) ? null : record.Label.Trim();
        string? description = null, parents = null;
        if (record.DataJson is not null)
        {
            try
            {
                using var doc = JsonDocument.Parse(record.DataJson);
                var root = doc.RootElement;
                description = Text(root, "description") ?? (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(SourcePull.Key, out var pulled) ? Text(pulled, "description") : null);
                if (Text(root, "parentString") is { } p)
                    parents = string.Join(", ", p.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Take(2));
            }
            catch (JsonException) { }
        }
        var detail = description ?? parents;
        if (label is null) return detail;
        return detail is null ? label : description is not null ? $"{label}: {description}" : $"{label}, {parents}";
    }

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;
}
