using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>A page inside a project: loads the project and the signed-in user's role before any handler runs, and refuses when the role is below what the page needs.</summary>
public abstract class ProjectPageModel(ProjectServices services, ProjectAccess access) : PageModel
{
    protected ProjectServices Services => services;

    [BindProperty(SupportsGet = true)] public string Dataset { get; set; } = "";
    public ProjectContext Project { get; private set; } = null!;
    public string Role { get; private set; } = "";
    public string Reviewer => User.Identity?.Name ?? "anonymous";

    /// <summary>The least role the page needs.</summary>
    protected virtual string RequiredRole => ProjectRole.Reviewer;

    public bool Can(string role) => ProjectRole.Covers(Role, role);

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var project = await services.GetAsync(Dataset, HttpContext.RequestAborted);
        if (project is null) { context.Result = NotFound(); return; }
        var role = await access.RoleAsync(Dataset, User.Identity?.Name, HttpContext.RequestAborted);
        if (role is null || !ProjectRole.Covers(role, RequiredRole)) { context.Result = Forbid(); return; }
        Project = project;
        Role = role;
        ViewData["Dataset"] = Dataset;
        ViewData["ProjectName"] = project.Project.Name;
        await next();
    }
}
