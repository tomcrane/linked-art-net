using System.Collections.Concurrent;
using LinkedArtNet.Alignment.Reconciliation;

namespace LinkedArtNet.Workbench.Services;

/// <summary>One check's word on an entity: the check, what it says of the entity, and where it is decided.</summary>
public sealed record CheckHit(string Title, string Says, string Href, string Action);

/// <summary>
/// Which of a project's entities are on a check's list, so that an entity's page and a string's card can say so (the owner,
/// 2026-11-22: "An entity page or string review should indicate to the user that the string/entity is in the checks queue").
/// The lists take seconds to make, so they are kept: made at the first asking, served as they stand afterwards and made again
/// behind the page once they are two minutes old, and dropped when the Checks or the merge page changes something.
/// </summary>
public static class CheckLists
{
    private sealed record Kept(DateTimeOffset At, Dictionary<string, List<CheckHit>> ByEntity);
    private static readonly ConcurrentDictionary<string, Kept> Cache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, bool> Refreshing = new(StringComparer.Ordinal);

    public static void Forget(string dataset) => Cache.TryRemove(dataset, out _);

    public static async Task<IReadOnlyList<CheckHit>> ForAsync(ProjectContext project, string? entityId, CancellationToken ct = default)
    {
        if (entityId is null) return [];
        var dataset = project.Project.Dataset;
        if (!Cache.TryGetValue(dataset, out var kept))
        {
            kept = new Kept(DateTimeOffset.UtcNow, await BuildAsync(project, ct));
            Cache[dataset] = kept;
        }
        else if (kept.At < DateTimeOffset.UtcNow.AddMinutes(-2) && Refreshing.TryAdd(dataset, true))
        {
            _ = Task.Run(async () =>
            {
                try { Cache[dataset] = new Kept(DateTimeOffset.UtcNow, await BuildAsync(project, CancellationToken.None)); }
                catch (Exception) { /* the lists as they stood are served until the next asking */ }
                finally { Refreshing.TryRemove(dataset, out _); }
            });
        }
        return kept.ByEntity.TryGetValue(entityId, out var hits) ? hits : [];
    }

    private static async Task<Dictionary<string, List<CheckHit>>> BuildAsync(ProjectContext project, CancellationToken ct)
    {
        var dataset = project.Project.Dataset;
        var map = new Dictionary<string, List<CheckHit>>(StringComparer.Ordinal);
        void Add(string id, CheckHit hit) { if (!map.TryGetValue(id, out var list)) map[id] = list = []; list.Add(hit); }
        foreach (var f in await project.Curation.PlacesAlignedAwayAsync(dataset, project.Manifest.HomePlace, ct))
            Add(f.Row.EntityId, new CheckHit("Places the import aligned to a namesake", (f.Likely ? "it is likely aligned to the wrong place of its name" : "it may be aligned to another place of its name") + (f.Reason is null ? "" : ": " + f.Reason),
                $"/p/{dataset}/checks?mend={Uri.EscapeDataString(f.Row.EntityId)}", "Decide\u2026"));
        foreach (var d in await project.Curation.PossibleDuplicatesAsync(dataset, ct))
        {
            var href = $"/p/{dataset}/merge?from=duplicates&ids={Uri.EscapeDataString(d.AId + "," + d.BId)}";
            Add(d.AId, new CheckHit("Entities that may be one thing", $"it may be the same thing as \u201c{d.BLabel ?? d.BId}\u201d ({d.BClass.ToLowerInvariant()})", href, "Compare\u2026"));
            Add(d.BId, new CheckHit("Entities that may be one thing", $"it may be the same thing as \u201c{d.ALabel ?? d.AId}\u201d ({d.AClass.ToLowerInvariant()})", href, "Compare\u2026"));
        }
        foreach (var n in await project.Curation.NotNameEntitiesAsync(dataset, ct))
            Add(n.EntityId, new CheckHit("Entities that are not names", "every string on it looks like no name at all", $"/p/{dataset}/checks?check=notnames", "Open the check"));
        return map;
    }
}
