using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>One heading whose split would change: the heading string, its parts as stored, the parts under the rule, and which rule did it.</summary>
public sealed record ResplitChange(SourceString Heading, IReadOnlyList<string> OldParts, IReadOnlyList<string> NewParts, string Rule);

/// <summary>What the repair did: the dry run it acted on, the headings read whole as no compound, the new parts reconciled, and the part strings no heading carries any more.</summary>
public sealed record ResplitApplied(ResplitReport DryRun, int Whole, int PartsReconciled, IReadOnlyList<string> Orphans)
{
    public override string ToString() => $"{DryRun}; applied: {DryRun.Changes.Count:N0} headings split again, {Whole:N0} read whole as no compound, {PartsReconciled:N0} new parts reconciled, {Orphans.Count:N0} part strings no heading carries now";
}

/// <summary>What the pass found: the headings read, the ones whose split changes by rule, and each change.</summary>
public sealed record ResplitReport(int Read, IReadOnlyList<ResplitChange> Changes)
{
    public int ByRule(string rule) => Changes.Count(c => c.Rule.Contains(rule, StringComparison.Ordinal));
    public override string ToString() =>
        $"resplit: {Read:N0} headings read, {Changes.Count:N0} would change (by the splitter's rules {ByRule("a"):N0}, by a hyphenated name the indexes hold {ByRule("b"):N0}, " +
        $"leftover rows of an earlier re-split {ByRule("leftover"):N0}, stale parts with no rule of today's {ByRule("stale"):N0})";
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
        if (tokens.Count > 1 && !tokens[0].Contains(' ') && await KnownHyphenatedAsync(store, tokens[0], tokens[1], ct))
        { tokens[0] = tokens[0] + "-" + tokens[1]; tokens.RemoveAt(1); rule += "b"; }
        return (tokens, rule);
    }

    /// <summary>Whether "head-next" (or "head-" plus the first word of next) is a label the LCSH or LoC index holds written with the hyphen: rule (b), asked by the pass and by the reconciler's reading alike.</summary>
    public static async Task<bool> KnownHyphenatedAsync(IAlignmentStore store, string head, string next, CancellationToken ct = default)
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

    /// <summary>
    /// The repair, on the owner's word after the dry run's listing was read (2026-11-07, 167 headings: 33 by the splitter's rules, 14 by the
    /// indexes', 119 leftover rows, one stale): for each heading whose parts differ, the old part rows go, the new parts are collected and
    /// linked, a part without a decision is reconciled, and the heading's composite decision is written again; a heading with one part
    /// left is no compound and is reconciled whole. A part string no heading carries any more is reported, not removed. The decisions
    /// carry the run, so undo-run takes them back; the part rows are structural and stay as repaired.
    /// </summary>
    public async Task<ResplitApplied> ApplyAsync(string dataset, Reconciler reconciler, string runId, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var dry = await DryRunAsync(dataset, progress, ct);
        int whole = 0, partsReconciled = 0;
        var orphans = new List<string>();
        foreach (var change in dry.Changes)
        {
            ct.ThrowIfCancellationRequested();
            var heading = change.Heading;
            var oldParts = await store.PartsAsync(heading.Id, ct);
            var standing = await store.DecisionForAsync(heading.Id, ct);
            await store.ClearPartsAsync(heading.Id, ct);
            if (change.NewParts.Count == 1)
            {
                // no compound: the heading is a string like any other, read whole (the reconciler's rule (b) joins the head for it)
                await store.ReopenStringAsync(heading.Id, ct);
                if (standing?.EntityId is { } concept) await store.RemoveProvisionalIfUnreferencedAsync(concept, ct);
                await reconciler.ReconcileOneDetailedAsync(dataset, heading, runId, ct);
                whole++;
            }
            else
            {
                var ids = new List<long>();
                var reading = reconciler.Read(heading);
                var rule = reading.PartsRule;
                for (var i = 0; i < change.NewParts.Count; i++)
                {
                    var context = rule is null ? heading.Context : (i == 0 ? rule.HeadContext ?? rule.Context ?? heading.Context : rule.Context ?? heading.Context);
                    var part = await store.CollectAsync(dataset, context, change.NewParts[i], ct: ct, count: false);
                    await store.LinkPartAsync(heading.Id, i, part.Id, i == 0 ? "head" : "sub", ct);
                    ids.Add(part.Id);
                    if (await store.DecisionForAsync(part.Id, ct) is null) { await reconciler.ReconcileOneDetailedAsync(dataset, part, runId, ct); partsReconciled++; }
                }
                if (standing is { EntityId: { } entity })
                    await store.DecideAsync(heading.Id, entity, DecisionMethod.Composite, Reconciler.Evidence("composite", reading, [], new { parts = ids, texts = change.NewParts, resplit = change.Rule }), runId: runId, ct: ct);
            }
            foreach (var old in oldParts)
                if (change.NewParts.All(p => StringKey.Normalise(p) != StringKey.Normalise(old.Raw)) && (await store.ParentsAsync(old.Id, ct)).Count == 0)
                    orphans.Add($"{old.Context} \"{old.Raw}\"");
        }
        return new ResplitApplied(dry, whole, partsReconciled, orphans);
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
                // compared by key, not text: a part string is shared by every heading that carries it and keeps the spelling of the first
                // ("portraits" for "Portraits"), and the key folds the no-break spaces and trailing stops of the catalogue's forms
                var oldKeys = old.Select(StringKey.Normalise).ToList();
                var newKeys = parts.Select(StringKey.Normalise).ToList();
                if (oldKeys.SequenceEqual(newKeys, StringComparer.Ordinal)) continue;
                // a heading split again with fewer parts (finding 4's rejoining, 2026-10-21) kept the old rows beyond the new count, since
                // a part is written by position: "Wood-engravers | Great Britain | Biography | Biography"
                if (oldKeys.Count > newKeys.Count && oldKeys.Take(newKeys.Count).SequenceEqual(newKeys, StringComparer.Ordinal)) rule = "leftover";
                // parts made before today's rules and never re-split differ with no rule of today's to name: "stale"
                changes.Add(new ResplitChange(d.String, old, parts, rule.Length == 0 ? "stale" : rule));
                if (changes.Count % 50 == 0) progress?.Report($"{read:N0} read, {changes.Count:N0} would change");
            }
        }
        return new ResplitReport(read, changes);
    }
}
