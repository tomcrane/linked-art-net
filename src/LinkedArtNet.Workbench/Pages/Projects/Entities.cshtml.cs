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
    public IReadOnlyList<EntityCount> Counts { get; private set; } = [];
    public long Total => Counts.Sum(c => c.Count);
    /// <summary>How many entities the current status and class filters select.</summary>
    public long Matching => Counts.Where(c => (Status is null || c.Status == Status) && (Class is null || c.Class == Class)).Sum(c => c.Count);
    public long ByStatus(EntityStatus s) => Counts.Where(c => c.Status == s && (Class is null || c.Class == Class)).Sum(c => c.Count);
    public long ByClass(EntityClass k) => Counts.Where(c => c.Class == k && (Status is null || c.Status == Status)).Sum(c => c.Count);

    public async Task OnGetAsync()
    {
        Counts = await Services.Store.EntityCountsAsync(Dataset);
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var found = await Project.Desk.SearchEntitiesAsync(Dataset, Q, 100);
            Entities = found.Where(e => (Status is null || e.Status == Status) && (Class is null || e.Class == Class)).ToList();
        }
        else Entities = await Services.Store.BrowseEntitiesAsync(Dataset, Status, Class, After, PageSize);
    }
}
