using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Authorities;

public sealed record WikidataLoadResult(long TriplesRead, long ItemsSeen, int Records, long SitelinkRows, TimeSpan Elapsed);

/// <summary>
/// A local extract of Wikidata as the <c>wikidata</c> index, from the "truthy" N-Triples dump
/// (<c>dumps.wikimedia.org/wikidatawiki/entities/latest-truthy.nt.gz</c>, about 72 GB gzipped, CC0) read in one pass: the
/// dump lists each item's statements together, so items are read one at a time and nothing is staged. An item is kept when it
/// carries one of the identifiers the other indexes use: VIAF (P214), Library of Congress (P244), ULAN (P245), TGN (P1667),
/// AAT (P1014) or GND (P227). Its English labels and aliases (and the language-neutral <c>mul</c> ones) are the labels, the
/// identifiers become <c>sameAs</c> links in the other indexes' URI forms, birth and death (P569, P570), inception and
/// dissolution (P571, P576) or the work period (P2031, P2032, as working dates) the lifespan, and the class comes from
/// <c>instance of</c> (P31) through a short table of Wikidata classes. The record's data keeps the description, the
/// instance-of ids and the identifiers. Prominence comes from a second file, the sitelinks table
/// (<c>wikidatawiki-latest-wb_items_per_site.sql.gz</c>, a MySQL dump): the number of Wikipedia and sister-project pages an
/// item has is written into each kept record's data as <c>sitelinks</c>.
/// </summary>
/// <remarks>
/// Shapes checked against the dump on 2026-10-23: subjects <c>http://www.wikidata.org/entity/Q31</c>, predicates
/// <c>http://www.wikidata.org/prop/direct/P214</c>, labels <c>rdfs:label</c> with language tags, aliases <c>skos:altLabel</c>,
/// descriptions <c>schema:description</c>, dates as <c>xsd:dateTime</c> literals ("1952-03-11T00:00:00Z"; a year-precision date is
/// written as 1 January, so only the year is read). The truthy dump does not carry sitelinks, hence the second file. The class
/// table is the assistant's own list of common Wikidata classes, not a sourced mapping. Credit: Wikidata (CC0).
/// </remarks>
public sealed partial class WikidataLoader(PostgresAlignmentStore store)
{
    public const string Source = "wikidata";
    private const string Entity = "http://www.wikidata.org/entity/";
    private const string Direct = "http://www.wikidata.org/prop/direct/";
    private const string Label = "http://www.w3.org/2000/01/rdf-schema#label";
    private const string AltLabel = "http://www.w3.org/2004/02/skos/core#altLabel";
    private const string DescriptionPredicate = "http://schema.org/description";

    /// <summary>The identifier properties that make an item worth keeping, with the key the record's data uses and the URI form the other indexes use.</summary>
    private static readonly Dictionary<string, (string Key, Func<string, string> Uri)> Identifiers = new(StringComparer.Ordinal)
    {
        ["P214"] = ("viaf", id => AuthorityUris.Viaf + id),
        ["P244"] = ("loc", AuthorityUris.Loc),
        ["P245"] = ("ulan", id => AuthorityUris.Ulan + id),
        ["P1667"] = ("tgn", id => AuthorityUris.Tgn + id),
        ["P1014"] = ("aat", id => AuthorityUris.Aat + id),
        ["P227"] = ("gnd", id => AuthorityUris.Gnd + id),
    };

    /// <summary>Common Wikidata classes and the entity class each means here. The assistant's own list (2026-10-23), not a sourced mapping; an item of any other class is kept untyped.</summary>
    private static readonly Dictionary<string, string> Classes = new(StringComparer.Ordinal)
    {
        ["Q5"] = nameof(EntityClass.Person),
        // bodies
        ["Q43229"] = nameof(EntityClass.Group), ["Q4830453"] = nameof(EntityClass.Group), ["Q783794"] = nameof(EntityClass.Group), ["Q3918"] = nameof(EntityClass.Group),
        ["Q33506"] = nameof(EntityClass.Group), ["Q207694"] = nameof(EntityClass.Group), ["Q2085381"] = nameof(EntityClass.Group), ["Q1320047"] = nameof(EntityClass.Group),
        ["Q7075"] = nameof(EntityClass.Group), ["Q166118"] = nameof(EntityClass.Group), ["Q31855"] = nameof(EntityClass.Group), ["Q484652"] = nameof(EntityClass.Group),
        ["Q163740"] = nameof(EntityClass.Group), ["Q1664720"] = nameof(EntityClass.Group), ["Q2385804"] = nameof(EntityClass.Group), ["Q215380"] = nameof(EntityClass.Group),
        ["Q327333"] = nameof(EntityClass.Group), ["Q2659904"] = nameof(EntityClass.Group), ["Q5341295"] = nameof(EntityClass.Group), ["Q16917"] = nameof(EntityClass.Group),
        ["Q11032"] = nameof(EntityClass.Group), ["Q41298"] = nameof(EntityClass.Group), ["Q1030034"] = nameof(EntityClass.Group), ["Q9826"] = nameof(EntityClass.Group),
        ["Q875538"] = nameof(EntityClass.Group), ["Q3914"] = nameof(EntityClass.Group), ["Q1371037"] = nameof(EntityClass.Group), ["Q2061186"] = nameof(EntityClass.Group),
        ["Q157031"] = nameof(EntityClass.Group), ["Q1416636"] = nameof(EntityClass.Group), ["Q4438121"] = nameof(EntityClass.Group), ["Q24354"] = nameof(EntityClass.Group),
        ["Q8436"] = nameof(EntityClass.Group), ["Q10648343"] = nameof(EntityClass.Group), ["Q17149090"] = nameof(EntityClass.Group), ["Q4504495"] = nameof(EntityClass.Group),
        // places
        ["Q515"] = nameof(EntityClass.Place), ["Q486972"] = nameof(EntityClass.Place), ["Q3957"] = nameof(EntityClass.Place), ["Q532"] = nameof(EntityClass.Place),
        ["Q6256"] = nameof(EntityClass.Place), ["Q3624078"] = nameof(EntityClass.Place), ["Q5107"] = nameof(EntityClass.Place), ["Q35657"] = nameof(EntityClass.Place),
        ["Q1549591"] = nameof(EntityClass.Place), ["Q7930989"] = nameof(EntityClass.Place), ["Q123705"] = nameof(EntityClass.Place), ["Q15284"] = nameof(EntityClass.Place),
        ["Q28575"] = nameof(EntityClass.Place), ["Q180673"] = nameof(EntityClass.Place), ["Q1136601"] = nameof(EntityClass.Place), ["Q211690"] = nameof(EntityClass.Place),
        ["Q4022"] = nameof(EntityClass.Place), ["Q8502"] = nameof(EntityClass.Place), ["Q23442"] = nameof(EntityClass.Place), ["Q39594"] = nameof(EntityClass.Place),
        ["Q5119"] = nameof(EntityClass.Place), ["Q1093829"] = nameof(EntityClass.Place), ["Q498162"] = nameof(EntityClass.Place), ["Q82794"] = nameof(EntityClass.Place),
        ["Q56061"] = nameof(EntityClass.Place), ["Q15642541"] = nameof(EntityClass.Place), ["Q1637706"] = nameof(EntityClass.Place), ["Q3024240"] = nameof(EntityClass.Place),
        ["Q2039348"] = nameof(EntityClass.Place), ["Q179049"] = nameof(EntityClass.Place), ["Q2514025"] = nameof(EntityClass.Place), ["Q1763214"] = nameof(EntityClass.Place),
        ["Q41176"] = nameof(EntityClass.Place), ["Q16970"] = nameof(EntityClass.Place), ["Q23413"] = nameof(EntityClass.Place), ["Q16560"] = nameof(EntityClass.Place),
        ["Q1802963"] = nameof(EntityClass.Place), ["Q35112127"] = nameof(EntityClass.Place), ["Q3947"] = nameof(EntityClass.Place), ["Q1497364"] = nameof(EntityClass.Place),
        ["Q22698"] = nameof(EntityClass.Place), ["Q570116"] = nameof(EntityClass.Place), ["Q24398318"] = nameof(EntityClass.Place), ["Q2977"] = nameof(EntityClass.Place),
        ["Q44613"] = nameof(EntityClass.Place), ["Q160742"] = nameof(EntityClass.Place), ["Q1021645"] = nameof(EntityClass.Place), ["Q207320"] = nameof(EntityClass.Place),
        // events
        ["Q1190554"] = nameof(EntityClass.Event), ["Q198"] = nameof(EntityClass.Event), ["Q178561"] = nameof(EntityClass.Event), ["Q1656682"] = nameof(EntityClass.Event),
        ["Q464980"] = nameof(EntityClass.Event), ["Q667276"] = nameof(EntityClass.Event), ["Q2761147"] = nameof(EntityClass.Event), ["Q1072427"] = nameof(EntityClass.Event),
        // concepts
        ["Q968159"] = nameof(EntityClass.Type), ["Q1792644"] = nameof(EntityClass.Type), ["Q11862829"] = nameof(EntityClass.Type), ["Q2198855"] = nameof(EntityClass.Type),
        ["Q1047113"] = nameof(EntityClass.Type), ["Q735"] = nameof(EntityClass.Type), ["Q15621286"] = nameof(EntityClass.Type), ["Q28640"] = nameof(EntityClass.Type),
    };

    private static readonly string[] LabelLanguages = ["en", "mul", "en-gb", "en-ca", "en-us", "en-au", "en-ie", "en-nz", "simple"];

    public Task<WikidataLoadResult> LoadAsync(string path, string? sitelinksPath = null, string? sourceVersion = null, IProgress<string>? progress = null, CancellationToken ct = default) =>
        LoadAsync(NTriples.Read(path), sitelinksPath is null ? null : () => new StreamReader(NTriples.Open(sitelinksPath)), sourceVersion ?? File.GetLastWriteTimeUtc(path).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), progress, ct);

    public async Task<WikidataLoadResult> LoadAsync(IEnumerable<Triple> triples, Func<TextReader>? sitelinks = null, string? sourceVersion = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var started = DateTimeOffset.UtcNow;
        var kept = new HashSet<long>();
        long read = 0, seen = 0;
        var records = await store.PutAuthoritiesAsync(ReadRecordsAsync(ct), ct);
        if (records > 0) await store.RetireAuthoritiesAsync(Source, started, ct);
        progress?.Report($"{read:N0} triples read, {seen:N0} items seen, {records:N0} records written");
        long rows = 0;
        if (sitelinks is not null && kept.Count > 0)
        {
            progress?.Report("counting sitelinks");
            using var reader = sitelinks();
            var counts = CountSitelinks(reader, kept, out rows);
            var patched = await store.MergeAuthorityDataAsync(Patches(counts), ct);
            progress?.Report($"{rows:N0} sitelink rows read, {patched:N0} records given a count");
        }
        return new WikidataLoadResult(read, seen, records, rows, DateTimeOffset.UtcNow - started);

        async IAsyncEnumerable<AuthorityRecord> ReadRecordsAsync([EnumeratorCancellation] CancellationToken c)
        {
            var now = DateTimeOffset.UtcNow;
            Item? current = null;
            foreach (var t in triples)
            {
                read++;
                if (read % 20_000_000 == 0) progress?.Report($"{read:N0} triples, {seen:N0} items, {kept.Count:N0} kept, {DateTimeOffset.UtcNow - started:hh\\:mm\\:ss}");
                c.ThrowIfCancellationRequested();
                if (!t.Subject.StartsWith(Entity, StringComparison.Ordinal) || t.Subject.Length <= Entity.Length || t.Subject[Entity.Length] != 'Q') continue;
                var id = t.Subject[Entity.Length..];
                if (current is null || !string.Equals(current.Id, id, StringComparison.Ordinal))
                {
                    if (current is not null && ToRecord(current, now, sourceVersion) is { } done) { kept.Add(current.Number); yield return done; }
                    current = new Item(id); seen++;
                }
                current.Take(t);
                if (read % 1_000_000 == 0) await Task.Yield();
            }
            if (current is not null && ToRecord(current, now, sourceVersion) is { } last) { kept.Add(current.Number); yield return last; }
        }

        static async IAsyncEnumerable<(string Uri, string DataJson)> Patches(Dictionary<long, int> counts)
        {
            await Task.CompletedTask;
            foreach (var (number, count) in counts) yield return (Entity + "Q" + number.ToString(CultureInfo.InvariantCulture), $"{{\"sitelinks\":{count}}}");
        }
    }

    /// <summary>One item's statements as the dump lists them, kept only in the parts the record needs.</summary>
    private sealed class Item(string id)
    {
        public string Id => id;
        public long Number => long.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : -1;
        public readonly Dictionary<string, string> Ids = new(StringComparer.Ordinal);
        public readonly List<(string Lang, string Text)> Labels = [];
        public readonly List<(string Lang, string Text)> AltLabels = [];
        public readonly List<string> InstanceOf = [];
        public string? Description, Birth, Death, Inception, Dissolution, WorkFrom, WorkTo;

        public void Take(Triple t)
        {
            if (t.Predicate.StartsWith(Direct, StringComparison.Ordinal))
            {
                var p = t.Predicate[Direct.Length..];
                switch (p)
                {
                    case "P31": if (t.Object.StartsWith(Entity, StringComparison.Ordinal)) InstanceOf.Add(t.Object[Entity.Length..]); break;
                    case "P569": Birth ??= t.Object; break;
                    case "P570": Death ??= t.Object; break;
                    case "P571": Inception ??= t.Object; break;
                    case "P576": Dissolution ??= t.Object; break;
                    case "P2031": WorkFrom ??= t.Object; break;
                    case "P2032": WorkTo ??= t.Object; break;
                    default: if (t.IsLiteral && Identifiers.TryGetValue(p, out var identifier)) Ids.TryAdd(identifier.Key, t.Object); break;
                }
                return;
            }
            if (!t.IsLiteral || t.Lang is null || !Wanted(t.Lang)) return;
            if (t.Predicate == Label) Labels.Add((t.Lang, t.Object));
            else if (t.Predicate == AltLabel) { if (AltLabels.Count < 40) AltLabels.Add((t.Lang, t.Object)); }
            else if (t.Predicate == DescriptionPredicate) Description ??= t.Object;
        }

        private static bool Wanted(string lang) => lang is "en" or "mul" || (lang.StartsWith("en-", StringComparison.Ordinal)) || lang == "simple";
    }

    private static AuthorityRecord? ToRecord(Item item, DateTimeOffset now, string? sourceVersion)
    {
        if (item.Ids.Count == 0 || item.Number < 0) return null;
        var label = LabelLanguages.Select(l => item.Labels.FirstOrDefault(x => x.Lang == l).Text).FirstOrDefault(x => x is not null) ?? item.Labels.FirstOrDefault().Text;
        var labels = new List<string>();
        if (label is not null) labels.Add(label);
        labels.AddRange(item.Labels.Select(x => x.Text));
        labels.AddRange(item.AltLabels.Select(x => x.Text));
        var type = item.InstanceOf.Select(q => Classes.GetValueOrDefault(q)).FirstOrDefault(c => c is not null);
        var lifespan = LifespanOf(item, type);
        var sameAs = item.Ids.Select(kv => Identifiers.First(i => i.Value.Key == kv.Key).Value.Uri(kv.Value)).Distinct(StringComparer.Ordinal).ToArray();
        var data = new Dictionary<string, object?> { ["ids"] = item.Ids };
        if (item.Description is not null) data["description"] = item.Description;
        if (item.InstanceOf.Count > 0) data["instanceOf"] = item.InstanceOf.Distinct(StringComparer.Ordinal).ToArray();
        return new AuthorityRecord(Entity + item.Id, Source, label, labels.Distinct(StringComparer.Ordinal).ToList(), type, lifespan, sameAs, JsonSerializer.Serialize(data), now, sourceVersion);
    }

    /// <summary>Birth and death for a person, inception and dissolution for anything else that has them, else the work period as working dates (born no later than its start, died no earlier than its end).</summary>
    private static Lifespan? LifespanOf(Item item, string? type)
    {
        var (begin, end) = type == nameof(EntityClass.Person) || (item.Birth is not null || item.Death is not null) ? (item.Birth, item.Death) : (item.Inception, item.Dissolution);
        if (begin is not null || end is not null) return GettyVocabLoader.LifespanFromYears(Year(begin), Year(end));
        if (item.WorkFrom is null && item.WorkTo is null) return null;
        var from = Year(item.WorkFrom ?? item.WorkTo); var to = Year(item.WorkTo ?? item.WorkFrom);
        if (!int.TryParse(from, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var f) || !int.TryParse(to, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var e) || f < 1 || e < 1 || e > 9999 || f > 9999) return null;
        return new Lifespan(null, new DateTimeOffset(f, 12, 31, 23, 59, 59, TimeSpan.Zero), new DateTimeOffset(e, 1, 1, 0, 0, 0, TimeSpan.Zero), null, f == e ? $"active {f}" : $"active {f}-{e}");
    }

    [GeneratedRegex(@"^(?<year>-?\d{1,6})(?:-\d\d-\d\d(?:T.*)?)?$")]
    private static partial Regex DateLiteral();

    /// <summary>The year of a dump date literal ("1952-03-11T00:00:00Z" gives "1952"; a BCE year keeps its sign and is left as text by the lifespan rules).</summary>
    internal static string? Year(string? literal)
    {
        if (literal is null) return null;
        var m = DateLiteral().Match(literal.Trim());
        return m.Success ? m.Groups["year"].Value.TrimStart('+') : null;
    }

    [GeneratedRegex(@"\((?<row>\d+),(?<item>\d+),'")]
    private static partial Regex SitelinkRow();

    /// <summary>Counts the sitelinks table's rows per kept item: the MySQL dump's <c>INSERT INTO `wb_items_per_site` VALUES (row, item, 'site', 'page'),...</c> lines.</summary>
    internal static Dictionary<long, int> CountSitelinks(TextReader reader, HashSet<long> kept, out long rows)
    {
        var counts = new Dictionary<long, int>();
        rows = 0;
        while (reader.ReadLine() is { } line)
        {
            if (!line.StartsWith("INSERT INTO", StringComparison.Ordinal) && !line.StartsWith('(')) continue;
            foreach (Match m in SitelinkRow().Matches(line))
            {
                rows++;
                var item = long.Parse(m.Groups["item"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture);
                if (kept.Contains(item)) counts[item] = counts.GetValueOrDefault(item) + 1;
            }
        }
        return counts;
    }
}
