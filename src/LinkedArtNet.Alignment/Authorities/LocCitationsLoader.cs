using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Authorities;

public sealed record LocCitationsResult(long TriplesRead, int Wanted, int Records, int Citations, long Unplaced, TimeSpan Elapsed);

/// <summary>
/// The source citations of Library of Congress name records ("found in": the works a heading was made from, MARC 670), read from the
/// bulk MADS/RDF file the names were loaded from, for the records a project's strings have as candidates. A heading made from a book
/// the string itself stands in is the best evidence there is for a personal author, and a card could show it only after asking
/// id.loc.gov for each record; the owner asked whether it was not in the bulk data (2026-11-27), and it is: each record
/// <c>madsrdf:hasSource</c> a blank node carrying <c>citationSource</c> ("Smith, Lindsay. Pre-Raphaelitism, 2013:") and
/// <c>citationNote</c> ("title page (Lindsay Smith) back cover (she is Professor of English ...)"). Kept on the record as
/// <c>sources</c>, which <see cref="SourcePull.Read"/> reads where the record has not been pulled. One pass over the file; the
/// citations of a record stand near it, so those met before their record are held in a bounded window. A reload of the names
/// replaces a record's data, and this is run again after it.
/// </summary>
public sealed class LocCitationsLoader(PostgresAlignmentStore store)
{
    private const string Mads = "http://www.loc.gov/mads/rdf/v1#";
    /// <summary>How many citations not yet claimed by a record are held; the oldest are let go beyond it.</summary>
    public const int Window = 400_000;
    /// <summary>The most citations kept for one record: the famous have hundreds.</summary>
    public const int MostPerRecord = 40;

    public Task<LocCitationsResult> LoadAsync(string path, IReadOnlySet<string> wanted, IProgress<string>? progress = null, CancellationToken ct = default) =>
        LoadAsync(NTriples.Read(path), wanted, progress, ct);

    public async Task<LocCitationsResult> LoadAsync(IEnumerable<Triple> triples, IReadOnlySet<string> wanted, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var owner = new Dictionary<string, string>(StringComparer.Ordinal);                                   // blank node -> the wanted record it is a source of
        var found = new Dictionary<string, Dictionary<string, (string? Source, string? Note)>>(StringComparer.Ordinal);
        var waiting = new Dictionary<string, (string? Source, string? Note)>(StringComparer.Ordinal);         // citations met before their record
        var order = new Queue<string>();
        long read = 0, unplaced = 0;

        void Put(string uri, string node, string? source, string? note)
        {
            if (!found.TryGetValue(uri, out var list)) found[uri] = list = new(StringComparer.Ordinal);
            list.TryGetValue(node, out var had);
            list[node] = (source ?? had.Source, note ?? had.Note);
        }

        foreach (var t in triples)
        {
            if (++read % 20_000_000 == 0) { ct.ThrowIfCancellationRequested(); progress?.Report($"{read:N0} triples read, {found.Count:N0} records with citations"); }
            if (!t.Predicate.StartsWith(Mads, StringComparison.Ordinal)) continue;
            var p = t.Predicate.AsSpan(Mads.Length);
            if (p.SequenceEqual("hasSource"))
            {
                if (!wanted.Contains(t.Subject)) continue;
                owner[t.Object] = t.Subject;
                if (waiting.Remove(t.Object, out var held)) Put(t.Subject, t.Object, held.Source, held.Note);
            }
            else if (p.SequenceEqual("citationSource") || p.SequenceEqual("citationNote"))
            {
                var isSource = p.SequenceEqual("citationSource");
                if (owner.TryGetValue(t.Subject, out var uri)) { Put(uri, t.Subject, isSource ? t.Object : null, isSource ? null : t.Object); continue; }
                if (!waiting.TryGetValue(t.Subject, out var had)) { order.Enqueue(t.Subject); if (order.Count > Window && waiting.Remove(order.Dequeue())) unplaced++; }
                waiting[t.Subject] = isSource ? (t.Object, had.Note) : (had.Source, t.Object);
            }
        }

        progress?.Report($"{read:N0} triples read; writing the citations of {found.Count:N0} records");
        var citations = 0;
        var rows = new List<(string Uri, IReadOnlyList<string> Sources)>(found.Count);
        foreach (var (uri, list) in found)
        {
            var texts = list.Values.Select(c => $"{c.Source} {c.Note}".Trim()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).Take(MostPerRecord).ToList();
            if (texts.Count == 0) continue;
            citations += texts.Count;
            rows.Add((uri, texts));
        }
        var written = await store.PutAuthoritySourcesAsync(rows, ct);
        return new LocCitationsResult(read, wanted.Count, written, citations, unplaced, DateTimeOffset.UtcNow - started);
    }
}
