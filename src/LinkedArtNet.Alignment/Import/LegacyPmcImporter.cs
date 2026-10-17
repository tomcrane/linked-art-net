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
/// placeholder kept in the parsed JSON so nothing is lost. Idempotent: entities already present are kept.
/// </summary>
public sealed class LegacyPmcImporter(NpgsqlDataSource legacy)
{
    public async Task<LegacyImportResult> ImportAsync(IAlignmentStore store, IdentifierScheme scheme, string dataset = "pmc", string? runId = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var byIdentifier = new Dictionary<string, string>(StringComparer.Ordinal);
        int entitiesAdded = 0, entitiesExisting = 0, equivalents = 0, skippedType = 0;
        await using (var cmd = legacy.CreateCommand("select identifier, type, label, ulan, aat, lux, loc, viaf, wikidata, pmc, tgn from authorities"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var identifier = Str(r, 0);
                if (string.IsNullOrWhiteSpace(identifier)) continue;
                var cls = PmcLuxOverrides.ClassOf(Str(r, 1));
                if (cls is null) { skippedType++; continue; }
                var record = new PmcLuxAuthority(identifier, Str(r, 1), Str(r, 2), Ulan: Str(r, 3), Aat: Str(r, 4), Lux: Str(r, 5), Loc: Str(r, 6), Viaf: Str(r, 7), Wikidata: Str(r, 8), Pmc: Str(r, 9), Tgn: Str(r, 10));
                var id = scheme.Uri(cls.Value, identifier);
                byIdentifier[identifier] = id;
                if (await store.GetEntityAsync(id, ct) is null)
                {
                    await store.AddEntityAsync(dataset, new NewEntity(cls.Value, record.Label, Id: id, Status: record.HasAnyAuthority ? EntityStatus.Aligned : EntityStatus.Provisional, RunId: runId), scheme, ct);
                    entitiesAdded++;
                }
                else entitiesExisting++;
                foreach (var uri in record.EquivalentUris())
                {
                    await store.AddEquivalentAsync(id, new Equivalent(uri, AuthorityUris.SourceOf(uri), DecisionMethod.Imported, 1.0, now, RunId: runId), ct);
                    equivalents++;
                }
            }
        }

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
                if (!byIdentifier.TryGetValue(authority, out var entityId)) { danglingAuthority++; unresolved++; continue; }
                if (await store.DecisionForAsync(collected.Id, ct) is { EntityId: not null }) { decided++; continue; }
                await store.DecideAsync(collected.Id, entityId, DecisionMethod.Imported, JsonSerializer.Serialize(new { legacyAuthority = authority }), reviewer: null, datasetVersion: "legacy", runId: runId, ct);
                decided++;
            }
        }
        return new LegacyImportResult(entitiesAdded, entitiesExisting, equivalents, skippedType, strings, decided, unresolved, danglingAuthority);
    }

    private static string? Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}

public sealed record LegacyImportResult(int EntitiesAdded, int EntitiesExisting, int Equivalents, int SkippedForType, int Strings, int Decided, int Unresolved, int DanglingAuthority);
