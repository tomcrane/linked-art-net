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

    public IReadOnlyList<AwayFinding> Away { get; private set; } = [];
    public string? HomePlace { get; private set; }
    public MendPlan? Plan { get; private set; }
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
            if (Plan is not null) return;
            Message ??= "That entity is not on the list any more.";
        }
        Away = await Project.Curation.PlacesAlignedAwayAsync(Dataset, HomePlace, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostMendAsync(string id, string[]? remove, string home)
    {
        try
        {
            var before = await Services.Store.GetEntityAsync(id, HttpContext.RequestAborted);
            var result = await Project.Curation.MendAwayAsync(Dataset, id, remove ?? [], home, Reviewer, HttpContext.RequestAborted);
            var n = (remove ?? []).Length;
            return Flash(result.Id == id
                ? $"\"{before?.Label}\" mended: {n} identifier{(n == 1 ? "" : "s")} removed, the place at home added."
                : $"\"{before?.Label}\" mended: {n} identifier{(n == 1 ? "" : "s")} removed, and merged into the project's entity for the place at home.", new { dataset = Dataset }, result);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset });
        }
    }
}
