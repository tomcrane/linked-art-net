using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

public sealed class EntitiesModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    public const int PageSize = 50;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public EntityStatus? Status { get; set; }
    [BindProperty(SupportsGet = true, Name = "cls")] public EntityClass? Class { get; set; }
    [BindProperty(SupportsGet = true)] public string? After { get; set; }

    public IReadOnlyList<Entity> Entities { get; private set; } = [];

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var found = await Project.Desk.SearchEntitiesAsync(Dataset, Q, 100);
            Entities = found.Where(e => (Status is null || e.Status == Status) && (Class is null || e.Class == Class)).ToList();
        }
        else Entities = await Services.Store.BrowseEntitiesAsync(Dataset, Status, Class, After, PageSize);
    }
}
