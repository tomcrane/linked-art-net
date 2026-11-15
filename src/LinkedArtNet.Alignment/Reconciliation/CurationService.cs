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

    /// <summary>Takes chosen strings away from an entity: their decisions are invalidated and they are undecided again for the next reconcile.</summary>
    public async Task<int> ReopenAsync(string dataset, IReadOnlyList<long> stringIds, string? reviewer = null, string? entityId = null, CancellationToken ct = default)
    {
        var n = 0;
        var raws = new List<string>();
        foreach (var id in stringIds.Distinct())
        {
            var s = await OwnAsync(dataset, id, ct);
            if (await store.ReopenStringAsync(id, ct)) { n++; raws.Add($"\"{s.Raw}\" ({s.Context})"); }
        }
        if (n > 0 && entityId is not null) await store.AddNoteAsync(dataset, entityId, reviewer, $"{n} string{(n == 1 ? "" : "s")} reopened for the next reconcile: {string.Join(", ", raws.Take(6))}{(raws.Count > 6 ? ", ..." : "")}.", ct);
        return n;
    }

    public Task<Note> NoteAsync(string dataset, string entityId, string reviewer, string text, CancellationToken ct = default) => store.AddNoteAsync(dataset, entityId, reviewer, text, ct);

    // ---- checks on the store's entities ------------------------------------------------------------------------------

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
        foreach (var pairs in (await store.ImportedPlacesWithHomeNamesakesAsync(dataset, homePlace, ct)).GroupBy(r => (r.EntityId, r.Item)))
        {
            var rows = pairs.ToList();
            var descr = rows[0].ItemDescription ?? "";
            bool Names(AwayRow r)
            {
                if (r.HomeItem == r.Item || descr.Contains(homePlace, StringComparison.OrdinalIgnoreCase)) return true;
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
            if (rows.Any(Names)) continue;
            var home = rows.OrderByDescending(r => r.HomeSitelinks).First();
            // what the strings themselves say
            var strings = (await store.StringsForEntityAsync(rows[0].EntityId, 200, ct)).Select(s => s.String.Raw).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();
            var qualifiers = strings.Select(raw => (Raw: raw, Words: Qualified.Tokens(Qualified.Split(raw).Qualifier))).ToList();
            if (strings.Count > 0 && rows[0].ItemDescription is not null && qualifiers.All(q => q.Words.Count > 0 && Qualified.Contains(descr, q.Words))) continue;
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
            found.Add(new AwayFinding(home, home.ItemDescription is not null && home.HomeItem is not null && home.HomeSitelinks >= home.ItemSitelinks, strings, null));
        }
        // the likely first, the widest margin leading; then the rest, the least written-about abroad first ("Perth" in Tasmania before "New York")
        return found.OrderByDescending(f => f.Likely).ThenByDescending(f => f.Reason is not null).ThenByDescending(f => f.Likely ? f.Row.HomeSitelinks - f.Row.ItemSitelinks : -f.Row.ItemSitelinks).ThenBy(f => f.Row.Label, StringComparer.Ordinal).ToList();
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
            .GroupBy(r => r.HomeUri).Select(g => g.First()).OrderByDescending(r => r.HomeSitelinks).ThenBy(r => r.HomeChain, StringComparer.Ordinal).ToList();
        if (homes.Count == 0) return null;
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
        return new MendPlan(entity, homes, holders) { Others = others };
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
public sealed record AwayFinding(AwayRow Row, bool Likely, IReadOnlyList<string> Strings, string? Reason);

public sealed record MendPlan(Entity Entity, IReadOnlyList<AwayRow> Homes, IReadOnlyDictionary<string, Entity> Holders)
{
    /// <summary>The project's other place entities of the same name, wherever they are: the place meant may be one of them.</summary>
    public IReadOnlyList<Entity> Others { get; init; } = [];
}
