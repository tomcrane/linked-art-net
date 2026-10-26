using System.Text.Json;
using System.Text.Json.Serialization;
using LinkedArtNet.Alignment.Import;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>Strings that resemble a seed, grouped by the entity each currently resolves to (null for undecided).</summary>
public sealed record VariantGroup(Entity? Entity, IReadOnlyList<DecidedOrOpenString> Strings);
public sealed record DecidedOrOpenString(SourceString String, Decision? Decision);

/// <summary>An entity with the strings that resolve to it and its notes, for the entity page.</summary>
public sealed record EntityView(Entity Entity, IReadOnlyList<DecidedString> Strings, IReadOnlyList<Note> Notes, IReadOnlyDictionary<string, AuthorityRecord> Records);

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
        return new EntityView(entity, strings, notes, records);
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
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            entity = await store.ResolveEntityAsync(entityId, ct) ?? throw new KeyNotFoundException($"No entity {entityId}.");
            if (entity.Dataset != dataset) throw new InvalidOperationException("The entity belongs to another project.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(newLabel)) throw new InvalidOperationException("Give the new entity a label, or choose an existing one.");
            var first = await OwnAsync(dataset, stringIds[0], ct);
            var reading = reconciler.Read(first);
            entity = await store.AddEntityAsync(dataset, new NewEntity(reading.Classes[0], newLabel.Trim(), [], reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Confirmed), scheme, ct);
        }
        foreach (var uri in externalUris.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal))
            await AddEquivalentAsync(dataset, entity.Id, uri, reviewer, ct);

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
        if (!string.IsNullOrWhiteSpace(note)) await store.AddNoteAsync(dataset, entity.Id, reviewer, note, ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    // ---- entity actions --------------------------------------------------------------------------------------------

    public async Task<Entity> SetLabelAsync(string dataset, string entityId, string label, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        label = label.Trim();
        if (label.Length == 0) throw new InvalidOperationException("A label is needed.");
        var labels = entity.Labels.Append(entity.Label).OfType<string>().Where(l => l != label).Distinct(StringComparer.Ordinal).ToList();
        await store.UpdateEntityAsync(entity with { Label = label, Labels = labels }, ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>Attaches an authority URI the store knows (a cached record) or that lives in a namespace the toolkit recognises, an ArchivesSpace agent's public URL among them.</summary>
    public async Task<Entity> AddEquivalentAsync(string dataset, string entityId, string uri, string reviewer, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        uri = uri.Trim();
        if (!Uri.TryCreate(uri, UriKind.Absolute, out _)) throw new InvalidOperationException("The identifier is not a URI.");
        if (scheme.Owns(uri)) throw new InvalidOperationException("Use merge for one of our own entities.");
        var known = await store.GetAuthorityAsync(uri, ct) is not null;
        if (!known && AuthorityUris.SourceOf(uri) == "other") throw new InvalidOperationException("Not a known authority record and not in a recognised namespace (ULAN, AAT, TGN, VIAF, Wikidata, GND, LoC, LUX, PeriodO, or an ArchivesSpace agent's URL).");
        await store.AddEquivalentAsync(entity.Id, new Equivalent(uri, AuthorityUris.SourceOf(uri), DecisionMethod.Human, 1.0, DateTimeOffset.UtcNow), ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    public async Task<Entity> RemoveEquivalentAsync(string dataset, string entityId, string uri, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        await store.RemoveEquivalentAsync(entity.Id, uri, ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>A person vouches for the entity: confirmed, in no external source or with the ids it has.</summary>
    public async Task<Entity> ConfirmAsync(string dataset, string entityId, string reviewer, string? note, CancellationToken ct = default)
    {
        var entity = await OwnEntityAsync(dataset, entityId, ct);
        await store.UpdateEntityAsync(entity with { Status = EntityStatus.Confirmed }, ct);
        if (!string.IsNullOrWhiteSpace(note)) await store.AddNoteAsync(dataset, entity.Id, reviewer, note, ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    public async Task<Entity> MergeAsync(string dataset, string loserId, string survivorId, string reviewer, CancellationToken ct = default)
    {
        await OwnEntityAsync(dataset, loserId, ct);
        var survivor = await OwnEntityAsync(dataset, survivorId, ct);
        await store.MergeAsync(loserId, survivor.Id, reviewer, ct);
        return (await store.ResolveEntityAsync(survivor.Id, ct))!;
    }

    /// <summary>Takes chosen strings away from an entity: their decisions are invalidated and they are undecided again for the next reconcile.</summary>
    public async Task<int> ReopenAsync(string dataset, IReadOnlyList<long> stringIds, CancellationToken ct = default)
    {
        var n = 0;
        foreach (var id in stringIds.Distinct()) { await OwnAsync(dataset, id, ct); if (await store.ReopenStringAsync(id, ct)) n++; }
        return n;
    }

    public Task<Note> NoteAsync(string dataset, string entityId, string reviewer, string text, CancellationToken ct = default) => store.AddNoteAsync(dataset, entityId, reviewer, text, ct);

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
