using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

public sealed class EntityModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? Merge { get; set; }
    /// <summary>The entity that already holds an identifier just refused here (doc 12 section 13, item 3): listed alone under Merge, so the merge is one click.</summary>
    [BindProperty(SupportsGet = true)] public string? Held { get; set; }
    /// <summary>A search of the local indexes for a record to add as an identifier (the desk, 2026-10-26: a Wikidata record seen under the desk's "Something else" could not be found again here).</summary>
    [BindProperty(SupportsGet = true)] public string? Find { get; set; }

    public EntityView View { get; private set; } = null!;
    public IReadOnlyList<Entity> MergeCandidates { get; private set; } = [];
    public IReadOnlyList<AuthorityRecord> Found { get; private set; } = [];
    /// <summary>The catalogue records the entity's strings were seen in, by string (the owner, 2026-11-05: on a placeholder's page, nothing said what the records it stood in were about).</summary>
    public IReadOnlyList<(SourceString String, IReadOnlyList<StringExample> Examples)> Examples { get; private set; } = [];
    /// <summary>The queued strings under this entity's name, still waiting at the desk (the owner, 2026-11-06, after the first merge: the banner's link to the survivor should show what is left to do under its name).</summary>
    public IReadOnlyList<DecidedString> Waiting { get; private set; } = [];
    /// <summary>This entity's own strings still queued (a placeholder's own string), which the name's list leaves out: the way from a placeholder's page to its desk page (the owner, 2026-11-06, at "Colchester (England)").</summary>
    public IReadOnlyList<DecidedString> OwnQueued { get; private set; } = [];
    /// <summary>What the aligned sources said when last pulled, by identifier, from the records' cache (plan section 7, "From the sources, on demand"; nothing is fetched to show the page).</summary>
    public IReadOnlyDictionary<string, Pulled> Pulled { get; private set; } = new Dictionary<string, Pulled>();
    /// <summary>The identifiers a fetcher exists for, in the entity's order: what "Pull in" would ask.</summary>
    public IReadOnlyList<Equivalent> Pullable => View.Entity.Equivalents.Where(q => SourcePull.CanPull(q.Uri)).ToList();
    /// <summary>Where the entity is, from the first of its records that places it (TGN's coordinates, or a pull's; its TGN parent's when the record has none); the map at the top of the page when set.</summary>
    public Placed? Coordinates { get; private set; }
    /// <summary>The first cached picture among the sources, for the top of the page.</summary>
    public Picture? Picture => Pullable.Select(q => Pulled.GetValueOrDefault(q.Uri)?.Picture).FirstOrDefault(p => p?.ThumbUrl is not null);
    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? message = null)
    {
        Message = message ?? TakeFlash();
        var view = await Project.Curation.EntityAsync(Dataset, Id);
        if (view is null) return NotFound();
        View = view;
        Pulled = view.Records.Select(r => (r.Key, Pulled: SourcePull.Read(r.Value))).Where(x => x.Pulled is not null).ToDictionary(x => x.Key, x => x.Pulled!);
        foreach (var q in view.Entity.Equivalents.OrderBy(q => q.Source == "tgn" ? 0 : 1))
            if (await LinkedArtNet.Alignment.Authorities.Coordinates.ResolveAsync(Services.Store, view.Records.GetValueOrDefault(q.Uri)) is { } placed && (Coordinates is null || !placed.Approximate)) { Coordinates = placed; if (!placed.Approximate) break; }
        if (!string.IsNullOrWhiteSpace(Held) && await Services.Store.ResolveEntityAsync(Held) is { } holder && holder.Id != view.Entity.Id)
            MergeCandidates = [holder];
        else if (!string.IsNullOrWhiteSpace(Merge))
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
        OwnQueued = view.Strings.Where(s => s.Decision.Method == DecisionMethod.Queued && Waiting.All(w => w.String.Id != s.String.Id)).ToList();
        return Page();
    }

    public Task<IActionResult> OnPostLabelAsync(string id, string label) => Act(id, async () => { await Project.Curation.SetLabelAsync(Dataset, id, label, Reviewer); return "Label saved."; });
    public Task<IActionResult> OnPostClassAsync(string id, EntityClass cls) => Act(id, async () => { var e = await Project.Curation.SetClassAsync(Dataset, id, cls, Reviewer); return $"Class: {e.Class.ToString().ToLowerInvariant()}."; });
    public async Task<IActionResult> OnPostAddEquivalentAsync(string id, string uri)
    {
        try { await Project.Curation.AddEquivalentAsync(Dataset, id, uri, Reviewer); return Flash("Identifier added.", new { dataset = Dataset, id }); }
        catch (IdentifierInUseException e) { return Flash(e.Message, new { dataset = Dataset, id, held = e.Holder.Id }); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { return Flash(e.Message, new { dataset = Dataset, id }); }
    }
    /// <summary>A set of identifiers removed as one (the owner, 2026-11-13: a wrong Wikidata item goes with the VIAF number it gave), each removal noted as any other.</summary>
    public Task<IActionResult> OnPostRemoveEquivalentsAsync(string id, string[]? uris) => Act(id, async () =>
    {
        var removed = 0;
        foreach (var uri in (uris ?? []).Distinct(StringComparer.Ordinal)) { await Project.Curation.RemoveEquivalentAsync(Dataset, id, uri, Reviewer); removed++; }
        return removed == 1 ? "Identifier removed." : $"{removed} identifiers removed.";
    });
    public Task<IActionResult> OnPostRemoveEquivalentAsync(string id, string uri) => Act(id, async () => { await Project.Curation.RemoveEquivalentAsync(Dataset, id, uri, Reviewer); return "Identifier removed."; });
    public Task<IActionResult> OnPostConfirmAsync(string id, string? note) => Act(id, async () => { await Project.Curation.ConfirmAsync(Dataset, id, Reviewer, note); return "Confirmed."; });
    public Task<IActionResult> OnPostNoteAsync(string id, string text) => Act(id, async () => { if (!string.IsNullOrWhiteSpace(text)) await Project.Curation.NoteAsync(Dataset, id, Reviewer, text.Trim()); return "Note added."; });
    public Task<IActionResult> OnPostReopenAsync(string id, long[]? strings) => Act(id, async () => { var n = await Project.Curation.ReopenAsync(Dataset, strings ?? [], Reviewer, id, HttpContext.RequestAborted); return $"{n} string(s) reopened."; });

    public async Task<IActionResult> OnPostMergeAsync(string id, string survivor, EntityClass? cls)
    {
        try
        {
            var result = await Project.Curation.MergeAsync(Dataset, id, survivor, Reviewer, cls);
            return Flash("Merged.", new { dataset = Dataset, id = result.Id });
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset, id });
        }
    }

    /// <summary>"Pull in": every identifier with a fetcher, or the one given; the message says which answered and which did not.</summary>
    public Task<IActionResult> OnPostPullAsync(string id, string? uri) => Act(id, async () =>
    {
        var entity = (await Project.Curation.EntityAsync(Dataset, id))?.Entity ?? throw new KeyNotFoundException($"No entity {id}.");
        var results = uri is null ? await Project.Pull.PullAllAsync(entity, HttpContext.RequestAborted) : [await Project.Pull.PullAsync(uri, HttpContext.RequestAborted)];
        if (results.Count == 0) return "Nothing to pull: none of this entity's identifiers is in Wikidata, ULAN, the Library of Congress or LUX.";
        var ok = results.Where(r => r.Error is null).Select(r => r.Source).ToList();
        var failed = results.Where(r => r.Error is not null).Select(r => r.Error!).Distinct().ToList();
        return (ok.Count == 0 ? "" : $"Pulled from {string.Join(", ", ok)}.") + (failed.Count == 0 ? "" : " " + string.Join(" ", failed));
    });

    private async Task<IActionResult> Act(string id, Func<Task<string>> action)
    {
        string message;
        try { message = await action(); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { message = e.Message; }
        return Flash(message, new { dataset = Dataset, id });
    }
}
