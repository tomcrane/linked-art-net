using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>One heading whose split would change: the heading string, its parts as stored, the parts under the rule, and which rule did it.</summary>
public sealed record ResplitChange(SourceString Heading, IReadOnlyList<string> OldParts, IReadOnlyList<string> NewParts, string Rule);

/// <summary>What the pass found: the headings read, the ones whose split changes by rule, and each change.</summary>
public sealed record ResplitReport(int Read, IReadOnlyList<ResplitChange> Changes)
{
    public int ByRule(string rule) => Changes.Count(c => c.Rule.Contains(rule, StringComparison.Ordinal));
    public override string ToString() =>
        $"resplit: {Read:N0} headings read, {Changes.Count:N0} would change (by the splitter's rules {ByRule("a"):N0}, by a hyphenated name the indexes hold {ByRule("b"):N0}, stale parts with no rule of today's {ByRule("stale"):N0})";
}

/// <summary>
/// Finding 112 (2026-11-07, the owner at "New" in keywords:head): the library's export writes one hyphen both as the subdivision separator
/// and as the hyphen of a word or a name, the 2024 cleaning made "--" of every one, and the legacy import brought the doubled forms in, so
/// the split at "--" broke "Wood-carving", "Still-life", "Pre-Raphaelites", "New-York Historical Society" and the double-barrelled surnames
/// into a one-word head and a sub. The rule at the split, three parts: (a) a "--" followed by a lower-case letter is a hyphen, since LCSH
/// subdivisions are capitalised, except a chronological subdivision the catalogue writes in lower case ("to 1500"); (c) a head that is a
/// bound prefix never standing alone ("Anglo", "Pre", "Counter", "Franco", "Neo", "Post") joins the next part; (b) a one-word head whose join
/// with the next part by a hyphen is a label the LCSH or LoC indexes hold with that hyphen ("Pre-Raphaelitism", "Anglo-Saxons", "New-York
/// Historical Society", "Kit-Cat Club") is a hyphenated name, and the two are one part. Otherwise the split stands. The pass reads the
/// headings already in the store and says which would split differently; applying it is a later step, on the owner's word.
/// </summary>
public sealed class HeadingResplit(IAlignmentStore store)
{
    private static readonly string[] Sources = ["lcsh", "loc"];

    /// <summary>The parts of a heading under today's rules: the one splitter's (finding 4 and 112, <see cref="StringReading.JoinHyphenated"/>, rule "a"), then the indexes asked whether a one-word head and the next part are a hyphenated name they hold (rule "b"); "" when the plain split stands.</summary>
    public async Task<(IReadOnlyList<string> Parts, string Rule)> SplitAsync(string heading, CancellationToken ct = default)
    {
        var pieces = heading.Split("--", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var joined = StringReading.JoinHyphenated(pieces);
        var rule = ReferenceEquals(joined, pieces) ? "" : "a";
        var tokens = joined.ToList();
        if (tokens.Count > 1 && !tokens[0].Contains(' ') && await KnownHyphenatedAsync(tokens[0], tokens[1], ct))
        { tokens[0] = tokens[0] + "-" + tokens[1]; tokens.RemoveAt(1); rule += "b"; }
        return (tokens, rule);
    }

    /// <summary>Whether "head-next" (or "head-" plus the first word of next) is a label an index holds written with the hyphen.</summary>
    private async Task<bool> KnownHyphenatedAsync(string head, string next, CancellationToken ct)
    {
        var whole = StringKey.Normalise(head + "-" + next);
        var firstWord = StringKey.Normalise(head + "-" + next.Split(' ', 2)[0]);
        var keys = new[] { whole, StringKey.WithoutQualifiers(whole), firstWord }.Distinct(StringComparer.Ordinal).ToList();
        foreach (var source in Sources)
            foreach (var record in await store.FindAuthoritiesByLabelsAsync(keys, source, null, 20, ct))
                foreach (var label in record.Labels.Append(record.Label).OfType<string>())
                {
                    var l = StringKey.Normalise(label);
                    if (l == whole || StringKey.WithoutQualifiers(l) == StringKey.WithoutQualifiers(whole) || l == firstWord || l.StartsWith(firstWord + " ", StringComparison.Ordinal) || l.StartsWith(firstWord + ",", StringComparison.Ordinal)) return true;
                }
        return false;
    }

    /// <summary>The dry run over a dataset's compound headings: each heading whose parts would differ under the rule.</summary>
    public async Task<ResplitReport> DryRunAsync(string dataset, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var read = 0;
        var changes = new List<ResplitChange>();
        long after = 0;
        while (true)
        {
            var page = await store.DecidedStringsAsync(dataset, DecisionMethod.Composite, null, after, 500, ct: ct);
            if (page.Count == 0) break;
            after = page[^1].String.Id;
            foreach (var d in page)
            {
                ct.ThrowIfCancellationRequested();
                read++;
                var (parts, rule) = await SplitAsync(d.String.Raw, ct);
                var old = (await store.PartsAsync(d.String.Id, ct)).Select(p => p.Raw).ToList();
                if (old.SequenceEqual(parts, StringComparer.Ordinal)) continue;
                // parts made before today's rules and never re-split differ with no rule of today's to name: "stale"
                changes.Add(new ResplitChange(d.String, old, parts, rule.Length == 0 ? "stale" : rule));
                if (changes.Count % 50 == 0) progress?.Report($"{read:N0} read, {changes.Count:N0} would change");
            }
        }
        return new ResplitReport(read, changes);
    }
}
