using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Import;

/// <summary>
/// The records behind the strings, for the desk (design doc 12 section 5: up to three example records per string, title, date and the
/// field; the first slice of the pipeline runner's collect stage, built 2026-10-26 for the desk's first session, when "Messrs Langford"
/// in corpauthor could not be told from its catalogue record). Reads a Heritage library export (DScribe's MAIN_CAT XML, one
/// <c>record</c> element per work with the catalogue fields as child elements) and, for each record, the strings it would have yielded
/// in each context the way the 2024 pmc-2 processor made them: every persauthorfull and corpauthor value, a trailing role of
/// publisher routing the value to the publisher context too and addressee to keywords; the publisher element; each keywords value and
/// its parts about " -- " (keywords:head, keywords:sub); the place element with its outer brackets trimmed, the hierarchical places of
/// the notes' (HIE) segments, and the parts of a place list about ";". A string the store knows, by context and normalised text, gains
/// the record as an example, up to the limit per string. Liberal on purpose: a value tried in a context the store has no string for
/// costs a dictionary lookup and nothing else. The export is read as UTF-8 with invalid bytes replaced, not refused: the March 2024
/// library file carries one stray byte (0x9D, in a series field) in 71 MB, and the XML reader's own decoder threw on it.
/// The 2024 process cleaned the keywords before the string map was made ("Hodgkin, Eliot, 1905-1987-Exhibitions" became "Hodgkin,
/// Eliot, 1905-1987--Exhibitions", the subdivisions marked by a double hyphen), so the store's keywords strings are the cleaned forms
/// and the export's raw ones reach 41 per cent of them (the first collection, 2026-10-26); given the cleaned-subjects table (the
/// owner's `pmc_subjects_csv`, a dump of insert statements: record id, raw keywords, cleaned keywords), the cleaned form is tried
/// too, with its parts.
/// </summary>
public sealed class HeritageExampleCollector(IAlignmentStore store, IReadOnlyDictionary<string, string>? cleanedKeywords = null)
{
    private static readonly Regex TrailingRole = new(@"\(([^()]*)\)\s*$", RegexOptions.Compiled);

    /// <summary>What a collection did: records read, examples written, strings covered, and per context the strings covered of those the store has.</summary>
    public sealed record Report(string Source, int Records, int Examples, int StringsCovered, IReadOnlyDictionary<string, (int Covered, int Strings)> ByContext);

    /// <summary>The records of the system that the last collect did not read, and marked gone.</summary>
    public int Gone { get; private set; }

    /// <summary>A record's fingerprint: a hash of its naming fields as collected, which is what it says apart from its identifier (design doc 12 section 14.5).</summary>
    public static string Fingerprint(string fieldsJson) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(fieldsJson)))[..32].ToLowerInvariant();

    /// <summary>
    /// Reads the export as one collect of one source system (design doc 12 section 14.1 and 14.2): every record's row, and every
    /// occurrence of a string the store knows, a part's with the whole value it is a part of. <paramref name="source"/> is the
    /// system's key ("library"); <paramref name="collect"/> names this reading of it (the export's date), and a record of the system
    /// that this collect did not read is marked gone. <paramref name="perString"/> caps the occurrences kept per string, for a test
    /// or a quick look; the default keeps them all.
    /// </summary>
    public async Task<Report> CollectAsync(string dataset, string path, string source, int perString = int.MaxValue, IProgress<string>? progress = null, CancellationToken ct = default, string? collect = null)
    {
        collect ??= Path.GetFileNameWithoutExtension(path);
        var rows = new List<SourceRecord>();
        var keys = await store.StringKeysAsync(dataset, ct);
        var contextOf = keys.ToDictionary(kv => kv.Value, kv => kv.Key.Context);
        var totals = keys.Keys.GroupBy(k => k.Context, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var taken = new Dictionary<long, int>();
        var covered = new HashSet<long>();
        var batch = new List<StringExample>();
        int records = 0, examples = 0;
        using var text = new StreamReader(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false), detectEncodingFromByteOrderMarks: true);
        using var reader = XmlReader.Create(text, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, Async = true });
        while (!reader.EOF)
        {
            ct.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "record") { await reader.ReadAsync(); continue; }
            var record = (XElement)XNode.ReadFrom(reader);
            records++;
            var id = record.Attribute("ID")?.Value ?? Values(record, "stdno").FirstOrDefault();
            if (id is null) continue;
            var title = Values(record, "title").FirstOrDefault();
            var year = Values(record, "year").FirstOrDefault();
            var json = NamingFieldsJson(record);
            rows.Add(new SourceRecord(source, id, title, year, json, Fingerprint(json)));
            var seen = new HashSet<long>();
            var position = 0;
            foreach (var (context, value, field, whole) in Candidates(record, cleanedKeywords))
            {
                position++;
                if (!keys.TryGetValue((context, StringKey.Normalise(value)), out var stringId) || !seen.Add(stringId)) continue;
                taken.TryGetValue(stringId, out var n);
                if (n >= perString) continue;
                taken[stringId] = n + 1;
                covered.Add(stringId);
                long? parent = whole is { } w && keys.TryGetValue((w.Context, StringKey.Normalise(w.Value)), out var parentId) && parentId != stringId ? parentId : null;
                batch.Add(new StringExample(stringId, id, source, title, year, field, json, parent, position));
                examples++;
            }
            if (rows.Count >= 500)
            {
                // the records before their occurrences, which refer to them
                await store.PutRecordsAsync(dataset, collect, rows, ct); rows.Clear();
                await store.PutExamplesAsync(batch, ct); batch.Clear();
            }
            if (records % 5000 == 0) progress?.Report($"{records:N0} records read, {examples:N0} occurrences of {covered.Count:N0} strings");
        }
        await store.PutRecordsAsync(dataset, collect, rows, ct);
        if (batch.Count > 0) await store.PutExamplesAsync(batch, ct);
        Gone = await store.MarkRecordsGoneAsync(dataset, source, collect, ct);
        var byContext = totals.ToDictionary(kv => kv.Key, kv => (covered.Count(s => contextOf[s] == kv.Key), kv.Value), StringComparer.Ordinal);
        return new Report(source, records, examples, covered.Count, byContext);
    }

    /// <summary>
    /// The raw-to-cleaned keywords of the 2024 process, read from a dump of insert statements into <c>pmc_subjects_csv</c> (record id,
    /// raw keywords, cleaned keywords, ...; SQL string literals with doubled quotes). The raw form keys the map; a raw form cleaned two
    /// ways keeps the first.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ReadCleanedKeywords(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            var i = line.IndexOf("VALUES (", StringComparison.Ordinal);
            if (i < 0) continue;
            var values = SqlStrings(line.AsSpan(i + 8)).Take(3).ToList();
            if (values.Count == 3 && values[1] is { } raw && values[2] is { } cleaned && raw.Length > 0 && cleaned.Length > 0) map.TryAdd(raw, cleaned);
        }
        return map;
    }

    /// <summary>The successive values of a SQL tuple: quoted strings with their doubled quotes undone, null for <c>null</c>.</summary>
    private static IEnumerable<string?> SqlStrings(ReadOnlySpan<char> tuple)
    {
        var values = new List<string?>();
        var i = 0;
        while (i < tuple.Length)
        {
            while (i < tuple.Length && (tuple[i] == ' ' || tuple[i] == ',')) i++;
            if (i >= tuple.Length || tuple[i] == ')') break;
            if (tuple[i] == '\'')
            {
                var sb = new StringBuilder();
                i++;
                while (i < tuple.Length)
                {
                    if (tuple[i] == '\'') { if (i + 1 < tuple.Length && tuple[i + 1] == '\'') { sb.Append('\''); i += 2; continue; } i++; break; }
                    sb.Append(tuple[i++]);
                }
                values.Add(sb.ToString());
            }
            else
            {
                var start = i;
                while (i < tuple.Length && tuple[i] != ',' && tuple[i] != ')') i++;
                var word = tuple[start..i].Trim().ToString();
                values.Add(word.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : word);
            }
        }
        return values;
    }

    /// <summary>The strings a record would have yielded, each with the context it belongs to and the element it came from.</summary>
    /// <remarks>A part (a keyword's head or subdivision, one place of a list) carries the whole value it is a part of, and its context.</remarks>
    public static IEnumerable<(string Context, string Value, string Field, (string Context, string Value)? Whole)> Candidates(XElement record, IReadOnlyDictionary<string, string>? cleanedKeywords = null)
    {
        foreach (var v in Values(record, "persauthorfull"))
        {
            yield return ("persauthorfull", v, "persauthorfull", null);
            switch (Role(v))
            {
                case "publisher": yield return ("publisher", v, "persauthorfull", null); break;
                case "addressee": yield return ("keywords", TrimOuterBrackets(v), "persauthorfull", null); break;
            }
        }
        foreach (var v in Values(record, "corpauthor"))
        {
            yield return ("corpauthor", v, "corpauthor", null);
            foreach (var part in ListParts(v)) yield return ("corpauthor", part, "corpauthor", ("corpauthor", v));
            if (Role(v) == "publisher") yield return ("publisher", v, "corpauthor", null);
        }
        // a publisher the cataloguer supplied stands in square brackets in the export ("[Osterley Park]", "[Agnew?]") and without them in
        // the strings the 2024 process collected: both forms are offered, as for place (the owner's desk, 2026-11-14: the publisher
        // string "Osterley Park", "seen 1 time" and no record shown; 487 of the 529 publisher strings without a record are of this kind)
        foreach (var v in Values(record, "publisher"))
        {
            yield return ("publisher", v, "publisher", null);
            if (TrimOuterBrackets(v) is var bare && bare != v) yield return ("publisher", bare, "publisher", null);
            // several publishers in one field, parted by semicolons ("Tate ; Ipswich Borough Council"): each is a string of its own,
            // carrying the whole value (2026-11-27: 275 such strings had each been minted as one body)
            foreach (var part in ListParts(TrimOuterBrackets(v))) yield return ("publisher", part, "publisher", ("publisher", TrimOuterBrackets(v)));
        }
        foreach (var raw in Values(record, "keywords"))
        {
            var forms = cleanedKeywords is not null && cleanedKeywords.TryGetValue(raw, out var cleaned) && cleaned != raw ? new[] { raw, cleaned } : [raw];
            foreach (var v in forms)
            {
                yield return ("keywords", v, "keywords", null);
                var parts = v.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 2) continue;
                yield return ("keywords:head", parts[0], "keywords", ("keywords", v));
                foreach (var sub in parts.Skip(1)) yield return ("keywords:sub", sub, "keywords", ("keywords", v));
            }
        }
        foreach (var v in Values(record, "place").Select(TrimOuterBrackets).Concat(HierarchicalPlaces(record)))
        {
            yield return ("place", v, "place", null);
            var parts = v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;
            foreach (var part in parts) yield return ("place", part, "place", ("place", v));
        }
    }

    /// <summary>The members of a value that lists several, parted by semicolons; none when it names one.</summary>
    private static string[] ListParts(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: >= 2 } parts ? parts : [];

    private static IEnumerable<string> Values(XElement record, string field) =>
        record.Elements().Where(e => e.Name.LocalName == field).Select(e => e.Value.Trim()).Where(v => v.Length > 0);

    /// <summary>The (HIE) segments of the notes field ("(GEN) ...||(HIE) London, England"), the hierarchical place of publication the 2024 processor used before the place element.</summary>
    private static IEnumerable<string> HierarchicalPlaces(XElement record) =>
        Values(record, "notescsvx").SelectMany(n => n.Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(s => s.StartsWith("(HIE)", StringComparison.Ordinal)).Select(s => s[5..].Trim()).Where(s => s.Length > 0);

    private static string? Role(string value) => TrailingRole.Match(value) is { Success: true } m ? m.Groups[1].Value.Trim().ToLowerInvariant() : null;

    private static string TrimOuterBrackets(string s) => s.Length >= 2 && s[0] == '[' && s[^1] == ']' ? s[1..^1].Trim() : s;

    private static readonly string[] NamingFields = ["title", "year", "medium", "persauthorfull", "corpauthor", "publisher", "place", "keywords", "class"];

    /// <summary>The record's fields that name things, as JSON of field to values, for the desk's card.</summary>
    private static string NamingFieldsJson(XElement record)
    {
        var fields = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var f in NamingFields)
        {
            var values = Values(record, f).ToArray();
            if (values.Length > 0) fields[f] = values;
        }
        return JsonSerializer.Serialize(fields);
    }
}
