using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

public sealed class VariantsModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    [BindProperty(SupportsGet = true)] public string? Seed { get; set; }
    [BindProperty(SupportsGet = true)] public string? Ids { get; set; }

    public string Word { get; private set; } = "";
    public IReadOnlyList<VariantGroup> Groups { get; private set; } = [];
    public IReadOnlyList<AuthorityRecord> IdCandidates { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task OnGetAsync(string? message = null)
    {
        Message = message;
        if (string.IsNullOrWhiteSpace(Seed)) { Seed = null; return; }
        Word = CurationService.SeedWord(Seed);
        Groups = await Project.Curation.VariantsAsync(Dataset, Seed);
        if (!string.IsNullOrWhiteSpace(Ids)) IdCandidates = await Project.Desk.SearchIndexesAsync(Ids, null, 12);
    }

    public async Task<IActionResult> OnPostClusterAsync(string seed, long[]? strings, string? entity, string? newLabel, string? note, string[]? externalUris)
    {
        try
        {
            var result = await Project.Curation.ClusterAsync(Dataset, strings ?? [], entity, newLabel, externalUris ?? [], Reviewer, string.IsNullOrWhiteSpace(note) ? null : note.Trim());
            return RedirectToPage(new { dataset = Dataset, seed, message = $"{(strings ?? []).Length} string(s) clustered into {result.Label}." });
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return RedirectToPage(new { dataset = Dataset, seed, message = e.Message });
        }
    }
}
