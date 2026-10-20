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
    /// <summary>What the project is being read with right now, in a sentence: no manifest, the toolkit's current PMC library rules, or the project's own with how they differ.</summary>
    public string RulesInEffect { get; private set; } = "";
    public IReadOnlyList<string> RulesDifferences { get; private set; } = [];

    public async Task OnGetAsync(string? message = null)
    {
        ManifestError = message ?? TakeFlash();
        Queue = await Project.Desk.QueueCountsAsync(Dataset);
        Counts = await Services.Store.CountsAsync(Dataset);
        Runs = await Services.Store.RunsAsync(Dataset, 8);
        ManifestJson = Project.Manifest.ToJson();
        var saved = Project.Project.ManifestJson is not null;
        RulesDifferences = saved ? Project.Manifest.Differences(ProjectManifest.PmcDefault, "the saved manifest", "the library rules") : [];
        var when = $"saved {Project.Project.UpdatedAt:yyyy-MM-dd HH:mm} UTC";
        RulesInEffect = !saved ? "No manifest is saved: a string from any field may name anything, no field is read as qualified or made of parts, and all loaded indexes are consulted."
            : RulesDifferences.Count == 0 ? $"The PMC library rules as the toolkit currently defines them ({when})."
            : $"This project's own rules ({when}). They differ from the toolkit's current PMC library rules: {string.Join("; ", RulesDifferences)}.";
    }

    public Task<IActionResult> OnPostManifestAsync(string manifest) => SaveManifestAsync(() => ProjectManifest.FromJson(manifest), "Manifest saved.");

    public Task<IActionResult> OnPostPmcRulesAsync() => SaveManifestAsync(() => ProjectManifest.PmcDefault, "The PMC library rules are in place for this project.");

    private async Task<IActionResult> SaveManifestAsync(Func<ProjectManifest> read, string done)
    {
        if (!Can(ProjectRole.Operator)) return Forbid();
        ProjectManifest parsed;
        try { parsed = read(); }
        catch (System.Text.Json.JsonException e) { return Flash("The manifest is not valid JSON: " + e.Message, new { dataset = Dataset }); }
        var p = Project.Project;
        await Services.Store.UpsertProjectAsync(p with { ManifestJson = parsed.ToJson(), UpdatedAt = DateTimeOffset.UtcNow });
        Services.Invalidate(Dataset);
        return Flash(done, new { dataset = Dataset });
    }
}
