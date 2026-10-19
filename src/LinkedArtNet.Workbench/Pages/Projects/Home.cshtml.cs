using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

public sealed class HomeModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    public IReadOnlyList<ContextMethodCount> Queue { get; private set; } = [];
    public StoreCounts Counts { get; private set; } = new(0, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<Run> Runs { get; private set; } = [];
    public string ManifestJson { get; private set; } = "";
    public string? ManifestError { get; private set; }

    public async Task OnGetAsync(string? message = null)
    {
        ManifestError = message;
        Queue = await Project.Desk.QueueCountsAsync(Dataset);
        Counts = await Services.Store.CountsAsync(Dataset);
        Runs = await Services.Store.RunsAsync(Dataset, 8);
        ManifestJson = Project.Manifest.ToJson();
    }

    public Task<IActionResult> OnPostManifestAsync(string manifest) => SaveManifestAsync(() => ProjectManifest.FromJson(manifest), "Manifest saved.");

    public Task<IActionResult> OnPostPmcRulesAsync() => SaveManifestAsync(() => ProjectManifest.PmcDefault, "The PMC library rules are in place for this project.");

    private async Task<IActionResult> SaveManifestAsync(Func<ProjectManifest> read, string done)
    {
        if (!Can(ProjectRole.Operator)) return Forbid();
        ProjectManifest parsed;
        try { parsed = read(); }
        catch (System.Text.Json.JsonException e) { return RedirectToPage(new { dataset = Dataset, message = "The manifest is not valid JSON: " + e.Message }); }
        var p = Project.Project;
        await Services.Store.UpsertProjectAsync(p with { ManifestJson = parsed.ToJson(), UpdatedAt = DateTimeOffset.UtcNow });
        Services.Invalidate(Dataset);
        return RedirectToPage(new { dataset = Dataset, message = done });
    }
}
