using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// Checks on the project's own entities (the owner, 2026-11-15): lists of entities that a named test finds doubtful, each row leading
/// to the entity's page, where it is mended or confirmed. Not the cataloguers' list (doc 12 section 14.4), which is of things the
/// source records could say better. The first check: places the old import aligned to a namesake.
/// </summary>
public sealed class ChecksModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    public IReadOnlyList<(AwayRow Row, bool Likely)> Away { get; private set; } = [];
    public string? HomePlace { get; private set; }

    public async Task OnGetAsync()
    {
        HomePlace = Project.Manifest.HomePlace;
        Away = await Project.Curation.PlacesAlignedAwayAsync(Dataset, HomePlace, HttpContext.RequestAborted);
    }
}
