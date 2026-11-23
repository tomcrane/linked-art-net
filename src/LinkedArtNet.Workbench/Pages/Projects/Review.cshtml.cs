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
    /// <summary>The search was run by the desk, not asked for: the card had no candidates.</summary>
    public bool Searched { get; private set; }
    /// <summary>Show the string's evidence as today's rules would give it, deciding nothing.</summary>
    [BindProperty(SupportsGet = true)] public bool Look { get; set; }
    /// <summary>Accepting the ticked together with two or more of the project's own entities among them comes here first: the plan is shown and the reviewer confirms (the owner, 2026-11-06, at "Chris Beetles Ltd").</summary>
    [BindProperty(SupportsGet = true)] public bool Confirm { get; set; }
    [BindProperty(SupportsGet = true)] public string[]? Targets { get; set; }
    [BindProperty(SupportsGet = true)] public string? Survivor { get; set; }
    /// <summary>"Always, in this field" on a group: accepted together and the spelling overridden to the entity in this field.</summary>
    [BindProperty(SupportsGet = true)] public bool Always { get; set; }
    /// <summary>Thumbnails on the cards whose records a pull has given a picture (plan section 7: the desk stays text by default, since pictures slow it and most cards have none).</summary>
    [BindProperty(SupportsGet = true)] public bool Pictures { get; set; }
    /// <summary>The record field the string's records are grouped by under "In the records"; the manifest's choice when not given.</summary>
    [BindProperty(SupportsGet = true)] public string? By { get; set; }

    public ReviewItem? Item { get; private set; }
    public IReadOnlyList<ContextMethodCount> Queue { get; private set; } = [];
    public IReadOnlyList<Entity> EntityMatches { get; private set; } = [];
    public IReadOnlyList<AuthorityRecord> IndexMatches { get; private set; } = [];
    /// <summary>For the entities a search found: where a place is, and how many strings stand on each, so that two of one name can be told apart.</summary>
    public Dictionary<string, (string? Placement, int Strings)> MatchClues { get; } = new(StringComparer.Ordinal);
    /// <summary>Longer forms of the card's string, in its field, that the project has already decided: "Arno Press" for "Arno".</summary>
    public IReadOnlyList<LongerForm> LongerForms { get; private set; } = [];
    /// <summary>For a queued publisher's string: bodies the indexes hold under its fuller name ("Arno Press" for "Arno"), not already on the card.</summary>
    public IReadOnlyList<AuthorityRecord> FullerNames { get; private set; } = [];
    /// <summary>By the URI of a candidate or a fuller name: what of the project already stands on it (the owner at "Cape", 2026-11-19: "One hint might be how many _other_ records the candidate is in").</summary>
    public IReadOnlyDictionary<string, Standing> StandingOn { get; private set; } = new Dictionary<string, Standing>();
    /// <summary>
    /// By a candidate's URI on the card, the project entity that candidate is or that holds its record now (not the string's own
    /// placeholder). So that one act has one name wherever the card offers it, "Same as X", and a longer form's entity can be told to
    /// be a candidate too (the owner at "Schwabe", 2026-11-20: the hint's "Same as" and the candidate's "Accept" did one thing).
    /// </summary>
    public Dictionary<string, (string Id, string? Label)> CandidateEntity { get; } = new(StringComparer.Ordinal);
    /// <summary>Set when a search of several words matched nothing and the results shown are those of the name alone ("Heaton" for "Heaton Bradford"): absence is then seen, not guessed at (the assistant as a user, 2026-11-19: nothing said whether the indexes had been searched and have none, or the search had failed).</summary>
    public string? NearName { get; private set; }
    public string? Message { get; private set; }
    /// <summary>What accepting the ticked together will do, when the desk is asking the reviewer to confirm it.</summary>
    public TogetherPlan? Plan { get; private set; }
    /// <summary>The entity the previous action made or chose, for the banner's link to it.</summary>
    public (string Id, string Label)? Decided { get; private set; }

    /// <summary>The checks whose lists the string's entity is on, with where each is decided.</summary>
    public IReadOnlyList<CheckHit> OnChecks { get; private set; } = [];

    /// <summary>The decisions single records of this string have, by "system|record".</summary>
    public Dictionary<string, RecordDecision> RecordDecisions { get; } = new(StringComparer.Ordinal);
    /// <summary>Queued strings that may be this one written another way: under its name, or a candidate on its card. Ticked, they are decided with it in one act (seen as a user, 2026-11-23: five London street addresses, one card after another).</summary>
    public List<DecidedString> Mates { get; } = [];
    /// <summary>Names the project uses that are one letter from this string's: a hint for a misspelling ("Londo").</summary>
    public IReadOnlyList<(string Id, string Label, int Records)> NearSpellings { get; private set; } = [];

    public long Remaining => Queue.Where(r => Context is null || r.Context == Context).Sum(r => r.Count);

    public async Task OnGetAsync(string? message = null)
    {
        Message = message ?? TakeFlash();
        Decided = TakeFlashEntity();
        if (string.IsNullOrWhiteSpace(Context)) Context = null;
        Queue = await Project.Desk.QueueCountsAsync(Dataset, Method);
        Item = Id is { } id ? (Look ? await Project.Desk.LookAgainAsync(Dataset, id) : await Project.Desk.GetAsync(id)) : await Project.Desk.NextAsync(Dataset, Method, Context, After);
        if (Item is not null && Item.String.Dataset != Dataset) Item = null;
        if (Item?.Entity is { } stands) OnChecks = await CheckLists.ForAsync(Project, stands.Id, HttpContext.RequestAborted);
        // a card with no candidate at all is searched for the reviewer under the string's name (the owner, 2026-11-14, sent from the
        // placeholder "Osterley Park" to its publisher string: "there's nothing to do at the desk"). The rules look only among the
        // kinds the field allows, a person or a body for a publisher; the search looks at every entity of the project and every
        // index, and finds the place "Osterley Park (Middlesex)" to say "Same as" to
        if (Item is not null && Item.Decision.Method == DecisionMethod.Queued) LongerForms = await Services.Store.LongerFormsAsync(Item.String.Id, 8, HttpContext.RequestAborted);
        if (Item is not null && Item.Decision.Method == DecisionMethod.Queued && Item.String.Context == "publisher")
        {
            var onCard = Item.Evidence.Groups.SelectMany(g => g.Members).Select(m => m.Uri).ToHashSet(StringComparer.Ordinal);
            FullerNames = (await Project.Desk.FullerNamesAsync(Item.Evidence.Reading?.Text is { Length: > 0 } read ? read : Item.String.Raw, 12, HttpContext.RequestAborted)).Where(r => !onCard.Contains(r.Uri)).ToList();
        }
        if (Item is not null && Item.Decision.Method == DecisionMethod.Queued)
            StandingOn = await Services.Store.StandingOnAsync(Dataset, Item.Evidence.Groups.SelectMany(g => g.Members).Where(m => !m.IsStore).Select(m => m.Uri).Concat(FullerNames.Select(r => r.Uri)).Distinct().ToList(), HttpContext.RequestAborted);
        if (Item is not null)
            foreach (var m in Item.Evidence.Groups.SelectMany(g => g.Members))
            {
                if (Item.Entities.TryGetValue(m.Uri, out var known)) { if (!known.IsPlaceholder) CandidateEntity[m.Uri] = (known.Entity.Id, known.Entity.Label); }
                else if (Item.Holders is { } hs && hs.TryGetValue(m.Uri, out var holder)) CandidateEntity[m.Uri] = (holder.Entity.Id, holder.Entity.Label);
                else if (StandingOn.TryGetValue(m.Uri, out var st)) CandidateEntity[m.Uri] = (st.EntityId, st.EntityLabel);
            }
        if (Item is not null)
        {
            foreach (var d in await Services.Store.RecordDecisionsForStringAsync(Item.String.Id, HttpContext.RequestAborted)) RecordDecisions[d.System + "|" + d.RecordId] = d;
            if (Item.Decision.Method == DecisionMethod.Queued)
            {
                Mates.AddRange(Item.Siblings.Concat(Item.Evidence.Groups.SelectMany(g => g.Members).Where(m => m.IsStore && Item.Entities.TryGetValue(m.Uri, out var f) && f.IsPlaceholder)
                    .SelectMany(m => Item.Entities[m.Uri].Strings)).Where(x => x.String.Id != Item.String.Id && x.Decision.Method == DecisionMethod.Queued).DistinctBy(x => x.String.Id).Take(12));
                var held = CandidateEntity.Values.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
                NearSpellings = (await Project.Desk.NearSpellingsAsync(Dataset, Item.Evidence.Reading?.Text is { Length: > 0 } read2 ? read2 : Item.String.Raw, Project.Reconciler.Read(Item.String).Classes, HttpContext.RequestAborted))
                    .Where(n => !held.Contains(n.Id)).ToList();
            }
        }
        if (Item is not null && string.IsNullOrWhiteSpace(Q) && Item.Evidence.Groups.Count == 0)
        {
            Q = Item.Evidence.Reading?.Text is { Length: > 0 } name ? name : Item.String.Raw;
            Searched = true;
            // not for a letter or two: "S" in a place of publication brought forty things of no use (seen as a user, 2026-11-20)
            if (Q.Trim().Length < 3) { Q = null; Searched = false; }
        }
        if (Item is not null && !string.IsNullOrWhiteSpace(Q))
        {
            // the string's own placeholder is under "Currently" already, and "Same as" onto it would decide nothing: left out of the results
            // (the owner, 2026-11-05, "Lawrence" beside its own provisional entity; the desk's form of finding 104)
            // the string's own kind first: a search for a place named "Heaton" opened with five people of that name (seen as a user, 2026-11-19)
            var kind = Project.Reconciler.Read(Item.String).Classes is [var first, ..] ? first : (EntityClass?)null;
            EntityMatches = (await Project.Desk.SearchEntitiesAsync(Dataset, Q)).Where(e => e.Id != Item.Entity?.Id).OrderByDescending(e => e.Class == kind ? 1 : 0).ToList();
            IndexMatches = (await Project.Desk.SearchIndexesAsync(Q)).OrderByDescending(r => kind is not null && string.Equals(r.Type, kind.ToString(), StringComparison.OrdinalIgnoreCase) ? 1 : 0).ToList();
            foreach (var e in EntityMatches.Take(15)) MatchClues[e.Id] = await Project.Desk.ClueAsync(e, HttpContext.RequestAborted);
            var words = Q.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var k = words.Length - 1; k >= 1 && EntityMatches.Count == 0 && IndexMatches.Count == 0; k--)
            {
                var name = string.Join(' ', words[..k]);
                IndexMatches = (await Project.Desk.SearchIndexesAsync(name, null, 12)).OrderByDescending(r => kind is not null && string.Equals(r.Type, kind.ToString(), StringComparison.OrdinalIgnoreCase) ? 1 : 0).ToList();
                if (IndexMatches.Count > 0) NearName = name;
            }
        }
        if (Item is not null && Confirm && Targets is { Length: > 0 })
        {
            try { Plan = await Project.Desk.PlanTogetherAsync(Dataset, Item.String.Id, Targets, Survivor); }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { Message = e.Message; }
        }
    }

    public Task<IActionResult> OnPostAcceptAsync(long id, string target, string? note, string[]? records, long[]? also) =>
        records is { Length: > 0 } ? ByRecord(id, records, target, note)
        : Act(id, async () => ("Accepted.", await Project.Desk.AcceptAsync(Dataset, id, target, Reviewer, note)), also);

    /// <summary>Records were ticked: they are decided, each for itself, and the string is not; the card comes back to show it (design doc 12 section 14.3).</summary>
    private async Task<IActionResult> ByRecord(long id, string[] records, string target, string? note)
    {
        try
        {
            var (entity, n, all) = await Project.Desk.DecideRecordsAsync(Dataset, id, records, target, Reviewer, note, HttpContext.RequestAborted);
            var said = $"{n} record{(n == 1 ? "" : "s")} decided as {entity.Label}, for {(n == 1 ? "that record" : "those records")} only.";
            return all
                ? Flash(said + " Every record of this string now has its own decision, so the string has left the queue.", new { dataset = Dataset, method = Method, context = Context, after = id }, entity)
                : Flash(said + " The string's other records are still to decide.", new { dataset = Dataset, method = Method, context = Context, id }, entity);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
    }

    public async Task<IActionResult> OnPostRecordUndoAsync(long id, long decision)
    {
        var undone = await Project.Desk.UndoRecordDecisionAsync(Dataset, id, decision, HttpContext.RequestAborted);
        return Flash(undone ? "That record's own decision is taken back; the string's decision stands for it again." : "Nothing to undo.", new { dataset = Dataset, method = Method, context = Context, id });
    }

    public async Task<IActionResult> OnPostAcceptTogetherAsync(long id, string[]? targets, string? note, string? survivor, bool confirmed, bool always, EntityClass? cls = null, string[]? records = null, long[]? also = null)
    {
        // a group's tick carries its members' URIs in one value, space-joined (the group is the card, 2026-11-06)
        var all = (targets ?? []).SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Distinct(StringComparer.Ordinal).ToArray();
        if (all.Length == 0) return Flash("Tick the records that are the thing first.", new { dataset = Dataset, method = Method, context = Context, id });
        // records ticked: decided for themselves onto the group's entity of the project, or its first record
        if (records is { Length: > 0 }) return await ByRecord(id, records, all.FirstOrDefault(Project.Scheme.Owns) ?? all[0], note);
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
        }, also);
    }

    public Task<IActionResult> OnPostSameAsAsync(long id, string entity, string? note, string[]? records, long[]? also) =>
        records is { Length: > 0 } ? ByRecord(id, records, entity, note)
        : Act(id, async () => ("Marked as the same entity.", await Project.Desk.SameAsAsync(Dataset, id, entity, Reviewer, note)), also);

    /// <summary>Pulls the records named (LoC's and ULAN's among a card's candidates, twelve at most) so that the card can show what each heading was made from; decides nothing.</summary>
    public async Task<IActionResult> OnPostWorksAsync(long id, string[]? uris)
    {
        var failed = 0;
        foreach (var uri in (uris ?? []).Where(LinkedArtNet.Alignment.Authorities.SourcePull.CanPull).Distinct().Take(12))
        {
            try { if ((await Project.Pull.PullAsync(uri, HttpContext.RequestAborted)).Error is not null) failed++; }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { failed++; }
        }
        TempData["Flash"] = failed == 0 ? "Fetched: each candidate now says what its heading was made from, where its source says." : $"Fetched, but {failed} could not be reached just now.";
        return RedirectToPage(new { dataset = Dataset, method = Method, context = Context, id });
    }

    public Task<IActionResult> OnPostLocalAsync(long id, string? label, string? note, string? url, EntityClass? cls) =>
        Act(id, async () => (string.IsNullOrWhiteSpace(url) ? "Kept as a local authority." : "Kept as a local authority, with the URL as its identifier.", await Project.Desk.KeepLocalAsync(Dataset, id, Reviewer, label, note, url, cls)));

    /// <summary>The string is not a name (the owner, 2026-11-22): no entity, and it leaves the queue. The reason is one of two, and the desk goes on.</summary>
    public async Task<IActionResult> OnPostNotANameAsync(long id, string? reason, string? note)
    {
        try { await Project.Curation.NotANameAsync(Dataset, id, Reviewer, reason ?? "", note, HttpContext.RequestAborted); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
        return Flash(reason == CurationService.NothingKnown
            ? "Decided: not a name, the cataloguer's way of saying nothing is known. No entity stands for it."
            : "Decided: not a name, and its record wants fixing. No entity stands for it.", new { dataset = Dataset, method = Method, context = Context, after = id });
    }

    /// <summary>A way out: the record is wrong. The finding is raised, and the card comes back searched for what the text should have read.</summary>
    public async Task<IActionResult> OnPostRecordWrongAsync(long id, string kind, string? shouldRead, string? note)
    {
        int raised;
        try { raised = await Project.Desk.RecordWrongAsync(Dataset, id, Reviewer, kind, shouldRead, note, HttpContext.RequestAborted); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
        var said = raised == 0 ? "Already on the cataloguers' list for this." : $"Logged for the cataloguers, on {raised} record{(raised == 1 ? "" : "s")}.";
        return string.IsNullOrWhiteSpace(shouldRead)
            ? Flash(said + " The string is still to decide.", new { dataset = Dataset, method = Method, context = Context, id })
            : FlashAt(said + $" Now choose what it should have been, from the results for \"{shouldRead.Trim()}\" below.", new { dataset = Dataset, method = Method, context = Context, id, q = shouldRead.Trim() }, "something-else");
    }

    /// <summary>A way out: set aside, as different things in different records or with a question. The desk goes on.</summary>
    public async Task<IActionResult> OnPostSetAsideAsync(long id, bool mixed, string? text)
    {
        try { await Project.Desk.SetAsideAsync(Dataset, id, Reviewer, mixed, text, HttpContext.RequestAborted); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
        return Flash(mixed ? "Set aside: it wants deciding record by record. It is on the Set aside list." : "Set aside with your question. It is on the Set aside list.", new { dataset = Dataset, method = Method, context = Context });
    }

    /// <summary>A way out: an entity of the project is wrong. A note on the entity; the card stays.</summary>
    public async Task<IActionResult> OnPostReportEntityAsync(long id, string entity, string? text)
    {
        try
        {
            var reported = await Project.Desk.ReportEntityAsync(Dataset, id, entity, Reviewer, text, HttpContext.RequestAborted);
            return Flash($"\"{reported.Label}\" reported as wrong, with your note on it. It is on the Set aside list. The string is still to decide.", new { dataset = Dataset, method = Method, context = Context, id }, reported);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
    }

    public Task<IActionResult> OnPostOverrideAsync(long id, string target, string? note) =>
        Act(id, async () => ("Override saved and applied.", await Project.Desk.OverrideAsync(Dataset, id, target, Reviewer, note)));

    public async Task<IActionResult> OnPostFlagAsync(long id, string text)
    {
        if (!string.IsNullOrWhiteSpace(text)) await Project.Desk.FlagAsync(Dataset, id, Reviewer, text.Trim());
        return Flash("Note added.", new { dataset = Dataset, method = Method, context = Context, id });
    }

    /// <summary>Skip sends the string to the back of the desk's order, kept in the store, so it does not come back to the front next session (the owner, 2026-11-07).</summary>
    public async Task<IActionResult> OnPostSkipAsync(long id)
    {
        try { await Project.Desk.SkipAsync(Dataset, id); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
        // from the front: the skipped string is now at the back, and the next unskipped one is the desk's next
        return RedirectToPage(new { dataset = Dataset, method = Method, context = Context });
    }

    public async Task<IActionResult> OnPostUnskipAsync(long id)
    {
        try { await Project.Desk.UnskipAsync(Dataset, id); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
        return Flash("Back in its place.", new { dataset = Dataset, method = Method, context = Context, id });
    }

    public async Task<IActionResult> OnPostUndoAsync(long id)
    {
        Decision? restored;
        try { restored = await Project.Desk.UndoAsync(Dataset, id); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException) { return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id }); }
        return Flash(restored is null ? "Nothing to undo." : $"Undone; the earlier decision ({restored.Method}) stands again.", new { dataset = Dataset, method = Method, context = Context, id });
    }

    /// <summary>A flash that lands at a part of the page, where what it speaks of is.</summary>
    private IActionResult FlashAt(string message, object routeValues, string fragment)
    {
        TempData["Flash"] = message;
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: routeValues, fragment: fragment);
    }

    private async Task<IActionResult> Act(long id, Func<Task<(string Message, Entity Entity)>> action, long[]? also = null)
    {
        // the queued strings whose placeholders stand among this card's candidates, read before the card is decided: a spelling near
        // enough to be offered is the next thing to settle (seen as a user, 2026-11-20: after "Allen and Unwin" the desk left "Allen and
        // Unwin [etc.]", a candidate on that card, and went on down the list)
        List<DecidedString> cardmates = [];
        var correcting = false;
        if (await Project.Desk.GetAsync(id) is { } card && card.String.Dataset == Dataset)
        {
            // a string that was not waiting is being corrected: the person wants to see the card they changed, not the queue
            // (seen as a user, 2026-11-22, moving "Charterhouse": the desk went on to a string of the queue)
            correcting = card.Decision.Method is not (DecisionMethod.Queued or DecisionMethod.Minted);
            cardmates = card.Evidence.Groups.SelectMany(g => g.Members).Where(m => m.IsStore && card.Entities.TryGetValue(m.Uri, out var f) && f.IsPlaceholder)
                .SelectMany(m => card.Entities[m.Uri].Strings).Where(x => x.String.Id != id && x.Decision.Method == DecisionMethod.Queued).DistinctBy(x => x.String.Id).ToList();
        }
        (string Message, Entity Entity) done;
        try { done = await action(); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            return Flash(e.Message, new { dataset = Dataset, method = Method, context = Context, id });
        }
        // the desk finishes a name before it leaves it: the other queued strings under this one's name come next, in list order, and only
        // then the list goes on (the owner, 2026-11-06, after the first merge at "Chris Beetles Ltd")
        // the strings ticked as the same thing written another way are decided with it, in the one act
        var with = 0;
        foreach (var other in (also ?? []).Distinct().Where(x => x != id))
        {
            try { await Project.Desk.SameAsAsync(Dataset, other, done.Entity.Id, Reviewer, $"decided with string {id}, as the same thing"); with++; }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException) { }
        }
        if (with > 0) done = (done.Message + $" {with} more string{(with == 1 ? "" : "s")} decided with it.", done.Entity);
        if (correcting) return Flash(done.Message + " This card shows it as it now stands.", new { dataset = Dataset, method = Method, context = Context, id }, done.Entity);
        var siblings = await Project.Desk.SiblingsAsync(Dataset, id, done.Entity);
        if (siblings.Count == 0)
        {
            foreach (var mate in cardmates)
            {
                // still waiting, the decision just made not having settled it too
                if (await Project.Desk.GetAsync(mate.String.Id) is not { Decision.Method: DecisionMethod.Queued }) continue;
                return Flash($"{done.Message} Next: \"{mate.String.Raw}\" in {mate.String.Context}, a spelling that was among that card's candidates.", new { dataset = Dataset, method = Method, context = Context, id = mate.String.Id }, done.Entity);
            }
            return Flash(done.Message, new { dataset = Dataset, method = Method, context = Context, after = id }, done.Entity);
        }
        var next = siblings[0];
        var more = siblings.Count - 1;
        return Flash($"{done.Message} Next: \"{next.String.Raw}\" in {next.String.Context}, also under this name{(more == 0 ? "" : $" ({more} more)")}.", new { dataset = Dataset, method = Method, context = Context, id = next.String.Id }, done.Entity);
    }
}
