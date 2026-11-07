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
    /// <summary>Show the string's evidence as today's rules would give it, deciding nothing.</summary>
    [BindProperty(SupportsGet = true)] public bool Look { get; set; }
    /// <summary>Accepting the ticked together with two or more of the project's own entities among them comes here first: the plan is shown and the reviewer confirms (the owner, 2026-11-06, at "Chris Beetles Ltd").</summary>
    [BindProperty(SupportsGet = true)] public bool Confirm { get; set; }
    [BindProperty(SupportsGet = true)] public string[]? Targets { get; set; }
    [BindProperty(SupportsGet = true)] public string? Survivor { get; set; }
    /// <summary>"Always, in this field" on a group: accepted together and the spelling overridden to the entity in this field.</summary>
    [BindProperty(SupportsGet = true)] public bool Always { get; set; }

    public ReviewItem? Item { get; private set; }
    public IReadOnlyList<ContextMethodCount> Queue { get; private set; } = [];
    public IReadOnlyList<Entity> EntityMatches { get; private set; } = [];
    public IReadOnlyList<AuthorityRecord> IndexMatches { get; private set; } = [];
    public string? Message { get; private set; }
    /// <summary>What accepting the ticked together will do, when the desk is asking the reviewer to confirm it.</summary>
    public TogetherPlan? Plan { get; private set; }
    /// <summary>The entity the previous action made or chose, for the banner's link to it.</summary>
    public (string Id, string Label)? Decided { get; private set; }

    public long Remaining => Queue.Where(r => Context is null || r.Context == Context).Sum(r => r.Count);

    public async Task OnGetAsync(string? message = null)
    {
        Message = message ?? TakeFlash();
        Decided = TakeFlashEntity();
        if (string.IsNullOrWhiteSpace(Context)) Context = null;
        Queue = await Project.Desk.QueueCountsAsync(Dataset, Method);
        Item = Id is { } id ? (Look ? await Project.Desk.LookAgainAsync(Dataset, id) : await Project.Desk.GetAsync(id)) : await Project.Desk.NextAsync(Dataset, Method, Context, After);
        if (Item is not null && Item.String.Dataset != Dataset) Item = null;
        if (Item is not null && !string.IsNullOrWhiteSpace(Q))
        {
            // the string's own placeholder is under "Currently" already, and "Same as" onto it would decide nothing: left out of the results
            // (the owner, 2026-11-05, "Lawrence" beside its own provisional entity; the desk's form of finding 104)
            EntityMatches = (await Project.Desk.SearchEntitiesAsync(Dataset, Q)).Where(e => e.Id != Item.Entity?.Id).ToList();
            IndexMatches = await Project.Desk.SearchIndexesAsync(Q);
        }
        if (Item is not null && Confirm && Targets is { Length: > 0 })
        {
            try { Plan = await Project.Desk.PlanTogetherAsync(Dataset, Item.String.Id, Targets, Survivor); }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { Message = e.Message; }
        }
    }

    public Task<IActionResult> OnPostAcceptAsync(long id, string target, string? note) =>
        Act(id, async () => ("Accepted.", await Project.Desk.AcceptAsync(Dataset, id, target, Reviewer, note)));

    public async Task<IActionResult> OnPostAcceptTogetherAsync(long id, string[]? targets, string? note, string? survivor, bool confirmed, bool always, EntityClass? cls = null)
    {
        // a group's tick carries its members' URIs in one value, space-joined (the group is the card, 2026-11-06)
        var all = (targets ?? []).SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct(StringComparer.Ordinal).ToArray();
        // two or more of the project's own entities among the ticked are a merge: shown first, done on the reviewer's word. Entities, not
        // URIs: two rows for one entity (a merge since the evidence was written) are no merge (the owner, 2026-11-06, sent to a confirm
        // step with one entity on it)
        var merging = false;
        if (all.Count(Project.Scheme.Owns) > 1 && !confirmed)
        {
            try { merging = (await Project.Desk.PlanTogetherAsync(Dataset, id, all, survivor)).Merging.Count > 0; }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
            // the confirm step lands in view: the page scrolls to it, and a line at the top says it is there (the owner, 2026-11-07: "I'd be lost")
            if (merging) return RedirectToPage(pageName: null, pageHandler: null, routeValues: new { dataset = Dataset, method = Method, context = Context, id, confirm = true, targets = all, survivor, always }, fragment: "confirm-together");
        }
        return await Act(id, async () =>
        {
            var entity = await Project.Desk.AcceptTogetherAsync(Dataset, id, all, Reviewer, note, survivor, mergeOurs: true, always, cls);
            var message = confirmed ? $"Merged and accepted together: {entity.Label}, with {entity.Equivalents.Count} identifiers." : $"Accepted together, with {entity.Equivalents.Count} identifiers.";
            return (always ? message + " This spelling will always mean it in this field." : message, entity);
        });
    }

    public Task<IActionResult> OnPostSameAsAsync(long id, string entity, string? note) =>
        Act(id, async () => ("Marked as the same entity.", await Project.Desk.SameAsAsync(Dataset, id, entity, Reviewer, note)));

    public Task<IActionResult> OnPostLocalAsync(long id, string? label, string? note) =>
        Act(id, async () => ("Kept as a local authority.", await Project.Desk.KeepLocalAsync(Dataset, id, Reviewer, label, note)));

    public Task<IActionResult> OnPostOverrideAsync(long id, string target, string? note) =>
        Act(id, async () => ("Override saved and applied.", await Project.Desk.OverrideAsync(Dataset, id, target, Reviewer, note)));

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

    private async Task<IActionResult> Act(long id, Func<Task<(string Message, Entity Entity)>> action)
    {
        (string Message, Entity Entity) done;
        try { done = await action(); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id });
        }
        // the desk finishes a name before it leaves it: the other queued strings under this one's name come next, in list order, and only
        // then the list goes on (the owner, 2026-11-06, after the first merge at "Chris Beetles Ltd")
        var siblings = await Project.Desk.SiblingsAsync(Dataset, id, done.Entity);
        if (siblings.Count == 0) return Flash(done.Message, new { dataset = Dataset, method = Method, context = Context, after = id }, done.Entity);
        var next = siblings[0];
        var more = siblings.Count - 1;
        return Flash($"{done.Message} Next: \"{next.String.Raw}\" in {next.String.Context}, also under this name{(more == 0 ? "" : $" ({more} more)")}.", new { dataset = Dataset, method = Method, context = Context, id = next.String.Id }, done.Entity);
    }
}
