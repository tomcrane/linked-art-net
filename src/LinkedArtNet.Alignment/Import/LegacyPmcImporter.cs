using System.Text.Json;
using LinkedArtNet.Alignment.Store;
using Npgsql;

namespace LinkedArtNet.Alignment.Import;

/// <summary>
/// Carries the owner's earlier PMC tables into the store (design doc 10, section 9). The old database had
/// <c>authorities</c> (identifier, type, label, ulan, aat, lux, loc, viaf, wikidata, pmc, tgn) and
/// <c>source_string_map</c> (source, string, authority, processed, unreconciled_authority). Authorities become
/// entities with the identifiers already published, their authority ids become equivalents (method imported);
/// every source string is collected under its old source name as the context, with a decision of method imported
/// when it pointed at an authority. Strings that only had an unreconciled placeholder stay unresolved, the
/// placeholder kept in the parsed JSON so nothing is lost. Idempotent: entities already present are kept, and a
/// string that already has a decision keeps it.
/// </summary>
/// <remarks>
/// A second copy of the old tables from another run (its own minted identifiers, mostly the same organisations)
/// imports with <see cref="LegacyImportOptions.ReuseByEquivalent"/> so an authority sharing a ULAN, VIAF, LoC or
/// Wikidata id with an entity already in the store maps onto that entity instead of becoming a duplicate, and
/// <see cref="LegacyImportOptions.OnlyReferenced"/> so only authorities some new decision needs become entities.
/// </remarks>
public sealed class LegacyPmcImporter(NpgsqlDataSource legacy)
{
    public Task<LegacyImportResult> ImportAsync(IAlignmentStore store, IdentifierScheme scheme, string dataset = "pmc", string? runId = null, CancellationToken ct = default)
        => ImportAsync(store, scheme, new LegacyImportOptions(), dataset, runId, ct);

    public async Task<LegacyImportResult> ImportAsync(IAlignmentStore store, IdentifierScheme scheme, LegacyImportOptions options, string dataset = "pmc", string? runId = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var records = new Dictionary<string, (EntityClass Class, PmcLuxAuthority Record)>(StringComparer.Ordinal);
        int skippedType = 0;
        await using (var cmd = legacy.CreateCommand("select identifier, type, label, ulan, aat, lux, loc, viaf, wikidata, pmc, tgn from authorities"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var identifier = Str(r, 0);
                if (string.IsNullOrWhiteSpace(identifier)) continue;
                var cls = PmcLuxOverrides.ClassOf(Str(r, 1));
                if (cls is null) { skippedType++; continue; }
                records[identifier] = (cls.Value, new PmcLuxAuthority(identifier, Str(r, 1), Str(r, 2), Ulan: Str(r, 3), Aat: Str(r, 4), Lux: Str(r, 5), Loc: Str(r, 6), Viaf: Str(r, 7), Wikidata: Str(r, 8), Pmc: Str(r, 9), Tgn: Str(r, 10)));
            }
        }

        // identifier -> the entity it maps to, settled once per authority
        var byIdentifier = new Dictionary<string, string>(StringComparer.Ordinal);
        int entitiesAdded = 0, entitiesExisting = 0, entitiesReused = 0, equivalents = 0;

        async Task<string> EntityFor(string identifier)
        {
            if (byIdentifier.TryGetValue(identifier, out var known)) return known;
            var (cls, record) = records[identifier];
            var own = scheme.Uri(cls, identifier);
            var id = own;
            var existing = await store.GetEntityAsync(own, ct);
            if (existing is null && options.ReuseByEquivalent)
            {
                foreach (var uri in record.EquivalentUris())
                {
                    var matches = await store.FindEntitiesByEquivalentAsync(uri, ct);
                    var match = matches.FirstOrDefault(e => e.Dataset == dataset && e.MergedInto is null && e.Class == cls);
                    if (match is null) continue;
                    existing = match;
                    id = match.Id;
                    entitiesReused++;
                    if (!string.IsNullOrWhiteSpace(record.Label) && !string.Equals(match.Label, record.Label, StringComparison.Ordinal) && !match.Labels.Contains(record.Label, StringComparer.Ordinal))
                        await store.UpdateEntityAsync(match with { Labels = [.. match.Labels, record.Label] }, ct);
                    break;
                }
            }
            if (existing is null)
            {
                await store.AddEntityAsync(dataset, new NewEntity(cls, record.Label, Id: id, Status: record.HasAnyAuthority ? EntityStatus.Aligned : EntityStatus.Provisional, RunId: runId), scheme, ct);
                entitiesAdded++;
            }
            else if (id == own) entitiesExisting++;
            var present = existing?.Equivalents.Select(e => e.Uri).ToHashSet(StringComparer.Ordinal) ?? [];
            foreach (var uri in record.EquivalentUris())
            {
                if (present.Contains(uri)) continue;
                await store.AddEquivalentAsync(id, new Equivalent(uri, AuthorityUris.SourceOf(uri), DecisionMethod.Imported, 1.0, now, options.DatasetVersion, runId), ct);
                equivalents++;
            }
            byIdentifier[identifier] = id;
            return id;
        }

        if (!options.OnlyReferenced)
            foreach (var identifier in records.Keys) await EntityFor(identifier);

        int strings = 0, decided = 0, unresolved = 0, danglingAuthority = 0;
        await using (var cmd = legacy.CreateCommand("select source, string, authority, unreconciled_authority from source_string_map"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var context = Str(r, 0) ?? "unknown";
                var raw = Str(r, 1);
                if (string.IsNullOrWhiteSpace(raw) || StringKey.Normalise(raw).Length == 0) continue;
                var authority = Str(r, 2);
                var unreconciled = Str(r, 3);
                var parsed = unreconciled is null ? null : JsonSerializer.Serialize(new { legacyUnreconciled = unreconciled });
                var collected = await store.CollectAsync(dataset, context, raw, parsed, ct);
                strings++;
                if (authority is null) { unresolved++; continue; }
                if (!records.ContainsKey(authority)) { danglingAuthority++; unresolved++; continue; }
                if (await store.DecisionForAsync(collected.Id, ct) is { EntityId: not null }) { decided++; continue; }
                var entityId = await EntityFor(authority);
                await store.DecideAsync(collected.Id, entityId, DecisionMethod.Imported, JsonSerializer.Serialize(new { legacyAuthority = authority }), reviewer: null, datasetVersion: options.DatasetVersion, runId: runId, ct);
                decided++;
            }
        }
        return new LegacyImportResult(entitiesAdded, entitiesExisting, equivalents, skippedType, strings, decided, unresolved, danglingAuthority, entitiesReused);
    }

    private static string? Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}

/// <param name="ReuseByEquivalent">Map an authority onto an existing entity of the same dataset and class that already has one of its authority ids, instead of adding a duplicate; the authority's label joins that entity's alternate labels.</param>
/// <param name="OnlyReferenced">Add entities only for authorities that a string without a decision points at; the rest of the table is ignored.</param>
/// <param name="DatasetVersion">Recorded on the decisions and equivalents this import writes, so two old runs stay distinguishable.</param>
public sealed record LegacyImportOptions(bool ReuseByEquivalent = false, bool OnlyReferenced = false, string DatasetVersion = "legacy");

/// <param name="EntitiesReused">Authorities mapped onto an entity already in the store through a shared authority id (<see cref="LegacyImportOptions.ReuseByEquivalent"/>).</param>
public sealed record LegacyImportResult(int EntitiesAdded, int EntitiesExisting, int Equivalents, int SkippedForType, int Strings, int Decided, int Unresolved, int DanglingAuthority, int EntitiesReused = 0);
