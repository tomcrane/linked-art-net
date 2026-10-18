using System.Globalization;
using System.Text.Json;
using LinkedArtNet.Alignment.Store;
using Npgsql;

namespace LinkedArtNet.Alignment.Authorities;

public enum GettyVocabulary { Ulan, Aat, Tgn }

/// <summary>What one load did.</summary>
public sealed record GettyLoadResult(GettyVocabulary Vocabulary, long TriplesRead, long TriplesKept, int Records, TimeSpan Elapsed);

/// <summary>
/// Loads a Getty vocabulary N-Triples dump (ULAN, AAT or TGN; the monthly full downloads, ODC-By) into the store's
/// authority records as a local index. The triples the index needs are staged in the database and joined there
/// (<see cref="TripleStaging"/>), because a full TGN is tens of millions of statements and an in-memory build would
/// not fit a laptop; labels are normalised in code so lookups use the same key as everything else. Re-loading
/// replaces records by URI.
/// </summary>
/// <remarks>
/// Read from the GVP ontology as published at vocab.getty.edu (checked 2026-10-18): a concept is typed
/// <c>gvp:PersonConcept</c>, <c>gvp:GroupConcept</c>, <c>gvp:UnknownPersonConcept</c>, <c>gvp:Concept</c>,
/// <c>gvp:AdminPlaceConcept</c> or <c>gvp:PhysPlaceConcept</c>; its labels are SKOS-XL terms reached by
/// <c>xl:prefLabel</c>/<c>xl:altLabel</c> with <c>xl:literalForm</c>, the display form by <c>gvp:prefLabelGVP</c>;
/// <c>skos:exactMatch</c>/<c>closeMatch</c> carry LoC, VIAF and Wikidata links; <c>foaf:focus</c> points at the agent
/// or place, whose <c>gvp:biographyPreferred</c> has <c>gvp:estStart</c>/<c>gvp:estEnd</c> (gYear) and
/// <c>schema:description</c>, and whose nationality is <c>gvp:nationalityPreferred</c>; places carry
/// <c>wgs:lat</c>/<c>wgs:long</c>. The explicit.zip downloads unpack to one file per table (subjects, terms,
/// agent map, biographies, alignments), which load together. Credit: "Contains information from the J. Paul Getty
/// Trust, Getty Research Institute, [Union List of Artist Names | Art &amp; Architecture Thesaurus | Thesaurus of
/// Geographic Names], which is made available under the ODC Attribution License".
/// </remarks>
public sealed class GettyVocabLoader(PostgresAlignmentStore store)
{
    private const string Gvp = "http://vocab.getty.edu/ontology#";
    private const string Xl = "http://www.w3.org/2008/05/skos-xl#";
    private const string Skos = "http://www.w3.org/2004/02/skos/core#";
    private const string RdfType = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";
    private const string FoafFocus = "http://xmlns.com/foaf/0.1/focus";
    private const string Schema = "http://schema.org/";
    private const string Wgs = "http://www.w3.org/2003/01/geo/wgs84_pos#";

    private static readonly HashSet<string> Wanted = new(StringComparer.Ordinal)
    {
        RdfType, Xl + "prefLabel", Xl + "altLabel", Xl + "literalForm", Gvp + "prefLabelGVP",
        Skos + "exactMatch", Skos + "closeMatch", Skos + "prefLabel", Skos + "altLabel",
        FoafFocus, Gvp + "biographyPreferred", Gvp + "estStart", Gvp + "estEnd", Schema + "description", Schema + "gender",
        Gvp + "nationalityPreferred", Gvp + "agentTypePreferred", Gvp + "placeTypePreferred", Wgs + "lat", Wgs + "long",
        Gvp + "broaderPreferred", Gvp + "parentString",
    };

    private static readonly Dictionary<string, string> ConceptTypes = new(StringComparer.Ordinal)
    {
        [Gvp + "PersonConcept"] = nameof(EntityClass.Person),
        [Gvp + "UnknownPersonConcept"] = nameof(EntityClass.Person),
        [Gvp + "GroupConcept"] = nameof(EntityClass.Group),
        [Gvp + "Concept"] = nameof(EntityClass.Type),
        [Gvp + "AdminPlaceConcept"] = nameof(EntityClass.Place),
        [Gvp + "PhysPlaceConcept"] = nameof(EntityClass.Place),
    };

    public static string SourceName(GettyVocabulary v) => v switch { GettyVocabulary.Ulan => "ulan", GettyVocabulary.Aat => "aat", _ => "tgn" };

    /// <summary>Loads one file, or every <c>.nt</c>/<c>.nt.gz</c> in a directory: a Getty zip unpacks to several files whose statements about one concept must be staged together.</summary>
    public Task<GettyLoadResult> LoadAsync(string path, GettyVocabulary vocabulary, string? sourceVersion = null, IProgress<string>? progress = null, CancellationToken ct = default)
        => LoadAsync(Files(path), vocabulary, sourceVersion, progress, ct);

    public Task<GettyLoadResult> LoadAsync(IReadOnlyList<string> paths, GettyVocabulary vocabulary, string? sourceVersion = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (paths.Count == 0) throw new ArgumentException("No N-Triples files to load.", nameof(paths));
        return LoadAsync(TripleStaging.ReadAll(paths, progress), vocabulary, sourceVersion ?? TripleStaging.VersionFor(paths), progress, ct);
    }

    public static IReadOnlyList<string> Files(string path) => TripleStaging.Files(path);

    public async Task<GettyLoadResult> LoadAsync(IEnumerable<Triple> triples, GettyVocabulary vocabulary, string? sourceVersion = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var (read, kept) = await TripleStaging.StageAsync(store, triples, t => Wanted.Contains(t.Predicate) && (t.Predicate != RdfType || ConceptTypes.ContainsKey(t.Object)), progress, ct);
        progress?.Report("building records");
        var records = await store.PutAuthoritiesAsync(ReadRecordsAsync(store.DataSource, vocabulary, sourceVersion, ct), ct);
        await TripleStaging.ClearAsync(store, ct);
        return new GettyLoadResult(vocabulary, read, kept, records, DateTimeOffset.UtcNow - started);
    }

    // One row per concept, joined in SQL; normalisation and the record shape are done in code.
    private static readonly string RecordsSql = $"""
        with concepts as (
            select s as uri, min(o) as type from {TripleStaging.Table} where p = '{RdfType}' group by s
        ),
        pref as (
            select st.s as uri, min(t.o) as label
            from {TripleStaging.Table} st join {TripleStaging.Table} t on t.s = st.o and t.p = '{Xl}literalForm'
            where st.p = '{Gvp}prefLabelGVP' group by st.s
        ),
        xlabels as (
            select st.s as uri, t.o as label
            from {TripleStaging.Table} st join {TripleStaging.Table} t on t.s = st.o and t.p = '{Xl}literalForm'
            where st.p in ('{Xl}prefLabel', '{Xl}altLabel')
            union
            select s, o from {TripleStaging.Table} where p in ('{Skos}prefLabel', '{Skos}altLabel')
        ),
        labels as (select uri, array_agg(distinct label) as labels from xlabels group by uri),
        matches as (
            select s as uri, array_agg(distinct o) as same_as from {TripleStaging.Table} where p in ('{Skos}exactMatch', '{Skos}closeMatch') group by s
        ),
        focus as (select s as uri, o as agent from {TripleStaging.Table} where p = '{FoafFocus}'),
        bio as (
            select f.uri, min(b.o) as bio from focus f join {TripleStaging.Table} b on b.s = f.agent and b.p = '{Gvp}biographyPreferred' group by f.uri
        ),
        bioinfo as (
            select bio.uri,
                   max(case when x.p = '{Gvp}estStart' then x.o end) as est_start,
                   max(case when x.p = '{Gvp}estEnd' then x.o end) as est_end,
                   max(case when x.p = '{Schema}description' then x.o end) as description,
                   max(case when x.p = '{Schema}gender' then x.o end) as gender
            from bio join {TripleStaging.Table} x on x.s = bio.bio group by bio.uri
        ),
        nat as (
            select f.uri, array_agg(distinct n.o) as nationalities from focus f join {TripleStaging.Table} n on n.s = f.agent and n.p = '{Gvp}nationalityPreferred' group by f.uri
        ),
        geo as (
            select f.uri,
                   max(case when g.p = '{Wgs}lat' then g.o end) as lat,
                   max(case when g.p = '{Wgs}long' then g.o end) as long
            from focus f join {TripleStaging.Table} g on g.s = f.agent and g.p in ('{Wgs}lat', '{Wgs}long') group by f.uri
        ),
        extra as (
            select s as uri,
                   max(case when p = '{Gvp}agentTypePreferred' then o end) as agent_type,
                   max(case when p = '{Gvp}placeTypePreferred' then o end) as place_type,
                   max(case when p = '{Gvp}broaderPreferred' then o end) as broader,
                   max(case when p = '{Gvp}parentString' then o end) as parent_string
            from {TripleStaging.Table} where p in ('{Gvp}agentTypePreferred', '{Gvp}placeTypePreferred', '{Gvp}broaderPreferred', '{Gvp}parentString') group by s
        )
        select c.uri, c.type, pref.label, labels.labels, matches.same_as,
               bioinfo.est_start, bioinfo.est_end, bioinfo.description, bioinfo.gender, nat.nationalities,
               geo.lat, geo.long, extra.agent_type, extra.place_type, extra.broader, extra.parent_string
        from concepts c
        left join pref on pref.uri = c.uri
        left join labels on labels.uri = c.uri
        left join matches on matches.uri = c.uri
        left join bioinfo on bioinfo.uri = c.uri
        left join nat on nat.uri = c.uri
        left join geo on geo.uri = c.uri
        left join extra on extra.uri = c.uri
        order by c.uri
        """;

    private static async IAsyncEnumerable<AuthorityRecord> ReadRecordsAsync(NpgsqlDataSource db, GettyVocabulary vocabulary, string? sourceVersion, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var source = SourceName(vocabulary);
        var now = DateTimeOffset.UtcNow;
        await using var cmd = db.CreateCommand(RecordsSql);
        cmd.CommandTimeout = 0;
        await using var r = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, ct);
        while (await r.ReadAsync(ct))
        {
            var uri = r.GetString(0);
            var type = ConceptTypes.GetValueOrDefault(r.GetString(1));
            if (type is null) continue;
            var label = r.IsDBNull(2) ? null : r.GetString(2);
            var labels = r.IsDBNull(3) ? [] : r.GetFieldValue<string[]>(3);
            var sameAs = r.IsDBNull(4) ? [] : r.GetFieldValue<string[]>(4).Select(CanonicalSameAs).Distinct(StringComparer.Ordinal).ToArray();
            var estStart = Str(r, 5); var estEnd = Str(r, 6); var description = Str(r, 7); var gender = Str(r, 8);
            var nationalities = r.IsDBNull(9) ? [] : r.GetFieldValue<string[]>(9);
            var lat = Str(r, 10); var lon = Str(r, 11);
            var agentType = Str(r, 12); var placeType = Str(r, 13); var broader = Str(r, 14); var parentString = Str(r, 15);

            label ??= labels.FirstOrDefault();
            var lifespan = LifespanFromYears(estStart, estEnd);
            var data = new Dictionary<string, object?>();
            if (nationalities.Length > 0) data["nationalities"] = nationalities;
            if (agentType is not null) data["agentType"] = agentType;
            if (placeType is not null) data["placeType"] = placeType;
            if (description is not null) data["description"] = description;
            if (gender is not null) data["gender"] = gender;
            if (broader is not null) data["broader"] = broader;
            if (parentString is not null) data["parentString"] = parentString;
            if (lat is not null && lon is not null && double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var la) && double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)) { data["lat"] = la; data["long"] = lo; }
            yield return new AuthorityRecord(uri, source, label, labels, type, lifespan, sameAs, data.Count == 0 ? null : JsonSerializer.Serialize(data), now, sourceVersion);
        }
    }

    private static string? Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    /// <summary>Getty writes Wikidata links as page URLs and VIAF and LoC links over https; the store keys each by one canonical form.</summary>
    internal static string CanonicalSameAs(string uri)
    {
        const string wikiPage = "wikidata.org/wiki/";
        var i = uri.IndexOf(wikiPage, StringComparison.Ordinal);
        if (i >= 0) return AuthorityUris.Wikidata + uri[(i + wikiPage.Length)..];
        if (uri.StartsWith("https://id.loc.gov/", StringComparison.Ordinal) || uri.StartsWith("https://viaf.org/", StringComparison.Ordinal) || uri.StartsWith("https://www.wikidata.org/", StringComparison.Ordinal)) return "http://" + uri[8..];
        return uri.EndsWith('/') ? uri.TrimEnd('/') : uri;
    }

    /// <summary>A lifespan from gYear bounds as the Getty biographies give them; years outside 1-9999 keep the text only.</summary>
    internal static Lifespan? LifespanFromYears(string? start, string? end)
    {
        if (start is null && end is null) return null;
        static DateTimeOffset? Day(string? y, bool last)
        {
            if (y is null || !int.TryParse(y.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var year) || year < 1 || year > 9999) return null;
            return last ? new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero) : new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        }
        var text = start is not null && end is not null ? $"{start}-{end}" : start ?? end;
        return new Lifespan(Day(start, false), Day(start, true), Day(end, false), Day(end, true), text);
    }
}
