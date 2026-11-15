using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// Checks on the project's own entities (the owner, 2026-11-15): lists of entities that a named test finds doubtful, each row leading
/// to the entity's page, where it is mended or confirmed, or to "Mend", which shows what it would do and does it on a person's word.
/// Not the cataloguers' list (doc 12 section 14.4), which is of things the source records could say better. The first check: places
/// the old import aligned to a namesake.
/// </summary>
public sealed class ChecksModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    [BindProperty(SupportsGet = true)] public string? Mend { get; set; }
    /// <summary>A search of the project's entities on the Mend screen, for a place known by another name (the owner, 2026-11-15, at "Bâle", which is Basel).</summary>
    [BindProperty(SupportsGet = true)] public string? Find { get; set; }
    public IReadOnlyList<Entity> Found { get; private set; } = [];

    public IReadOnlyList<AwayFinding> Away { get; private set; } = [];
    public string? HomePlace { get; private set; }
    public MendPlan? Plan { get; private set; }
    /// <summary>The strings that stand on the entity being mended, each with up to three of the records it is in (the owner, 2026-11-15: "it would help to re-list the record context on the Mend page").</summary>
    public List<(SourceString String, IReadOnlyList<StringExample> Records, int Count)> PlanStrings { get; } = [];
    public Dictionary<string, (string? Placement, int Strings)> Clues { get; } = new(StringComparer.Ordinal);
    public string? Message { get; private set; }
    public (string Id, string Label)? MessageEntity { get; private set; }

    public async Task OnGetAsync()
    {
        Message = TakeFlash();
        MessageEntity = TakeFlashEntity();
        HomePlace = Project.Manifest.HomePlace;
        if (!string.IsNullOrWhiteSpace(Mend))
        {
            try { Plan = await Project.Curation.MendPlanAsync(Dataset, Mend, HomePlace, HttpContext.RequestAborted); }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException) { Message = e.Message; }
            if (Plan is not null)
            {
                foreach (var s in (await Services.Store.StringsForEntityAsync(Plan.Entity.Id, 12, HttpContext.RequestAborted)).OrderBy(s => s.String.Raw, StringComparer.Ordinal))
                    PlanStrings.Add((s.String, await Project.Desk.ExamplesForAsync(s.String.Id, HttpContext.RequestAborted), await Services.Store.RecordCountAsync(s.String.Id, HttpContext.RequestAborted)));
                if (!string.IsNullOrWhiteSpace(Find))
                    Found = (await Project.Desk.SearchEntitiesAsync(Dataset, Find.Trim())).Where(e => e.Class == EntityClass.Place && e.MergedInto is null && e.Id != Plan.Entity.Id && Plan.Others.All(o => o.Id != e.Id)).Take(12).ToList();
                foreach (var other in Plan.Others.Concat(Plan.Holders.Values).Concat(Found)) Clues[other.Id] = await Project.Desk.ClueAsync(other, HttpContext.RequestAborted);
                return;
            }
            Message ??= "That entity is not on the list any more.";
        }
        Away = await Project.Curation.PlacesAlignedAwayAsync(Dataset, HomePlace, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostRelabelAsync(string id, string label)
    {
        try
        {
            var result = await Project.Curation.RelabelAndConfirmAsync(Dataset, id, label, Reviewer, HttpContext.RequestAborted);
            return Flash($"Relabelled \"{result.Label}\" and confirmed.", new { dataset = Dataset }, result);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset });
        }
    }

    public async Task<IActionResult> OnPostMendAsync(string id, string[]? remove, string? home, string? otherUri, long[]? strings, string? act)
    {
        try
        {
            var before = await Services.Store.GetEntityAsync(id, HttpContext.RequestAborted);
            if (string.IsNullOrWhiteSpace(home)) throw new InvalidOperationException("Choose the place meant.");
            if (act == "move")
            {
                var moved = await Project.Curation.MoveStringsAsync(Dataset, id, strings ?? [], home == "other" ? otherUri ?? "" : home, Reviewer, HttpContext.RequestAborted);
                var m = (strings ?? []).Length;
                return Flash($"{m} string{(m == 1 ? "" : "s")} moved from \"{before?.Label}\" to \"{moved.Label}\"; \"{before?.Label}\" and its identifiers are as they were.", new { dataset = Dataset }, moved);
            }
            var result = home.StartsWith("entity:", StringComparison.Ordinal)
                ? await Project.Curation.MendIntoAsync(Dataset, id, remove ?? [], home["entity:".Length..], Reviewer, HttpContext.RequestAborted)
                : await Project.Curation.MendAwayAsync(Dataset, id, remove ?? [], home == "other" ? otherUri ?? "" : home, Reviewer, HttpContext.RequestAborted);
            var n = (remove ?? []).Length;
            return Flash(result.Id == id
                ? $"\"{before?.Label}\" mended: {n} identifier{(n == 1 ? "" : "s")} removed, the identifier of the place meant added."
                : $"\"{before?.Label}\" mended: {n} identifier{(n == 1 ? "" : "s")} removed, and merged into the project's entity for the place meant.", new { dataset = Dataset }, result);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            // back to the confirm screen with what went wrong, the list not reloaded in its place
            return Flash(e.Message, new { dataset = Dataset, mend = id });
        }
    }
}
