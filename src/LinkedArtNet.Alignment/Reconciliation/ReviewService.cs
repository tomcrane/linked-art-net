using System.Text.Json;
using System.Text.Json.Serialization;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>The evidence a decision carried, read back for the desk.</summary>
public sealed record EvidenceView(string? Stage, EvidenceReading? Reading, List<EvidenceGroup> Groups, JsonElement? Extra);
public sealed record EvidenceReading(string? Text, string? Key, List<string>? Classes, string? Lifespan, List<string>? Nationalities, List<string>? Roles);
public sealed record EvidenceGroup(double Score, List<string> Sources, List<EvidenceMember> Members);
public sealed record EvidenceMember(string Uri, string Source, string? Label, string? Stage, double Score, bool? Veto, string? Reason, bool? SetAside = null)
{
    public bool IsStore => Stage == "store";
}

/// <summary>One item on the review desk: the string, its current decision and evidence, the entity it points at, notes, the records behind the candidates, and the catalogue records the string was seen in.</summary>
/// <param name="LookedAgain">Set when the evidence shown is a fresh evaluation under today's rules, not the decision's own: what the string would be decided as now ("queued", "accepted", ...). The stored decision is unchanged.</param>
public sealed record ReviewItem(SourceString String, Decision Decision, EvidenceView Evidence, Entity? Entity, IReadOnlyList<Note> Notes, IReadOnlyDictionary<string, AuthorityRecord> Records, IReadOnlyList<StringExample> Examples, string? LookedAgain = null)
{
    public bool IsQueued => Decision.Method == DecisionMethod.Queued;
}

/// <summary>
/// The desk's actions (design doc 12, section 5), so they are tested without a browser and the workbench pages stay
/// thin. Every action writes a decision with method <see cref="DecisionMethod.Human"/> (or override) carrying what
/// was chosen and why; the earlier decision is invalidated, never deleted, so <see cref="UndoAsync"/> restores it.
/// A provisional entity a queued string pointed at is left in place when the string is re-pointed; an orphaned
/// provisional entity is harmless and a later housekeeping job removes them.
/// </summary>
public sealed class ReviewService(IAlignmentStore store, IdentifierScheme scheme, Reconciler reconciler)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static string StringTarget(long stringId) => "string:" + stringId;

    // ---- reading the queue ---------------------------------------------------------------------------------------

    public Task<IReadOnlyList<ContextMethodCount>> QueueCountsAsync(string dataset, string method = DecisionMethod.Queued, CancellationToken ct = default) => store.QueueCountsAsync(dataset, method, ct);

    /// <summary>The next item after a string id in the filtered queue, or null at the end.</summary>
    public async Task<ReviewItem?> NextAsync(string dataset, string method = DecisionMethod.Queued, string? context = null, long afterStringId = 0, CancellationToken ct = default)
    {
        var page = await store.DecidedStringsAsync(dataset, method, context, afterStringId, 1, false, ct);
        return page.Count == 0 ? null : await LoadAsync(page[0], ct);
    }

    public async Task<ReviewItem?> GetAsync(long stringId, CancellationToken ct = default)
    {
        var decided = await store.DecidedStringAsync(stringId, ct);
        return decided is null ? null : await LoadAsync(decided, ct);
    }

    /// <summary>
    /// The string looked at again under the rules as they stand (the desk, 2026-10-26: a decision shows the evidence stored with it, so a
    /// string decided before a rule change, or by hand, never shows what today's rules would offer). Nothing is decided; the item carries
    /// the fresh evidence and what the outcome would be, and the actions act on what is shown.
    /// </summary>
    public async Task<ReviewItem> LookAgainAsync(string dataset, long stringId, CancellationToken ct = default)
    {
        var decided = await OwnAsync(dataset, stringId, ct);
        var evaluation = await reconciler.EvaluateAsync(dataset, decided.String, includeStore: true, ct);
        var fresh = ParseEvidence(Reconciler.Evidence(evaluation.Outcome.ToString().ToLowerInvariant(), evaluation.Reading, evaluation.Groups, null));
        return await LoadAsync(decided, ct, fresh, evaluation.Outcome.ToString().ToLowerInvariant());
    }

    private async Task<ReviewItem> LoadAsync(DecidedString decided, CancellationToken ct, EvidenceView? fresh = null, string? lookedAgain = null)
    {
        var evidence = fresh ?? ParseEvidence(decided.Decision.EvidenceJson);
        var entity = decided.Decision.EntityId is null ? null : await store.ResolveEntityAsync(decided.Decision.EntityId, ct);
        var notes = await store.NotesAsync(decided.String.Dataset, StringTarget(decided.String.Id), ct);
        var records = new Dictionary<string, AuthorityRecord>(StringComparer.Ordinal);
        foreach (var uri in evidence.Groups.SelectMany(g => g.Members).Where(m => !m.IsStore).Select(m => m.Uri).Distinct(StringComparer.Ordinal).Take(40))
            if (await store.GetAuthorityAsync(uri, ct) is { } record) records[uri] = record;
        return new ReviewItem(decided.String, decided.Decision, evidence, entity, notes, records, await ExamplesForAsync(decided.String.Id, ct), lookedAgain);
    }

    /// <summary>The catalogue records a string was seen in (design doc 12 section 5), up to three; a part string with none of its own (a keyword's head, a place in a list) shows its parents'.</summary>
    public async Task<IReadOnlyList<StringExample>> ExamplesForAsync(long stringId, CancellationToken ct = default)
    {
        var own = await store.ExamplesForAsync(stringId, 3, ct);
        if (own.Count > 0) return own;
        var list = new List<StringExample>();
        foreach (var parent in await store.ParentsAsync(stringId, ct))
        {
            foreach (var e in await store.ExamplesForAsync(parent.Id, 3, ct))
                if (list.All(x => x.RecordId != e.RecordId)) list.Add(e);
            if (list.Count >= 3) break;
        }
        return list.Take(3).ToList();
    }

    public static EvidenceView ParseEvidence(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new EvidenceView(null, null, [], null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var stage = root.TryGetProperty("stage", out var st) ? st.GetString() : null;
            var reading = root.TryGetProperty("reading", out var rd) ? rd.Deserialize<EvidenceReading>(Json) : null;
            var groups = root.TryGetProperty("groups", out var gs) ? gs.Deserialize<List<EvidenceGroup>>(Json) ?? [] : [];
            JsonElement? extra = root.TryGetProperty("extra", out var ex) && ex.ValueKind != JsonValueKind.Null ? ex.Clone() : null;
            return new EvidenceView(stage, reading, groups, extra);
        }
        catch (JsonException) { return new EvidenceView(null, null, [], null); }
    }

    // ---- searching, for "same as" and "accept another" -----------------------------------------------------------

    public async Task<IReadOnlyList<Entity>> SearchEntitiesAsync(string dataset, string text, int limit = 20, CancellationToken ct = default)
    {
        var found = new Dictionary<string, Entity>(StringComparer.Ordinal);
        foreach (var key in NameMatcher.Keys(text))
            foreach (var e in await store.FindEntitiesByLabelAsync(key, dataset, null, ct))
                if (found.Count < limit) found[e.Id] = e;
        // then anything whose labels contain the text, for a single word or a part of a name
        if (found.Count < limit)
            foreach (var e in await store.FindEntitiesByLabelContainingAsync(dataset, StringKey.Normalise(text), limit - found.Count, ct))
                found.TryAdd(e.Id, e);
        return found.Values.ToList();
    }

    public Task<IReadOnlyList<AuthorityRecord>> SearchIndexesAsync(string text, string? source = null, int limit = 20, CancellationToken ct = default) =>
        store.FindAuthoritiesByLabelsAsync(NameMatcher.Keys(text), source, null, limit, ct);

    // ---- actions -------------------------------------------------------------------------------------------------

    /// <summary>The string with its decision, and only if it belongs to the project: a page of another project cannot act on it.</summary>
    private async Task<DecidedString> OwnAsync(string dataset, long stringId, CancellationToken ct)
    {
        var decided = await store.DecidedStringAsync(stringId, ct) ?? throw new KeyNotFoundException($"No decided string {stringId}.");
        if (!string.Equals(decided.String.Dataset, dataset, StringComparison.Ordinal)) throw new InvalidOperationException("The string belongs to another project.");
        return decided;
    }

    /// <summary>A target the reviewer may choose: a candidate in the decision's evidence, an authority record the store holds, or an entity of this project. Anything else is refused rather than minted.</summary>
    private async Task EnsureTargetAsync(string dataset, DecidedString decided, string targetUri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(targetUri) || !Uri.TryCreate(targetUri, UriKind.Absolute, out _)) throw new InvalidOperationException("The target is not a URI.");
        if (scheme.Owns(targetUri))
        {
            var entity = await store.ResolveEntityAsync(targetUri, ct);
            if (entity is null || entity.Dataset != dataset) throw new InvalidOperationException("The target is not an entity of this project.");
            return;
        }
        if (await store.GetAuthorityAsync(targetUri, ct) is not null) return;
        var evidence = ParseEvidence(decided.Decision.EvidenceJson);
        if (evidence.Groups.Any(g => g.Members.Any(m => m.Uri == targetUri))) return;
        throw new InvalidOperationException("The target is not a candidate, a known authority record or an entity of this project.");
    }

    /// <summary>
    /// The reviewer says several cards are one thing (the desk, 2026-10-26: ULAN's "Cock, Christopher" and LoC's "Cock, Mr. (Christopher)"
    /// in two cards): one entity, made or found for the first target as Accept does it, or for the one entity of ours among the targets,
    /// and every other target an equivalent the reviewer vouched for. At most one of our entities; two would be a merge, which the
    /// entity page does.
    /// </summary>
    public async Task<Entity> AcceptTogetherAsync(string dataset, long stringId, IReadOnlyList<string> targetUris, string reviewer, string? note = null, CancellationToken ct = default)
    {
        var decided = await OwnAsync(dataset, stringId, ct);
        var targets = targetUris.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (targets.Count == 0) throw new InvalidOperationException("Nothing is ticked.");
        foreach (var t in targets) await EnsureTargetAsync(dataset, decided, t, ct);
        var ours = targets.Where(scheme.Owns).ToList();
        if (ours.Count > 1) throw new InvalidOperationException("Two of the project's own entities are ticked: merge them on the entity page first.");
        var reading = reconciler.Read(decided.String);
        var entity = await reconciler.EntityForUriAsync(dataset, ours.Count == 1 ? ours[0] : targets[0], reading, null, ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var t in targets.Where(t => !scheme.Owns(t)))
            if (entity.Equivalents.All(q => q.Uri != t)) await store.AddEquivalentAsync(entity.Id, new Equivalent(t, AuthorityUris.SourceOf(t), DecisionMethod.Human, 1.0, now), ct);
        await FoldProvisionalAsync(decided, entity, reviewer, ct);
        await store.DecideAsync(stringId, entity.Id, DecisionMethod.Human, Evidence("accept-together", reading, new { targets, entity = entity.Id, note }), reviewer, ct: ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>The reviewer says the string is this authority record (a URI from the candidates or a search) or this entity (one of ours).</summary>
    public async Task<Entity> AcceptAsync(string dataset, long stringId, string targetUri, string reviewer, string? note = null, CancellationToken ct = default)
    {
        var decided = await OwnAsync(dataset, stringId, ct);
        await EnsureTargetAsync(dataset, decided, targetUri, ct);
        var reading = reconciler.Read(decided.String);
        var entity = await reconciler.EntityForUriAsync(dataset, targetUri, reading, null, ct);
        await FoldProvisionalAsync(decided, entity, reviewer, ct);
        await store.DecideAsync(stringId, entity.Id, DecisionMethod.Human, Evidence("accept", reading, new { target = targetUri, entity = entity.Id, note }), reviewer, ct: ct);
        await NoteIfAnyAsync(dataset, stringId, reviewer, note, ct);
        return entity;
    }

    /// <summary>The provisional entity a queued or minted string pointed at merges into the entity the reviewer chose, so nothing is left behind and its other strings follow. An entity with authority behind it is left alone.</summary>
    private async Task FoldProvisionalAsync(DecidedString decided, Entity chosen, string reviewer, CancellationToken ct)
    {
        if (decided.Decision.EntityId is null || decided.Decision.EntityId == chosen.Id) return;
        var current = await store.ResolveEntityAsync(decided.Decision.EntityId, ct);
        if (current is null || current.Id == chosen.Id || current.Status != EntityStatus.Provisional || current.Equivalents.Count > 0) return;
        try { await store.MergeAsync(current.Id, chosen.Id, reviewer, ct); }
        catch (InvalidOperationException) { /* a chain or project mismatch: the decision still re-points below */ }
    }

    /// <summary>The reviewer says the string is the same as an entity already in the store.</summary>
    public async Task<Entity> SameAsAsync(string dataset, long stringId, string entityId, string reviewer, string? note = null, CancellationToken ct = default)
    {
        var decided = await OwnAsync(dataset, stringId, ct);
        var entity = await store.ResolveEntityAsync(entityId, ct) ?? throw new KeyNotFoundException($"No entity {entityId}.");
        if (entity.Dataset != dataset) throw new InvalidOperationException("The entity belongs to another project.");
        var reading = reconciler.Read(decided.String);
        await FoldProvisionalAsync(decided, entity, reviewer, ct);
        await store.DecideAsync(stringId, entity.Id, DecisionMethod.Human, Evidence("same-as", reading, new { entity = entity.Id, note }), reviewer, ct: ct);
        await NoteIfAnyAsync(dataset, stringId, reviewer, note, ct);
        return entity;
    }

    /// <summary>The reviewer says none of the candidates is right and the thing is real: the provisional entity becomes a confirmed local authority, renamed if asked.</summary>
    public async Task<Entity> KeepLocalAsync(string dataset, long stringId, string reviewer, string? label = null, string? note = null, CancellationToken ct = default)
    {
        var decided = await OwnAsync(dataset, stringId, ct);
        var reading = reconciler.Read(decided.String);
        var entity = decided.Decision.EntityId is null ? null : await store.ResolveEntityAsync(decided.Decision.EntityId, ct);
        if (entity is null || entity.Status is EntityStatus.Aligned or EntityStatus.Confirmed && entity.Equivalents.Count > 0)
            entity = await store.AddEntityAsync(dataset, new NewEntity(reading.Classes[0], label ?? reading.Text, [decided.String.Raw], reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Confirmed), scheme, ct);
        else
        {
            var labels = entity.Labels.Append(decided.String.Raw).Append(entity.Label).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            await store.UpdateEntityAsync(entity with { Label = string.IsNullOrWhiteSpace(label) ? entity.Label : label, Labels = labels, Status = EntityStatus.Confirmed }, ct);
            entity = (await store.GetEntityAsync(entity.Id, ct))!;
        }
        await store.DecideAsync(stringId, entity.Id, DecisionMethod.Human, Evidence("local", reading, new { entity = entity.Id, label, note }), reviewer, ct: ct);
        await NoteIfAnyAsync(dataset, stringId, reviewer, note, ct);
        return entity;
    }

    /// <summary>The reviewer makes this spelling always mean the target: an override, applied now. Scoped to the field the string came from, since the same spelling in another field may be something else; <paramref name="anyField"/> widens it to every field.</summary>
    public async Task<Entity> OverrideAsync(string dataset, long stringId, string targetUri, string reviewer, string? note = null, bool anyField = false, CancellationToken ct = default)
    {
        var decided = await OwnAsync(dataset, stringId, ct);
        await EnsureTargetAsync(dataset, decided, targetUri, ct);
        var reading = reconciler.Read(decided.String);
        var entity = await reconciler.EntityForUriAsync(dataset, targetUri, reading, null, ct);
        var scope = anyField ? null : decided.String.Context;
        // the override this one replaces, in the same scope, is kept in the evidence so an undo can put it back
        var previous = await store.OverrideForAsync(dataset, scope ?? "", decided.String.Normalised, ct);
        previous = previous is not null && previous.Context == scope ? previous : null;
        await store.PutOverrideAsync(new Override(dataset, scope, decided.String.Normalised, entity.Id, "workbench:" + reviewer, $"\"{decided.String.Raw}\" → {targetUri}" + (note is null ? "" : $" ({note})")), ct);
        await store.DecideAsync(stringId, entity.Id, DecisionMethod.Override, Evidence("override", reading, new { target = targetUri, entity = entity.Id, note, overrideKey = decided.String.Normalised, overrideContext = scope, previousOverride = previous is null ? null : new { previous.Target, previous.Source, previous.Note } }), reviewer, ct: ct);
        await NoteIfAnyAsync(dataset, stringId, reviewer, note, ct);
        return entity;
    }

    /// <summary>A note for another reviewer; the decision is unchanged.</summary>
    public async Task<Note> FlagAsync(string dataset, long stringId, string reviewer, string text, CancellationToken ct = default)
    {
        await OwnAsync(dataset, stringId, ct);
        return await store.AddNoteAsync(dataset, StringTarget(stringId), reviewer, text, ct);
    }

    /// <summary>Takes back the latest decision; what stood before is valid again. Undoing an "always" also puts the previous override back, or removes the one it made.</summary>
    public async Task<Decision?> UndoAsync(string dataset, long stringId, CancellationToken ct = default)
    {
        var decided = await OwnAsync(dataset, stringId, ct);
        if (decided.Decision.Method == DecisionMethod.Override && ParseEvidence(decided.Decision.EvidenceJson).Extra is { } extra && extra.TryGetProperty("overrideKey", out var keyElement) && keyElement.GetString() is { } key)
        {
            var scope = extra.TryGetProperty("overrideContext", out var ctxElement) && ctxElement.ValueKind == JsonValueKind.String ? ctxElement.GetString() : null;
            if (extra.TryGetProperty("previousOverride", out var prev) && prev.ValueKind == JsonValueKind.Object)
                await store.PutOverrideAsync(new Override(dataset, scope, key, prev.GetProperty("target").GetString()!, prev.GetProperty("source").GetString() ?? "unknown", prev.TryGetProperty("note", out var n) ? n.GetString() : null), ct);
            else
                await store.RemoveOverrideAsync(dataset, scope, key, ct);
        }
        return await store.RevertDecisionAsync(stringId, ct);
    }

    private async Task NoteIfAnyAsync(string dataset, long stringId, string reviewer, string? note, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(note)) await store.AddNoteAsync(dataset, StringTarget(stringId), reviewer, note, ct);
    }

    private static string Evidence(string action, StringReading reading, object extra) => Reconciler.Evidence(action, reading, [], extra);
}
