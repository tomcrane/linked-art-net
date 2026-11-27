using System.Text.Json;
using System.Text.Json.Serialization;
using LinkedArtNet.Alignment.Import;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>Strings that resemble a seed, grouped by the entity each currently resolves to (null for undecided).</summary>
public sealed record VariantGroup(Entity? Entity, IReadOnlyList<DecidedOrOpenString> Strings);
public sealed record DecidedOrOpenString(SourceString String, Decision? Decision);

/// <summary>An entity with the strings that resolve to it and its notes, for the entity page.</summary>
/// <summary>
/// An identifier another entity of the project already holds (doc 12 section 13, item 3, from the earlier Authority Server's live check):
/// adding it here would make a twin, so the add is refused with the holder named, and the page offers the merge instead.
/// </summary>
public sealed class IdentifierInUseException(string uri, Entity holder)
    : InvalidOperationException($"{uri} is already held by the project's entity \"{holder.Label}\" ({holder.Id}): merge into it rather than make a twin.")
{
    public string Uri { get; } = uri;
    public Entity Holder { get; } = holder;
}

/// <param name="Parts">When the entity is a compound heading's own concept: its parts in order, each with its standing decision and the entity it resolves to.</param>
public sealed record EntityView(Entity Entity, IReadOnlyList<DecidedString> Strings, IReadOnlyList<Note> Notes, IReadOnlyDictionary<string, AuthorityRecord> Records, IReadOnlyList<CompoundPart> Parts)
{
    public bool IsCompound => Parts.Count > 0;
}

/// <summary>A part of a compound heading as the entity page shows it: the part string, what was decided about it, and the entity that decision points at.</summary>
public sealed record CompoundPart(SourceString Part, Decision? Decision, Entity? Entity);

/// <summary>
/// Collection knowledge as a first-class input (design doc 12, section 6): clustering a set of variant spellings
/// into one entity with overrides, attaching or removing authority ids, confirming an entity as a local authority,
/// reopening strings, merging entities, and exporting the project's overrides in the pmc-lux JSON shape so the
/// curated file is an output of review.
/// </summary>
public sealed class CurationService(IAlignmentStore store, IdentifierScheme scheme, Reconciler reconciler)
{
    // ---- reading ---------------------------------------------------------------------------------------------------

    public async Task<EntityView?> EntityAsync(string dataset, string entityId, CancellationToken ct = default)
    {
        var entity = await store.GetEntityAsync(entityId, ct);
        if (entity is null || entity.Dataset != dataset) return null;
        var strings = await store.StringsForEntityAsync(entity.Id, 200, ct);
        var notes = await store.NotesAsync(dataset, entity.Id, ct);
        var records = new Dictionary<string, AuthorityRecord>(StringComparer.Ordinal);
        foreach (var q in entity.Equivalents) if (await store.GetAuthorityAsync(q.Uri, ct) is { } r) records[q.Uri] = r;
        // a compound heading's own concept (the owner, 2026-11-07, at "Ackermann & Son, Arthur (London)--History" in the entities list,
        // a provisional Type that said nothing of what it was): its parts, each with what it resolved to
        var parts = new List<CompoundPart>();
        if (strings.FirstOrDefault(s => s.Decision.Method == DecisionMethod.Composite) is { } heading)
            foreach (var part in await store.PartsAsync(heading.String.Id, ct))
            {
                var decision = await store.DecisionForAsync(part.Id, ct);
                var target = decision?.EntityId is null ? null : await store.ResolveEntityAsync(decision.EntityId, ct);
                parts.Add(new CompoundPart(part, decision, target));
            }
        return new EntityView(entity, strings, notes, records, parts);
    }

    /// <summary>Strings of the dataset containing a word, with the entity each resolves to, so a cluster of spellings can be seen as one screen.</summary>
    public async Task<IReadOnlyList<VariantGroup>> VariantsAsync(string dataset, string seed, int limit = 200, CancellationToken ct = default)
    {
        var word = SeedWord(seed);
        if (word.Length < 2) return [];
        var strings = await store.FindStringsAsync(dataset, word, limit, ct);
        var groups = new Dictionary<string, List<DecidedOrOpenString>>(StringComparer.Ordinal);
        var entities = new Dictionary<string, Entity?>(StringComparer.Ordinal);
        foreach (var s in strings)
        {
            var decision = await store.DecisionForAsync(s.Id, ct);
            var entityId = decision?.EntityId is null ? null : (await store.ResolveEntityAsync(decision.EntityId, ct))?.Id;
            var key = entityId ?? "";
            if (!groups.TryGetValue(key, out var list)) { groups[key] = list = []; entities[key] = entityId is null ? null : await store.GetEntityAsync(entityId, ct); }
            list.Add(new DecidedOrOpenString(s, decision));
        }
        return groups.OrderByDescending(g => g.Value.Count).Select(g => new VariantGroup(entities[g.Key], g.Value.OrderBy(x => x.String.Raw, StringComparer.OrdinalIgnoreCase).ToList())).ToList();
    }

    /// <summary>Words that say what kind of body something is rather than which one: left out when choosing a seed's distinctive word. Written from general knowledge of catalogue headings.</summary>
    private static readonly HashSet<string> Generic = new(StringComparer.Ordinal)
    {
        "the", "of", "and", "for", "in", "at", "de", "du", "la", "le", "des", "von", "van", "der",
        "sons", "son", "co", "company", "firm", "ltd", "limited", "inc", "brothers", "bros", "associates", "partners",
        "gallery", "galleries", "galerie", "museum", "museums", "musee", "library", "archive", "archives", "collection", "trust", "foundation", "fund",
        "society", "association", "institute", "institution", "academy", "college", "school", "university", "centre", "center", "council", "committee", "department",
        "royal", "national", "british", "english", "scottish", "irish", "american", "international", "fine", "art", "arts", "modern", "contemporary", "city", "new", "old",
        "mr", "mrs", "miss", "dr", "sir", "lady", "lord",
    };

    /// <summary>The most distinctive word of a seed: the longest token of its comparison form that is neither a generic word nor an expanded given-name abbreviation ("Thos. Agnew &amp; Sons Ltd" gives "agnew").</summary>
    public static string SeedWord(string seed)
    {
        var tokens = NameForms.Canonical(StringKey.Normalise(seed)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expanded = NameForms.Abbreviations.Values.ToHashSet(StringComparer.Ordinal);
        var distinctive = tokens.Where(t => !Generic.Contains(t) && !expanded.Contains(t)).OrderByDescending(t => t.Length).FirstOrDefault();
        return distinctive ?? tokens.OrderByDescending(t => t.Length).FirstOrDefault() ?? StringKey.Normalise(seed);
    }

    // ---- clustering ------------------------------------------------------------------------------------------------

    /// <summary>
    /// The reviewer says these strings are one thing: an existing entity, or a new confirmed local authority with the
    /// label given. External ids attach to it; each string gets an override for its spelling in the field it came from
    /// (or in any field when asked), any provisional entity it had folds in, and a human decision points it at the
    /// entity. Returns the entity.
    /// </summary>
    public async Task<Entity> ClusterAsync(string dataset, IReadOnlyList<long> stringIds, string? entityId, string? newLabel, IReadOnlyList<string> externalUris, string reviewer, string? note = null, bool anyField = false, CancellationToken ct = default)
    {
        if (stringIds.Count == 0) throw new InvalidOperationException("Choose at least one string.");
        Entity entity;
        var created = string.IsNullOrWhiteSpace(entityId);
        if (!created)
        {
            entity = await store.ResolveEntityAsync(entityId!, ct) ?? throw new KeyNotFoundException($"No entity {entityId}.");
            if (entity.Dataset != dataset) throw new InvalidOperationException("The entity belongs to another project.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(newLabel)) throw new InvalidOperationException("Give the new entity a label, or choose an existing one.");
            var first = await OwnAsync(dataset, stringIds[0], ct);
            var reading = reconciler.Read(first);
            // the class is the record's when an identifier given here has one the field allows, the field's first otherwise (the owner,
            // 2026-11-07, at "Ackermann and Son": a persauthorfull cluster given ULAN's corporate body was a Person holding a Group's record)
            var cls = await ClassFromRecordsAsync(externalUris, reading, ct) ?? reading.Classes[0];
            entity = await store.AddEntityAsync(dataset, new NewEntity(cls, newLabel.Trim(), [], reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Confirmed), scheme, ct);
        }
        var added = new List<string>();
        foreach (var uri in externalUris.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal))
            added.Add(await AddIdentifierAsync(dataset, entity, uri, ct));

        var labels = new List<string>();
        foreach (var id in stringIds.Distinct())
        {
            var s = await OwnAsync(dataset, id, ct);
            var decision = await store.DecisionForAsync(s.Id, ct);
            if (decision?.EntityId is not null && decision.EntityId != entity.Id)
            {
                var current = await store.ResolveEntityAsync(decision.EntityId, ct);
                if (current is not null && current.Id != entity.Id && current.Status == EntityStatus.Provisional && current.Equivalents.Count == 0)
                { try { await store.MergeAsync(current.Id, entity.Id, reviewer, ct); } catch (InvalidOperationException) { } }
            }
            // the override is scoped to the field the string came from: the same spelling in another field may be something else
            await store.PutOverrideAsync(new Override(dataset, anyField ? null : s.Context, s.Normalised, entity.Id, "workbench:" + reviewer, $"\"{s.Raw}\" clustered" + (note is null ? "" : $": {note}")), ct);
            var reading = reconciler.Read(s);
            await store.DecideAsync(s.Id, entity.Id, DecisionMethod.Human, Reconciler.Evidence("cluster", reading, [], new { entity = entity.Id, note, strings = stringIds.Count }), reviewer, ct: ct);
            labels.Add(reading.Text); labels.Add(s.Raw);
        }
        var fresh = (await store.GetEntityAsync(entity.Id, ct))!;
        var allLabels = fresh.Labels.Concat(labels).Where(l => !string.IsNullOrWhiteSpace(l) && l != fresh.Label).Distinct(StringComparer.Ordinal).ToList();
        if (allLabels.Count != fresh.Labels.Count) await store.UpdateEntityAsync(fresh with { Labels = allLabels }, ct);
        // one audit note for the whole cluster (doc 12 section 13, item 1), the person's own words after it
        var n = stringIds.Distinct().Count();
        await store.AddNoteAsync(dataset, entity.Id, reviewer, $"{n} string{(n == 1 ? "" : "s")} clustered {(created ? "into this new entity" : "here")}"
            + (added.Count == 0 ? "" : ", with " + string.Join(", ", added)) + (string.IsNullOrWhiteSpace(note) ? "." : ": " + note.Trim()), ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    // ---- entity actions --------------------------------------------------------------------------------------------

    /// <summary>The class of the first record among the URIs whose type is a class the string's field allows; null when none says.</summary>
    private async Task<EntityClass?> ClassFromRecordsAsync(IEnumerable<string> uris, StringReading reading, CancellationToken ct)
    {
        foreach (var uri in uris.Where(u => !string.IsNullOrWhiteSpace(u)))
            if (await store.GetAuthorityAsync(uri.Trim(), ct) is { Type: { } type } && Enum.TryParse<EntityClass>(type, true, out var cls) && reading.Classes.Contains(cls))
                return cls;
        return null;
    }

    /// <summary>
    /// A person says what kind of thing the entity is (the owner, 2026-11-07, at "Ackermann and Son", a Person holding ULAN's corporate
    /// body): the class changes and a note on the entity says from what, by whom. The identifier keeps its minted path.
    /// </summary>
    public async Task<Entity> SetClassAsync(string dataset, string entityId, EntityClass cls, string reviewer, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        if (entity.Class == cls) return entity;
        await store.UpdateEntityAsync(entity with { Class = cls }, ct);
        await store.AddNoteAsync(dataset, entity.Id, reviewer, $"Class changed from {entity.Class} to {cls}.", ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>Notes are the audit log (doc 12 section 13, item 1, 2026-11-08): every hand change writes one with before and after, so the entity page's History and the home page's Recent changes read them.</summary>
    public async Task<Entity> SetLabelAsync(string dataset, string entityId, string label, string? reviewer = null, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        label = label.Trim();
        if (label.Length == 0) throw new InvalidOperationException("A label is needed.");
        if (label == entity.Label) return entity;
        var labels = entity.Labels.Append(entity.Label).OfType<string>().Where(l => l != label).Distinct(StringComparer.Ordinal).ToList();
        await store.UpdateEntityAsync(entity with { Label = label, Labels = labels }, ct);
        await store.AddNoteAsync(dataset, entity.Id, reviewer, $"Label changed from \"{entity.Label}\" to \"{label}\".", ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>
    /// A person sets a name of the entity aside, or restores it (the owner, 2026-11-15, at "Plymouth": TGN's record gives the former name
    /// "Sutton", the entity gathered it, and the place of publication "Sutton", of two books from Sutton in London, was matched to
    /// Plymouth by it). Set aside, the name is kept and shown as such, no string is matched on it, no pull or merge puts it back, and the
    /// strings a rule (not a person) had resolved here by that name are reopened for the next reconcile. Returns how many were reopened.
    /// </summary>
    public async Task<int> SetNameAsideAsync(string dataset, string entityId, string name, bool aside, string? reviewer = null, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        if (!await store.SetLabelAsideAsync(entity.Id, name, aside, ct)) return 0;
        await store.AddNoteAsync(dataset, entity.Id, reviewer, aside
            ? $"The name \"{name}\" set aside: no string is matched to this entity on it."
            : $"The name \"{name}\" restored: strings may be matched to this entity on it again.", ct);
        if (!aside) return 0;
        var after = (await store.GetEntityAsync(entity.Id, ct))!;
        var standing = after.Label is null ? after.Labels : after.Labels.Prepend(after.Label);
        var byThatName = new List<long>();
        foreach (var s in await store.StringsForEntityAsync(entity.Id, 1000, ct))
        {
            if (s.Decision.Method == DecisionMethod.Human || s.Decision.Reviewer is not null) continue;
            var text = reconciler.Read(s.String).Text;
            if (NameMatcher.Score(text, [name]) >= 0.97 && NameMatcher.Score(text, standing) < 0.97) byThatName.Add(s.String.Id);
        }
        return byThatName.Count == 0 ? 0 : await ReopenAsync(dataset, byThatName, reviewer, entity.Id, ct);
    }

    /// <summary>Attaches an authority URI the store knows (a cached record) or that lives in a namespace the toolkit recognises, an ArchivesSpace agent's public URL among them.</summary>
    public async Task<Entity> AddEquivalentAsync(string dataset, string entityId, string uri, string reviewer, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        uri = await AddIdentifierAsync(dataset, entity, uri, ct);
        await store.AddNoteAsync(dataset, entity.Id, reviewer, $"Identifier added: {uri} ({AuthorityUris.SourceOf(uri)}).", ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>The checks and the write behind "Add identifier", without its note: the cluster writes one note for all of its identifiers.</summary>
    private async Task<string> AddIdentifierAsync(string dataset, Entity entity, string uri, CancellationToken ct)
    {
        uri = AuthorityUris.Canonical(uri);
        if (!Uri.TryCreate(uri, UriKind.Absolute, out _)) throw new InvalidOperationException("The identifier is not a URI.");
        if (scheme.Owns(uri)) throw new InvalidOperationException("Use merge for one of our own entities.");
        if (await HolderOfAsync(dataset, uri, entity.Id, ct) is { } holder) throw new IdentifierInUseException(uri, holder);
        var known = await store.GetAuthorityAsync(uri, ct) is not null;
        if (!known && AuthorityUris.SourceOf(uri) == "other") throw new InvalidOperationException("Not a known authority record and not in a recognised namespace (ULAN, AAT, TGN, VIAF, Wikidata, GND, LoC, LUX, PeriodO, or an ArchivesSpace agent's URL).");
        await store.AddEquivalentAsync(entity.Id, new Equivalent(uri, AuthorityUris.SourceOf(uri), DecisionMethod.Human, 1.0, DateTimeOffset.UtcNow), ct);
        return uri;
    }

    /// <summary>The project's other entity that holds this URI, if any: not merged away, and not the entity it is being added to.</summary>
    public async Task<Entity?> HolderOfAsync(string dataset, string uri, string? exceptEntityId, CancellationToken ct = default)
    {
        foreach (var e in await store.FindEntitiesByEquivalentAsync(uri.Trim(), ct))
            if (e.Dataset == dataset && e.MergedInto is null && e.Id != exceptEntityId) return e;
        return null;
    }

    public async Task<Entity> RemoveEquivalentAsync(string dataset, string entityId, string uri, string? reviewer = null, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        if (await store.RemoveEquivalentAsync(entity.Id, uri, ct))
            await store.AddNoteAsync(dataset, entity.Id, reviewer, $"Identifier removed: {uri} ({entity.Equivalents.FirstOrDefault(q => q.Uri == uri)?.Source ?? AuthorityUris.SourceOf(uri)}).", ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>A person vouches for the entity: confirmed, in no external source or with the ids it has.</summary>
    public async Task<Entity> ConfirmAsync(string dataset, string entityId, string reviewer, string? note, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        await store.UpdateEntityAsync(entity with { Status = EntityStatus.Confirmed }, ct);
        await store.AddNoteAsync(dataset, entity.Id, reviewer, $"Confirmed (was {entity.Status.ToString().ToLowerInvariant()}){(string.IsNullOrWhiteSpace(note) ? "." : ": " + note.Trim())}", ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>
    /// Merges the loser into the survivor; <paramref name="cls"/>, given, is the merged entity's class (the two differed, and the person
    /// chose). The loser's queued and minted strings are decided onto the survivor first, as the person's own (<see cref="MergeSettlement"/>).
    /// </summary>
    public async Task<Entity> MergeAsync(string dataset, string loserId, string survivorId, string reviewer, EntityClass? cls = null, CancellationToken ct = default)
    {
        var loser = await OwnEntityAsync(dataset, loserId, ct);
        var survivor = await OwnEntityAsync(dataset, survivorId, ct);
        var loserStrings = (await store.StringsForEntityAsync(loser.Id, 1000, ct)).Count;
        await MergeSettlement.SettleAsync(store, reconciler, loser, survivor, reviewer, null, ct);
        await store.MergeAsync(loserId, survivor.Id, reviewer, ct);
        await MergeNotesAsync(store, dataset, loser, survivor, loserStrings, reviewer, ct);
        if (cls is { } chosen && chosen != survivor.Class) await SetClassAsync(dataset, survivor.Id, chosen, reviewer, ct);
        return (await store.ResolveEntityAsync(survivor.Id, ct))!;
    }

    /// <summary>
    /// The strings whose standing queued or minted decision points at an entity that is no longer a placeholder: a person merged their
    /// placeholder into it before merges settled strings (2026-11-07). Listed, and with <paramref name="apply"/> decided onto that entity as
    /// human, with the earlier method in the evidence.
    /// </summary>
    public async Task<IReadOnlyList<(DecidedString String, Entity Entity)>> SettleResolvedAsync(string dataset, string reviewer, bool apply, CancellationToken ct = default)
    {
        var found = new List<(DecidedString, Entity)>();
        foreach (var method in new[] { DecisionMethod.Queued, DecisionMethod.Minted })
        {
            long after = 0;
            while (true)
            {
                var page = await store.DecidedStringsAsync(dataset, method, null, after, 500, ct: ct);
                if (page.Count == 0) break;
                after = page[^1].String.Id;
                foreach (var s in page)
                {
                    if (s.Decision.EntityId is null) continue;
                    var entity = await store.ResolveEntityAsync(s.Decision.EntityId, ct);
                    if (entity is null || entity.Status == EntityStatus.Provisional) continue;
                    found.Add((s, entity));
                    if (apply)
                        await store.DecideAsync(s.String.Id, entity.Id, DecisionMethod.Human,
                            Reconciler.Evidence("same-as-by-merge", reconciler.Read(s.String), [], new { entity = entity.Id, merged = s.Decision.EntityId, was = s.Decision.Method, settledLater = true }), reviewer, ct: ct);
                }
            }
        }
        return found;
    }

    /// <summary>The merge written on both sides: what the survivor took in, and where the loser went (the audit log, doc 12 section 13).</summary>
    public static async Task MergeNotesAsync(IAlignmentStore store, string dataset, Entity loser, Entity survivor, int loserStrings, string? reviewer, CancellationToken ct)
    {
        var ids = loser.Equivalents.Select(q => $"{q.Source}'s {q.Uri.Split('/').Last()}").ToList();
        await store.AddNoteAsync(dataset, survivor.Id, reviewer, $"Merged in \"{loser.Label}\" ({loser.Id}): {loserStrings} string{(loserStrings == 1 ? "" : "s")}{(ids.Count == 0 ? ", no identifier" : ", identifiers " + string.Join(", ", ids))}.", ct);
        await store.AddNoteAsync(dataset, loser.Id, reviewer, $"Merged into \"{survivor.Label}\" ({survivor.Id}).", ct);
    }

    /// <summary>
    /// Takes chosen strings away from an entity. Taken off by a person from the entity's page (<paramref name="decideAfresh"/>), each is
    /// decided again there and then from the authorities alone, the project's entities not asked, so the entity it was taken from cannot
    /// claim it back by its name; what the rules cannot settle waits at the desk. Otherwise the strings are left undecided, for the
    /// next reconcile or for their card to decide as it opens.
    /// </summary>
    public async Task<int> ReopenAsync(string dataset, IReadOnlyList<long> stringIds, string? reviewer = null, string? entityId = null, CancellationToken ct = default, bool decideAfresh = false)
    {
        var n = 0;
        var raws = new List<string>();
        foreach (var id in stringIds.Distinct())
        {
            var s = await OwnAsync(dataset, id, ct);
            if (!await store.ReopenStringAsync(id, ct)) continue;
            n++; raws.Add($"\"{s.Raw}\" ({s.Context})");
            if (decideAfresh) await reconciler.ReconcileOneAfreshAsync(dataset, s, null, ct);
        }
        if (n > 0 && entityId is not null) await store.AddNoteAsync(dataset, entityId, reviewer, $"{n} string{(n == 1 ? "" : "s")} {(decideAfresh ? "taken off this entity and decided again without it" : "reopened for the next reconcile")}: {string.Join(", ", raws.Take(6))}{(raws.Count > 6 ? ", ..." : "")}.", ct);
        return n;
    }

    public Task<Note> NoteAsync(string dataset, string entityId, string reviewer, string text, CancellationToken ct = default) => store.AddNoteAsync(dataset, entityId, reviewer, text, ct);

    // ---- checks on the store's entities ------------------------------------------------------------------------------

    /// <summary>How the notes that leave a row of the checks undecided begin: the Decide screen's own, and the two the assistant wrote on the entities' pages before the screen could.</summary>
    /// <summary>How a note begins that says two entities the duplicates check paired are two things; the other's id follows, so the pair leaves the list.</summary>
    public const string TwoThings = "At the checks, two things and not one: not the same as ";

    /// <summary>The check "entities that may be one thing": pairs of the project's own entities, for a person to merge or to say are two.</summary>
    public Task<IReadOnlyList<PossibleDuplicate>> PossibleDuplicatesAsync(string dataset, CancellationToken ct = default) => store.PossibleDuplicatesAsync(dataset, TwoThings, ct);

    // ---- not a name --------------------------------------------------------------------------------------------------

    /// <summary>Why a string is not a name: the cataloguer wrote that nothing is known ("s.n.", sine nomine; "S.l.", sine loco; RDA's "[publisher not identified]"). The record is as it should be.</summary>
    public const string NothingKnown = "nothing-known";
    /// <summary>Why a string is not a name: it is what is left of a slip ("S", "21"), and the record wants a cataloguer's eye.</summary>
    public const string RecordWantsFixing = "record-wants-fixing";

    /// <summary>
    /// What the third check lists by: a string that is "s.n." or "S.l." however spaced and bracketed, that ends "not identified", that is
    /// one letter (with or without a question mark), or that has no letter in it at all ("?", "21", "(1986)"). Counted before it was
    /// built (2026-11-22): 25 entities or so in the PMC project, some 1,300 records on them. Two letters are not listed: "UK", "RA" and
    /// "EP" are names or initials, for the desk. People, bodies and places only: the first list shown, uncounted beyond the name fields,
    /// had 160 rows, 136 of them the date subdivisions of subject headings ("1837-1901"), which are concepts and rightly entities.
    /// </summary>
    public const string NotNamePattern = @"^\W*(s\W*[nl]|.*not identified|[[:alpha:]]\??|[^[:alpha:]]+)\W*$";

    private static readonly System.Text.RegularExpressions.Regex SaysNothingKnown = new(@"^\W*(s\W*[nl]|.*not identified|\?+)\W*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>The reason the check suggests for a string it lists: nothing known for the cataloguer's own forms and a bare "?", a record to fix for a letter or a number.</summary>
    public static string SuggestedReason(IEnumerable<string> raws) => raws.All(SaysNothingKnown.IsMatch) ? NothingKnown : RecordWantsFixing;

    /// <summary>The check "entities that are not names": placeholders and minted entities whose every string is no name.</summary>
    /// <summary>The note that takes a place off the fourth check's list: a person has looked, and no gazetteer has it (a house, a region of a library's own making).</summary>
    public const string NoGazetteerRecord = "At the checks: no gazetteer record is this place";

    /// <summary>
    /// The fourth check (the owner, 2026-11-23, after "Chatham": "entities that say too little"; counted 2026-11-24, 617 places, 252
    /// of them then given their record by the pass through their headings): the places still without a gazetteer's or Wikidata's
    /// identifier whose name the gazetteer has. One of the name, or one at home among several, is offered in a press; the rest are
    /// found on the entity's page. A place whose name the gazetteer has not is not listed: there is nothing to give it.
    /// </summary>
    public async Task<IReadOnlyList<ThinPlace>> ThinPlacesAsync(string dataset, string? homePlace, CancellationToken ct = default)
    {
        var list = new List<ThinPlace>();
        var kindLabels = new Dictionary<string, string?>(StringComparer.Ordinal);
        async Task<string?> KindOfAsync(AuthorityRecord r)
        {
            if (r.DataJson is null) return null;
            string? type;
            using (var doc = JsonDocument.Parse(r.DataJson)) type = doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("placeType", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;
            if (type is null) return null;
            if (!kindLabels.TryGetValue(type, out var label)) kindLabels[type] = label = (await store.GetAuthorityAsync(type, ct))?.Label;
            return label;
        }
        var gathered = new List<(ThinPlaceRow Row, IReadOnlyList<AuthorityRecord> Named)>();
        foreach (var row in await store.ThinPlacesAsync(dataset, NoGazetteerRecord, ct))
        {
            if (row.Keys.Count == 0) continue;
            var records = await store.FindAuthoritiesByLabelsAsync(row.Keys.ToList(), "tgn", "Place", 30, ct);
            if (records.Count > 0) gathered.Add((row, records));
        }
        var sitelinks = await store.SitelinksOfAsync(gathered.Where(g => g.Named.Count > 1).SelectMany(g => g.Named.Select(r => r.Uri)).ToList(), ct);
        foreach (var (row, records) in gathered)
        {
            var named = records;
            // the records whose own name it is, where there are any: a record that bears the name as a variant only is another place
            // (the owner, 2026-11-24: "Norwich" was offered a search for "Northwich", which the gazetteer also calls Norwich)
            var name = Qualified.Split(row.Label ?? "").Name;
            var key = StringKey.Normalise(name);
            if (named.Where(r => r.Label is not null && StringKey.Normalise(r.Label) == key).ToList() is { Count: > 0 } own) named = own;
            bool Home(AuthorityRecord r) => !string.IsNullOrWhiteSpace(homePlace) && (ReviewService.ChainOf(r) ?? "").Contains(homePlace, StringComparison.OrdinalIgnoreCase);
            // where there are several, those at home lead and a town before the district or county of its name, so that the few shown
            // on the row are the likeliest (the assistant as a user, 2026-11-24: 115 rows of several, "Cardiff", "Nottingham",
            // "Sheffield", each a search and a press on another page)
            var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var r in named.Take(30)) if (await KindOfAsync(r) is { } kind) kinds[r.Uri] = kind;
            // and the most written-about before the rest, by the Wikidata item that names each ("Madrid" showed four small Madrids and
            // not Spain's): counted only where there is a choice to make
            var written = named.Count > 1 ? named.Where(r => sitelinks.ContainsKey(r.Uri)).ToDictionary(r => r.Uri, r => sitelinks[r.Uri], StringComparer.Ordinal) : new Dictionary<string, int>(StringComparer.Ordinal);
            var ordered = named.OrderByDescending(Home).ThenByDescending(r => written.GetValueOrDefault(r.Uri)).ThenByDescending(r => kinds.TryGetValue(r.Uri, out var k) && k.Contains("inhabited", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ThenBy(r => r.Label, StringComparer.Ordinal).ThenBy(r => r.Uri, StringComparer.Ordinal).ToList();
            var atHome = ordered.Count(Home);
            var offer = ordered.Count == 1 ? ordered[0] : atHome == 1 ? ordered[0] : null;
            var doubt = false;
            // an entity whose own label says where it is is offered only a record that is there: every word of the label's qualifier in
            // the record's chain (read as a user, 2026-11-24, of 99 offers some 18 contradicted the label: "Rowsley (England)" a place
            // in Victoria, "Calton Hill (Edinburgh, Scotland)" one in Queensland, "Crimea (Ukraine)" one in Louisiana, "Audley End
            // (Essex)" one in Suffolk). An abbreviation the chain spells out loses its offer too ("(N.J.)"): it is found by hand
            if (offer is not null && System.Text.RegularExpressions.Regex.Match(row.Label ?? "", @"(?:\(([^)]*)\)|,\s*([^,()]+))\s*$") is { Success: true } said)
            {
                var chain = (ReviewService.ChainOf(offer) ?? "").ToLowerInvariant();
                var qualifierWords = System.Text.RegularExpressions.Regex.Matches((said.Groups[1].Value + " " + said.Groups[2].Value).ToLowerInvariant(), @"\p{L}{3,}").Select(w => w.Value).Where(w => w is not ("and" or "the")).ToList();
                // not borne out: abroad it is no offer; at home it is offered apart, to be read, since a gazetteer files a town under
                // its present district where the label has the county ("Chatham (Kent)" is under Medway)
                if (qualifierWords.Count > 0 && !qualifierWords.All(w => chain.Contains(w, StringComparison.Ordinal))) { if (Home(offer)) doubt = true; else offer = null; }
            }
            var holder = offer is null ? null : (await store.FindEntitiesByEquivalentAsync(offer.Uri, ct)).FirstOrDefault(o => o.Dataset == dataset && o.MergedInto is null && o.Id != row.EntityId);
            list.Add(new ThinPlace(row, ordered, atHome, offer, holder) { Name = name, Doubt = doubt && offer is not null, Kinds = kinds, Written = written });
        }
        return list;
    }

    public Task<IReadOnlyList<NotNameEntity>> NotNameEntitiesAsync(string dataset, CancellationToken ct = default) => store.NotNameEntitiesAsync(dataset, NotNamePattern, ct);

    /// <summary>
    /// A person says the string is not a name (the owner, 2026-11-22). The decision has no entity and the method
    /// <see cref="DecisionMethod.NotAName"/>, the reason in its evidence; the string leaves the queue. The placeholder it stood on goes
    /// when nothing else points at it. Undo puts the string back in the queue.
    /// </summary>
    /// <returns>True when the entity the string stood on was removed with it.</returns>
    public async Task<bool> NotANameAsync(string dataset, long stringId, string reviewer, string reason, string? note = null, CancellationToken ct = default)
    {
        if (reason is not (NothingKnown or RecordWantsFixing)) throw new ArgumentException("Say which: nothing is known, or the record wants fixing.");
        var decided = await store.DecidedStringAsync(stringId, ct) ?? throw new KeyNotFoundException($"No decided string {stringId}.");
        if (!string.Equals(decided.String.Dataset, dataset, StringComparison.Ordinal)) throw new InvalidOperationException("The string belongs to another project.");
        var was = decided.Decision.EntityId is null ? null : await store.ResolveEntityAsync(decided.Decision.EntityId, ct);
        // only a string that stands on nothing real: one on an entity with identifiers or a person's confirmation is decided at that entity first
        if (was is not null && (was.Status != EntityStatus.Provisional || was.Equivalents.Count > 0))
            throw new InvalidOperationException($"\"{decided.String.Raw}\" stands on {was.Label}, an entity with identifiers or a confirmation of its own: move the string off it first.");
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await store.DecideAsync(stringId, null, DecisionMethod.NotAName, Reconciler.Evidence("not-a-name", reconciler.Read(decided.String), [], new { reason, note, was = was?.Id, wasLabel = was?.Label }), reviewer, ct: ct);
        if (note is not null) await store.AddNoteAsync(dataset, ReviewService.StringTarget(stringId), reviewer, note, ct);
        // a record that wants fixing goes on the cataloguers' list, once for each record the string is in (doc 12 section 14.4)
        if (reason == RecordWantsFixing)
            await store.RaiseFindingsForStringAsync(stringId, Finding.NotAName, decided.String.Raw, null, note ?? "a stray letter or number where a name should be", reviewer, ct);
        return was is not null && await store.RemoveProvisionalIfUnreferencedAsync(was.Id, ct, withMerged: true);
    }

    /// <summary>Every string of an entity the third check lists is said to be no name, and the entity goes. Refused when a string on it does not match the check's pattern any more.</summary>
    public async Task<(int Strings, bool Removed)> NotANameEntityAsync(string dataset, string entityId, string reviewer, string reason, CancellationToken ct = default)
    {
        var listed = (await NotNameEntitiesAsync(dataset, ct)).FirstOrDefault(e => e.EntityId == entityId) ?? throw new InvalidOperationException("That entity is not on the list any more.");
        var removed = false;
        foreach (var id in listed.StringIds) removed = await NotANameAsync(dataset, id, reviewer, reason, null, ct) || removed;
        return (listed.StringIds.Count, removed);
    }

    public static readonly IReadOnlyList<string> LeftUndecided = ["Left undecided at the checks:", "From the checks, left for the owner:"];

    private static readonly HashSet<string> ChainNoise = new(StringComparer.OrdinalIgnoreCase) { "Europe", "World", "Greater", "County", "City", "Borough", "United", "Kingdom", "States", "North", "South", "East", "West", "upon", "with" };

    /// <summary>
    /// The check "a place the old import aligned to a namesake" (the owner, 2026-11-15: the project's "Glasgow" on Glasgow, Kentucky,
    /// its "Madison" on Madison, New Jersey; an audit list at the workbench, "YES"). A place entity with a bare label and a Wikidata
    /// item from an import is listed when a place of exactly that name stands in the project's home place and what the item says of
    /// itself names nothing of that place's chain. Likely wrong when the place at home is at least as written about as the item;
    /// otherwise for a look ("New York" beside a hamlet in Lincolnshire is right as it is). An entity a person has confirmed is not
    /// listed. Counted before it was built (reports/q-check1.txt): 509 such entities, 277 with a namesake at home, 77 listed.
    /// </summary>
    /// <remarks>
    /// The check reads the strings that stand on the entity as well as its label (the owner, 2026-11-15, at "Birmingham", one string,
    /// "Birmingham, Ala.", of a book from the Birmingham Museum of Art: "Birmingham Alabama is actually correct"). Counted first
    /// (reports/q-check2.txt): of the imported places with a bare label and strings, 122 have every string qualified, 177 some, 252
    /// none. An entity whose every string carries a qualifier the item's description bears is not listed ("Cambridge, Mass." on
    /// Cambridge, Massachusetts); one with a string whose qualifier is in a home namesake's chain and not in the item's description
    /// is likely wrong, and the string is the reason ("Stowe (England)" on Stowe, Vermont); the strings are shown on every row.
    /// </remarks>
    public async Task<IReadOnlyList<AwayFinding>> PlacesAlignedAwayAsync(string dataset, string? homePlace, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(homePlace)) return [];
        var found = new List<AwayFinding>();
        // left undecided with a note: such an entity stays on the list until it is confirmed or mended, whatever else is learnt of
        // it meanwhile (the assistant as a user, 2026-11-17, left "Charterhouse" and "Wallington" with notes, and both left the list
        // when their items were pulled and found to be at home)
        var left = await store.NotedAsync(dataset, LeftUndecided, ct);
        foreach (var pairs in (await store.ImportedPlacesWithHomeNamesakesAsync(dataset, homePlace, ct)).GroupBy(r => (r.EntityId, r.Item)))
        {
            var rows = pairs.ToList();
            var descr = rows[0].ItemDescription ?? "";
            if (rows.Any(r => DescriptionNames(descr, r, homePlace)))
            {
                // at home, then: not this check's business unless the item is one place of the name and the entity is plainly another
                // (seen as a user, 2026-11-17, at "Bridgend": the import's item a hamlet in Aberdeenshire, the entity holding TGN's
                // Bridgend in Wales by a later rule, its three records Welsh; once the item's description was pulled the row would have
                // left the list as "at home", wrong as it was. Counted, reports/q-check3.txt: of 35 described entities holding a TGN
                // place of their name at home, 25 items name that place's county, 1 names another's, 9 name no county)
                var fine = rows.Where(r => NamesWithin(descr, r, homePlace)).ToList();
                if (fine.Count == 0) continue;
                var theirs = await store.StringsForEntityAsync(rows[0].EntityId, 200, ct);
                var theirRaws = theirs.Select(s => s.String.Raw).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
                var theirWords = theirRaws.Select(raw => (Raw: raw, Words: Qualified.Tokens(Qualified.Split(raw).Qualifier))).Where(q => q.Words.Count > 0).ToList();
                var meant = rows.Where(r => r.Holds).Concat(rows.Where(r => theirWords.Any(q => Qualified.Contains(r.HomeChain, q.Words)))).ToList();
                if (meant.Count > 0 && !meant.Any(m => fine.Any(f => f.HomeUri == m.HomeUri)))
                    found.Add(new AwayFinding(meant[0], true, theirRaws, $"its Wikidata item is another place of the name ({descr}) than the one {(meant[0].Holds ? "whose TGN identifier it holds" : "its strings say")}"));
                // A string whose qualifier is in neither the item's description nor a namesake's chain is NOT listed for that alone. It was,
                // for an hour, uncounted, and listed seventeen as likely wrong of which two were (the building and the house above): the
                // other fifteen were right places written with an old county or an abbreviation ("Bath (Avon)", "Twickenham (Middlesex)",
                // "Henley on Thames, Oxon", "Newport, IoW", "Kelso, Roxburghshire"). Telling those from "Wallington (Northumberland)"
                // wants a table of former and abbreviated county names, which the library does not have
                else if (left.TryGetValue(rows[0].EntityId, out var note))
                    found.Add(new AwayFinding(fine[0], false, theirRaws, "left undecided with a note: " + (note.Length > 160 ? note[..160] + "…" : note)));
                continue;
            }
            // what the strings themselves say; a spelling that stands in two fields is shown once and says so (seen as a user,
            // 2026-11-17: "stands for 3 strings" beside two spellings)
            var standing = await store.StringsForEntityAsync(rows[0].EntityId, 200, ct);
            var raws = standing.Select(s => s.String.Raw).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
            var strings = raws.Select(raw => standing.Count(s => s.String.Raw == raw) is var n and > 1 ? $"{raw} (in {n} fields)" : raw).ToList();
            var qualifiers = raws.Select(raw => (Raw: raw, Words: Qualified.Tokens(Qualified.Split(raw).Qualifier))).ToList();
            // the place at home shown is the one the strings' own qualifiers name, then the most written-about, then by its identifier
            // so that it is the same at every load (seen as a user: "Compton, Surrey" beside Compton in Leeds, and in West Berkshire at
            // the next load; "Ely, Cambridgeshire" beside Ely in Cardiff)
            var home = BestHome(rows, qualifiers.Select(q => q.Words));
            // every string places it, and none in a namesake at home ("Kingston, Jamaica" beside Kingston in West Sussex): said on the row
            var doubt = raws.Count > 0 && qualifiers.All(q => q.Words.Count > 0) && !qualifiers.Any(q => rows.Any(r => Qualified.Contains(r.HomeChain, q.Words)))
                ? $"none of the places of this name at home is what its string{(raws.Count == 1 ? " says" : "s say")}: \"{raws[0]}\"" : null;
            if (raws.Count > 0 && rows[0].ItemDescription is not null && qualifiers.All(q => q.Words.Count > 0 && Qualified.Contains(descr, q.Words))) continue;
            // only against a description that is there: an item absent from the index says nothing, and that is not disagreement
            // (the owner, 2026-11-15, at "Biggleswade": right, and listed as likely wrong because "Biggleswade, Bedfordshire" was
            // not found in a description the index does not have)
            var saysHome = rows[0].ItemDescription is null ? default
                : qualifiers.FirstOrDefault(q => q.Words.Count > 0 && !Qualified.Contains(descr, q.Words) && rows.Any(r => Qualified.Contains(r.HomeChain, q.Words)));
            if (saysHome.Raw is not null)
            {
                found.Add(new AwayFinding(rows.OrderByDescending(r => Qualified.Contains(r.HomeChain, saysHome.Words) ? 1 : 0).ThenByDescending(r => r.HomeSitelinks).First(), true, strings, $"the string \"{saysHome.Raw}\" says so"));
                continue;
            }
            // likely only where both counts are known: the first reading of the real list (2026-11-15) had items absent from the index
            // (no description, no count) and places at home with no Wikidata item found through TGN's id standing as "0 against 0"
            found.Add(new AwayFinding(home, home.ItemDescription is not null && home.HomeItem is not null && home.HomeSitelinks >= home.ItemSitelinks, strings, null) { HomeDoubt = doubt });
        }
        // the second kind (the owner, 2026-11-24, "add this condition to the first check and reuse its Decide screen as it is"): a string
        // with no qualifier on a place whose label has one, where the best-known place of the name is another. Counted first: 58
        // strings flagged, some 33 wrong on about 480 records, of two kinds, the import's namesake abroad ("Milano" in 100 records on
        // Milano (Tex.), "Stuttgart" on Stuttgart (Ark.)) and the wrong level of the right place ("Chichester" on the District, "Munich"
        // on the Landkreis); some 22 right ("Dedham (England)", outweighed by Massachusetts's). So a list for a person, never likely
        foreach (var r in await store.QualifiedPlacesOutweighedAsync(dataset, ct))
        {
            if (found.Any(f => f.Row.EntityId == r.EntityId)) continue;
            var standing = await store.StringsForEntityAsync(r.EntityId, 200, ct);
            var raws = standing.Select(s => s.String.Raw).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
            // two kinds (the owner, 2026-11-24: "sometimes easy because the mistake is ... a small town in the US ... sometimes hard,
            // where the 'mistake' is a category error rather than a place error"; counted, 18 and 35 of 53): a namesake elsewhere, and
            // the same place, or one in the same country, under another record; the row says which, and of the second what differs
            var (same, level) = SamePlace(r);
            var says = !same ? $"a string with no qualifier stands on it, and the best-known place of that name is elsewhere, written about in {r.HomeSitelinks} against {r.ItemSitelinks}"
                : level is not null ? $"its label says what kind of place it is ({level}); a string with no qualifier stands on it, and the best-known place of the name is another kind: {r.HomeKind ?? "another record"}"
                : $"its Wikidata item is {r.ItemDescription ?? "of no description"}; the best-known place of the name is {r.HomeKind ?? "another record"}";
            found.Add(new AwayFinding(r, false, raws, says) { Outweighed = true, SamePlace = same });
        }
        // the likely first, the widest margin leading; then the rest, the least written-about abroad first ("Perth" in Tasmania before "New York")
        // a label that says which place it is, for the rows that are right as aligned and only want telling from their namesakes (the
        // owner, 2026-11-15: "nothing to mend, and the fix is to clarify the place name, e.g. Berkeley => Berkeley (Calif.)"): LoC's
        // heading where the entity holds one that begins with its label and carries a qualifier, the form most of the project's
        // places already have
        for (var i = 0; i < found.Count; i++)
        {
            if (await store.GetEntityAsync(found[i].Row.EntityId, ct) is not { } e) continue;
            foreach (var q in e.Equivalents.Where(q => q.Source == "loc"))
                if (await store.GetAuthorityAsync(q.Uri, ct) is { Label: { } heading } && heading.StartsWith(found[i].Row.Label + " (", StringComparison.Ordinal) && heading.EndsWith(')'))
                { found[i] = found[i] with { SuggestedLabel = heading }; break; }
        }
        return found.OrderByDescending(f => f.Likely).ThenByDescending(f => f.Reason is not null).ThenByDescending(f => f.Likely ? f.Row.HomeSitelinks - f.Row.ItemSitelinks : -f.Row.ItemSitelinks).ThenBy(f => f.Row.Label, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// For a row of the second kind: whether the better-known place is where the entity's own label says the entity is (a word of the
    /// label's qualifier is in the better-known place's chain or description), and the kind of thing the label names when it names one
    /// after a colon, as a library's heading does ("Chichester (England : District)").
    /// </summary>
    public static (bool Same, string? Level) SamePlace(AwayRow r)
    {
        // the label's own last parenthesis, whole: the reading of a qualifier drops the kind after the colon
        var qualifier = System.Text.RegularExpressions.Regex.Match(r.EntityLabel ?? r.Label, @"\(([^)]*)\)\s*$") is { Success: true } m ? m.Groups[1].Value : "";
        var parts = qualifier.Split(':', 2, StringSplitOptions.TrimEntries);
        var level = parts.Length == 2 && parts[1].Length > 0 ? parts[1] : null;
        var where = (r.HomeChain + " " + r.HomeKind).ToLowerInvariant();
        var same = System.Text.RegularExpressions.Regex.Matches(parts[0].ToLowerInvariant(), @"\p{L}{3,}").Any(w => w.Value != "and" && where.Contains(w.Value, StringComparison.Ordinal));
        // a label that names the kind the better-known record is too ("Wicklow (Ireland : County)" beside "county in Ireland") names no
        // other division than the place meant: there it is the item that differs
        if (level is not null && (r.HomeKind ?? "").TrimStart().StartsWith(level, StringComparison.OrdinalIgnoreCase)) level = null;
        return (same, same ? level : null);
    }

    /// <summary>
    /// The entity is the place its strings mean, and only its Wikidata item is another record (the owner, 2026-11-24, at "West Sussex
    /// (England)", whose item was a military unit): the item is replaced by the better-known record of the name, and the gazetteer's
    /// record that item names is added when no other entity holds it. The library's heading and the label stay. Returns the entity
    /// and what was done.
    /// </summary>
    public async Task<(Entity Result, string Summary)> ReplaceItemAsync(string dataset, string entityId, string reviewer, string? itemUri = null, IReadOnlyList<string>? gazetteerUris = null, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        var row = (await store.QualifiedPlacesOutweighedAsync(dataset, ct)).FirstOrDefault(r => r.EntityId == entity.Id) ?? throw new InvalidOperationException("That entity is not on the list any more.");
        // the record chosen, or the best-known when none is named. A person chooses because the best-known is not always the one
        // meant (the owner at "Kingston-upon-Thames", 2026-11-24: the most written-about record of the name is the Royal Borough, and
        // the strings, places of publication, mean the town, the second)
        var better = string.IsNullOrWhiteSpace(itemUri) ? row.HomeItem ?? throw new InvalidOperationException("There is no better-known record to give it.") : AuthorityUris.Canonical(itemUri.Trim());
        var item = await store.GetAuthorityAsync(better, ct);
        if (item is not { Source: "wikidata" }) throw new InvalidOperationException("Choose one of the Wikidata records listed.");
        if (entity.Equivalents.Any(q => q.Uri == better)) throw new InvalidOperationException("The entity holds that record already.");
        if ((await store.FindEntitiesByEquivalentAsync(better, ct)).FirstOrDefault(e => e.Dataset == dataset && e.MergedInto is null && e.Id != entity.Id) is { } holder)
            throw new InvalidOperationException($"That record is held by \"{holder.Label}\" already: the two may be one place. Compare them from that entity's page, or move the strings there.");
        var was = new List<string>();
        foreach (var q in entity.Equivalents.Where(q => q.Source == "wikidata").ToList())
        {
            was.Add(Authorities.RecordSummary.Says(await store.GetAuthorityAsync(q.Uri, ct)) ?? q.Uri);
            await RemoveEquivalentAsync(dataset, entity.Id, q.Uri, reviewer, ct);
        }
        await AddEquivalentAsync(dataset, entity.Id, better, reviewer, ct);
        // the gazetteer's record: the one the item names, and any a person ticked, where the project has it nowhere else
        var wanted = new List<string>();
        foreach (var uri in item.SameAs) if (await store.GetAuthorityAsync(uri, ct) is { Source: "tgn", Type: "Place" }) wanted.Add(uri);
        wanted.AddRange((gazetteerUris ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => AuthorityUris.Canonical(u.Trim())));
        var gazetteer = 0;
        foreach (var uri in wanted.Distinct(StringComparer.Ordinal))
        {
            if (entity.Equivalents.Any(q => q.Uri == uri) || await store.GetAuthorityAsync(uri, ct) is not { Source: "tgn" }) continue;
            if ((await store.FindEntitiesByEquivalentAsync(uri, ct)).Any(e => e.Dataset == dataset && e.MergedInto is null && e.Id != entity.Id)) continue;
            await AddEquivalentAsync(dataset, entity.Id, uri, reviewer, ct); gazetteer++;
        }
        var now = Authorities.RecordSummary.Says(item) ?? item.Label ?? better;
        await store.AddNoteAsync(dataset, entity.Id, reviewer, $"At the checks: the place is right and its Wikidata item was another record ({string.Join("; ", was)}); replaced by {better} ({now}).", ct);
        return ((await store.GetEntityAsync(entity.Id, ct))!, $"\"{entity.Label}\": its Wikidata item ({(was.Count == 0 ? "none" : string.Join("; ", was))}) replaced by the record chosen ({now}){(gazetteer > 0 ? $", and {(gazetteer == 1 ? "the gazetteer's record" : gazetteer + " gazetteer records")} added" : "")}. Its label and its other identifiers are as they were.");
    }

    /// <summary>Whether what an item says of itself names the place at home: the item is the home place's own, or its description bears the home place, a place of the chain, or a telling word of one.</summary>
    private static bool DescriptionNames(string descr, AwayRow r, string homePlace)
    {
        if (r.HomeItem == r.Item || (descr.Length > 0 && descr.Contains(homePlace, StringComparison.OrdinalIgnoreCase))) return true;
        if (descr.Length == 0) return false;
        foreach (var segment in r.HomeChain.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "Europe" or "World" || string.Equals(segment, r.Label, StringComparison.OrdinalIgnoreCase)) continue;
            if (descr.Contains(segment, StringComparison.OrdinalIgnoreCase)) return true;
            foreach (var word in segment.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (word.Length >= 4 && !ChainNoise.Contains(word) && !string.Equals(word, r.Label, StringComparison.OrdinalIgnoreCase)
                    && System.Text.RegularExpressions.Regex.IsMatch(descr, $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether what an item says of itself names the place the home place is in: its county or district, the first of its chain. Finer
    /// than <see cref="DescriptionNames"/>, which is content with the nation: "hamlet in Gartly, Aberdeenshire, Scotland" is at home
    /// by "Scotland", and is the Bridgend of Aberdeenshire only, not the one on Islay.
    /// </summary>
    private static bool NamesWithin(string descr, AwayRow r, string homePlace)
    {
        if (r.HomeItem == r.Item) return true;
        if (descr.Length == 0) return false;
        // any place of the chain below the nation, not the nearest alone (seen as a user, 2026-11-17: "village in west London" beside
        // "Harmondsworth, Hillingdon, Greater London", and "in West Yorkshire" beside "West Bretton, Wakefield, West Yorkshire", were
        // each told they named none of the places of the name). The nation is the place of the chain just inside the home place
        var parts = r.HomeChain.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        var at = parts.FindIndex(x => string.Equals(x, homePlace, StringComparison.OrdinalIgnoreCase));
        var nation = at > 0 ? parts[at - 1] : null;
        foreach (var segment in parts)
        {
            if (segment is "Europe" or "World" || segment == nation || string.Equals(segment, homePlace, StringComparison.OrdinalIgnoreCase) || string.Equals(segment, r.Label, StringComparison.OrdinalIgnoreCase)) continue;
            if (descr.Contains(segment, StringComparison.OrdinalIgnoreCase)) return true;
            if (segment.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(word => word.Length >= 4 && !ChainNoise.Contains(word) && !string.Equals(word, r.Label, StringComparison.OrdinalIgnoreCase)
                && System.Text.RegularExpressions.Regex.IsMatch(descr, $@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))) return true;
        }
        return false;
    }

    /// <summary>The place at home to show for an entity: one a string's qualifier names, then the most written-about, then by identifier, so that the choice is the same at every load.</summary>
    private static AwayRow BestHome(IEnumerable<AwayRow> rows, IEnumerable<IReadOnlyList<string>> qualifierWords)
    {
        var words = qualifierWords.Where(w => w.Count > 0).ToList();
        return rows.OrderByDescending(r => words.Any(w => Qualified.Contains(r.HomeChain, w)) ? 1 : 0).ThenByDescending(r => r.HomeSitelinks).ThenBy(r => r.HomeUri, StringComparer.Ordinal).First();
    }

    /// <summary>
    /// The entity is right as aligned: it is confirmed with the label given, each noted, so that it leaves the checks; and, when asked,
    /// it is given the identifier of the place at home first (seen as a user, 2026-11-17, at "Blickling": right, and without a TGN
    /// identifier, which only the form for a wrong place could add).
    /// </summary>
    public async Task<Entity> RelabelAndConfirmAsync(string dataset, string entityId, string label, string reviewer, string? addUri = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new InvalidOperationException("A label is needed.");
        if (!string.IsNullOrWhiteSpace(addUri) && (await OwnEntityAsync(dataset, entityId, ct)).Equivalents.All(q => q.Uri != addUri)) await AddEquivalentAsync(dataset, entityId, addUri, reviewer, ct);
        await SetLabelAsync(dataset, entityId, label, reviewer, ct);
        return await ConfirmAsync(dataset, entityId, reviewer, "right as aligned; the label made to say which place it is, at the checks", ct);
    }

    /// <summary>
    /// What mending a listed place would do, for the person to read before anything is written (the owner, 2026-11-15, after mending
    /// "Cardiff" and "Belfast" by hand: the row and its TGN address left the list with the identifiers, and the project's other
    /// "Cardiff", already the Welsh city, was not pointed at): the entity, the places of its name at home, the most written-about
    /// first, and for each the project's entity that already holds it, if one does. Null when the entity is not on the list.
    /// </summary>
    public async Task<MendPlan?> MendPlanAsync(string dataset, string entityId, string? homePlace, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(homePlace)) return null;
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        var homes = (await store.ImportedPlacesWithHomeNamesakesAsync(dataset, homePlace, ct)).Where(r => r.EntityId == entity.Id)
            .GroupBy(r => r.HomeUri).Select(g => g.First()).ToList();
        // the second kind of row: the better-known place of the name stands where the places at home do
        if (homes.Count == 0) homes = (await store.QualifiedPlacesOutweighedAsync(dataset, ct)).Where(r => r.EntityId == entity.Id).ToList();
        if (homes.Count == 0) return null;
        // the place at home that the item's own description names, if one: the entity is then probably right; the homes stand with
        // that one first, then those a string's qualifier names, then the most written-about
        var descr = homes[0].ItemDescription ?? "";
        // agreement is on the county, not the nation: the first notice of this kind told the assistant, as a user, that a hamlet in
        // Aberdeenshire "agrees with" the Bridgend on Islay, both being in Scotland, of an entity whose records are Welsh (2026-11-17)
        var agreeing = homes.Where(h => NamesWithin(descr, h, homePlace)).Select(h => h.HomeUri).ToHashSet(StringComparer.Ordinal);
        var qualified = (await store.StringsForEntityAsync(entity.Id, 200, ct)).Select(s => s.String.Raw).Distinct(StringComparer.Ordinal)
            .Select(raw => (Raw: raw, Words: Qualified.Tokens(Qualified.Split(raw).Qualifier))).Where(q => q.Words.Count > 0).ToList();
        var words = qualified.Select(q => q.Words).ToList();
        var qualifiedNames = (await store.StringsForEntityAsync(entity.Id, 200, ct)).Select(s => Qualified.Split(s.String.Raw).Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        bool Said(AwayRow h) => words.Any(w => Qualified.Contains(h.HomeChain, w));
        // a string that is neither where the item is nor in the place at home the item names: the entity is wrong for that string at least
        var odd = qualified.FirstOrDefault(q => !Qualified.Contains(descr, q.Words) && !homes.Any(h => agreeing.Contains(h.HomeUri) && Qualified.Contains(h.HomeChain, q.Words))).Raw;
        homes = homes.OrderByDescending(h => h.Holds ? 1 : 0).ThenByDescending(h => agreeing.Contains(h.HomeUri) ? 1 : 0).ThenByDescending(h => Said(h) ? 1 : 0)
            .ThenByDescending(h => h.HomeSitelinks).ThenBy(h => h.HomeUri, StringComparer.Ordinal).ToList();
        // the item is one place of the name and the entity, by the identifier it holds or by its strings, another
        var meant = homes.Where(h => h.Holds || Said(h)).ToList();
        var conflict = agreeing.Count > 0 && meant.Count > 0 && !meant.Any(m => agreeing.Contains(m.HomeUri));
        // a better-known place of the same name elsewhere, which a bare label would be taken for (the owner, 2026-11-17: "on the whole,
        // UK places don't need qualifiers. But sometimes they do! Boston would usually be taken as Boston, Mass rather than Boston in
        // Lincolnshire"): the most written-about Wikidata place of the name, when it is another place than this entity's and more
        // written about than the entity's own item
        (string Uri, string? Description, int Sitelinks)? rival = null;
        // only where the entity's own item has a count to compare: a pulled item has none, and the "better-known" place was then the
        // entity's own under another item ("Maidstone", "Stafford", "Ely": seen as a user, 2026-11-17)
        if (entity.Label is { } name && homes[0].ItemSitelinks > 0 && await store.BestKnownNamesakeAsync(name, homes[0].Item, ct) is { } best && best.Sitelinks > homes[0].ItemSitelinks
            && !homes.Any(h => (agreeing.Contains(h.HomeUri) || entity.Equivalents.Any(q => q.Uri == h.HomeUri)) && (h.HomeItem == best.Uri || DescriptionNames(best.Description ?? "", h with { Item = best.Uri }, homePlace))))
            rival = best;
        var holders = new Dictionary<string, Entity>(StringComparer.Ordinal);
        foreach (var h in homes)
            if ((await store.FindEntitiesByEquivalentAsync(h.HomeUri, ct)).FirstOrDefault(e => e.Dataset == dataset && e.MergedInto is null && e.Id != entity.Id) is { } holder) holders[h.HomeUri] = holder;
        // and the project's other entities of the name, wherever they are: the place meant may be abroad and not the one the import
        // chose (the owner, 2026-11-15, at "Raleigh": a neighbourhood of Memphis, meant for Raleigh, North Carolina, which the
        // project has, and Mend offered only the Raleigh in Devon)
        var others = new List<Entity>();
        foreach (var key in StringKey.Variants(entity.Label ?? "").Take(1))
            foreach (var e in await store.FindEntitiesByLabelAsync(key, dataset, EntityClass.Place, ct))
                if (e.Id != entity.Id && e.MergedInto is null && holders.Values.All(h => h.Id != e.Id) && others.All(o => o.Id != e.Id)) others.Add(e);
        var swap = (await store.QualifiedPlacesOutweighedAsync(dataset, ct)).FirstOrDefault(r => r.EntityId == entity.Id);
        // the records to choose among, should only the item be wrong: Wikidata's places of the name, the most written-about first, and
        // the gazetteer's places of exactly that name
        var swapItems = new List<(AuthorityRecord Record, int Sitelinks, string? Gazetteer)>();
        var swapGazetteer = new List<AuthorityRecord>();
        if (swap is not null && SamePlace(swap) is { Same: true, Level: null })
        {
            var names = new[] { Qualified.Split(entity.Label ?? "").Name }.Concat(qualifiedNames).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var keys = names.SelectMany(NameMatcher.Keys).Distinct(StringComparer.Ordinal).ToList();
            foreach (var r in await store.FindAuthoritiesByLabelsAsync(keys, "wikidata", nameof(EntityClass.Place), 40, ct))
            {
                if (entity.Equivalents.Any(q => q.Uri == r.Uri)) continue;
                var links = 0;
                if (r.DataJson is not null) { using var doc = JsonDocument.Parse(r.DataJson); if (doc.RootElement.TryGetProperty("sitelinks", out var sl) && sl.TryGetInt32(out var n)) links = n; }
                string? linked = null;
                foreach (var uri in r.SameAs) if (uri.Contains("/tgn/", StringComparison.Ordinal) && await store.GetAuthorityAsync(uri, ct) is { Source: "tgn" } g) { linked = $"{g.Label} ({ReviewService.ChainOf(g)})"; break; }
                swapItems.Add((r, links, linked));
            }
            swapItems = swapItems.OrderByDescending(x => x.Sitelinks).Take(6).ToList();
            var nameKeys = names.Select(StringKey.Normalise).ToHashSet(StringComparer.Ordinal);
            swapGazetteer = (await store.FindAuthoritiesByLabelsAsync(keys, "tgn", nameof(EntityClass.Place), 40, ct))
                .Where(g => g.Label is not null && nameKeys.Contains(StringKey.Normalise(g.Label)) && entity.Equivalents.All(q => q.Uri != g.Uri)).Take(6).ToList();
        }
        return new MendPlan(entity, homes, holders) { Swap = swap is not null && SamePlace(swap) is { Same: true, Level: null } ? swap : null, SwapItems = swapItems, SwapGazetteer = swapGazetteer, Others = others, Agreeing = homes.FirstOrDefault(h => agreeing.Contains(h.HomeUri))?.HomeUri, Rival = rival, Meant = conflict ? meant[0].HomeUri : null, Odd = odd,
            Preselect = conflict ? meant[0].HomeUri : meant.Select(m => m.HomeUri).Distinct(StringComparer.Ordinal).ToList() is [var only] ? only : null };
    }

    /// <summary>
    /// Moves some of an entity's strings to another place and leaves the entity as it is (the owner, 2026-11-15, at "Peterborough":
    /// the import's entity is Peterborough, Ontario, right for "Peterborough, Ontario" and "Peterborough, Canada" and wrong for the
    /// bare "Peterborough" of nine English imprints and six subject headings about the cathedral: "build the move strings to another
    /// entity as well"). The target is an entity of the project ("entity:" and its id), or a place's identifier: the project's entity
    /// that holds it, or a new one made for it and named from its record. The strings are decided there by the person, each with an
    /// override for its spelling in its field, so that a later run does not match them back by name. Returns the entity they stand on.
    /// </summary>
    public async Task<Entity> MoveStringsAsync(string dataset, string fromEntityId, IReadOnlyList<long> stringIds, string target, string reviewer, CancellationToken ct = default)
    {
        var from = await OwnEntityAsync(dataset, fromEntityId, ct);
        if (stringIds.Count == 0) throw new InvalidOperationException("Tick the strings to move.");
        var standing = (await store.StringsForEntityAsync(from.Id, 1000, ct)).Select(s => s.String.Id).ToHashSet();
        if (stringIds.Any(id => !standing.Contains(id))) throw new InvalidOperationException("One of the strings does not stand on this entity any more.");
        var note = $"moved from {from.Label ?? from.Id} at the checks";
        Entity to;
        if (target.StartsWith("entity:", StringComparison.Ordinal))
        {
            var chosen = await OwnEntityAsync(dataset, target["entity:".Length..], ct);
            if (chosen.Id == from.Id) throw new InvalidOperationException("The strings stand on that entity already.");
            to = await ClusterAsync(dataset, stringIds, chosen.Id, null, [], reviewer, note, false, ct);
        }
        else
        {
            var uri = AuthorityUris.Canonical(target.Trim());
            if (uri.Length == 0) throw new InvalidOperationException("Give the identifier of the place meant.");
            if (from.Equivalents.Any(q => q.Uri == uri)) throw new InvalidOperationException("This entity holds that identifier itself: the strings stand on it already.");
            var holder = (await store.FindEntitiesByEquivalentAsync(uri, ct)).FirstOrDefault(e => e.Dataset == dataset && e.MergedInto is null);
            if (holder is not null) to = await ClusterAsync(dataset, stringIds, holder.Id, null, [], reviewer, note, false, ct);
            else
            {
                var record = await store.GetAuthorityAsync(uri, ct) ?? throw new InvalidOperationException("That identifier is not a record of the local index: add the place as an entity first, or give one that is.");
                // named so that it is told from its namesakes: the record's name and the first two places of its chain
                string? chain = null;
                if (record.DataJson is not null)
                {
                    using var doc = JsonDocument.Parse(record.DataJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("parentString", out var ps)) chain = ps.GetString();
                }
                var within = chain?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Where(x => x is not ("Europe" or "World") && !string.Equals(x, record.Label, StringComparison.OrdinalIgnoreCase)).Take(2).ToList() ?? [];
                var label = within.Count == 0 || (record.Label ?? "").Contains('(') ? record.Label ?? uri : $"{record.Label} ({string.Join(", ", within)})";
                to = await ClusterAsync(dataset, stringIds, null, label, [uri], reviewer, note, false, ct);
            }
        }
        await store.AddNoteAsync(dataset, from.Id, reviewer, $"{stringIds.Count} string{(stringIds.Count == 1 ? "" : "s")} moved from here to {to.Label ?? to.Id} at the checks; this entity and its identifiers are as they were.", ct);
        return to;
    }

    /// <summary>
    /// One decision for an entity of the checks, said string by string (the owner, 2026-11-17: "when there are several things that need
    /// to be done ... making sure the user doesn't lose track of the simple task"; the assistant as a user had met an entity right for
    /// one string and wrong for another three times in twenty-one, with only whole-entity forms to say it in). Each string either
    /// stays, being the place the entity is aligned to, or goes to the other place. None goes: the entity is kept and confirmed, with
    /// the label and, when asked, the identifier of its place. All go: the entity is mended whole, the identifiers named removed, and
    /// it is merged into or given the other place. Some go: those are moved, and the entity is confirmed for the rest. Returns the
    /// entity the staying strings stand on (or the one all went to) and what was done, in words.
    /// </summary>
    public async Task<(Entity Result, string Summary)> ApplyAsync(string dataset, string entityId, IReadOnlyCollection<long> goes, string? target, IReadOnlyList<string> removeUris,
        string? label, string? addUri, string reviewer, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        var name = entity.Label ?? entity.Id;
        var standing = (await store.StringsForEntityAsync(entity.Id, 1000, ct)).Select(s => s.String.Id).ToList();
        var going = standing.Where(goes.Contains).ToList();
        var keepAs = string.IsNullOrWhiteSpace(label) ? name : label.Trim();
        static string N(int n, string what) => n == 1 ? $"1 {what}" : $"{n} {what}s";
        if (going.Count == 0)
        {
            var kept = await RelabelAndConfirmAsync(dataset, entity.Id, keepAs, reviewer, addUri, ct);
            return (kept, (kept.Label == name ? $"\"{name}\" confirmed as it is" : $"\"{name}\" relabelled \"{kept.Label}\" and confirmed") + (string.IsNullOrWhiteSpace(addUri) ? "." : ", and given the identifier of its place."));
        }
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("Choose the other place: where the strings that are not this place should stand.");
        if (going.Count == standing.Count)
        {
            var held = entity.Equivalents.Any(q => q.Uri == target);
            var removed = removeUris.Distinct(StringComparer.Ordinal).Count(u => u != target && entity.Equivalents.Any(q => q.Uri == u));
            var mended = target.StartsWith("entity:", StringComparison.Ordinal)
                ? await MendIntoAsync(dataset, entity.Id, removeUris, target["entity:".Length..], reviewer, ct)
                : await MendAwayAsync(dataset, entity.Id, removeUris, target, reviewer, ct);
            return (mended, $"\"{name}\" mended: {(removed == 0 ? "no identifier" : N(removed, "identifier"))} removed, " + (mended.Id != entity.Id ? $"and merged into \"{mended.Label}\"."
                : held ? "the identifier of the place meant was there already and is kept." : "and the identifier of the place meant added."));
        }
        var to = await MoveStringsAsync(dataset, entity.Id, going, target, reviewer, ct);
        var rest = await RelabelAndConfirmAsync(dataset, entity.Id, keepAs, reviewer, addUri, ct);
        return (rest, $"{N(going.Count, "string")} moved from \"{name}\" to \"{to.Label}\"; \"{rest.Label}\" confirmed for the {N(standing.Count - going.Count, "string")} that stay{(standing.Count - going.Count == 1 ? "s" : "")}.");
    }

    /// <summary>Mends by merging into another entity of the project that a person chose: the identifiers named are removed first, so that the wrong ones do not travel with the merge.</summary>
    public async Task<Entity> MendIntoAsync(string dataset, string entityId, IReadOnlyList<string> removeUris, string survivorId, string reviewer, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        var survivor = await OwnEntityAsync(dataset, survivorId, ct);
        if (survivor.Id == entity.Id) throw new InvalidOperationException("An entity cannot be merged into itself.");
        foreach (var uri in removeUris.Distinct(StringComparer.Ordinal))
            if (entity.Equivalents.Any(q => q.Uri == uri)) await RemoveEquivalentAsync(dataset, entity.Id, uri, reviewer, ct);
        await store.AddNoteAsync(dataset, entity.Id, reviewer, $"Mended from the checks: aligned by the import to a namesake; merged into {survivor.Label ?? survivor.Id}.", ct);
        return await MergeAsync(dataset, entity.Id, survivor.Id, reviewer, null, ct);
    }

    /// <summary>
    /// Mends a place aligned to a namesake, in one step and by a person's word: the identifiers named are removed, and then the entity
    /// is merged into the project's entity that already holds the place at home, or, when none does, takes that place's identifier.
    /// Every part is noted as it is when done by hand. Returns the entity the strings now stand on.
    /// </summary>
    public async Task<Entity> MendAwayAsync(string dataset, string entityId, IReadOnlyList<string> removeUris, string homeUri, string reviewer, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        homeUri = AuthorityUris.Canonical(homeUri.Trim());
        if (homeUri.Length == 0) throw new InvalidOperationException("Give the identifier of the place meant.");
        // any place's identifier, not only one at home: a record of the local index, or a namespace the toolkit recognises (as "Add identifier" has it)
        var home = await store.GetAuthorityAsync(homeUri, ct);
        foreach (var uri in removeUris.Distinct(StringComparer.Ordinal))
            if (uri != homeUri && entity.Equivalents.Any(q => q.Uri == uri)) await RemoveEquivalentAsync(dataset, entity.Id, uri, reviewer, ct);
        var holder = (await store.FindEntitiesByEquivalentAsync(homeUri, ct)).FirstOrDefault(e => e.Dataset == dataset && e.MergedInto is null && e.Id != entity.Id);
        var where = home?.Label is null ? homeUri : $"{home.Label} ({homeUri})";
        if (holder is not null)
        {
            await store.AddNoteAsync(dataset, entity.Id, reviewer, $"Mended from the checks: aligned by the import to a namesake; merged into {holder.Label ?? holder.Id}, which holds {where}.", ct);
            return await MergeAsync(dataset, entity.Id, holder.Id, reviewer, null, ct);
        }
        if (!entity.Equivalents.Any(q => q.Uri == homeUri)) await AddEquivalentAsync(dataset, entity.Id, homeUri, reviewer, ct);
        // a label whose qualifier said the wrong place does not outlive it: the entity takes the name of the place it now is (the owner,
        // 2026-11-24: "Milano (Tex.)", mended to Milan, was still called "Milano (Tex.)")
        if (home?.Label is { Length: > 0 } named && entity.Label is { } old && old.Contains('(') && !string.Equals(old, named, StringComparison.Ordinal)
            && !home.Labels.Append(named).Any(l => string.Equals(l, old, StringComparison.OrdinalIgnoreCase)))
        {
            // the name its strings use, where the record bears it too, before the record's own first name ("Munich (Germany :
            // Landkreis)", mended to the city, became "München": seen as a user, 2026-11-24)
            var bare = Qualified.Split(old).Name;
            await SetLabelAsync(dataset, entity.Id, bare.Length > 0 && home.Labels.Append(named).Any(l => string.Equals(l, bare, StringComparison.OrdinalIgnoreCase)) ? bare : named, reviewer, ct);
        }
        await store.AddNoteAsync(dataset, entity.Id, reviewer, $"Mended from the checks: aligned by the import to a namesake; now {where}.", ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    // ---- export ----------------------------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions ExportJson = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The project's overrides in the pmc-lux JSON shape: <c>local_string_map</c> from raw spelling to identifier, <c>authorities</c> for the entities those identifiers name with their ULAN, VIAF, LoC, Wikidata, AAT, TGN and LUX ids. The identifier is the last segment of the entity's URI.</summary>
    public async Task<string> ExportOverridesJsonAsync(string dataset, CancellationToken ct = default)
    {
        var overrides = await store.OverridesAsync(dataset, ct);
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var entities = new Dictionary<string, Entity>(StringComparer.Ordinal);
        foreach (var o in overrides)
        {
            var entity = scheme.Owns(o.Target) ? await store.ResolveEntityAsync(o.Target, ct) : null;
            var identifier = entity is null ? o.Target : Identifier(entity.Id);
            var spelling = o.Note is { } n && n.StartsWith('"') && n.IndexOf('"', 1) is var end and > 1 ? n[1..end] : o.Normalised;
            map[spelling] = identifier;
            if (entity is not null) entities[entity.Id] = entity;
        }
        var authorities = entities.Values.OrderBy(e => Identifier(e.Id), StringComparer.Ordinal).Select(e =>
        {
            string? Id(string prefix) => e.Equivalents.FirstOrDefault(q => q.Uri.StartsWith(prefix, StringComparison.Ordinal))?.Uri[prefix.Length..];
            return new PmcLuxAuthority(Identifier(e.Id), e.Class.ToString(), e.Label,
                Ulan: Id(AuthorityUris.Ulan), Aat: Id(AuthorityUris.Aat), Tgn: Id(AuthorityUris.Tgn), Viaf: Id(AuthorityUris.Viaf), Wikidata: Id(AuthorityUris.Wikidata),
                Loc: Id(AuthorityUris.LocNames) ?? Id(AuthorityUris.LocSubjects), Lux: e.Equivalents.FirstOrDefault(q => q.Source == "lux")?.Uri);
        }).ToList();
        return JsonSerializer.Serialize(new { local_string_map = map, authorities }, ExportJson);
    }

    private static string Identifier(string entityId) => entityId.TrimEnd('/').Split('/')[^1];

    // ---- guards ----------------------------------------------------------------------------------------------------

    private async Task<SourceString> OwnAsync(string dataset, long stringId, CancellationToken ct)
    {
        var s = await store.GetSourceStringAsync(stringId, ct) ?? throw new KeyNotFoundException($"No string {stringId}.");
        if (s.Dataset != dataset) throw new InvalidOperationException("The string belongs to another project.");
        return s;
    }

    private async Task<Entity> OwnEntityAsync(string dataset, string entityId, CancellationToken ct)
    {
        var e = await store.GetEntityAsync(entityId, ct) ?? throw new KeyNotFoundException($"No entity {entityId}.");
        if (e.Dataset != dataset) throw new InvalidOperationException("The entity belongs to another project.");
        return e;
    }
}

/// <summary>What mending a place aligned to a namesake would do: the entity, the places of its name at home (the most written-about first) and, by the home place's URI, the project's entity that already holds it.</summary>
/// <summary>An entity the check lists: the row, whether it is likely wrong, the strings that stand on it as written, and the reason when a string itself gives one.</summary>
public sealed record AwayFinding(AwayRow Row, bool Likely, IReadOnlyList<string> Strings, string? Reason)
{
    /// <summary>A label that tells the entity from its namesakes, should it be right as aligned: LoC's heading when the entity holds one ("Berkeley (Calif.)"); null when there is none to suggest.</summary>
    public string? SuggestedLabel { get; init; }
    /// <summary>Said on the row when every string of the entity places it, and none in a place of the name at home: the place at home shown is then beside the point.</summary>
    public string? HomeDoubt { get; init; }
    /// <summary>The second kind of row: a bare name on a place labelled as another, the row's "home" being the better-known place of the name, wherever it is.</summary>
    public bool Outweighed { get; init; }
    /// <summary>Of the second kind: the better-known place is where the entity's own label says the entity is, so it is the same place, or one in the same country, under another record; false for a namesake elsewhere.</summary>
    public bool SamePlace { get; init; }
}

public sealed record MendPlan(Entity Entity, IReadOnlyList<AwayRow> Homes, IReadOnlyDictionary<string, Entity> Holders)
{
    /// <summary>The project's other place entities of the same name, wherever they are: the place meant may be one of them.</summary>
    public IReadOnlyList<Entity> Others { get; init; } = [];
    /// <summary>The place at home (its URI) that the entity's Wikidata item names in its own description: the entity is then probably right. Null when the item names none, or says nothing.</summary>
    public string? Agreeing { get; init; }
    /// <summary>
    /// Set when the item and the entity are two places of the name at home: the place at home (its URI) that the entity is by the TGN
    /// identifier it holds, or by what its strings say, while <see cref="Agreeing"/> is the one the import's Wikidata item is. The
    /// import's identifiers are then probably the wrong ones. Null when there is no such difference.
    /// </summary>
    public string? Meant { get; init; }
    /// <summary>A string of the entity that is neither where its item is nor in the place at home the item names ("Wallington (Northumberland)" on Wallington in Sutton): the entity is wrong for that string at least. Null when none.</summary>
    public string? Odd { get; init; }
    /// <summary>Set when the entity may be the right place with only its Wikidata item another record (the better-known place is where its label says it is, and the label names no district or other level): the row whose better-known record could replace the item.</summary>
    public AwayRow? Swap { get; init; }
    /// <summary>For <see cref="Swap"/>: Wikidata's places of the entity's name, the most written-about first, each with how much is written of it and the gazetteer record it names, if any. The person chooses; the best-known is not always the one meant.</summary>
    public IReadOnlyList<(AuthorityRecord Record, int Sitelinks, string? Gazetteer)> SwapItems { get; init; } = [];
    /// <summary>For <see cref="Swap"/>: the gazetteer's places of exactly the entity's name, to give it one where the item chosen names none.</summary>
    public IReadOnlyList<AuthorityRecord> SwapGazetteer { get; init; } = [];
    /// <summary>The place at home to have chosen for the person under "the place meant": the one the entity holds or its strings name, when there is exactly one such, or the one of a conflict. Null when the person must choose.</summary>
    public string? Preselect { get; init; }
    /// <summary>A better-known place of the same name elsewhere, which the bare label would be taken for: a reason to give this entity a label with a qualifier. Null when there is none.</summary>
    public (string Uri, string? Description, int Sitelinks)? Rival { get; init; }
}
