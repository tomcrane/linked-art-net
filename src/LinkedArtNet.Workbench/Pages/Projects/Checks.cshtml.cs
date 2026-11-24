using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// Checks on the project's own entities (the owner, 2026-11-15): lists of entities that a named test finds doubtful, each row leading
/// to the entity's page, where it is mended or confirmed, or to "Mend", which shows what it would do and does it on a person's word.
/// Not the cataloguers' list (doc 12 section 14.4), which is of things the source records could say better. The first check: places
/// the old import aligned to a namesake.
/// </summary>
public sealed class ChecksModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    /// <summary>What is decided on this page changes the checks' lists: the kept copy that the entity pages and the cards read is dropped.</summary>
    public override async Task OnPageHandlerExecutionAsync(Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutingContext context, Microsoft.AspNetCore.Mvc.Filters.PageHandlerExecutionDelegate next)
    {
        await base.OnPageHandlerExecutionAsync(context, next);
        if (HttpMethods.IsPost(Request.Method)) CheckLists.Forget(Dataset);
    }

    [BindProperty(SupportsGet = true)] public string? Mend { get; set; }
    /// <summary>Which check's list to show: null for the first, the places; "duplicates" for the entities that may be one thing (the second check, 2026-11-20).</summary>
    [BindProperty(SupportsGet = true)] public string? Check { get; set; }
    public IReadOnlyList<PossibleDuplicate> Duplicates { get; private set; } = [];
    /// <summary>The third check (2026-11-22): entities every one of whose strings is no name ("S.l.", "Publisher not identified", "S").</summary>
    public IReadOnlyList<NotNameEntity> NotNames { get; private set; } = [];
    /// <summary>The fourth check (2026-11-24): places with no gazetteer's or Wikidata's identifier, whose name the gazetteer has.</summary>
    public IReadOnlyList<ThinPlace> Thin { get; private set; } = [];
    /// <summary>A search of the project's entities on the Mend screen, for a place known by another name (the owner, 2026-11-15, at "Bâle", which is Basel).</summary>
    [BindProperty(SupportsGet = true)] public string? Find { get; set; }
    public IReadOnlyList<Entity> Found { get; private set; } = [];
    /// <summary>A search of the authority indexes on the Decide screen, for a place the project does not have and none of the name at home is (seen as a user, 2026-11-17: "Kingston, Jamaica" wanted TGN's Kingston in Jamaica, and the screen took only an identifier typed in).</summary>
    [BindProperty(SupportsGet = true)] public string? IFind { get; set; }
    /// <summary>The strings the person had set to "it goes" when they pressed a search on the Decide screen: the search reloads the screen, and they are set so again (seen as a user, 2026-11-17: the choice was lost, and the results hidden with it).</summary>
    [BindProperty(SupportsGet = true)] public long[]? Go { get; set; }
    public IReadOnlyList<AuthorityRecord> IFound { get; private set; } = [];

    public static string? ChainOf(AuthorityRecord record) => ReviewService.ChainOf(record);

    public IReadOnlyList<AwayFinding> Away { get; private set; } = [];
    public string? HomePlace { get; private set; }
    public MendPlan? Plan { get; private set; }
    /// <summary>LoC's heading for the entity being decided, when it holds one that begins with its label and carries a qualifier: the label suggested for keeping it.</summary>
    public string? SuggestedLabel { get; private set; }
    /// <summary>What happened when the screen pulled the entity's Wikidata item, the local index not having it: what the item says it is, or why the pull failed. Null when no pull was needed.</summary>
    public string? Pulled { get; private set; }
    /// <summary>The strings that stand on the entity being mended, each with up to three of the records it is in (the owner, 2026-11-15: "it would help to re-list the record context on the Mend page").</summary>
    public List<(SourceString String, IReadOnlyList<StringExample> Records, int Count)> PlanStrings { get; } = [];
    public Dictionary<string, (string? Placement, int Strings)> Clues { get; } = new(StringComparer.Ordinal);
    public string? Message { get; private set; }
    public (string Id, string Label)? MessageEntity { get; private set; }

    public async Task OnGetAsync()
    {
        Message = TakeFlash();
        MessageEntity = TakeFlashEntity();
        HomePlace = Project.Manifest.HomePlace;
        if (!string.IsNullOrWhiteSpace(Mend))
        {
            try { Plan = await Project.Curation.MendPlanAsync(Dataset, Mend, HomePlace, HttpContext.RequestAborted); }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException) { Message = e.Message; }
            // the item is pulled when the screen is opened and the index does not have it, one item for one decision (the owner,
            // 2026-11-15: "a mend page that needs to pull from wikidata could do so on load ... better than a button to load all
            // wikidata, which while OK here may not be appropriate if there are hundreds of items")
            if (Plan is { Homes: [{ ItemDescription: null, Item: var item }, ..] })
            {
                try
                {
                    var result = await Project.Pull.PullAsync(item, HttpContext.RequestAborted);
                    if (result.Error is not null) Pulled = "The Wikidata item is not in the local index and could not be pulled just now: " + result.Error;
                    else
                    {
                        Plan = await Project.Curation.MendPlanAsync(Dataset, Mend, HomePlace, HttpContext.RequestAborted) ?? Plan;
                        Pulled = Plan.Homes[0].ItemDescription is { } says
                            ? $"The Wikidata item was not in the local index, so it was pulled from Wikidata just now. It says it is: {says}."
                            : "The Wikidata item was not in the local index; it was pulled from Wikidata just now, and has no description.";
                    }
                }
                catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
                {
                    Pulled = "The Wikidata item is not in the local index and could not be pulled just now: " + e.Message;
                }
            }
            if (Plan is not null)
            {
                foreach (var s in (await Services.Store.StringsForEntityAsync(Plan.Entity.Id, 60, HttpContext.RequestAborted)).OrderBy(s => s.String.Raw, StringComparer.Ordinal))
                    PlanStrings.Add((s.String, await Project.Desk.ExamplesForAsync(s.String.Id, HttpContext.RequestAborted), await Services.Store.RecordCountAsync(s.String.Id, HttpContext.RequestAborted)));
                foreach (var q in Plan.Entity.Equivalents.Where(q => q.Source == "loc"))
                    if (await Services.Store.GetAuthorityAsync(q.Uri, HttpContext.RequestAborted) is { Label: { } heading } && heading.StartsWith(Plan.Entity.Label + " (", StringComparison.Ordinal) && heading.EndsWith(')')) { SuggestedLabel = heading; break; }
                if (!string.IsNullOrWhiteSpace(Find))
                    Found = (await Project.Desk.SearchEntitiesAsync(Dataset, Find.Trim())).Where(e => e.Class == EntityClass.Place && e.MergedInto is null && e.Id != Plan.Entity.Id && Plan.Others.All(o => o.Id != e.Id)).Take(12).ToList();
                if (!string.IsNullOrWhiteSpace(IFind))
                {
                    // every index that has places, and in LCSH and Wikidata the houses and museums typed as bodies too (the owner, 2026-11-22, at
                    // "Wallington": the Northumberland house is LCSH's "Wallington Hall (England)" and a Wikidata item typed as a body, and the
                    // search looked only at TGN and LoC's names)
                    IFound = (await Project.Desk.SearchIndexesAsync(IFind.Trim(), null, 120, HttpContext.RequestAborted))
                        .Where(r => ((r.Type == "Place" && r.Source is "tgn" or "loc" or "lcsh" or "wikidata") || (r.Type == "Group" && r.Source is "lcsh" or "wikidata")) && Plan.Entity.Equivalents.All(q => q.Uri != r.Uri))
                        .OrderBy(r => r.Type == "Place" ? 0 : 1).Take(16).ToList();
                }
                foreach (var other in Plan.Others.Concat(Plan.Holders.Values).Concat(Found)) Clues[other.Id] = await Project.Desk.ClueAsync(other, HttpContext.RequestAborted);
                return;
            }
            Message ??= "That entity is not on the list any more.";
        }
        // with no check named the page is the list of checks, each with how much it holds, so both are counted
        if (Check is null or "duplicates") Duplicates = await Project.Curation.PossibleDuplicatesAsync(Dataset, HttpContext.RequestAborted);
        if (Check is null or "places") Away = await Project.Curation.PlacesAlignedAwayAsync(Dataset, HomePlace, HttpContext.RequestAborted);
        if (Check is null or "notnames") NotNames = await Project.Curation.NotNameEntitiesAsync(Dataset, HttpContext.RequestAborted);
        if (Check is null or "thin") Thin = await Project.Curation.ThinPlacesAsync(Dataset, HomePlace, HttpContext.RequestAborted);
    }

    /// <summary>The place is given the gazetteer's record the row offered: an identifier added, as on the entity's own page.</summary>
    public async Task<IActionResult> OnPostGiveRecordAsync(string id, string uri)
    {
        try
        {
            var entity = await Project.Curation.AddEquivalentAsync(Dataset, id, uri, Reviewer, HttpContext.RequestAborted);
            var record = await Services.Store.GetAuthorityAsync(uri, HttpContext.RequestAborted);
            return Flash($"\"{entity.Label}\" given the gazetteer's {record?.Label ?? uri}{(record is not null && ReviewService.ChainOf(record) is { } chain ? " (" + chain + ")" : "")}. Its page can remove it.", new { dataset = Dataset, check = "thin" }, entity);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException or IdentifierInUseException) { return Flash(e.Message, new { dataset = Dataset, check = "thin" }); }
    }

    /// <summary>No gazetteer has the place: a note on it, and it leaves the list.</summary>
    public async Task<IActionResult> OnPostNoRecordAsync(string id)
    {
        var entity = await Services.Store.GetEntityAsync(id, HttpContext.RequestAborted);
        if (entity is null || entity.Dataset != Dataset) return NotFound();
        await Project.Curation.NoteAsync(Dataset, id, Reviewer, CurationService.NoGazetteerRecord + ": none of the records of its name is it.");
        return Flash($"\"{entity.Label}\" noted as in no gazetteer; it is off the list.", new { dataset = Dataset, check = "thin" }, entity);
    }

    /// <summary>Every string of a listed entity is decided as not a name, for the reason the row gave, and the entity goes.</summary>
    public async Task<IActionResult> OnPostNotANameAsync(string id, string reason)
    {
        try
        {
            var label = (await Services.Store.GetEntityAsync(id, HttpContext.RequestAborted))?.Label ?? id;
            var (strings, removed) = await Project.Curation.NotANameEntityAsync(Dataset, id, Reviewer, reason, HttpContext.RequestAborted);
            return Flash($"\"{label}\": {strings} string{(strings == 1 ? "" : "s")} decided as not a name ({(reason == CurationService.NothingKnown ? "nothing is known" : "the record wants fixing")}). {(removed ? "The entity is gone." : "The entity is kept: something else still points at it.")} Each string's card can undo it.", new { dataset = Dataset, check = "notnames" });
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { return Flash(e.Message, new { dataset = Dataset, check = "notnames" }); }
    }

    /// <summary>The pair is two things: a note on each naming the other, and the pair leaves the list.</summary>
    public async Task<IActionResult> OnPostTwoThingsAsync(string a, string b, string? why)
    {
        var ct = HttpContext.RequestAborted;
        var first = await Services.Store.GetEntityAsync(a, ct);
        var second = await Services.Store.GetEntityAsync(b, ct);
        if (first is null || second is null || first.Dataset != Dataset || second.Dataset != Dataset) return NotFound();
        var because = string.IsNullOrWhiteSpace(why) ? "" : " (" + why.Trim() + ")";
        await Project.Curation.NoteAsync(Dataset, a, Reviewer, $"{CurationService.TwoThings}{b} \"{second.Label}\"{because}");
        await Project.Curation.NoteAsync(Dataset, b, Reviewer, $"{CurationService.TwoThings}{a} \"{first.Label}\"{because}");
        return Flash($"\"{first.Label}\" and \"{second.Label}\" noted as two things; the pair is off the list.", new { dataset = Dataset, check = "duplicates" });
    }

    /// <summary>
    /// The one button of the Decide screen: each string stays or goes (<c>outcome</c>, by string id, "stay" or "go"), and the rest of the
    /// form says where the going ones go, what the entity is called for those that stay, and which identifiers leave it when all go.
    /// Afterwards the person is returned to the same entity if the check still lists it, with what was done and what is left, and to
    /// the list when it does not.
    /// </summary>
    /// <param name="records">The records of the indexes ticked as the other place: one place under several identifiers (the owner, 2026-11-22, at "Wallington" and "Charterhouse": LCSH's heading and Wikidata's item, "all valid and I would want to tick both"). The first, a gazetteer's or a library's before Wikidata's, is where the strings go; the entity they stand on is then given the rest.</param>
    public async Task<IActionResult> OnPostApplyAsync(string id, Dictionary<long, string>? outcome, string? home, string? otherUri, string[]? remove, string? label, string? other, string? addUri, string[]? records)
    {
        try
        {
            var goes = (outcome ?? []).Where(o => o.Value == "go").Select(o => o.Key).ToHashSet();
            static int Rank(string uri) => AuthorityUris.SourceOf(uri) switch { "tgn" => 0, "loc" or "lcsh" => 1, "wikidata" => 3, _ => 2 };
            var picked = goes.Count == 0 ? [] : (records ?? []).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.Ordinal).OrderBy(Rank).ToList();
            // a record an entity of the project holds already comes first: the strings go to that entity, and it is given the others
            // (the owner at "Charterhouse", 2026-11-22: Wikidata's item was held by the project's London Charterhouse, the strings
            // went to a new entity made for LCSH's heading, and the project had the building twice)
            var heldFirst = new List<string>();
            foreach (var uri in picked)
                if ((await Services.Store.FindEntitiesByEquivalentAsync(uri, HttpContext.RequestAborted)).Any(e => e.Dataset == Dataset && e.MergedInto is null && e.Id != id)) heldFirst.Add(uri);
            picked = heldFirst.Concat(picked.Except(heldFirst)).ToList();
            var target = picked.Count > 0 ? picked[0] : home == "other" ? (otherUri ?? "").Trim() : home;
            var keepAs = string.IsNullOrWhiteSpace(label) ? (other ?? "").Trim() : label.Trim();
            var (result, summary) = await Project.Curation.ApplyAsync(Dataset, id, goes, target, remove ?? [], keepAs, string.IsNullOrWhiteSpace(addUri) ? null : addUri, Reviewer, HttpContext.RequestAborted);
            // the place's other identifiers, on whichever entity holds the first now
            if (picked.Count > 1 && (await Services.Store.FindEntitiesByEquivalentAsync(picked[0], HttpContext.RequestAborted)).FirstOrDefault(e => e.Dataset == Dataset && e.MergedInto is null) is { } placed)
            {
                var added = 0; var refused = new List<string>();
                foreach (var uri in picked.Skip(1).Where(u => placed.Equivalents.All(q => q.Uri != u)))
                {
                    try { await Project.Curation.AddEquivalentAsync(Dataset, placed.Id, uri, Reviewer, HttpContext.RequestAborted); added++; }
                    catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or ArgumentException or IdentifierInUseException) { refused.Add($"{uri} ({e.Message})"); }
                }
                if (added > 0) summary += $" \"{placed.Label}\" also given {added} more identifier{(added == 1 ? "" : "s")} of the place.";
                if (refused.Count > 0) summary += " Not added: " + string.Join("; ", refused) + ".";
                if (result.Id == placed.Id) result = (await Services.Store.GetEntityAsync(placed.Id, HttpContext.RequestAborted)) ?? result;
            }
            var still = result.Id == id && (await Project.Curation.PlacesAlignedAwayAsync(Dataset, Project.Manifest.HomePlace, HttpContext.RequestAborted)).Any(f => f.Row.EntityId == id);
            return still
                ? Flash(summary + " It is still on the list: see below what is left to decide.", new { dataset = Dataset, mend = id }, result)
                : Flash(summary + " Nothing is left to decide for it.", new { dataset = Dataset, check = "places" }, result);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset, mend = id });
        }
    }

    /// <summary>Not decided: the row stays on the list, with a note on the entity for whoever takes it up (seen as a user: two rows that wanted more than the screen could do had nowhere to say so but the entity's own page).</summary>
    public async Task<IActionResult> OnPostNoteAsync(string id, string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Flash("Write the note first: what is wrong, and what you could not do.", new { dataset = Dataset, mend = id });
        var entity = await Services.Store.GetEntityAsync(id, HttpContext.RequestAborted);
        if (entity is null || entity.Dataset != Dataset) return NotFound();
        await Project.Curation.NoteAsync(Dataset, id, Reviewer, "Left undecided at the checks: " + text.Trim());
        return Flash($"\"{entity.Label}\" left on the list, with your note on it.", new { dataset = Dataset, check = "places" }, entity);
    }
}
