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
    public const int Limit = 200;
    public int Shown => Groups.Sum(g => g.Strings.Count);
    public IReadOnlyList<AuthorityRecord> IdCandidates { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task OnGetAsync(string? message = null)
    {
        Message = message ?? TakeFlash();
        if (string.IsNullOrWhiteSpace(Seed)) { Seed = null; return; }
        Word = CurationService.SeedWord(Seed);
        Groups = await Project.Curation.VariantsAsync(Dataset, Seed, Limit);
        if (!string.IsNullOrWhiteSpace(Ids)) IdCandidates = await Project.Desk.SearchIndexesAsync(Ids, null, 12);
    }

    public async Task<IActionResult> OnPostClusterAsync(string seed, long[]? strings, string? entity, string? newLabel, string? note, string[]? externalUris, bool anyField = false)
    {
        try
        {
            var result = await Project.Curation.ClusterAsync(Dataset, strings ?? [], entity, newLabel, externalUris ?? [], Reviewer, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), anyField);
            return Flash($"{(strings ?? []).Length} string(s) clustered into {result.Label}.", new { dataset = Dataset, seed });
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset, seed });
        }
    }
}
