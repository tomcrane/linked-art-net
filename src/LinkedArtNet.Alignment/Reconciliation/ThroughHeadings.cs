using LinkedArtNet.Alignment.Authorities;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>What the pass did, or would do: the places read, those given a record, the equivalents added by source, and those left alone, each with why.</summary>
/// <param name="Read">Live places of the dataset, aligned or confirmed, with no gazetteer's (TGN) and no Wikidata identifier.</param>
/// <param name="Given">Places that gained, or would gain, at least one identifier.</param>
/// <param name="AddedBySource">Identifiers added, by the record's source.</param>
/// <param name="NoHeading">Places with no library heading among their identifiers: nothing to follow.</param>
/// <param name="NoItem">Places whose headings no Wikidata item in the store names.</param>
/// <param name="Gave">One line for each place given a record: the place, and what it was given.</param>
/// <param name="LeftAlone">One line for each place with an item that the pass would not use: several items, an item that is no place, an item another entity of the project holds.</param>
public sealed record ThroughHeadingsReport(int Read, int Given, IReadOnlyDictionary<string, int> AddedBySource, int NoHeading, int NoItem, IReadOnlyList<string> Gave, IReadOnlyList<string> LeftAlone)
{
    public override string ToString() =>
        $"through headings: {Read:N0} places read, {Given:N0} given a record ({string.Join(", ", AddedBySource.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value:N0}"))}); " +
        $"{NoHeading:N0} with no library heading, {NoItem:N0} whose heading no item names, {LeftAlone.Count:N0} left alone";
}

/// <summary>
/// The pass over a project's thin places (the owner, 2026-11-23, after "Chatham": the project's Chatham (Kent) held LoC's heading and
/// nothing else, so it stood low on its card and was the one candidate not on the map; agreed 2026-11-24 as a pass before a check).
/// Counted over PMC: 617 live places with no TGN and no Wikidata identifier, 395 of them with exactly one record in the store that
/// names one of their identifiers as the same thing, every one a Wikidata item. Read in a sample of seventy, the items reached by the
/// library's heading were the place; those reached by a VIAF identifier alone were often a namesake abroad, the old import's own
/// mistake ("Darlington" a suburb of Perth, "Leipsic" a village in Ohio, "Inverness" in Mississippi). So the pass follows the library
/// headings (LoC's names and subjects) and nothing else: a place whose heading exactly one Wikidata item names, the item a place and
/// held by no other entity of the project, gains the item, and through it the one TGN record the item names, when that too is held by
/// no other. Method <see cref="DecisionMethod.ThroughWikidata"/> and the pass's run id, so <c>undo-run</c> takes it back. With no run
/// id nothing is written and the report says what would be.
/// </summary>
public sealed class ThroughHeadings(IAlignmentStore store)
{
    private static readonly string[] Libraries = ["loc", "lcsh"];

    public async Task<ThroughHeadingsReport> RunAsync(string dataset, string? runId, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        int read = 0, given = 0, noHeading = 0, noItem = 0;
        var added = new Dictionary<string, int>(StringComparer.Ordinal);
        var gave = new List<string>();
        var left = new List<string>();
        string? after = null;
        while (true)
        {
            var page = await store.ListEntitiesAsync(dataset, null, after, 500, ct);
            if (page.Count == 0) break;
            after = page[^1].Id;
            foreach (var e in page)
            {
                if (e.Class != EntityClass.Place || e.MergedInto is not null || e.Status is not (EntityStatus.Aligned or EntityStatus.Confirmed)) continue;
                if (e.Equivalents.Any(q => q.Source is "tgn" or "wikidata")) continue;
                read++;
                var headings = e.Equivalents.Where(q => Libraries.Contains(q.Source, StringComparer.Ordinal)).Select(q => q.Uri).ToList();
                if (headings.Count == 0) { noHeading++; continue; }
                var naming = new Dictionary<string, AuthorityRecord>(StringComparer.Ordinal);
                foreach (var heading in headings)
                    foreach (var r in await store.FindAuthoritiesBySameAsAsync(heading, ct))
                        if (r.Source is "wikidata" or "tgn") naming[r.Uri] = r;
                var items = naming.Values.Where(r => r.Source == "wikidata").ToList();
                if (items.Count == 0 && naming.Count == 0) { noItem++; continue; }
                var name = $"{e.Label} <{e.Id}>";
                if (items.Count > 1) { left.Add($"{name}: {items.Count} items name its heading ({string.Join(", ", items.Select(Says))})"); continue; }
                var gains = new List<AuthorityRecord>();
                if (items.Count == 1)
                {
                    var item = items[0];
                    if (!string.Equals(item.Type, "Place", StringComparison.OrdinalIgnoreCase)) { left.Add($"{name}: the item is no place ({Says(item)}, {item.Type ?? "of no kind the store knows"})"); continue; }
                    if (await HolderAsync(item.Uri, e, ct) is { } holder) { left.Add($"{name}: the item {Says(item)} is held by {holder.Label} <{holder.Id}>: one place twice?"); continue; }
                    gains.Add(item);
                    foreach (var uri in item.SameAs.Distinct(StringComparer.Ordinal))
                        if (!naming.ContainsKey(uri) && await store.GetAuthorityAsync(uri, ct) is { Source: "tgn" } g) naming[g.Uri] = g;
                }
                // the gazetteer's record: the one the item names, or that names the heading itself
                var places = naming.Values.Where(r => r.Source == "tgn" && string.Equals(r.Type, "Place", StringComparison.OrdinalIgnoreCase)).ToList();
                if (places.Count == 1 && await HolderAsync(places[0].Uri, e, ct) is null) gains.Add(places[0]);
                else if (places.Count > 1) left.Add($"{name}: {places.Count} gazetteer records ({string.Join(", ", places.Select(Says))}); {(gains.Count > 0 ? "given the item only" : "nothing given")}");
                else if (places.Count == 1) left.Add($"{name}: the gazetteer's {Says(places[0])} is held by another entity of the project; {(gains.Count > 0 ? "given the item only" : "nothing given")}");
                if (gains.Count == 0) continue;
                foreach (var r in gains)
                {
                    if (runId is not null) await store.AddEquivalentAsync(e.Id, new Equivalent(r.Uri, r.Source, DecisionMethod.ThroughWikidata, 1, DateTimeOffset.UtcNow, null, runId), ct);
                    added[r.Source] = added.GetValueOrDefault(r.Source) + 1;
                }
                given++;
                gave.Add($"{name}: {string.Join("; ", gains.Select(Says))}");
                if (given % 50 == 0) progress?.Report($"{read:N0} read, {given:N0} given a record");
            }
        }
        return new ThroughHeadingsReport(read, given, added, noHeading, noItem, gave, left);
    }

    private async Task<Entity?> HolderAsync(string uri, Entity self, CancellationToken ct) =>
        (await store.FindEntitiesByEquivalentAsync(uri, ct)).FirstOrDefault(o => o.Dataset == self.Dataset && o.MergedInto is null && o.Id != self.Id);

    private static string Says(AuthorityRecord r)
    {
        var what = ReviewService.ChainOf(r) ?? RecordSummary.Says(r);
        return $"{r.Source} {r.Label}{(string.IsNullOrWhiteSpace(what) ? "" : " (" + what + ")")} <{r.Uri}>";
    }
}
