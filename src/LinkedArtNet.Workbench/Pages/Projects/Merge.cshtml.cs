using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// The merge page behind the tray (doc 12 section 13, item 2): the collected entities side by side, the survivor, label and class chosen,
/// one merge for all of them. The tray itself lives in the browser (wwwroot/tray.js); this page takes its ids.
/// </summary>
public sealed class MergeModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    /// <summary>What is decided on this page changes the checks' lists: the kept copy that the entity pages and the cards read is dropped.</summary>
    public override async Task OnPageHandlerExecutionAsync(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context, Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutionDelegate next)
    {
        await base.OnPageHandlerExecutionAsync(context, next);
        if (HttpMethods.IsPost(Request.Method)) CheckLists.Forget(Dataset);
    }

    /// <summary>The entities' ids, comma-separated, as the tray sends them.</summary>
    [BindProperty(SupportsGet = true)] public string? Ids { get; set; }

    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public string? Message { get; private set; }
    /// <summary>The suggested survivor: the one with most strings, then the firmer status, then the one with more identifiers.</summary>
    public string? Suggested => Rows.OrderByDescending(r => r.Strings).ThenByDescending(r => r.Entity.Status).ThenByDescending(r => r.Entity.Equivalents.Count).FirstOrDefault()?.Entity.Id;
    public bool ClassesDiffer => Rows.Select(r => r.Entity.Class).Distinct().Count() > 1;

    /// <summary>
    /// One entity to compare: what tells it from the others (where a place lies), how much stands on it, the strings that stand on it
    /// with the records each is in, and its identifiers with what each record calls the thing (the owner, 2026-11-20, of the page as
    /// it was: "this looks a bit clunky", a table with a name, a count and the sources' names, and nothing to compare by).
    /// </summary>
    public sealed record Row(Entity Entity, int Strings, int Queued, string? Placement = null, int Records = 0,
        IReadOnlyList<(SourceString String, int Records)>? Sample = null, IReadOnlyList<(Equivalent Id, string? Says)>? Identifiers = null, int ByRecord = 0);

    /// <summary>Where the person came from, to go back to: "duplicates" for the check's list.</summary>
    [BindProperty(SupportsGet = true)] public string? From { get; set; }

    /// <summary>A search for another of the project's entities to bring into the comparison (the owner, 2026-11-24: the pair on the page was not the right pair, and the "real" entity had to be found from the other's own page).</summary>
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public IReadOnlyList<(Entity Entity, string? Placement, int Strings)> Found { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        Message = TakeFlash();
        Rows = await LoadAsync();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var found = new List<(Entity, string?, int)>();
            foreach (var e in (await Project.Desk.SearchEntitiesAsync(Dataset, Q.Trim(), 30)).Where(e => e.MergedInto is null && Rows.All(r => r.Entity.Id != e.Id))
                .OrderByDescending(e => Rows.Any(r => r.Entity.Class == e.Class) ? 1 : 0).ThenBy(e => e.Status == EntityStatus.Provisional ? 1 : 0).Take(12))
            {
                var clue = await Project.Desk.ClueAsync(e, HttpContext.RequestAborted);
                found.Add((e, clue.Placement, clue.Strings));
            }
            Found = found;
        }
        return Page();
    }

    private async Task<IReadOnlyList<Row>> LoadAsync()
    {
        var rows = new List<Row>();
        foreach (var id in (Ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().Take(20))
        {
            var entity = await Services.Store.ResolveEntityAsync(id);
            if (entity is null || entity.Dataset != Dataset || rows.Any(r => r.Entity.Id == entity.Id)) continue;
            var strings = await Services.Store.StringsForEntityAsync(entity.Id, 1000);
            var counted = new List<(SourceString String, int Records)>();
            foreach (var st in strings.Take(40)) counted.Add((st.String, await Services.Store.RecordCountAsync(st.String.Id, HttpContext.RequestAborted)));
            var identifiers = new List<(Equivalent Id, string? Says)>();
            foreach (var q in entity.Equivalents.Take(16))
                identifiers.Add((q, await SaysAsync(q)));
            var clue = await Project.Desk.ClueAsync(entity, HttpContext.RequestAborted);
            rows.Add(new Row(entity, strings.Count, strings.Count(s => s.Decision.Method == DecisionMethod.Queued), clue.Placement, counted.Sum(x => x.Records),
                counted.OrderByDescending(x => x.Records).Take(6).ToList(), identifiers, (await Services.Store.RecordDecisionsForEntityAsync(entity.Id, HttpContext.RequestAborted)).Count));
        }
        return rows;
    }

    private int _pulls;

    /// <summary>
    /// What an identifier is, for a person deciding whether it should follow a merge (the owner, 2026-11-24: of two entities' VIAF and
    /// LUX identifiers the page said only that the store had no record; one was dead and the other an American place's, learnt by
    /// following them). The store's record; else, for a source that can be asked, what it answers now, asked as the page opens (a few
    /// a page) and kept; else what a Wikidata item that gives the identifier says it is.
    /// </summary>
    private async Task<string?> SaysAsync(Equivalent q)
    {
        var ct = HttpContext.RequestAborted;
        var record = await Services.Store.GetAuthorityAsync(q.Uri, ct);
        if (record is not null && (LinkedArtNet.Alignment.Authorities.RecordSummary.Says(record) ?? record.Label) is { } known) return known;
        foreach (var item in await Services.Store.FindAuthoritiesBySameAsAsync(q.Uri, ct))
            if (item.Source == "wikidata") return $"Wikidata gives this identifier to {LinkedArtNet.Alignment.Authorities.RecordSummary.Says(item) ?? item.Label}";
        if (!LinkedArtNet.Alignment.Authorities.SourcePull.CanPull(q.Uri)) return null;
        var pulled = LinkedArtNet.Alignment.Authorities.SourcePull.Read(record);
        if (pulled is null && _pulls++ < 6)
        {
            try { pulled = await Project.Pull.PullAsync(q.Uri, ct); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { return $"asked just now, and it could not be reached ({e.Message})"; }
        }
        if (pulled is null) return null;
        // a source that answers with something that is no record has usually withdrawn it: said so, in a person's words
        if (pulled.Error is { } error) return error.Contains("not with a record that could be read", StringComparison.Ordinal) ? "asked just now: its source gave no record for it. It has probably been withdrawn, and leads nowhere" : $"asked just now: {error}";
        var what = string.Join(": ", new[] { pulled.Label, pulled.Description }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var facts = string.Join("; ", pulled.Facts.Take(2).Select(f => $"{f.Key} {f.Value}"));
        return (what.Length > 0 ? what : "a record with no name") + (facts.Length > 0 ? " · " + facts : "") + (pulled.SelfEcho ? " · this project's own earlier export, reflected back" : "");
    }

    /// <summary>The two are two things: a note on each naming the other, with the reason if one was given, and the pair leaves the check's list.</summary>
    public async Task<IActionResult> OnPostTwoThingsAsync(string? why)
    {
        Rows = await LoadAsync();
        if (Rows.Count != 2) { Message = "\"Two things\" is said of a pair."; return Page(); }
        var (a, b) = (Rows[0].Entity, Rows[1].Entity);
        var because = string.IsNullOrWhiteSpace(why) ? "" : " (" + why.Trim() + ")";
        await Project.Curation.NoteAsync(Dataset, a.Id, Reviewer, $"{LinkedArtNet.Alignment.Reconciliation.CurationService.TwoThings}{b.Id} \"{b.Label}\"{because}");
        await Project.Curation.NoteAsync(Dataset, b.Id, Reviewer, $"{LinkedArtNet.Alignment.Reconciliation.CurationService.TwoThings}{a.Id} \"{a.Label}\"{because}");
        TempData["Flash"] = $"\"{a.Label}\" and \"{b.Label}\" noted as two things{(because.Length == 0 ? "" : ", with your reason")}; the pair is off the list.";
        return Redirect($"/p/{Dataset}/checks?check=duplicates");
    }

    /// <remarks>Identifiers unticked are left out of the merge: each is taken off the entity that holds it first, so that one that describes something else does not travel to the entity kept (the owner, 2026-11-24, at "Birmingham" and "Sheffield": from the Compare page he went to the wrong entity's own page, removed a dead LUX identifier and an American place's, searched for the right entity and merged there).</remarks>
    /// <param name="shown">The identifiers the page showed with a box.</param>
    /// <param name="bring">Those of them still ticked: they go with the merge. One shown and not ticked is left out.</param>
    public async Task<IActionResult> OnPostAsync(string survivor, string? label, EntityClass? cls, string[]? shown, string[]? bring)
    {
        var leave = (shown ?? []).Except(bring ?? [], StringComparer.Ordinal).ToArray();
        Rows = await LoadAsync();
        var keep = Rows.FirstOrDefault(r => r.Entity.Id == survivor)?.Entity;
        if (keep is null || Rows.Count < 2) { Message = "Choose the survivor among at least two entities."; return Page(); }
        var merged = new List<string>();
        var left = 0;
        try
        {
            foreach (var uri in (leave ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal))
                foreach (var row in Rows.Where(r => r.Entity.Equivalents.Any(q => q.Uri == uri)))
                { await Project.Curation.RemoveEquivalentAsync(Dataset, row.Entity.Id, uri, Reviewer); left++; }
            foreach (var row in Rows.Where(r => r.Entity.Id != keep.Id))
            {
                await Project.Curation.MergeAsync(Dataset, row.Entity.Id, keep.Id, Reviewer, ClassesDiffer ? cls : null);
                merged.Add(row.Entity.Label ?? row.Entity.Id);
            }
            if (!string.IsNullOrWhiteSpace(label) && label.Trim() != keep.Label) await Project.Curation.SetLabelAsync(Dataset, keep.Id, label, Reviewer);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            Message = e.Message + (merged.Count == 0 ? "" : $" Merged before it: {string.Join(", ", merged)}.");
            Rows = await LoadAsync();
            return Page();
        }
        Services.Invalidate(Dataset);
        TempData["Flash"] = $"Merged {string.Join(", ", merged.Select(m => $"\"{m}\""))} into \"{keep.Label}\"{(left == 0 ? "" : $", {left} identifier{(left == 1 ? "" : "s")} left out")}.";
        // the fragment tells the tray script the tray's work is done, so it empties itself
        return RedirectToPage("/Projects/Entity", pageHandler: null, routeValues: new { dataset = Dataset, id = keep.Id }, fragment: "tray-cleared");
    }
}
