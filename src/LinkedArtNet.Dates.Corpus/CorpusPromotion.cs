using System.Text;
using System.Text.Json;

namespace LinkedArtNet.Dates.Corpus;

/// <summary>
/// Changes the <c>status</c> of corpus entries in place, editing only the status tokens so the rest of the file,
/// including hand formatting and comments, is untouched. Promoting to <see cref="EntryStatus.Verified"/> first
/// evaluates the entries and refuses if any fail, because a verified entry is a CI gate.
/// </summary>
public static class CorpusPromotion
{
    /// <summary>What a promotion did, or would do.</summary>
    /// <param name="Path">The file edited.</param>
    /// <param name="Changed">Ids whose status was changed.</param>
    /// <param name="Unchanged">Ids that already had the requested status.</param>
    /// <param name="Failures">Entries that fail evaluation; when non-empty nothing was written.</param>
    public sealed record Outcome(string Path, IReadOnlyList<string> Changed, IReadOnlyList<string> Unchanged, IReadOnlyList<EntryResult> Failures)
    {
        public bool Applied => Failures.Count == 0;
    }

    /// <summary>
    /// Sets the status of the named entries (or every entry when <paramref name="ids"/> is null) in the file at
    /// <paramref name="path"/>. For <see cref="EntryStatus.Verified"/> the entries are evaluated first and the file is
    /// left alone if any fail. With <paramref name="dryRun"/> the file is never written.
    /// </summary>
    /// <exception cref="ArgumentException">An id is not in the file.</exception>
    public static Outcome Promote(string path, IReadOnlyCollection<string>? ids, EntryStatus to, CorpusEvaluator evaluator, bool dryRun = false, string? note = null)
    {
        var file = CorpusFile.Load(path);
        var known = file.Entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        if (ids is not null)
        {
            var unknown = ids.Where(id => !known.Contains(id)).ToList();
            if (unknown.Count > 0) throw new ArgumentException($"Not in {file.Path}: {string.Join(", ", unknown)}", nameof(ids));
        }
        var wanted = ids is null ? known : ids.ToHashSet(StringComparer.Ordinal);
        var targets = file.Entries.Where(e => wanted.Contains(e.Id)).ToList();
        var changed = targets.Where(e => e.Status != to).Select(e => e.Id).ToList();
        var unchanged = targets.Where(e => e.Status == to).Select(e => e.Id).ToList();

        var failures = to == EntryStatus.Verified
            ? evaluator.Evaluate(targets.Where(e => e.Status != to)).Where(r => !r.Passed).ToList()
            : [];
        if (failures.Count > 0 || changed.Count == 0) return new Outcome(path, failures.Count > 0 ? [] : changed, unchanged, failures);

        var edited = Edit(File.ReadAllBytes(path), ids is null, changed.ToHashSet(StringComparer.Ordinal), to, note);
        if (!dryRun)
        {
            File.WriteAllBytes(path, edited);
            var reloaded = CorpusFile.Load(path);
            var wrong = reloaded.Entries.Where(e => wanted.Contains(e.Id) && e.Status != to).Select(e => e.Id).ToList();
            if (wrong.Count > 0) throw new InvalidOperationException($"{path}: status edit did not take for {string.Join(", ", wrong)}; please inspect the file.");
        }
        return new Outcome(path, changed, unchanged, failures);
    }

    private sealed record Replacement(long Start, long End, string Text);

    /// <summary>
    /// Rewrites status tokens. Whole-file promotions set <c>defaults.status</c> (adding it when the defaults object
    /// exists without one) and every explicit entry status; partial promotions set or insert a status on each named entry.
    /// </summary>
    internal static byte[] Edit(byte[] bytes, bool wholeFile, IReadOnlySet<string> ids, EntryStatus to, string? note = null)
    {
        var bom = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        var value = $"\"{JsonNamingPolicy.CamelCase.ConvertName(to.ToString())}\"";
        var reader = new Utf8JsonReader(bytes.AsSpan(bom), new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var edits = new List<Replacement>();

        string? property = null;       // last property name at the current depth
        var inDefaults = false;        // inside the root "defaults" object
        var inEntries = false;         // inside the root "entries" array
        long? defaultsStart = null;    // position just after the defaults object's "{"
        var defaultsHasStatus = false;
        var defaultsHasProperties = false;
        // Per entry: the id, the id value's end position (for inserting a status), and the status value span if present.
        string? entryId = null; long entryIdEnd = -1; Replacement? entryStatus = null; Replacement? entryNotes = null; string? entryNotesText = null;

        while (reader.Read())
        {
            var depth = reader.CurrentDepth;
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    property = reader.GetString();
                    if (inDefaults && depth == 2) defaultsHasProperties = true;
                    break;
                case JsonTokenType.StartObject when depth == 1 && property == "defaults":
                    inDefaults = true; defaultsStart = bom + reader.BytesConsumed; break;
                case JsonTokenType.EndObject when inDefaults && depth == 1:
                    inDefaults = false; break;
                case JsonTokenType.StartArray when depth == 1 && property == "entries":
                    inEntries = true; break;
                case JsonTokenType.EndArray when inEntries && depth == 1:
                    inEntries = false; break;
                case JsonTokenType.StartObject when inEntries && depth == 2:
                    entryId = null; entryIdEnd = -1; entryStatus = null; entryNotes = null; entryNotesText = null; break;
                case JsonTokenType.EndObject when inEntries && depth == 2:
                    if (entryId is not null && ids.Contains(entryId))
                    {
                        if (entryStatus is not null) edits.Add(entryStatus);
                        else if (!wholeFile) edits.Add(new Replacement(entryIdEnd, entryIdEnd, $", \"status\": {value}"));
                        if (note is not null)
                        {
                            // The reviewer's reason travels with the status: appended to existing notes, or a new notes property after the id.
                            if (entryNotes is { } existing) edits.Add(existing with { Text = JsonSerializer.Serialize(entryNotesText + " " + note) });
                            else edits.Add(new Replacement(entryIdEnd, entryIdEnd, $", \"notes\": {JsonSerializer.Serialize(note)}"));
                        }
                    }
                    break;
                case JsonTokenType.String when inDefaults && depth == 2 && property == "status":
                    defaultsHasStatus = true;
                    if (wholeFile) edits.Add(new Replacement(bom + reader.TokenStartIndex, bom + reader.BytesConsumed, value));
                    break;
                case JsonTokenType.String when inEntries && depth == 3 && property == "id":
                    entryId = reader.GetString(); entryIdEnd = bom + reader.BytesConsumed; break;
                case JsonTokenType.String when inEntries && depth == 3 && property == "status":
                    entryStatus = new Replacement(bom + reader.TokenStartIndex, bom + reader.BytesConsumed, value); break;
                case JsonTokenType.String when inEntries && depth == 3 && property == "notes":
                    entryNotesText = reader.GetString(); entryNotes = new Replacement(bom + reader.TokenStartIndex, bom + reader.BytesConsumed, ""); break;
            }
        }

        if (wholeFile)
        {
            if (defaultsStart is { } start && !defaultsHasStatus)
                edits.Add(new Replacement(start, start, defaultsHasProperties ? $" \"status\": {value}," : $" \"status\": {value} "));
            else if (defaultsStart is null)
                // No defaults object: the implicit default is proposed, so every targeted entry needs an explicit status.
                return Edit(bytes, false, ids, to);
        }

        var output = new List<byte>(bytes.Length + edits.Count * 32);
        long position = 0;
        foreach (var edit in edits.OrderBy(e => e.Start))
        {
            output.AddRange(bytes.AsSpan((int)position, (int)(edit.Start - position)));
            output.AddRange(Encoding.UTF8.GetBytes(edit.Text));
            position = edit.End;
        }
        output.AddRange(bytes.AsSpan((int)position));
        return output.ToArray();
    }
}
