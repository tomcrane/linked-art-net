using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>What the pass did: the entities read, those given a record, the equivalents added by source, and the items it could not use.</summary>
/// <param name="Read">Aligned entities of the dataset with a Wikidata item and no record of the local indexes.</param>
/// <param name="Enriched">Entities that gained at least one equivalent.</param>
/// <param name="AddedBySource">Equivalents added, by the record's source.</param>
/// <param name="ItemsNotInIndex">Entities whose Wikidata item the store does not hold.</param>
/// <param name="ItemsReachingNothing">Entities whose item links no record the store holds of the entity's class.</param>
/// <param name="Ambiguous">Entities whose item links two or more records of one source, that source left alone: the entity, the source and the records.</param>
public sealed record ThroughWikidataReport(int Read, int Enriched, IReadOnlyDictionary<string, int> AddedBySource, int ItemsNotInIndex, int ItemsReachingNothing, IReadOnlyList<string> Ambiguous)
{
    public override string ToString() =>
        $"through wikidata: {Read:N0} entities read, {Enriched:N0} given a record ({string.Join(", ", AddedBySource.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:N0}"))}); " +
        $"{ItemsNotInIndex:N0} items not in the index, {ItemsReachingNothing:N0} reaching no record of the entity's class, {Ambiguous.Count:N0} linking two records of one source (left alone)";
}

/// <summary>
/// The pass over a project's legacy alignments (2026-11-06, the owner at "Birmingham (England)": three of the project's places labelled
/// "Birmingham", aligned to Wikidata, VIAF and LUX and to no TGN or LoC record, unplaced, which nothing in the index stage could join;
/// 2,191 such entities in PMC, 2,023 of them reaching a local record through their item). For each aligned entity with a Wikidata item
/// and no record of the local indexes, the Wikidata record the store holds (the prominence lookup reads the same records) says which
/// TGN, LoC, ULAN and AAT records the item is the same as; those the store holds, of the entity's own class, become its equivalents,
/// one per source, with method <see cref="DecisionMethod.ThroughWikidata"/> and the pass's run id, so <c>undo-run</c> takes the pass
/// back. An item linking two records of one source (a city and its borough) is reported and that source left alone. Then the rules
/// reach the legacy entities unchanged: finding 35 takes a heading onto the entity whose record its group holds, and two legacy
/// entities of one place, both holding its record, meet at the desk as holders to merge.
/// </summary>
public sealed class ThroughWikidata(IAlignmentStore store)
{
    public static readonly IReadOnlyList<string> LocalSources = ["tgn", "loc", "lcsh", "ulan", "aat"];

    public async Task<ThroughWikidataReport> RunAsync(string dataset, string runId, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        int read = 0, enriched = 0, notInIndex = 0, reachingNothing = 0;
        var added = new Dictionary<string, int>(StringComparer.Ordinal);
        var ambiguous = new List<string>();
        string? after = null;
        while (true)
        {
            var page = await store.ListEntitiesAsync(dataset, "wikidata", after, 500, ct);
            if (page.Count == 0) break;
            after = page[^1].Id;
            foreach (var e in page)
            {
                if (e.Status != EntityStatus.Aligned || e.Equivalents.Any(q => LocalSources.Contains(q.Source, StringComparer.Ordinal))) continue;
                read++;
                var item = e.Equivalents.FirstOrDefault(q => q.Source == "wikidata");
                var record = item is null ? null : await store.GetAuthorityAsync(item.Uri, ct);
                if (record is null) { notInIndex++; continue; }
                // the local records the item is the same as, of the entity's own class, by source
                var bySource = new Dictionary<string, List<AuthorityRecord>>(StringComparer.Ordinal);
                foreach (var uri in record.SameAs.Distinct(StringComparer.Ordinal))
                {
                    var local = await store.GetAuthorityAsync(uri, ct);
                    if (local is null || !LocalSources.Contains(local.Source, StringComparer.Ordinal)) continue;
                    if (!string.Equals(local.Type, e.Class.ToString(), StringComparison.OrdinalIgnoreCase)) continue;
                    (bySource.TryGetValue(local.Source, out var list) ? list : bySource[local.Source] = []).Add(local);
                }
                if (bySource.Count == 0) { reachingNothing++; continue; }
                var gained = false;
                foreach (var (source, records) in bySource)
                {
                    if (records.Count > 1) { ambiguous.Add($"{e.Id} ({e.Label}): {source} {string.Join(", ", records.Select(r => r.Uri))}"); continue; }
                    await store.AddEquivalentAsync(e.Id, new Equivalent(records[0].Uri, source, DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow, null, runId), ct);
                    added[source] = added.GetValueOrDefault(source) + 1;
                    gained = true;
                }
                if (gained) enriched++;
                if (read % 200 == 0) progress?.Report($"{read:N0} read, {enriched:N0} given a record");
            }
        }
        return new ThroughWikidataReport(read, enriched, added, notInIndex, reachingNothing, ambiguous);
    }
}
