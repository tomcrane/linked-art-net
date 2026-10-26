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

    public async Task<Report> CollectAsync(string dataset, string path, string source, int perString = 3, IProgress<string>? progress = null, CancellationToken ct = default)
    {
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
            string? json = null;
            var seen = new HashSet<long>();
            foreach (var (context, value, field) in Candidates(record, cleanedKeywords))
            {
                if (!keys.TryGetValue((context, StringKey.Normalise(value)), out var stringId) || !seen.Add(stringId)) continue;
                taken.TryGetValue(stringId, out var n);
                if (n >= perString) continue;
                taken[stringId] = n + 1;
                covered.Add(stringId);
                json ??= NamingFieldsJson(record);
                batch.Add(new StringExample(stringId, id, source, title, year, field, json));
                examples++;
            }
            if (batch.Count >= 500) { await store.PutExamplesAsync(batch, ct); batch.Clear(); }
            if (records % 5000 == 0) progress?.Report($"{records:N0} records read, {examples:N0} examples for {covered.Count:N0} strings");
        }
        if (batch.Count > 0) await store.PutExamplesAsync(batch, ct);
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
    public static IEnumerable<(string Context, string Value, string Field)> Candidates(XElement record, IReadOnlyDictionary<string, string>? cleanedKeywords = null)
    {
        foreach (var v in Values(record, "persauthorfull"))
        {
            yield return ("persauthorfull", v, "persauthorfull");
            switch (Role(v))
            {
                case "publisher": yield return ("publisher", v, "persauthorfull"); break;
                case "addressee": yield return ("keywords", TrimOuterBrackets(v), "persauthorfull"); break;
            }
        }
        foreach (var v in Values(record, "corpauthor"))
        {
            yield return ("corpauthor", v, "corpauthor");
            if (Role(v) == "publisher") yield return ("publisher", v, "corpauthor");
        }
        foreach (var v in Values(record, "publisher")) yield return ("publisher", v, "publisher");
        foreach (var raw in Values(record, "keywords"))
        {
            var forms = cleanedKeywords is not null && cleanedKeywords.TryGetValue(raw, out var cleaned) && cleaned != raw ? new[] { raw, cleaned } : [raw];
            foreach (var v in forms)
            {
                yield return ("keywords", v, "keywords");
                var parts = v.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 2) continue;
                yield return ("keywords:head", parts[0], "keywords");
                foreach (var sub in parts.Skip(1)) yield return ("keywords:sub", sub, "keywords");
            }
        }
        foreach (var v in Values(record, "place").Select(TrimOuterBrackets).Concat(HierarchicalPlaces(record)))
        {
            yield return ("place", v, "place");
            var parts = v.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;
            foreach (var part in parts) yield return ("place", part, "place");
        }
    }

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
