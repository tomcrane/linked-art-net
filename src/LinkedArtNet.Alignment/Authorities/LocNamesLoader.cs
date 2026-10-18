using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinkedArtNet.Alignment.Store;
using Npgsql;

namespace LinkedArtNet.Alignment.Authorities;

public sealed record LocLoadResult(long TriplesRead, long TriplesKept, int Records, TimeSpan Elapsed);

/// <summary>
/// Loads the Library of Congress Name Authority File (the MADS/RDF N-Triples bulk download at id.loc.gov/download,
/// about 7 GB gzipped, read without unpacking) into the store's authority records as the <c>loc</c> index.
/// </summary>
/// <remarks>
/// Shapes read from the MADS/RDF export (checked against a 2024 record on 2026-10-18): the authority
/// <c>http://id.loc.gov/authorities/names/n…</c> is typed <c>madsrdf:PersonalName</c>, <c>CorporateName</c>,
/// <c>ConferenceName</c>, <c>FamilyName</c> or <c>Geographic</c> (titles and name-title headings are left out),
/// carries <c>madsrdf:authoritativeLabel</c>, <c>madsrdf:hasVariant</c> to blank nodes with
/// <c>madsrdf:variantLabel</c>, <c>madsrdf:hasExactExternalAuthority</c>/<c>hasCloseExternalAuthority</c> and
/// <c>skos:exactMatch</c>/<c>closeMatch</c> to VIAF, Wikidata and others, and <c>madsrdf:identifiesRWO</c> to the
/// real-world-object node whose <c>madsrdf:birthDate</c>/<c>deathDate</c> and <c>madsrdf:occupation</c> describe
/// the person. Personal headings embed dates ("Dürer, Albrecht, 1471-1528"); the loader also indexes the heading
/// without them and takes a lifespan from them when the RWO has none. VIAF links in the <c>sourceID</c> form are
/// not cluster ids and are dropped. The data are in the public domain in the United States; LoC asks for credit to
/// the Library of Congress Name Authority File.
/// </remarks>
public sealed partial class LocNamesLoader(PostgresAlignmentStore store)
{
    private const string Mads = "http://www.loc.gov/mads/rdf/v1#";
    private const string Skos = "http://www.w3.org/2004/02/skos/core#";
    private const string RdfType = "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";
    private const string NamesPrefix = "http://id.loc.gov/authorities/names/";

    private static readonly Dictionary<string, string> HeadingTypes = new(StringComparer.Ordinal)
    {
        [Mads + "PersonalName"] = nameof(EntityClass.Person),
        [Mads + "CorporateName"] = nameof(EntityClass.Group),
        [Mads + "ConferenceName"] = nameof(EntityClass.Event),
        [Mads + "FamilyName"] = nameof(EntityClass.Group),
        [Mads + "Geographic"] = nameof(EntityClass.Place),
    };

    private static readonly HashSet<string> Wanted = new(StringComparer.Ordinal)
    {
        RdfType, Mads + "authoritativeLabel", Mads + "hasVariant", Mads + "variantLabel",
        Mads + "hasExactExternalAuthority", Mads + "hasCloseExternalAuthority", Skos + "exactMatch", Skos + "closeMatch",
        Mads + "identifiesRWO", Mads + "birthDate", Mads + "deathDate", Mads + "occupation",
    };

    public Task<LocLoadResult> LoadAsync(string path, string? sourceVersion = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var files = TripleStaging.Files(path);
        if (files.Count == 0) throw new FileNotFoundException("No N-Triples file at " + path);
        return LoadAsync(TripleStaging.ReadAll(files, progress), sourceVersion ?? TripleStaging.VersionFor(files), progress, ct);
    }

    public async Task<LocLoadResult> LoadAsync(IEnumerable<Triple> triples, string? sourceVersion = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var table = TripleStaging.NewTable();
        try
        {
            // types are kept only for headings in the names file (variants are typed too, as blank nodes); labels of
            // headings and variants, the links, and the RWO's dates and occupations
            var (read, kept) = await TripleStaging.StageAsync(store, table, triples,
                t => Wanted.Contains(t.Predicate) && (t.Predicate != RdfType || (HeadingTypes.ContainsKey(t.Object) && t.Subject.StartsWith(NamesPrefix, StringComparison.Ordinal))),
                progress, ct);
            progress?.Report("building records");
            var records = await store.PutAuthoritiesAsync(ReadRecordsAsync(store.DataSource, table, sourceVersion, ct), ct);
            if (records > 0) await store.RetireAuthoritiesAsync("loc", started, ct);
            return new LocLoadResult(read, kept, records, DateTimeOffset.UtcNow - started);
        }
        finally { await TripleStaging.DropAsync(store, table, ct); }
    }

    private static string RecordsSql(string table) => $"""
        with headings as (
            select s as uri, min(o) as type from {table} where p = '{RdfType}' and s like '{NamesPrefix}%' group by s
        ),
        auth as (select s as uri, min(o) as label from {table} where p = '{Mads}authoritativeLabel' and s like '{NamesPrefix}%' group by s),
        variants as (
            select hv.s as uri, array_agg(distinct vl.o) as labels
            from {table} hv join {table} vl on vl.s = hv.o and vl.p = '{Mads}variantLabel'
            where hv.p = '{Mads}hasVariant' group by hv.s
        ),
        links as (
            select s as uri, array_agg(distinct o) as same_as from {table}
            where p in ('{Mads}hasExactExternalAuthority', '{Mads}hasCloseExternalAuthority', '{Skos}exactMatch', '{Skos}closeMatch') group by s
        ),
        rwo as (select s as uri, min(o) as rwo from {table} where p = '{Mads}identifiesRWO' group by s),
        rwoinfo as (
            select rwo.uri,
                   max(case when x.p = '{Mads}birthDate' then x.o end) as birth,
                   max(case when x.p = '{Mads}deathDate' then x.o end) as death,
                   array_remove(array_agg(distinct case when x.p = '{Mads}occupation' then x.o end), null) as occupations
            from rwo join {table} x on x.s = rwo.rwo group by rwo.uri
        )
        select h.uri, h.type, auth.label, variants.labels, links.same_as, rwoinfo.birth, rwoinfo.death, rwoinfo.occupations
        from headings h
        left join auth on auth.uri = h.uri
        left join variants on variants.uri = h.uri
        left join links on links.uri = h.uri
        left join rwoinfo on rwoinfo.uri = h.uri
        order by h.uri
        """;

    private static async IAsyncEnumerable<AuthorityRecord> ReadRecordsAsync(NpgsqlDataSource db, string table, string? sourceVersion, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var cmd = db.CreateCommand(RecordsSql(table));
        cmd.CommandTimeout = 0;
        await using var r = await cmd.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, ct);
        while (await r.ReadAsync(ct))
        {
            var uri = r.GetString(0);
            var type = HeadingTypes.GetValueOrDefault(r.GetString(1));
            if (type is null) continue;
            var label = r.IsDBNull(2) ? null : r.GetString(2);
            var variants = r.IsDBNull(3) ? [] : r.GetFieldValue<string[]>(3);
            var links = r.IsDBNull(4) ? [] : r.GetFieldValue<string[]>(4);
            var birth = r.IsDBNull(5) ? null : r.GetString(5);
            var death = r.IsDBNull(6) ? null : r.GetString(6);
            var occupations = r.IsDBNull(7) ? [] : r.GetFieldValue<string[]>(7);
            if (label is null) continue;

            var heading = SplitHeading(label);
            var labels = new List<string>(variants.Length + 2) { label };
            if (heading.Name != label) labels.Add(heading.Name);
            foreach (var v in variants) { labels.Add(v); var sv = SplitHeading(v).Name; if (sv != v) labels.Add(sv); }
            var sameAs = links.Select(GettyVocabLoader.CanonicalSameAs).Where(UsableLink).Distinct(StringComparer.Ordinal).ToArray();
            var lifespan = LifespanFrom(birth, death) ?? heading.Lifespan;
            var data = new Dictionary<string, object?>();
            if (occupations.Length > 0) data["occupations"] = occupations;
            if (heading.Dates is not null) data["headingDates"] = heading.Dates;
            yield return new AuthorityRecord(uri, "loc", label, labels.Distinct(StringComparer.Ordinal).ToList(), type, lifespan, sameAs, data.Count == 0 ? null : JsonSerializer.Serialize(data), now, sourceVersion);
        }
    }

    /// <summary>A link worth keeping: a VIAF cluster, a Wikidata entity, a Getty or ISNI id; not VIAF's <c>sourceID</c> form, which is LoC's own id reflected back.</summary>
    internal static bool UsableLink(string uri) =>
        !uri.Contains("/viaf/sourceID/", StringComparison.Ordinal) && !uri.Contains("#skos:Concept", StringComparison.Ordinal)
        && (uri.StartsWith(AuthorityUris.Viaf, StringComparison.Ordinal) || uri.StartsWith(AuthorityUris.Wikidata, StringComparison.Ordinal)
            || uri.StartsWith("http://vocab.getty.edu/", StringComparison.Ordinal) || uri.StartsWith("http://isni.org/", StringComparison.Ordinal) || uri.StartsWith("https://isni.org/", StringComparison.Ordinal));

    [GeneratedRegex(@"^(?<name>.+?),\s*(?<dates>(approximately |ca\. |b\. |d\. |fl\. |active |flourished )?-?\d{3,4}(\??)[-–]?(\s*(approximately |ca\. )?\d{3,4}\??)?\.?|(?<dates2>-\d{3,4}))$")]
    private static partial Regex TrailingDates();

    [GeneratedRegex(@"\d{1,3}[Xx]{1,3}|\d{1,3}[Uu]{1,3}|-?\d{3,4}")] // the unspecified-digit forms first, or "185X" would match as "185"
    private static partial Regex Year();

    /// <summary>A year from a heading or an EDTF date: "1471" is exact; "185X" is 1850 to 1859 and "18XX" 1800 to 1899 (EDTF unspecified digits); a negative year is BCE and stays text only.</summary>
    internal static (int? From, int? To) ParseYear(string token)
    {
        token = token.Trim();
        if (token.StartsWith('-')) return (null, null);
        var unspecified = token.Count(c => c is 'X' or 'x' or 'U' or 'u');
        if (unspecified == 0) return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var y) && y > 0 ? (y, y) : (null, null);
        var digits = token[..^unspecified];
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var lead)) return (null, null);
        var scale = (int)Math.Pow(10, unspecified);
        return (lead * scale, lead * scale + scale - 1);
    }

    private static Lifespan? LifespanFromRanges((int? From, int? To) start, (int? From, int? To) end, string text)
    {
        static DateTimeOffset? Day(int? y, bool last) => y is null || y < 1 || y > 9999 ? null : last ? new DateTimeOffset(y.Value, 12, 31, 23, 59, 59, TimeSpan.Zero) : new DateTimeOffset(y.Value, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var l = new Lifespan(Day(start.From, false), Day(start.To, true), Day(end.From, false), Day(end.To, true), text);
        return l.IsEmpty ? new Lifespan(null, null, null, null, text) : l;
    }

    /// <summary>Splits a personal heading into the name and its date segment: "Dürer, Albrecht, 1471-1528" gives the name "Dürer, Albrecht" and a lifespan 1471 to 1528; "Smith, John, 1900-" an open one; "approximately 1500" a start only.</summary>
    internal static (string Name, string? Dates, Lifespan? Lifespan) SplitHeading(string label)
    {
        var m = TrailingDates().Match(label);
        if (!m.Success) return (label, null, null);
        var name = m.Groups["name"].Value.TrimEnd(',', ' ');
        var dates = m.Groups["dates"].Success ? m.Groups["dates"].Value : m.Groups["dates2"].Value;
        var years = Year().Matches(dates).Select(y => y.Value.TrimStart('-')).Where(y => y.Length >= 3).ToList();
        if (years.Count == 0 || name.Length == 0) return (label, null, null);
        var startsWithDash = dates.TrimStart().StartsWith('-') || dates.StartsWith("d. ", StringComparison.Ordinal);
        (int?, int?) start = (null, null), end = (null, null);
        if (dates.StartsWith("d. ", StringComparison.Ordinal) || (startsWithDash && years.Count == 1)) end = ParseYear(years[0]);
        else { start = ParseYear(years[0]); if (years.Count > 1) end = ParseYear(years[1]); }
        if (dates.StartsWith("fl. ", StringComparison.Ordinal) || dates.StartsWith("active ", StringComparison.Ordinal) || dates.StartsWith("flourished ", StringComparison.Ordinal))
            return (name, dates, null); // active dates are not a lifespan
        return (name, dates, LifespanFromRanges(start, end, dates));
    }

    /// <summary>A lifespan from the RWO's birth and death dates, which are years or ISO dates, sometimes with "approximately".</summary>
    internal static Lifespan? LifespanFrom(string? birth, string? death)
    {
        static (int?, int?) FirstYear(string? s) => s is null ? (null, null) : Year().Match(s) is { Success: true } m ? ParseYear(m.Value) : (null, null);
        if (birth is null && death is null) return null;
        var text = birth is not null && death is not null ? $"{birth}-{death}" : (birth ?? death)!;
        return LifespanFromRanges(FirstYear(birth), FirstYear(death), text);
    }
}
