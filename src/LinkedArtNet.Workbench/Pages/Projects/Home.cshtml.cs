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
    /// <summary>The project's recent hand changes, newest first: notes written by the audit (label, class, identifiers, merges, reopens) and decisions a person made at the desk (doc 12 section 13, item 1).</summary>
    public IReadOnlyList<(DateTimeOffset When, string Who, string What, string Link, string LinkText)> RecentChanges { get; private set; } = [];
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
        var changes = new List<(DateTimeOffset, string, string, string, string)>();
        foreach (var n in await Services.Store.RecentNotesAsync(Dataset, 30))
        {
            if (n.Target.StartsWith("string:", StringComparison.Ordinal))
                changes.Add((n.CreatedAt, n.Author ?? "", n.Text, $"/p/{Dataset}/review?id={n.Target[7..]}", "the string"));
            else
            {
                var entity = await Services.Store.ResolveEntityAsync(n.Target);
                changes.Add((n.CreatedAt, n.Author ?? "", n.Text, $"/p/{Dataset}/entity?id={Uri.EscapeDataString(n.Target)}", entity?.Label ?? n.Target));
            }
        }
        foreach (var d in await Services.Store.RecentHumanDecisionsAsync(Dataset, 30))
            changes.Add((d.Decision.DecidedAt, d.Decision.Reviewer ?? "", $"\"{d.String.Raw}\" ({d.String.Context}) decided: {d.Decision.Method}", $"/p/{Dataset}/review?id={d.String.Id}", "the string"));
        RecentChanges = changes.OrderByDescending(c => c.Item1).Take(30).ToList();
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
