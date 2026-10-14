using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet.Dates.Corpus;

/// <summary>
/// Adopts the parser's current reading as an entry's expectation: for imported corpora whose source was wrong or
/// inconsistent, this is how a maintainer records "the parser is right here". The source's expectation moves to
/// <c>sourceExpectation</c> (unless one is already recorded), a dated note says what was accepted, and the status can be
/// set in the same edit. Like <see cref="CorpusPromotion"/>, only the affected tokens are rewritten.
/// </summary>
public static class CorpusAcceptance
{
    public sealed record Accepted(string Id, string Input, string Before, string After);

    /// <param name="Changed">Entries whose expectation was rewritten.</param>
    /// <param name="AlreadyPassing">Targeted entries that already pass and were left alone.</param>
    public sealed record Outcome(string Path, IReadOnlyList<Accepted> Changed, IReadOnlyList<string> AlreadyPassing);

    private static readonly JsonSerializerOptions Compact = new(CorpusFile.JsonOptions)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    /// <summary>The expectation that pins exactly what a parse result says: status, kind, canonical, EDTF and realised bounds.</summary>
    public static Expectation FromResult(TimeSpanParseResult result)
    {
        var status = result.Status.ToString().ToLowerInvariant();
        if (result.Status is not (ParseStatus.Parsed or ParseStatus.Partial)) return new Expectation { Status = status };
        return new Expectation
        {
            Status = status,
            Kind = Camel(result.Kind.ToString()),
            Canonical = result.Canonical,
            Edtf = result.Edtf,
            TimeSpan = Span(result.TimeSpan),
            Begin = Span(result.Begin),
            End = Span(result.End),
        };
    }

    /// <summary>A one-line reading of an expectation, for notes and reports: "status parsed; years 1858..1858; flags Approximate".</summary>
    public static string Describe(Expectation e)
    {
        var parts = new List<string>();
        if (e.Status is { } s) parts.Add("status " + s);
        if (e.Kind is { } k) parts.Add("kind " + k);
        if (e.Canonical is { } c) parts.Add("canonical " + c);
        if (e.Edtf is { } d) parts.Add("edtf " + d);
        if (e.Years is { } y) parts.Add($"years {y.Start?.ToString() ?? ".."}..{y.End?.ToString() ?? ".."}");
        if (e.TimeSpan is { } ts) parts.Add("timespan " + Bounds(ts));
        if (e.Begin is { } b) parts.Add("begin " + Bounds(b));
        if (e.End is { } en) parts.Add("end " + Bounds(en));
        if (e.Flags is { Count: > 0 } f) parts.Add("flags " + string.Join(",", f));
        if (e.NotFlags is { Count: > 0 } nf) parts.Add("not " + string.Join(",", nf));
        if (e.MinConfidence is { } min) parts.Add($"confidence >= {min}");
        if (e.MaxConfidence is { } max) parts.Add($"confidence <= {max}");
        if (e.ToleranceYears > 0) parts.Add($"tolerance {e.ToleranceYears}y");
        return parts.Count == 0 ? "nothing" : string.Join("; ", parts);
    }

    /// <summary>
    /// Rewrites the expectation of every targeted entry that currently fails to the parser's reading. <paramref name="ids"/>
    /// and <paramref name="filter"/> both narrow the targets (null means all). <paramref name="setStatus"/> also changes the
    /// status of the rewritten entries. Returns what changed; writes nothing with <paramref name="dryRun"/>.
    /// </summary>
    public static Outcome Accept(string path, IReadOnlyCollection<string>? ids, Func<CorpusEntry, bool>? filter, EntryStatus? setStatus,
        CorpusEvaluator evaluator, bool dryRun = false, DateOnly? today = null)
    {
        var file = CorpusFile.Load(path);
        var known = file.Entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        if (ids is not null)
        {
            var unknown = ids.Where(id => !known.Contains(id)).ToList();
            if (unknown.Count > 0) throw new ArgumentException($"Not in {file.Path}: {string.Join(", ", unknown)}", nameof(ids));
        }
        var targets = file.Entries.Where(e => (ids is null || ids.Contains(e.Id)) && (filter is null || filter(e))).ToList();

        var date = (today ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");
        var changes = new Dictionary<string, (string Expect, string? Source, string Notes, bool SetStatus)>(StringComparer.Ordinal);
        var changed = new List<Accepted>();
        var passing = new List<string>();
        foreach (var entry in targets)
        {
            var result = evaluator.Evaluate(entry);
            if (result.Passed) { passing.Add(entry.Id); continue; }
            var expect = FromResult(result.Result);
            var before = Describe(entry.Expect);
            var after = Describe(expect);
            var note = $"Accepted the parser's reading on {date}; the expectation had been: {before}.";
            var notes = string.IsNullOrWhiteSpace(entry.Notes) ? note : entry.Notes.Trim() + " " + note;
            changes[entry.Id] = (JsonSerializer.Serialize(expect, Compact), entry.SourceExpectation is null ? before : null, notes, setStatus is { } s && entry.Status != s);
            changed.Add(new Accepted(entry.Id, entry.Input, before, after));
        }
        if (changed.Count == 0) return new Outcome(path, changed, passing);

        var edited = Edit(File.ReadAllBytes(path), changes, setStatus);
        if (!dryRun)
        {
            File.WriteAllBytes(path, edited);
            var reloaded = CorpusFile.Load(path);
            var still = reloaded.Entries.Where(e => changes.ContainsKey(e.Id)).Select(evaluator.Evaluate).Where(r => !r.Passed).Select(r => r.Entry.Id).ToList();
            if (still.Count > 0) throw new InvalidOperationException($"{path}: accepted entries still fail after the edit: {string.Join(", ", still)}; please inspect the file.");
        }
        return new Outcome(path, changed, passing);
    }

    private sealed record Replacement(long Start, long End, string Text);

    /// <summary>Per entry: where its expect object, status, sourceExpectation and notes sit, so each can be replaced or inserted.</summary>
    private sealed class Layout
    {
        public string? Id;
        public long IdEnd = -1;
        public (long Start, long End)? Expect;
        public (long Start, long End)? Status;
        public (long Start, long End)? Source;
        public (long Start, long End)? Notes;
    }

    private static byte[] Edit(byte[] bytes, Dictionary<string, (string Expect, string? Source, string Notes, bool SetStatus)> changes, EntryStatus? setStatus)
    {
        var bom = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        var reader = new Utf8JsonReader(bytes.AsSpan(bom), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var layouts = new List<Layout>();
        string? property = null;
        var inEntries = false;
        Layout? current = null;
        long expectStart = -1;
        var expectDepth = -1;

        while (reader.Read())
        {
            var depth = reader.CurrentDepth;
            var position = bom + reader.TokenStartIndex;
            var consumed = bom + reader.BytesConsumed;
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    property = reader.GetString();
                    break;
                case JsonTokenType.StartArray when depth == 1 && property == "entries":
                    inEntries = true; break;
                case JsonTokenType.EndArray when inEntries && depth == 1:
                    inEntries = false; break;
                case JsonTokenType.StartObject when inEntries && depth == 2:
                    current = new Layout(); break;
                case JsonTokenType.EndObject when inEntries && depth == 2:
                    if (current?.Id is not null) layouts.Add(current);
                    current = null; break;
                case JsonTokenType.StartObject when current is not null && depth == 3 && property == "expect":
                    expectStart = position; expectDepth = depth; break;
                case JsonTokenType.EndObject when current is not null && expectDepth == depth && expectStart >= 0:
                    current.Expect = (expectStart, consumed); expectStart = -1; expectDepth = -1; break;
                case JsonTokenType.String when current is not null && depth == 3:
                    switch (property)
                    {
                        case "id": current.Id = reader.GetString(); current.IdEnd = consumed; break;
                        case "status": current.Status = (position, consumed); break;
                        case "sourceExpectation": current.Source = (position, consumed); break;
                        case "notes": current.Notes = (position, consumed); break;
                    }
                    break;
            }
        }

        var edits = new List<Replacement>();
        var statusValue = setStatus is { } st ? $"\"{JsonNamingPolicy.CamelCase.ConvertName(st.ToString())}\"" : null;
        foreach (var layout in layouts)
        {
            if (layout.Id is null || !changes.TryGetValue(layout.Id, out var change)) continue;
            if (layout.Expect is not { } expect) throw new InvalidDataException($"Entry {layout.Id} has no expect object.");
            edits.Add(new Replacement(expect.Start, expect.End, change.Expect));
            var additions = new StringBuilder();
            if (change.Source is { } source)
            {
                if (layout.Source is { } span) edits.Add(new Replacement(span.Start, span.End, JsonSerializer.Serialize(source)));
                else additions.Append(", \"sourceExpectation\": ").Append(JsonSerializer.Serialize(source));
            }
            if (layout.Notes is { } notes) edits.Add(new Replacement(notes.Start, notes.End, JsonSerializer.Serialize(change.Notes)));
            else additions.Append(", \"notes\": ").Append(JsonSerializer.Serialize(change.Notes));
            if (additions.Length > 0) edits.Add(new Replacement(expect.End, expect.End, additions.ToString()));
            if (change.SetStatus && statusValue is not null)
            {
                if (layout.Status is { } status) edits.Add(new Replacement(status.Start, status.End, statusValue));
                else edits.Add(new Replacement(layout.IdEnd, layout.IdEnd, $", \"status\": {statusValue}"));
            }
        }

        var output = new List<byte>(bytes.Length + edits.Count * 64);
        long at = 0;
        foreach (var edit in edits.OrderBy(e => e.Start).ThenBy(e => e.End))
        {
            if (edit.Start < at) throw new InvalidOperationException("Overlapping edits.");
            output.AddRange(bytes.AsSpan((int)at, (int)(edit.Start - at)));
            output.AddRange(Encoding.UTF8.GetBytes(edit.Text));
            at = edit.End;
        }
        output.AddRange(bytes.AsSpan((int)at));
        return output.ToArray();
    }

    private static ExpectedTimeSpan? Span(LinkedArtTimeSpan? ts) => ts is null ? null : new ExpectedTimeSpan
    {
        BeginOfTheBegin = ts.BeginOfTheBegin?.ToString(), EndOfTheBegin = ts.EndOfTheBegin?.ToString(),
        BeginOfTheEnd = ts.BeginOfTheEnd?.ToString(), EndOfTheEnd = ts.EndOfTheEnd?.ToString(),
    };

    private static string Bounds(ExpectedTimeSpan ts) => $"{ts.BeginOfTheBegin ?? ".."}..{ts.EndOfTheEnd ?? ".."}";

    private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
