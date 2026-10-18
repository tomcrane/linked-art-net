using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

public sealed class EntityModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? Merge { get; set; }

    public EntityView View { get; private set; } = null!;
    public IReadOnlyList<Entity> MergeCandidates { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? message = null)
    {
        Message = message;
        var view = await Project.Curation.EntityAsync(Dataset, Id);
        if (view is null) return NotFound();
        View = view;
        if (!string.IsNullOrWhiteSpace(Merge))
            MergeCandidates = (await Project.Desk.SearchEntitiesAsync(Dataset, Merge)).Where(e => e.Id != view.Entity.Id).ToList();
        return Page();
    }

    public Task<IActionResult> OnPostLabelAsync(string id, string label) => Act(id, async () => { await Project.Curation.SetLabelAsync(Dataset, id, label); return "Label saved."; });
    public Task<IActionResult> OnPostAddEquivalentAsync(string id, string uri) => Act(id, async () => { await Project.Curation.AddEquivalentAsync(Dataset, id, uri, Reviewer); return "Identifier added."; });
    public Task<IActionResult> OnPostRemoveEquivalentAsync(string id, string uri) => Act(id, async () => { await Project.Curation.RemoveEquivalentAsync(Dataset, id, uri); return "Identifier removed."; });
    public Task<IActionResult> OnPostConfirmAsync(string id, string? note) => Act(id, async () => { await Project.Curation.ConfirmAsync(Dataset, id, Reviewer, note); return "Confirmed."; });
    public Task<IActionResult> OnPostNoteAsync(string id, string text) => Act(id, async () => { if (!string.IsNullOrWhiteSpace(text)) await Project.Curation.NoteAsync(Dataset, id, Reviewer, text.Trim()); return "Note added."; });
    public Task<IActionResult> OnPostReopenAsync(string id, long[]? strings) => Act(id, async () => { var n = await Project.Curation.ReopenAsync(Dataset, strings ?? [], HttpContext.RequestAborted); return $"{n} string(s) reopened."; });

    public async Task<IActionResult> OnPostMergeAsync(string id, string survivor)
    {
        try
        {
            var result = await Project.Curation.MergeAsync(Dataset, id, survivor, Reviewer);
            return RedirectToPage(new { dataset = Dataset, id = result.Id, message = "Merged." });
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return RedirectToPage(new { dataset = Dataset, id, message = e.Message });
        }
    }

    private async Task<IActionResult> Act(string id, Func<Task<string>> action)
    {
        string message;
        try { message = await action(); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { message = e.Message; }
        return RedirectToPage(new { dataset = Dataset, id, message });
    }
}
