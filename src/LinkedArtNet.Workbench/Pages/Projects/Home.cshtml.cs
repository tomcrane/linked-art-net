using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;

namespace LinkedArtNet.Workbench.Pages.Projects;

public sealed class HomeModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    public IReadOnlyList<ContextMethodCount> Queue { get; private set; } = [];
    public StoreCounts Counts { get; private set; } = new(0, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<Run> Runs { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Queue = await Project.Desk.QueueCountsAsync(Dataset);
        Counts = await Services.Store.CountsAsync(Dataset);
        Runs = await Services.Store.RunsAsync(Dataset, 8);
    }
}
