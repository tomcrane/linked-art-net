using System.Collections.Concurrent;
using LinkedArtNet.Alignment;
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
    /// <summary>The five checks' lists as they were last made, for the page that only counts them.</summary>
    public sealed record Lists(DateTimeOffset At, IReadOnlyList<PossibleDuplicate> Duplicates, IReadOnlyList<AwayFinding> Away, IReadOnlyList<NotNameEntity> NotNames, IReadOnlyList<NamesakePerson> Namesakes, IReadOnlyList<ThinPlace> Thin);
    private sealed record Kept(DateTimeOffset At, Dictionary<string, List<CheckHit>> ByEntity, Lists Lists);
    private static readonly ConcurrentDictionary<string, Kept> Cache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, bool> Refreshing = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Lazy<Task<Kept>>> First = new(StringComparer.Ordinal);

    public static void Forget(string dataset) => Cache.TryRemove(dataset, out _);

    public static async Task<IReadOnlyList<CheckHit>> ForAsync(ProjectContext project, string? entityId, CancellationToken ct = default)
    {
        if (entityId is null) return [];
        var kept = await KeptAsync(project, ct);
        return kept.ByEntity.TryGetValue(entityId, out var hits) ? hits : [];
    }

    /// <summary>
    /// The lists as kept, for the list of checks, which shows only how much each holds: the page ran every check in full to count it
    /// and took eight to twelve seconds (the desk run of 2026-11-25), more with the fifth. A check's own page still makes its list afresh.
    /// </summary>
    public static async Task<Lists> ListsAsync(ProjectContext project, CancellationToken ct = default) => (await KeptAsync(project, ct)).Lists;

    private static async Task<Kept> KeptAsync(ProjectContext project, CancellationToken ct)
    {
        var dataset = project.Project.Dataset;
        if (!Cache.TryGetValue(dataset, out var kept))
        {
            // the first asking makes the lists, and two pages asking at once wait on the one making (each made its own, twice the work
            // and twice the wait: seen timing the list of checks, 2026-11-27)
            var making = First.GetOrAdd(dataset, _ => new Lazy<Task<Kept>>(() => BuildAsync(project, CancellationToken.None)));
            try { kept = await making.Value.WaitAsync(ct); Cache[dataset] = kept; }
            finally { First.TryRemove(dataset, out _); }
        }
        else if (kept.At < DateTimeOffset.UtcNow.AddMinutes(-2) && Refreshing.TryAdd(dataset, true))
        {
            _ = Task.Run(async () =>
            {
                try { Cache[dataset] = await BuildAsync(project, CancellationToken.None); }
                catch (Exception) { /* the lists as they stood are served until the next asking */ }
                finally { Refreshing.TryRemove(dataset, out _); }
            });
        }
        return kept;
    }

    private static async Task<Kept> BuildAsync(ProjectContext project, CancellationToken ct)
    {
        var dataset = project.Project.Dataset;
        var map = new Dictionary<string, List<CheckHit>>(StringComparer.Ordinal);
        var away = await project.Curation.PlacesAlignedAwayAsync(dataset, project.Manifest.HomePlace, ct);
        var duplicates = await project.Curation.PossibleDuplicatesAsync(dataset, ct);
        var notNames = await project.Curation.NotNameEntitiesAsync(dataset, ct);
        var namesakes = await project.Curation.NamesakePeopleAsync(dataset, ct);
        var thin = await project.Curation.ThinPlacesAsync(dataset, project.Manifest.HomePlace, ct);
        void Add(string id, CheckHit hit) { if (!map.TryGetValue(id, out var list)) map[id] = list = []; list.Add(hit); }
        foreach (var f in away)
            Add(f.Row.EntityId, new CheckHit("Places the import aligned to a namesake", (f.Likely ? "it is likely aligned to the wrong place of its name" : "it may be aligned to another place of its name") + (f.Reason is null ? "" : ": " + f.Reason),
                $"/p/{dataset}/checks?mend={Uri.EscapeDataString(f.Row.EntityId)}", "Decide\u2026"));
        foreach (var d in duplicates)
        {
            var href = $"/p/{dataset}/merge?from=duplicates&ids={Uri.EscapeDataString(d.AId + "," + d.BId)}";
            Add(d.AId, new CheckHit("Entities that may be one thing", $"it may be the same thing as \u201c{d.BLabel ?? d.BId}\u201d ({d.BClass.ToLowerInvariant()})", href, "Compare\u2026"));
            Add(d.BId, new CheckHit("Entities that may be one thing", $"it may be the same thing as \u201c{d.ALabel ?? d.AId}\u201d ({d.AClass.ToLowerInvariant()})", href, "Compare\u2026"));
        }
        foreach (var n in notNames)
            Add(n.EntityId, new CheckHit("Entities that are not names", "every string on it looks like no name at all", $"/p/{dataset}/checks?check=notnames", "Open the check"));
        foreach (var n in namesakes)
            Add(n.EntityId, new CheckHit("People aligned to a namesake", $"an identifier on it was born too late for its first record, of {n.FirstYear}", $"/p/{dataset}/checks?check=namesakes", "Open the check"));
        foreach (var p in thin)
            Add(p.Row.EntityId, new CheckHit("Places that say too little", "none of its identifiers says where it is, and the gazetteer has " + (p.Named.Count == 1 ? "a record" : $"{p.Named.Count} records") + " of its name", $"/p/{dataset}/checks?check=thin", "Open the check"));
        var now = DateTimeOffset.UtcNow;
        return new Kept(now, map, new Lists(now, duplicates, away, notNames, namesakes, thin));
    }
}
