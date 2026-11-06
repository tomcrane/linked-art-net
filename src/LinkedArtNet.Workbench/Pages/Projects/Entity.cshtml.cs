using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

public sealed class EntityModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? Merge { get; set; }
    /// <summary>A search of the local indexes for a record to add as an identifier (the desk, 2026-10-26: a Wikidata record seen under the desk's "Something else" could not be found again here).</summary>
    [BindProperty(SupportsGet = true)] public string? Find { get; set; }

    public EntityView View { get; private set; } = null!;
    public IReadOnlyList<Entity> MergeCandidates { get; private set; } = [];
    public IReadOnlyList<AuthorityRecord> Found { get; private set; } = [];
    /// <summary>The catalogue records the entity's strings were seen in, by string (the owner, 2026-11-05: on a placeholder's page, nothing said what the records it stood in were about).</summary>
    public IReadOnlyList<(SourceString String, IReadOnlyList<StringExample> Examples)> Examples { get; private set; } = [];
    /// <summary>The queued strings under this entity's name, still waiting at the desk (the owner, 2026-11-06, after the first merge: the banner's link to the survivor should show what is left to do under its name).</summary>
    public IReadOnlyList<DecidedString> Waiting { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? message = null)
    {
        Message = message ?? TakeFlash();
        var view = await Project.Curation.EntityAsync(Dataset, Id);
        if (view is null) return NotFound();
        View = view;
        if (!string.IsNullOrWhiteSpace(Merge))
            MergeCandidates = (await Project.Desk.SearchEntitiesAsync(Dataset, Merge)).Where(e => e.Id != view.Entity.Id).ToList();
        if (!string.IsNullOrWhiteSpace(Find))
            Found = (await Project.Desk.SearchIndexesAsync(Find, null, 12)).Where(r => view.Entity.Equivalents.All(q => q.Uri != r.Uri)).ToList();
        // the records behind the strings, the most frequent strings first, a record shown once, eight at most
        var examples = new List<(SourceString, IReadOnlyList<StringExample>)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in view.Strings.OrderByDescending(x => x.String.Occurrences).Take(12))
        {
            var own = (await Project.Desk.ExamplesForAsync(s.String.Id)).Where(x => seen.Add(x.RecordId)).ToList();
            if (own.Count > 0) examples.Add((s.String, own));
            if (seen.Count >= 8) break;
        }
        Examples = examples;
        Waiting = await Project.Desk.WaitingForAsync(Dataset, view.Entity);
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
            return Flash("Merged.", new { dataset = Dataset, id = result.Id });
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset, id });
        }
    }

    private async Task<IActionResult> Act(string id, Func<Task<string>> action)
    {
        string message;
        try { message = await action(); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { message = e.Message; }
        return Flash(message, new { dataset = Dataset, id });
    }
}
