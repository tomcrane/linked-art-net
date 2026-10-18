using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>The project's overrides as a pmc-lux shaped JSON file, so the curated file is an output of review.</summary>
public sealed class ExportOverridesModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    public async Task<IActionResult> OnGetAsync()
    {
        var json = await Project.Curation.ExportOverridesJsonAsync(Dataset);
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", $"{Dataset}-overrides.json");
    }
}
