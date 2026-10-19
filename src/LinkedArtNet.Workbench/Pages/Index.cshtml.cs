using System.Text.RegularExpressions;
using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Alignment.Store;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.Workbench.Pages;

public sealed partial class IndexModel(IAlignmentStore store, ProjectAccess access, ProjectServices services, WorkbenchOptions options) : PageModel
{
    public IReadOnlyList<(Project Project, string Role)> Projects { get; private set; } = [];
    public bool IsOwner => options.IsOwner(User.Identity?.Name);
    public string? Error { get; private set; }

    public async Task OnGetAsync() => Projects = await access.ProjectsForAsync(User.Identity?.Name);

    public async Task<IActionResult> OnPostAsync(string dataset, string name, string identifierBase, string? description)
    {
        if (!IsOwner) return Forbid();
        dataset = (dataset ?? "").Trim().ToLowerInvariant();
        if (!DatasetKey().IsMatch(dataset) || string.IsNullOrWhiteSpace(name) || !Uri.TryCreate(identifierBase, UriKind.Absolute, out var baseUri))
        {
            Error = "A dataset key is lower-case letters, digits and hyphens; the identifier base must be an absolute URL.";
            await OnGetAsync();
            return Page();
        }
        var now = DateTimeOffset.UtcNow;
        await store.UpsertProjectAsync(new Project(dataset, name.Trim(), string.IsNullOrWhiteSpace(description) ? null : description.Trim(), baseUri.ToString(), new ProjectManifest().ToJson(), now, now));
        services.Invalidate(dataset);
        return Redirect("/p/" + dataset);
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,40}$")] private static partial Regex DatasetKey();
}
