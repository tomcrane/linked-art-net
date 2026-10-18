using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

public sealed class MembersModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    protected override string RequiredRole => ProjectRole.Owner;

    public IReadOnlyList<Membership> Members { get; private set; } = [];

    public async Task OnGetAsync() => Members = await Services.Store.MembershipsAsync(Dataset);

    public async Task<IActionResult> OnPostAddAsync(string login, string role)
    {
        login = (login ?? "").Trim();
        if (login.Length is > 0 and <= 60 && ProjectRole.All.Contains(role))
        {
            await Services.Store.UpsertUserAsync(login, null);
            await Services.Store.SetMembershipAsync(Dataset, login, role);
        }
        return RedirectToPage(new { dataset = Dataset });
    }

    public async Task<IActionResult> OnPostRemoveAsync(string login)
    {
        await Services.Store.RemoveMembershipAsync(Dataset, login);
        return RedirectToPage(new { dataset = Dataset });
    }
}
