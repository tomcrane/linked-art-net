using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// The project's search box (the owner, 2026-11-19: "I'm frequently reaching for a top-level search box, rather than a click first into
/// entities. But a top-level (or project) search box might be expected to give other results in other contexts"). One query, and the
/// results in sections by kind so that the box need not guess which was meant: strings as the records write them, each with a link to
/// its card (the step the owner was making by way of the entities), entities, and records. The authority indexes are not searched
/// here: they are searched where a string is being decided.
/// </summary>
public sealed class SearchModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public IReadOnlyList<StringHit> Strings { get; private set; } = [];
    public IReadOnlyList<Entity> Entities { get; private set; } = [];
    public IReadOnlyList<RecordHit> Records { get; private set; } = [];
    public Dictionary<string, (string? Placement, int Strings)> Clues { get; } = new(StringComparer.Ordinal);
    public const int StringLimit = 40;

    public async Task<IActionResult> OnGetAsync()
    {
        Q = Q?.Trim();
        if (string.IsNullOrEmpty(Q) || Q.Length < 2) return Page();
        var ct = HttpContext.RequestAborted;
        Strings = await Services.Store.SearchStringsAsync(Dataset, Q, StringLimit, ct);
        // a string's own empty placeholder is not a second thing found: the string is listed, and says it waits (seen in the browser,
        // 2026-11-19: "Matador" found its one string and, as an entity, the placeholder that stands for nothing but that string)
        Entities = (await Project.Desk.SearchEntitiesAsync(Dataset, Q, 20, ct))
            .Where(e => e.MergedInto is null && !(e.Status == EntityStatus.Provisional && e.Equivalents.Count == 0 && Strings.Any(h => h.EntityId == e.Id))).ToList();
        Records = await Services.Store.SearchRecordsAsync(Dataset, Q, 15, ct);
        // one string and nothing else: its card is what was wanted
        if (Strings.Count == 1 && Entities.Count == 0 && Records.Count == 0) return Redirect($"/p/{Dataset}/review?id={Strings[0].String.Id}");
        foreach (var e in Entities.Take(12)) Clues[e.Id] = await Project.Desk.ClueAsync(e, ct);
        return Page();
    }
}
