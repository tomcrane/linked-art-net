using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// The review desk (design doc 12, section 5): one queued string, the reconciler's evidence beside it, and the
/// actions. Navigation is by string id: <c>id</c> pins an item, <c>after</c> asks for the next one in the filtered
/// queue. Every action redirects to the next item so the reviewer keeps moving; undo pins the item it restored.
/// </summary>
public sealed class ReviewModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    [BindProperty(SupportsGet = true)] public string Method { get; set; } = DecisionMethod.Queued;
    [BindProperty(SupportsGet = true)] public string? Context { get; set; }
    [BindProperty(SupportsGet = true)] public long After { get; set; }
    [BindProperty(SupportsGet = true)] public long? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    public ReviewItem? Item { get; private set; }
    public IReadOnlyList<ContextMethodCount> Queue { get; private set; } = [];
    public IReadOnlyList<Entity> EntityMatches { get; private set; } = [];
    public IReadOnlyList<AuthorityRecord> IndexMatches { get; private set; } = [];
    public string? Message { get; private set; }

    public long Remaining => Queue.Where(r => Context is null || r.Context == Context).Sum(r => r.Count);

    public async Task OnGetAsync(string? message = null)
    {
        Message = message ?? TakeFlash();
        if (string.IsNullOrWhiteSpace(Context)) Context = null;
        Queue = await Project.Desk.QueueCountsAsync(Dataset, Method);
        Item = Id is { } id ? await Project.Desk.GetAsync(id) : await Project.Desk.NextAsync(Dataset, Method, Context, After);
        if (Item is not null && Item.String.Dataset != Dataset) Item = null;
        if (Item is not null && !string.IsNullOrWhiteSpace(Q))
        {
            EntityMatches = await Project.Desk.SearchEntitiesAsync(Dataset, Q);
            IndexMatches = await Project.Desk.SearchIndexesAsync(Q);
        }
    }

    public Task<IActionResult> OnPostAcceptAsync(long id, string target, string? note) =>
        Act(id, async () => { await Project.Desk.AcceptAsync(Dataset, id, target, Reviewer, note); return "Accepted."; });

    public Task<IActionResult> OnPostSameAsAsync(long id, string entity, string? note) =>
        Act(id, async () => { await Project.Desk.SameAsAsync(Dataset, id, entity, Reviewer, note); return "Marked as the same entity."; });

    public Task<IActionResult> OnPostLocalAsync(long id, string? label, string? note) =>
        Act(id, async () => { await Project.Desk.KeepLocalAsync(Dataset, id, Reviewer, label, note); return "Kept as a local authority."; });

    public Task<IActionResult> OnPostOverrideAsync(long id, string target, string? note) =>
        Act(id, async () => { await Project.Desk.OverrideAsync(Dataset, id, target, Reviewer, note); return "Override saved and applied."; });

    public async Task<IActionResult> OnPostFlagAsync(long id, string text)
    {
        if (!string.IsNullOrWhiteSpace(text)) await Project.Desk.FlagAsync(Dataset, id, Reviewer, text.Trim());
        return Flash("Note added.", new { dataset = Dataset, method = Method, context = Context, id });
    }

    public IActionResult OnPostSkip(long id) => RedirectToPage(new { dataset = Dataset, method = Method, context = Context, after = id });

    public async Task<IActionResult> OnPostUndoAsync(long id)
    {
        Decision? restored;
        try { restored = await Project.Desk.UndoAsync(Dataset, id); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
        return Flash(restored is null ? "Nothing to undo." : $"Undone; the earlier decision ({restored.Method}) stands again.", new { dataset = Dataset, method = Method, context = Context, id });
    }

    private async Task<IActionResult> Act(long id, Func<Task<string>> action)
    {
        string message;
        try { message = await action(); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id });
        }
        return Flash(message, new { dataset = Dataset, method = Method, context = Context, after = id });
    }
}
