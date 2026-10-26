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
/// costs a dictionary lookup and nothing else.
/// </summary>
public sealed class HeritageExampleCollector(IAlignmentStore store)
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
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, IgnoreWhitespace = true, Async = true });
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
            foreach (var (context, value, field) in Candidates(record))
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

    /// <summary>The strings a record would have yielded, each with the context it belongs to and the element it came from.</summary>
    public static IEnumerable<(string Context, string Value, string Field)> Candidates(XElement record)
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
        foreach (var v in Values(record, "keywords"))
        {
            yield return ("keywords", v, "keywords");
            var parts = v.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;
            yield return ("keywords:head", parts[0], "keywords");
            foreach (var sub in parts.Skip(1)) yield return ("keywords:sub", sub, "keywords");
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
