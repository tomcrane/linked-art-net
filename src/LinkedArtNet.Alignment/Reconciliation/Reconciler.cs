using System.Text.Json;
using LinkedArtNet.Agents;
using LinkedArtNet.Alignment.Store;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>Thresholds and reading rules for a run; the manifest carries these per dataset.</summary>
public sealed record ReconcilerOptions
{
    /// <summary>A group at or above this score may be accepted.</summary>
    public double AcceptScore { get; init; } = 0.9;
    /// <summary>A group at or above this score, when not accepted, sends the string to the review queue; below it the string gets a provisional entity.</summary>
    public double ReviewScore { get; init; } = 0.6;
    /// <summary>How many independent sources must agree for a vote to carry.</summary>
    public int MinIndependentSources { get; init; } = 2;
    /// <summary>Accept one source alone when the name is exact and the string's and the record's dates agree (both known on the same side). Design doc 10 sends a lone source to review; this is the pragmatic default while ULAN is the only local index, and a manifest can turn it off.</summary>
    public bool AcceptSingleSourceWithDates { get; init; } = true;
    /// <summary>
    /// Accept a group that is one source's exact match when it is the only candidate at any score and the string carries
    /// no dates to check (an undated string with one exact LoC heading and no namesake anywhere). Policy, not evidence:
    /// a third of the PMC corpus looked like this in the 2026-10-19 shadow run, far more than a desk can take, and the
    /// 2024 process accepted them. Decided under DecisionMethod.ExactSingle so the set stays reviewable and reversible.
    /// </summary>
    public bool AcceptUniqueExactSingleSource { get; init; } = true;
    /// <summary>Reading rules by context (the field a string came from); contexts not listed use <see cref="DefaultContext"/>.</summary>
    public IReadOnlyDictionary<string, ContextRule> Contexts { get; init; } = new Dictionary<string, ContextRule>(StringComparer.OrdinalIgnoreCase);
    public ContextRule DefaultContext { get; init; } = ContextRule.Anything;
    /// <summary>Mint a provisional entity when nothing was found; off, such strings are left undecided for a later run.</summary>
    public bool MintWhenNothingFound { get; init; } = true;
    /// <summary>The longest lifetime a person is allowed when one side's birth is compared with the other's death.</summary>
    public int MaxPersonLifetimeYears { get; init; } = 120;
    /// <summary>Where the dataset's places mostly are ("United Kingdom"): when a qualifier's name is a county in several countries ("Essex"), the one here is meant (finding 71).</summary>
    public string? HomePlace { get; init; }
}

public enum Outcome { Skipped, Overridden, MatchedStore, Accepted, Queued, Minted, Deferred, Decomposed }

/// <param name="Lists">Strings decided as lists of parts; their parts' outcomes are counted with everything else, so the outcome counts add up to Considered minus Lists plus Parts.</param>
/// <param name="Parts">Parts reconciled for the first time in this run (a part already decided by an earlier string is not counted again).</param>
/// <param name="Single">Accepted on one source carried by the string's own dates or placed qualifier (DecisionMethod.Single).</param>
/// <param name="Compounds">Compound headings decided as concepts coordinated from their parts (DecisionMethod.Composite); counted like lists.</param>
/// <param name="DecidedEarlier">Strings in the undecided list that were decided before the run reached them, as parts of an earlier string (part strings outlive an undo as rows), and so were passed over rather than decided twice.</param>
/// <param name="Reconsidered">Strings the run queued and looked at again at its end (the second pass, finding 51).</param>
/// <param name="Settled">Of those, the strings the second pass decided: counted under matched in store or accepted, and no longer under queued.</param>
public sealed record ReconcileReport(int Considered, int Overridden, int MatchedStore, int Accepted, int Queued, int Minted, int Deferred, int Skipped, int ExactSingle = 0, int Lists = 0, int Parts = 0, int Single = 0, int Compounds = 0, int DecidedEarlier = 0, int Reconsidered = 0, int Settled = 0)
{
    /// <summary>Accepted by independent sources agreeing, as opposed to one source carried by the string or the single-source policy.</summary>
    public int Voted => Accepted - ExactSingle - Single;
    public override string ToString() => $"considered {Considered}: overridden {Overridden}, matched in store {MatchedStore}, accepted {Accepted} (vote {Voted}, single {Single}, exact-single {ExactSingle}), queued {Queued}, minted {Minted}, deferred {Deferred}, skipped {Skipped}" + (Lists + Compounds > 0 ? $", lists {Lists}, compounds {Compounds} (between them {Parts} part strings)" : "") + (DecidedEarlier > 0 ? $"; {DecidedEarlier} of those considered were decided as parts before the run reached them" : "") + (Reconsidered > 0 ? $"; second pass: {Reconsidered} queued strings reconsidered, {Settled} settled" : "");
}

/// <summary>
/// What the reconciler would do with a string, before anything is written: the outcome, how the string was read, the
/// candidate groups, and whichever of an override, a store entity or an accepted group decided it.
/// </summary>
/// <param name="Truncated">A local index returned more candidates than it was asked for, so the best may not have been seen; such a string is never accepted, only queued.</param>
/// <param name="Parts">For a decomposed string, the evaluations of the parts reconciled for the first time under it.</param>
public sealed record Evaluation(Outcome Outcome, StringReading Reading, IReadOnlyList<CandidateGroup> Groups, Override? Override = null, Entity? StoreMatch = null, CandidateGroup? Accepted = null, ScoredCandidate? StoreChoice = null, bool Truncated = false, string? AcceptedMethod = null, IReadOnlyList<Evaluation>? Parts = null)
{
    /// <summary>The URIs the evaluation points at: the override target, the store entity and its equivalents, or the accepted group's URIs; for a queued string the top group's.</summary>
    public IReadOnlyList<string> TargetUris => Outcome switch
    {
        Outcome.Overridden when Override is not null => [Override.Target],
        Outcome.MatchedStore when StoreMatch is not null => [StoreMatch.Id, .. StoreMatch.Equivalents.Select(q => q.Uri)],
        Outcome.Accepted when Accepted is not null => Accepted.Uris,
        Outcome.Queued when Groups.Count > 0 => Groups[0].Uris,
        _ => [],
    };
}

/// <summary>
/// Decides what undecided source strings refer to (design doc 10, section 7). For each string, in order and
/// stopping at the first acceptance: the overrides; entities already in the store whose labels match and whose
/// lifespan does not conflict; the local authority indexes; then remote sources (step 3b). Candidates are scored on
/// name, dates, type and nationality, grouped through their sameAs links so sources that name the same thing vote
/// together, and accepted, queued for review with a provisional entity, or given a provisional entity when nothing
/// was found. Every decision records its evidence. <see cref="EvaluateAsync"/> does the thinking without writing,
/// which the shadow run uses to re-judge old decisions from scratch.
/// </summary>
public sealed class Reconciler(IAlignmentStore store, IdentifierScheme scheme, IReadOnlyList<IAuthoritySource> sources, ReconcilerOptions? options = null, ProfileCatalog? datesProfiles = null)
{
    private readonly ReconcilerOptions _options = options ?? new ReconcilerOptions();
    private readonly Dictionary<string, AgentStringParser> _parsers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _timings = new(StringComparer.Ordinal);

    private void Time(string stage, System.Diagnostics.Stopwatch sw) { _timings[stage] = _timings.GetValueOrDefault(stage) + sw.ElapsedMilliseconds; sw.Restart(); }

    /// <summary>Milliseconds spent per stage so far (reading, overrides, store lookups, each source, scoring and grouping, commits), for finding where the time goes.</summary>
    public IReadOnlyDictionary<string, long> Timings => _timings;

    public string TimingsText => string.Join(", ", _timings.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / 1000.0:0.0}s"));

    public ReconcilerOptions Options => _options;

    /// <summary>Works through the dataset's undecided strings: all of them when <paramref name="limit"/> is 0 or less. <paramref name="onDecided"/> sees each string's evaluation as it is made, with its position in the run and the run's length (the tool's verbose mode, <see cref="EvaluationText"/>).</summary>
    public async Task<ReconcileReport> RunAsync(string dataset, int limit = 0, string? runId = null, IProgress<string>? progress = null, Action<Evaluation, int, int>? onDecided = null, CancellationToken ct = default)
    {
        var strings = await store.UndecidedAsync(dataset, limit > 0 ? limit : int.MaxValue, ct);
        int overridden = 0, matched = 0, accepted = 0, exactSingle = 0, single = 0, queued = 0, minted = 0, deferred = 0, skipped = 0, lists = 0, compounds = 0, parts = 0, decidedEarlier = 0, n = 0;
        var queuedStrings = new List<SourceString>();
        void Count(Evaluation e)
        {
            switch (e.Outcome)
            {
                case Outcome.Overridden: overridden++; break;
                case Outcome.MatchedStore: matched++; break;
                case Outcome.Accepted: accepted++; if (e.AcceptedMethod == DecisionMethod.ExactSingle) exactSingle++; else if (e.AcceptedMethod == DecisionMethod.Single) single++; break;
                case Outcome.Queued: queued++; queuedStrings.Add(e.Reading.String); break;
                case Outcome.Minted: minted++; break;
                case Outcome.Deferred: deferred++; break;
                case Outcome.Decomposed: if (e.Reading.Shape == PartsShape.Compound) compounds++; else lists++; foreach (var p in e.Parts ?? []) { parts++; Count(p); } break;
                default: skipped++; break;
            }
        }
        foreach (var s in strings)
        {
            ct.ThrowIfCancellationRequested();
            // a part string from an earlier run is a row of its own and undecided when the list is made; its parent, earlier in the
            // list, decides it on the way, and deciding it again here would supersede that decision and count it twice
            n++;
            if (await store.DecisionForAsync(s.Id, ct) is not null) decidedEarlier++;
            else { var e = await ReconcileOneDetailedAsync(dataset, s, runId, ct); Count(e); onDecided?.Invoke(e, n, strings.Count); }
            if (n % 500 == 0) progress?.Report($"{n} of {strings.Count}: accepted {accepted} (exact-single {exactSingle}), queued {queued}, minted {minted}" + (lists + compounds > 0 ? $", lists {lists}, compounds {compounds}" : ""));
        }
        // the second pass (finding 51, 2026-10-23): a string queued early in the run may be settled by what the run did later, when an
        // entity aligned for the same name in another context answers it from the store ("Cornwall" in keywords:sub, decided before the
        // place field's "Cornwall" made the entity) or breaks a tie among places (finding 35). Each queued string is evaluated again at the
        // end; one that comes back matched in store or accepted is decided afresh, the queued decision superseded and its placeholder,
        // which nothing else points at, removed; one still queued keeps its decision and its placeholder. The indexes have not changed,
        // so nothing else can come back different
        int reconsidered = 0, settled = 0;
        if (queuedStrings.Count > 0)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            progress?.Report($"second pass over {queuedStrings.Count} queued strings");
            foreach (var s in queuedStrings)
            {
                ct.ThrowIfCancellationRequested();
                reconsidered++;
                var e = await EvaluateAsync(dataset, s, includeStore: true, ct);
                if (e.Outcome is not (Outcome.MatchedStore or Outcome.Accepted)) continue;
                var old = await store.DecisionForAsync(s.Id, ct);
                await CommitAsync(dataset, s, e, runId, ct);
                if (old?.EntityId is { } placeholder) await store.RemoveProvisionalIfUnreferencedAsync(placeholder, ct);
                settled++; queued--; Count(e);
                onDecided?.Invoke(e, reconsidered, queuedStrings.Count);
            }
            Time("second-pass", sw);
            progress?.Report($"second pass: {reconsidered} reconsidered, {settled} settled");
        }
        return new ReconcileReport(strings.Count, overridden, matched, accepted, queued, minted, deferred, skipped, exactSingle, lists, parts, single, compounds, decidedEarlier, reconsidered, settled);
    }

    public async Task<Outcome> ReconcileOneAsync(string dataset, SourceString s, string? runId = null, CancellationToken ct = default) =>
        (await ReconcileOneDetailedAsync(dataset, s, runId, ct)).Outcome;

    /// <summary>Evaluates and commits one string, returning the evaluation so the caller can see how it was decided.</summary>
    public async Task<Evaluation> ReconcileOneDetailedAsync(string dataset, SourceString s, string? runId = null, CancellationToken ct = default)
    {
        var evaluation = await EvaluateAsync(dataset, s, includeStore: true, ct);
        if (evaluation.Outcome == Outcome.Decomposed) return await DecomposeAsync(dataset, s, evaluation, runId, ct);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await CommitAsync(dataset, s, evaluation, runId, ct);
        Time("commit", sw);
        return evaluation;
    }

    /// <summary>
    /// A string made of parts is decided as its parts: each part is collected as a string of its own (shared with every
    /// other parent naming it, and not counted as a new occurrence; a compound's head in the head context, the rest in
    /// the parts context), linked in order, and reconciled when it has no decision yet. A list's decision is the list,
    /// with no entity; a compound heading's is composite, with the heading's own Type entity (doc 10, section 7.5).
    /// </summary>
    private async Task<Evaluation> DecomposeAsync(string dataset, SourceString s, Evaluation e, string? runId, CancellationToken ct)
    {
        var reading = e.Reading;
        var compound = reading.Shape == PartsShape.Compound;
        var decided = new List<Evaluation>();
        var ids = new List<long>();
        for (var i = 0; i < reading.Parts!.Count; i++)
        {
            var part = await store.CollectAsync(dataset, reading.PartContext(i, s.Context), reading.Parts[i], ct: ct, count: false);
            await store.LinkPartAsync(s.Id, i, part.Id, compound ? (i == 0 ? "head" : "sub") : "member", ct);
            ids.Add(part.Id);
            if (await store.DecisionForAsync(part.Id, ct) is null) decided.Add(await ReconcileOneDetailedAsync(dataset, part, runId, ct));
        }
        if (compound)
        {
            var entity = await CompositeEntityAsync(dataset, reading, runId, ct);
            await store.DecideAsync(s.Id, entity.Id, DecisionMethod.Composite, Evidence("composite", reading, e.Groups, new { parts = ids, texts = reading.Parts }), runId: runId, ct: ct);
        }
        else
            await store.DecideAsync(s.Id, null, DecisionMethod.List, Evidence("list", reading, [], new { parts = ids, texts = reading.Parts }), runId: runId, ct: ct);
        return e with { Parts = decided };
    }

    /// <summary>The heading's own concept: the project's existing Type entity under that label when it has one (the imported headings keep their identifiers), else a new one under the identifier the earlier pipeline gave the string, else under a fresh one. Its parts are the decisions of its part strings, read at export.</summary>
    private async Task<Entity> CompositeEntityAsync(string dataset, StringReading reading, string? runId, CancellationToken ct)
    {
        foreach (var key in reading.Keys)
            foreach (var found in await store.FindEntitiesByLabelAsync(key, dataset, EntityClass.Type, ct))
                if (found.MergedInto is null && NameMatcher.Score(reading.Text, found.Label is null ? found.Labels : found.Labels.Prepend(found.Label)) >= 0.97) return found;
        return await OwnEntityAsync(dataset, reading, new NewEntity(EntityClass.Type, reading.Text, [reading.String.Raw], null, [], [], EntityStatus.Provisional, RunId: runId), ct);
    }

    /// <summary>
    /// A new entity of the project's own for a string: under the identifier the owner's earlier pipeline gave the string when it had
    /// no authority (<c>unrec_…</c>, kept by the import in the string's parsed JSON as <c>legacyUnreconciled</c>), since LUX holds
    /// records under those identifiers and a re-export under the same ones updates them rather than adding islands (doc 10 section
    /// 7.5; the owner's decision 2026-10-21, "keep"); under a fresh identifier otherwise. The identifier is placed under the class's
    /// segment as the import places the <c>authorities</c> identifiers; an entity already there (the same placeholder on a string in
    /// two contexts, or a run not undone) is reused.
    /// </summary>
    private async Task<Entity> OwnEntityAsync(string dataset, StringReading reading, NewEntity entity, CancellationToken ct)
    {
        if (LegacyIdentifier(reading.String) is { } legacy)
        {
            var id = scheme.Uri(entity.Class, legacy);
            // the earlier pipeline's column held tentative mappings to real authorities as well as placeholders ("Athens" to the
            // aligned legacy Athens): an aligned or confirmed entity is not reused for a queued or minted string, which would make
            // a tentative decision look settled (finding 29, 2026-10-21); only a provisional one is, and an absent one is created
            var kept = await store.ResolveEntityAsync(id, ct);
            if (kept is { Status: EntityStatus.Provisional }) return kept;
            if (kept is null) return await store.AddEntityAsync(dataset, entity with { Id = id }, scheme, ct);
        }
        return await store.AddEntityAsync(dataset, entity, scheme, ct);
    }

    private static readonly System.Text.RegularExpressions.Regex IdentifierShape = new(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>The identifier the earlier pipeline gave a string that had no authority, from the import's <c>legacyUnreconciled</c> in the string's parsed JSON; null when there is none or it would not make a path segment.</summary>
    public static string? LegacyIdentifier(SourceString s)
    {
        if (string.IsNullOrWhiteSpace(s.ParsedJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(s.ParsedJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("legacyUnreconciled", out var v) || v.ValueKind != JsonValueKind.String) return null;
            var id = v.GetString()?.Trim();
            return id is not null && IdentifierShape.IsMatch(id) ? id : null;
        }
        catch (JsonException) { return null; }
    }

    private readonly Dictionary<string, string?> _ancestors = new(StringComparer.Ordinal);

    /// <summary>
    /// The qualifier's own place in TGN, when it is a county or above and TGN has one such place of the name: "Yorkshire" is in
    /// "England, United Kingdom, Europe, World", so LCSH's "Fountains Hall (England)" is placed by "Fountains Hall (Yorkshire)" though
    /// its parenthetical is the coarser (finding 56, 2026-10-22). A town's name ("Bridgend") brings nothing: too many namesakes.
    /// </summary>
    private async Task<StringReading> WithQualifierAncestorsAsync(StringReading reading, CancellationToken ct)
    {
        if (reading.Qualifier is null || !sources.Any(x => x.Name == "tgn")) return reading;
        // every segment in turn until one is a county or above ("Exmouth, Devonshire": the town brings nothing, the county does), the
        // qualifier's "&" read as "and" ("Dumfries & Galloway"); finding 77, 2026-10-23
        foreach (var segment in reading.Qualifier.Split(',').Select(x => x.Trim().Replace(" & ", " and ", StringComparison.Ordinal)).Where(x => x.Length >= 3))
        {
            if (!_ancestors.TryGetValue(segment, out var ancestors))
            {
                ancestors = null;
                var records = await store.FindAuthoritiesByLabelsAsync(NameMatcher.Keys(segment), "tgn", nameof(EntityClass.Place), 50, ct);
                var own = new List<string>();
                // the name may be TGN's variant ("Devonshire" for its "Devon"): then the preferred name leads the ancestors, so that a heading
                // placed by the preferred name ("Arlington, Devon, England") agrees (finding 77)
                foreach (var r in records.Where(r => r.Label is not null && NameMatcher.Score(segment, r.Labels.Prepend(r.Label!)) >= 0.97))
                {
                    string? parent = null, placeType = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(r.DataJson ?? "{}");
                        if (doc.RootElement.TryGetProperty("parentString", out var ps)) parent = ps.GetString();
                        if (doc.RootElement.TryGetProperty("placeType", out var pt)) placeType = pt.GetString();
                    }
                    catch (JsonException) { }
                    if (parent is null || placeType is null) continue;
                    var label = (await store.GetAuthorityAsync(placeType, ct))?.Label;
                    if (Qualified.LevelOf(label) is >= 3) own.Add(NameMatcher.Score(segment, [r.Label!]) >= 0.97 ? parent : $"{r.Label}, {parent}");
                }
                // several counties of the name ("Essex" in England, Ontario and five American states): the project's home place decides
                // (finding 71, 2026-10-22; the PMC manifest says United Kingdom)
                if (own.Count > 1 && _options.HomePlace is { } home) own = own.Where(p => Qualified.Contains(p, Qualified.Tokens(home))).ToList();
                if (own.Distinct(StringComparer.Ordinal).Count() == 1) ancestors = own[0];
                _ancestors[segment] = ancestors;
            }
            if (ancestors is not null) return reading with { QualifierAncestors = ancestors };
        }
        return reading;
    }

    /// <summary>
    /// A dated heading in a context that uses the parser for dated strings only ("World War, 1939-1945" in the keywords field) is read as
    /// a person with a lifespan; when the whole heading as written is a label of a concept, a period or an event in any index, it is
    /// that and not a person, and is read again without the parser (finding 80, 2026-10-23).
    /// </summary>
    private async Task<StringReading> WithWholeConceptAsync(SourceString s, StringReading reading, CancellationToken ct)
    {
        if (reading.Lifespan is null) return reading;
        var rule = _options.Contexts.TryGetValue(s.Context, out var r) ? r : _options.DefaultContext;
        if (!rule.ParseDatedOnly || !rule.Classes.Contains(EntityClass.Type)) return reading;
        // the per-source union query: a lookup without a source literal cannot use the partial label indexes and scans the table
        // (the run of 2026-10-23 afternoon slowed to a crawl on the dated keywords strings before this was changed)
        var whole = await store.FindAuthoritiesByLabelsAsync([StringKey.Normalise(s.Raw)], null, null, 20, ct);
        return whole.Any(a => a.Type is nameof(EntityClass.Type) or nameof(EntityClass.Period) or nameof(EntityClass.Event)) ? StringReading.Read(s, rule, null) : reading;
    }

    /// <summary>How a string is read under its context's rule: the name part, keys, classes, lifespan.</summary>
    public StringReading Read(SourceString s)
    {
        var rule = _options.Contexts.TryGetValue(s.Context, out var r) ? r : _options.DefaultContext;
        return StringReading.Read(s, rule, ParserFor(rule.AgentProfile));
    }

    /// <summary>Reads the string and finds what it would be decided as, writing nothing. With <paramref name="includeStore"/> false the store's own entities are not consulted, so an old decision cannot answer itself.</summary>
    public async Task<Evaluation> EvaluateAsync(string dataset, SourceString s, bool includeStore = true, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var reading = await WithQualifierAncestorsAsync(await WithWholeConceptAsync(s, Read(s), ct), ct);
        Time("read", sw);
        if (reading.Key.Length == 0) return new Evaluation(Outcome.Skipped, reading, []);

        // 1. overrides, by the whole string and by the name part
        var over = await store.OverrideForAsync(dataset, s.Context, s.Normalised, ct);
        if (over is null && reading.Key != s.Normalised) over = await store.OverrideForAsync(dataset, s.Context, reading.Key, ct);
        Time("overrides", sw);
        if (over is not null) return new Evaluation(Outcome.Overridden, reading, [], Override: over);
        // a list is not looked up itself (an override on the whole still wins above): its parts are, by DecomposeAsync. A compound
        // heading is looked up whole first, and decomposes below when nothing holds it
        if (reading.Shape == PartsShape.List) return new Evaluation(Outcome.Decomposed, reading, []);

        // 2. the store: entities already minted whose labels match
        var candidates = new List<Candidate>();
        var storeMatches = new Dictionary<string, Entity>(StringComparer.Ordinal);
        if (includeStore)
        {
            foreach (var cls in reading.Classes)
                foreach (var key in reading.Keys)
                    foreach (var e in await store.FindEntitiesByLabelAsync(key, dataset, cls, ct))
                        storeMatches[e.Id] = e;
            foreach (var e in storeMatches.Values)
                candidates.Add(await StoreCandidateAsync(e, reading, ct));
            Time("store", sw);
            // one acceptable entity, or one acceptable entity with authority behind it among provisional lookalikes. An entity
            // minted for a string that is itself awaiting review never answers silently; an entity minted because nothing
            // was found anywhere does, so the same unknown name across contexts becomes one local authority.
            var storeScored = Score(reading, candidates).Where(c => !c.Veto && !c.SetAside && c.Score >= _options.AcceptScore).ToList();
            // in a qualified context ("Heaton, Bradford") an entity answers silently only when its qualifier agrees with the string's, or
            // neither has one: a bare "Heaton" must meet the namesakes in the indexes, not fall to whichever Heaton was aligned first
            if (reading.IsQualified) storeScored = storeScored.Where(c => c.QualifierAgrees == true || (reading.Qualifier is null && c.Candidate.Placement is null)).ToList();
            var firm = storeScored.Where(c => !c.Candidate.Provisional).ToList();
            var chosen = storeScored.Count == 1 ? storeScored[0] : firm.Count == 1 ? firm[0] : null;
            // two of the project's entities, one labelled with the whole heading as written ("Surrealism (Literature)") and one with its
            // bare form ("Surrealism"): the heading names the first (finding 81, 2026-10-23)
            if (chosen is null && reading.LabelOverride is { } wholeLabel && firm.Count > 1)
            {
                var wholeKey = StringKey.Normalise(wholeLabel);
                var ofWhole = firm.Where(c => storeMatches[c.Candidate.EntityId!].Label is { } l && StringKey.Normalise(l) == wholeKey).ToList();
                if (ofWhole.Count == 1) chosen = ofWhole[0];
            }
            if (chosen is not null && chosen.Candidate.ProvisionalQueued) chosen = null;
            if (chosen is not null)
                return new Evaluation(Outcome.MatchedStore, reading, [Group([chosen])], StoreMatch: storeMatches[chosen.Candidate.EntityId!], StoreChoice: chosen);
        }

        // 3. local indexes (remote sources come in step 3b)
        var truncated = false;
        foreach (var source in sources.Where(x => !x.IsRemote))
        {
            var found = await source.FindAsync(reading, ct);
            candidates.AddRange(found.Candidates);
            truncated |= found.Truncated;
            Time("source:" + source.Name, sw);
        }

        var groups = Groups(Score(reading, candidates));
        Time("score", sw);
        var live = groups.Where(g => !g.Vetoed).ToList();
        var top = live.FirstOrDefault();
        // at acceptance score, a group two independent sources carry outranks one source's higher-scoring record (TGN's Waihou at 1.00
        // through its variant "Thames River", against TGN's Thames with LCSH's heading at 0.97): the owner's outvoted-rivals policy,
        // read at acceptance score rather than at an equal score (finding 50, 2026-10-22)
        var strongest = live.Where(g => g.Score >= _options.AcceptScore).OrderByDescending(g => g.Sources.Count).ThenByDescending(g => g.Score).FirstOrDefault();
        if (strongest is not null && top is not null && strongest != top && strongest.Sources.Count >= 2 && top.Sources.Count <= 1) { live.Remove(strongest); live.Insert(0, strongest); top = strongest; }
        // a rival at acceptance score is a disagreement: another authority, or another provisional lookalike when the top is only a lookalike itself
        var topIsFirm = top is not null && top.Members.Any(m => !m.Veto && !m.Candidate.Provisional);
        var second = live.Skip(1).FirstOrDefault(g => !topIsFirm || g.Members.Any(m => !m.Veto && !m.Candidate.Provisional));
        var carried = top is null ? null : CarriedBy(top);
        // outvoted rivals (the owner's policy, 2026-10-21, doc 10 section 7.6): for a place or a concept, a group two independent sources
        // carry beats rivals one source carries at the same score (TGN's Scottish Aberdeen with LoC's "Aberdeen (Scotland)" joined,
        // against four TGN namesakes no LoC heading reaches); never over one of the project's own aligned entities or another
        // two-source group, and never for people and bodies, where sameness of name is weak evidence and the desk decides
        var rivals = top is null ? [] : live.Skip(1).Where(g => g.Score >= _options.AcceptScore).ToList();
        // and for a body when the string's qualifier places a member of the winning group (TGN's National Maritime Museum in London,
        // with LoC's heading through it, against ULAN's three unplaced records): not a name-only tie (finding 60; the owner's
        // policy, 2026-10-22, ceiling 18)
        var placedWinner = reading.Qualifier is not null && top is not null && top.Members.Any(m => !m.Veto && !m.SetAside && m.QualifierAgrees == true);
        var outvoted = top is not null && second is not null && second.Score >= _options.AcceptScore && top.Sources.Count >= 2
            && ((top.Best.Candidate.Class ?? reading.Classes[0]) is EntityClass.Place or EntityClass.Type or EntityClass.Period || placedWinner)
            && rivals.All(g => g.Sources.Count <= 1 && !g.Members.Any(m => !m.Veto && !m.SetAside && m.Candidate.EntityId is not null && !m.Candidate.Provisional));
        if (outvoted)
            groups = groups.Select(g => rivals.Contains(g) ? g with { Members = g.Members.Select(m => m with { Reason = m.Reason + $"; outvoted by a group of {top!.Sources.Count} sources" }).ToList() } : g).ToList();
        // the project's own aligned entity breaks a tie among places (finding 35; the owner's policy, 2026-10-22, ceiling 26): "Cornwall"
        // ties between TGN's county with LoC's heading and the project's own entity, and Ontario's and Connecticut's Cornwalls each
        // with a LoC heading placed through; the earlier pipeline identified the name with the county in this project, and a bare
        // "Cornwall" here is that place. Only among two-source groups at acceptance score, only when one of them holds the entity
        static bool Firm(ScoredCandidate m) => !m.Veto && !m.SetAside && m.Candidate.EntityId is not null && !m.Candidate.Provisional;
        if (top is not null && (top.Best.Candidate.Class ?? reading.Classes[0]) == EntityClass.Place)
        {
            var tied = live.Where(g => g.Score >= _options.AcceptScore && g.Sources.Count >= 2).ToList();
            var withEntity = tied.Where(g => g.Members.Any(Firm)).ToList();
            if (tied.Count >= 2 && withEntity.Count == 1 && !live.Any(g => g != withEntity[0] && g.Score >= _options.AcceptScore && g.Members.Any(Firm)))
            {
                var prior = withEntity[0];
                groups = groups.Select(g => g != prior && !g.Vetoed && g.Score >= _options.AcceptScore ? g with { Members = g.Members.Select(m => m with { Reason = m.Reason + "; the project's own aligned entity breaks the tie" }).ToList() } : g).ToList();
                if (includeStore && await HoldersAsync(dataset, prior.Uris, ct) > 1) return new Evaluation(Outcome.Queued, reading, groups, Truncated: truncated);
                return new Evaluation(Outcome.Accepted, reading, groups, Accepted: prior, AcceptedMethod: DecisionMethod.Vote);
            }
        }
        if (top is not null && top.Score >= _options.AcceptScore && !truncated && carried is not null && (second is null || second.Score < _options.AcceptScore || outvoted))
        {
            // the group's ids already on two different entities of ours means the store disagrees with itself: review, not a vote onto the first
            if (includeStore && await HoldersAsync(dataset, top.Uris, ct) > 1) return new Evaluation(Outcome.Queued, reading, groups, Truncated: truncated);
            return new Evaluation(Outcome.Accepted, reading, groups, Accepted: top, AcceptedMethod: carried);
        }
        // policy: one source's exact match, the only candidate there is (vetoed or not), authority records only, and
        // nothing on the string to check it against (an undated string); see ReconcilerOptions.AcceptUniqueExactSingleSource
        // a name of one word ("Mr Farebrother" read as "Farebrother") is not exact enough to carry this alone: a person decides
        // a group of records set aside by preference (another vocabulary's two exact terms, a record outside the wanted collections) is not a namesake
        // a dated string whose only candidate is a source's one exact record of the name, undated, is that record all the same: the
        // source knows one of the name and dates none other (finding 24; the owner's policy, 2026-10-21, ceiling 45 strings, the dated
        // name headings of the keywords field); a second record of the name anywhere, dated or not, is a second group and the desk's
        // the exact-single rule reads the best group that is not the project's own provisional placeholder (finding 68, 2026-10-22: the
        // placeholder minted for "E & H Lumley" stood above LoC's "E. & H. Lumley (Firm)" and took the rule's eye); and a record dated only
        // by a window of possibility that holds the string's dates is undated for finding 24 (finding 65: ULAN's "MacDonald, Biddy",
        // 1855~-~1968, for "MacDonald, Biddy, 1871-1952")
        var exactTop = live.FirstOrDefault(g => g.Members.Any(m => !m.SetAside && !(m.Candidate.Provisional && m.Candidate.EntityId is not null))) ?? top;
        // a string with working dates ("Dodd, Phyllis, op 1959") against a source's one record of the name whose lifespan does not conflict
        // (LoC's "Dodd, Phyllis, 1899-1995") is that record for the same reason: the source knows one, and nothing contradicts (finding 75,
        // 2026-10-23); a record exact only through a variant is not the source's record of the name (finding 74)
        var loneUndated = reading.Lifespan is not null && exactTop is { Members.Count: 1 } && !exactTop.Best.Veto && !exactTop.Best.Candidate.ByVariant
            && (exactTop.Best.Candidate.Lifespan is null or { IsDated: false } || reading.Lifespan.IsWorking);
        // a provisional entity of the project's own (a placeholder minted for the name, awaiting review) is a lookalike, not a second
        // group: ULAN's one "England & Co." is an exact-single beside the project's queued "England & Co" (finding 38, 2026-10-22)
        // a group below the review score is noise, not a rival (ULAN's two "Williams, Peter" at 0.40 beside LoC's exact dated heading); a
        // vetoed group still counts, as the policy says (finding 44, 2026-10-22)
        // and for a concept or a place, a rival below acceptance score is a lookalike too: AAT's "Decorated Style" beside LCSH's
        // "Architecture, Gothic--Decorated style" at 0.85 (finding 48; the owner's policy, 2026-10-22, ceiling 180; not for people and
        // bodies, where "Smith, John" beside "Smith, John Raphael" is the namesake risk)
        var rivalFloor = exactTop is not null && (exactTop.Best.Candidate.Class ?? reading.Classes[0]) is EntityClass.Type or EntityClass.Place or EntityClass.Period ? _options.AcceptScore : _options.ReviewScore;
        // a name of one word is not exact enough for a person or a body ("Mr Farebrother" read as "Farebrother"), unless the record is
        // plainly a firm by its own label ("Remondini (Firm)", "Macmillan, Inc."): a one-word firm name is a firm's name (finding 42,
        // 2026-10-22)
        var wordsEnough = reading.Classes[0] is not (EntityClass.Person or EntityClass.Group) || StringKey.Normalise(reading.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
            || (exactTop is not null && exactTop.Best.Candidate.Class == EntityClass.Group && NameMatcher.PlainlyABody(exactTop.Best.Candidate.Label));
        if (_options.AcceptUniqueExactSingleSource && exactTop is not null && !truncated && groups.Count(g => (g.Vetoed || g.Score >= rivalFloor) && g.Members.Any(m => !m.SetAside && !(m.Candidate.Provisional && m.Candidate.EntityId is not null))) == 1 && (reading.Lifespan is null || loneUndated)
            && wordsEnough
            && exactTop.Score >= _options.AcceptScore && exactTop.Best.Candidate.NameScore >= 0.97
            && exactTop.Members.All(m => !m.Veto && !m.Candidate.Provisional && m.Candidate.EntityId is null)
            // a heading that carries dates was disambiguated from namesakes by its cataloguer: an undated string cannot claim it alone
            // unless the string is that dated heading, dates and all ("Williams, Peter, 1914-1995" whole in the keywords field; finding 45)
            // (and a string with working dates that the heading's lifespan holds is not an undated string: finding 75)
            && !exactTop.Members.Any(m => m.Candidate.Disambiguated && NameMatcher.Score(reading.String.Raw, [m.Candidate.Label ?? ""]) < 0.97
                && !(reading.Lifespan is { IsWorking: true } && m.Candidate.Lifespan is { IsEmpty: false }))
            // a qualifier that the record does not bear out ("Princeton, N.J." against a Princeton elsewhere) is for the desk
            && exactTop.Best.QualifierAgrees != false)
        {
            if (includeStore && await HoldersAsync(dataset, exactTop.Uris, ct) > 1) return new Evaluation(Outcome.Queued, reading, groups, Truncated: truncated);
            return new Evaluation(Outcome.Accepted, reading, groups, Accepted: exactTop, AcceptedMethod: DecisionMethod.ExactSingle);
        }
        // nothing holds a compound heading whole: its parts decide it
        if (reading.Parts is not null) return new Evaluation(Outcome.Decomposed, reading, groups, Truncated: truncated);
        if (top is not null && top.Score >= _options.ReviewScore)
            return new Evaluation(Outcome.Queued, reading, groups, Truncated: truncated);
        return new Evaluation(_options.MintWhenNothingFound ? Outcome.Minted : Outcome.Deferred, reading, groups, Truncated: truncated);
    }

    /// <summary>How many distinct live entities of the dataset hold any of the URIs as an equivalent.</summary>
    private async Task<int> HoldersAsync(string dataset, IReadOnlyList<string> uris, CancellationToken ct)
    {
        var holders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var uri in uris.Where(u => !scheme.Owns(u)))
            foreach (var e in await store.FindEntitiesByEquivalentAsync(uri, ct))
                if (e.Dataset == dataset && e.MergedInto is null) holders.Add(e.Id);
        return holders.Count;
    }

    /// <summary>A store entity as a candidate: its lifespan from itself or, when it has none (an imported entity, say), from the authority records behind its equivalents; and whether it is a lookalike awaiting review.</summary>
    private async Task<Candidate> StoreCandidateAsync(Entity e, StringReading reading, CancellationToken ct)
    {
        var labels = e.Label is null ? e.Labels : e.Labels.Prepend(e.Label);
        var lifespan = e.Lifespan is { IsEmpty: false } ? e.Lifespan : null;
        if (lifespan is null)
            foreach (var q in e.Equivalents.Where(q => q.Source is "ulan" or "loc").Take(3))
                if (await store.GetAuthorityAsync(q.Uri, ct) is { Lifespan: { IsEmpty: false } } record) { lifespan = record.Lifespan; break; }
        var provisional = e.Status == EntityStatus.Provisional;
        var queued = provisional && await store.HasValidDecisionAsync(e.Id, DecisionMethod.Queued, ct);
        // an entity's own placement is the qualifier its label carries ("Heaton, Bradford"), kept there by EntityForGroupAsync and MintAsync
        // an entity's own placement is the qualifier its label carries ("Heaton, Bradford", "Hampton Court (Richmond upon Thames, London,
        // England)"), whenever the string has a qualifier to check it against (finding 70, 2026-10-22)
        var placement = e.Label is null ? null
            : reading.IsQualified ? Qualified.Split(e.Label).Qualifier
            : reading.Qualifier is not null ? Qualified.PlacementOf(null, labels, placesOnly: true) : null;
        return new Candidate(e.Id, "store", e.Label, e.Class, lifespan ?? e.Lifespan, e.Equivalents.Select(q => q.Uri).ToArray(), "store", NameMatcher.Score(reading.Text, labels), e.Id, e.Nationalities, provisional, queued, Placement: placement,
            PlaceType: e.Class == EntityClass.Place ? Qualified.TypeOf(labels) : null);
    }

    /// <summary>Writes an evaluation: the decision with its evidence and, where needed, the entity.</summary>
    public async Task CommitAsync(string dataset, SourceString s, Evaluation e, string? runId = null, CancellationToken ct = default)
    {
        var reading = e.Reading;
        var extra = e.Truncated ? new { truncated = true } : null;
        switch (e.Outcome)
        {
            case Outcome.Overridden:
            {
                var entity = await EntityForTargetAsync(dataset, e.Override!.Target, reading, runId, ct);
                await store.DecideAsync(s.Id, entity.Id, DecisionMethod.Override, Evidence("override", reading, [], new { e.Override.Target, e.Override.Source, e.Override.Note }), runId: runId, ct: ct);
                break;
            }
            case Outcome.MatchedStore:
                await store.DecideAsync(s.Id, e.StoreMatch!.Id, DecisionMethod.ExactName, Evidence("store", reading, e.Groups, null), runId: runId, ct: ct);
                break;
            case Outcome.Accepted:
            {
                var method = e.AcceptedMethod ?? DecisionMethod.Vote;
                var entity = await EntityForGroupAsync(dataset, reading, e.Accepted!, runId, ct, method);
                await store.DecideAsync(s.Id, entity.Id, method, Evidence(method, reading, e.Groups, null), runId: runId, ct: ct);
                break;
            }
            case Outcome.Queued:
            {
                var provisional = await MintAsync(dataset, reading, runId, ct);
                await store.DecideAsync(s.Id, provisional.Id, DecisionMethod.Queued, Evidence("queued", reading, e.Groups, extra), runId: runId, ct: ct);
                break;
            }
            case Outcome.Minted:
            {
                var fresh = await MintAsync(dataset, reading, runId, ct);
                await store.DecideAsync(s.Id, fresh.Id, DecisionMethod.Minted, Evidence("minted", reading, e.Groups, extra), runId: runId, ct: ct);
                break;
            }
        }
    }

    // ---- scoring and grouping ----------------------------------------------------------------------------------

    /// <summary>Name score, then dates (agreement lifts, conflict vetoes, dates that cannot be compared change nothing), type (a mismatch vetoes), nationality (agreement lifts).</summary>
    public IReadOnlyList<ScoredCandidate> Score(StringReading reading, IEnumerable<Candidate> candidates) => Score(reading, candidates, _options.MaxPersonLifetimeYears);

    public static IReadOnlyList<ScoredCandidate> Score(StringReading reading, IEnumerable<Candidate> candidates, int maxPersonLifetimeYears)
    {
        var list = new List<ScoredCandidate>();
        foreach (var candidate in candidates)
        {
            var c = candidate;
            var notes = new List<string>();
            // a heading split by its parenthetical (finding 13) still names the record whose label is the whole heading: LCSH's
            // "Grand tours (Education)" is exact for the string "Grand tours (Education)", not an 0.85 lookalike of "Grand tours"
            // (doc 10 section 7.6 finding 19)
            if (reading.LabelOverride is { } whole && c.NameScore < 0.97 && c.Label is not null && NameMatcher.Score(whole, [c.Label]) >= 0.97)
            { c = c with { NameScore = 1.0 }; notes.Add("the whole heading"); }
            var score = c.NameScore;
            var reasons = new List<string> { $"name {c.NameScore:0.00}" };
            reasons.AddRange(notes);
            var veto = false;
            var datesAgree = false;
            if (c.Class is { } cls && !reading.Classes.Contains(cls)) { veto = true; reasons.Add($"type {cls} not allowed here"); }
            // a heading that carries a lifespan names a person or a body: TGN's Gilbert Rivers are not "Gilbert, 1943-" (finding 31, 2026-10-22)
            else if (reading.HasLifespan && c.Class == EntityClass.Place) { veto = true; reasons.Add("a dated heading names a person or body, not a place"); }
            if (reading.Lifespan is { IsEmpty: false } rl && c.Lifespan is { IsEmpty: false } cl)
            {
                var person = (c.Class ?? reading.Classes[0]) == EntityClass.Person;
                switch (rl.Compare(cl, person ? maxPersonLifetimeYears : null))
                {
                    case DateAgreement.Agree: score = Math.Min(1, score + 0.05); datesAgree = true; reasons.Add("dates agree"); break;
                    case DateAgreement.Conflict: veto = true; reasons.Add($"dates conflict ({rl.Text} vs {cl.Text})"); break;
                    default: reasons.Add($"dates not comparable ({rl.Text} vs {cl.Text})"); break;
                }
            }
            // a heading the profile read as a person with a lifespan names a person: ULAN's firm "Thomas, Thomas" is not "Thomas, Thomas,
            // 1817-1888" unless its own dates agree (finding 64, 2026-10-22; finding 31 did the same for places)
            if (!veto && reading.HasLifespan && reading.Classes[0] == EntityClass.Person && c.Class == EntityClass.Group && !datesAgree) { veto = true; reasons.Add("a dated personal heading names a person, not a body"); }
            if (reading.Nationalities.Count > 0 && c.Nationalities is { Count: > 0 } cn && cn.Intersect(reading.Nationalities, StringComparer.Ordinal).Any())
            { score = Math.Min(1, score + 0.03); reasons.Add("nationality agrees"); }
            // the qualifier as written first; its TGN ancestors (finding 56) only to widen agreement, never to narrow it: "Raglan Castle
            // (Wales)" against LCSH's "(Raglan, Wales)" agrees on "Wales" alone, ancestors or no (2026-10-22)
            var qualifierAgrees = Qualified.Agrees(reading.Qualifier, c.Placement);
            if (qualifierAgrees == false && reading.QualifierAncestors is not null && Qualified.Agrees(reading.QualifierForPlacement, c.Placement) == true) qualifierAgrees = true;
            // a qualifier TGN knows as a county or above ("Herefordshire") names a real place, and a record placed elsewhere is not the
            // string's whether or not any record is placed: "Hampton Court (Herefordshire)" is neither the Jamaican nor the Richmond
            // one, and is minted (finding 66, 2026-10-22)
            if (qualifierAgrees == false && reading.QualifierAncestors is not null && !veto) { veto = true; reasons.Add("the qualifier names a known place and this is elsewhere"); }
            // and a string placed in a known place ("Newcastle (Bridgend, Wales)") is a place, a building or a body there, not a person:
            // LoC's "Captain Newcastle, approximately 1701-1756" through its variant is not it (finding 69, 2026-10-22)
            if (reading.QualifierAncestors is not null && c.Class == EntityClass.Person && !veto) { veto = true; reasons.Add("a string placed in a known place is not a person"); }
            switch (qualifierAgrees)
            {
                case true:
                    score = Math.Min(1, score + 0.05); reasons.Add($"qualifier places it ({c.Placement})");
                    // a qualified heading the string's qualifier places is exact on its name: "Carlton House (London, England)" for
                    // "Carlton House (London)" is the name exact and the parenthetical read as the placement, which the single-source
                    // rule may carry, not an 0.85 lookalike (finding 20)
                    if (c.NameScore < 0.97 && c.Label is not null && Qualified.SplitParenthetical(c.Label) is var (bare, inParentheses) && inParentheses is not null && NameMatcher.Score(reading.Text, [bare]) >= 0.97)
                    { c = c with { NameScore = 0.97 }; score = 1.0; reasons.Add("the name exact once its parenthetical is read as the placement"); }
                    break;
                case false: reasons.Add($"qualifier not placed ({reading.Qualifier} vs {c.Placement})"); break;
            }
            list.Add(new ScoredCandidate(c, Math.Round(score, 3), veto, string.Join("; ", reasons), datesAgree, qualifierAgrees));
        }
        // a string that carries dates and a record of a source that agrees with them: that source's other records of the name
        // are namesakes, not the dated person (LoC's headings are unique, so its undated "Doyle, Richard" beside "Doyle, Richard,
        // 1824-1883" is another Richard Doyle by LoC's own rule); the owner's policy, 2026-10-21, for the undated ones; widened
        // 2026-10-22 (finding 54) to the dated ones that merely do not conflict (LoC's "Clarke, Geoffrey, -1950" beside its
        // "Clarke, Geoffrey, 1924-2014") and to ULAN's windows of possibility (1871~-~2030), by the same rule of uniqueness
        if (reading.HasLifespan)
            foreach (var source in list.Where(m => m.DatesAgree && !m.Veto).Select(m => m.Candidate.Source).Distinct(StringComparer.Ordinal).ToList())
                for (var i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    if (b.Veto || b.DatesAgree || b.Candidate.Source != source || b.Candidate.EntityId is not null) continue;
                    var undated = b.Candidate.Lifespan is null || !b.Candidate.Lifespan.IsDated;
                    list[i] = b with { Veto = true, Reason = b.Reason + $"; {source}'s dated record of the name agrees with the string's dates: this {(undated ? "undated" : "other")} one is a namesake" };
                }
        // an undated record that is its source's only exact record of the name is the person another source dates: "LoC knows one
        // Marit Guinness Aschan; ULAN dates her", and the two vote (doc 10, section 7.6 finding 14; the owner's policy, 2026-10-21)
        // and two sources that each date one record of the name as the string does date the same person ("Friedmann, Gloria, 1950-"
        // in ULAN and LoC, unlinked), so the two vote (finding 16, 2026-10-21)
        if (reading.HasLifespan && list.FirstOrDefault(m => m.DatesAgree && !m.Veto && !m.SetAside) is { } dated)
            foreach (var source in list.Select(m => m.Candidate.Source).Distinct(StringComparer.Ordinal).ToList())
            {
                if (source == dated.Candidate.Source || source == "store") continue;
                var agreeing = list.Select((m, i) => (m, i)).Where(x => x.m.Candidate.Source == source && x.m.DatesAgree && !x.m.Veto && !x.m.SetAside && x.m.JoinedTo is null && x.m.Candidate.NameScore >= 0.97).ToList();
                if (agreeing.Count == 1 && !SameThing(agreeing[0].m, dated) && !(agreeing[0].m.Candidate.Class is { } ac && dated.Candidate.Class is { } dc && ac != dc))
                {
                    list[agreeing[0].i] = agreeing[0].m with { JoinedTo = dated.Candidate.Uri, Reason = agreeing[0].m.Reason + $"; dated as {dated.Candidate.Source}'s record is" };
                    continue;
                }
                // its source's only exact record once the namesakes vetoed by their dates are gone, and a record ULAN dates only by a
                // window of possibility (1955~-~2035) is undated for this purpose (finding 55, 2026-10-22)
                // and a record exact only through a variant ("Morgan, John Lester") is not the source's record of the name (finding 74)
                var exact = list.Select((m, i) => (m, i)).Where(x => x.m.Candidate.Source == source && x.m.Candidate.NameScore >= 0.97 && !x.m.Veto && !x.m.Candidate.ByVariant).ToList();
                if (exact.Count != 1) continue;
                var (lone, at) = exact[0];
                // and a lone record dated without conflict ("Bolton, James", ULAN's 1699~-1799, beside LoC's agreeing "active 1775-1795" for
                // "Bolton, James, fl. 1775-1795") counts as the undated one does: nothing contradicts, and a conflict would have vetoed it
                // (finding 79, 2026-10-23; before it only an undated record or a window joined)
                if (lone.SetAside || lone.JoinedTo is not null) continue;
                if (lone.Candidate.Class is { } cls && dated.Candidate.Class is { } datedClass && cls != datedClass) continue;
                list[at] = lone with { JoinedTo = dated.Candidate.Uri, Reason = lone.Reason + $"; {source}'s one record of the name, which {dated.Candidate.Source} dates" };
            }
        // a dated string that no record dates tightly, with two sources each holding exactly one live exact record of the name, neither
        // at odds with the dates (ULAN's "Carwitham, John" by a window of 1683~-~1783, LoC's "Carwitham, J. (John)" undated, for
        // "Carwitham, John, c 1700- c1760"): the two name the same person and vote, as finding 24 takes one such record alone
        // (finding 72, 2026-10-22). Not when any record agrees tightly: findings 14 and 16 have that
        if (reading.HasLifespan && !list.Any(m => m.DatesAgree && !m.Veto && !m.SetAside))
        {
            var bySource = list.Select((m, i) => (m, i))
                .Where(x => !x.m.Veto && !x.m.SetAside && x.m.JoinedTo is null && x.m.Candidate.Source != "store" && x.m.Candidate.NameScore >= 0.97 && !x.m.Candidate.ByVariant && x.m.Candidate.Class is EntityClass.Person or null)
                .GroupBy(x => x.m.Candidate.Source, StringComparer.Ordinal).ToList();
            var exactBySource = list.Where(m => m.Candidate.Source != "store" && m.Candidate.NameScore >= 0.97 && !m.Candidate.ByVariant).GroupBy(m => m.Candidate.Source, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
            if (bySource.Count == 2 && bySource.All(g => g.Count() == 1 && exactBySource[g.Key] == 1))
            {
                var (a, _) = bySource[0].Single(); var (b, at) = bySource[1].Single();
                if (!SameThing(a, b)) list[at] = b with { JoinedTo = a.Candidate.Uri, Reason = b.Reason + $"; {b.Candidate.Source}'s one record of the name, as {a.Candidate.Source}'s one is, neither at odds with the dates" };
            }
        }
        if (reading.Collections is { Count: > 0 } wanted) PreferCollections(wanted, list);
        // a heading the home vocabulary holds whole beside its bare form ("Surrealism (Literature)" beside "Surrealism" in LCSH) names the
        // whole heading's record; the home vocabulary's other records exact only on the bare name are its other concepts, set aside, so
        // that the home join below has one term to carry the others to (finding 81, 2026-10-23)
        if (reading.LabelOverride is { } wholeHeading && reading.Home is { } homeVocabulary)
        {
            var wholeExact = list.Where(m => !m.Veto && !m.SetAside && m.Candidate.Source == homeVocabulary && m.Candidate.Label is not null && NameMatcher.Score(wholeHeading, [m.Candidate.Label]) >= 0.97).ToList();
            if (wholeExact.Count == 1)
                for (var i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    if (b.Veto || b.SetAside || b.Candidate.Source != homeVocabulary || b.Candidate.NameScore < 0.97 || b.Candidate.Uri == wholeExact[0].Candidate.Uri) continue;
                    list[i] = b with { SetAside = true, Reason = b.Reason + $"; {homeVocabulary} holds the whole heading as {wholeExact[0].Candidate.Label}: this bare heading is another concept" };
                }
        }
        if (reading.Home is not null) JoinToHome(reading.Home, list);
        // in a subject field a bare common noun names the idea, and a place would carry its qualifier: AAT's "peers (aristocrats)"
        // against TGN's two hamlets called Peers is the concept, the hamlets set aside (finding 63; the owner's policy, 2026-10-22,
        // ceiling 20). Only when the one exact concept record stands against inhabited places alone: a nation or a county of the
        // name ("Turkey") is another matter, and so is any record of another source
        if (reading.Home is not null && reading.Qualifier is null && !reading.HasLifespan)
        {
            // only exact records count on either side: LoC's "Peers (Musical group)" at 0.85 is a lookalike, finding 48's business, not a
            // rival of another source (the owner's evidence query, 2026-10-22 evening)
            var live = list.Select((m, i) => (m, i)).Where(x => !x.m.Veto && !x.m.SetAside && x.m.Candidate.NameScore >= 0.97).ToList();
            var concepts = live.Where(x => x.m.Candidate.Source is "aat" or "lcsh" && x.m.Candidate.Class is EntityClass.Type or EntityClass.Period or null).ToList();
            var others = live.Where(x => !concepts.Contains(x)).ToList();
            if (concepts.Count == 1 && others.Count > 0 && others.All(x => x.m.Candidate.Source == "tgn" && x.m.Candidate.Class == EntityClass.Place && Qualified.LevelOf(x.m.Candidate.PlaceType) is null or 2))
                foreach (var (b, i) in others)
                    list[i] = b with { SetAside = true, Reason = b.Reason + $"; {concepts[0].m.Candidate.Source} {concepts[0].m.Candidate.Label} decides: a place of the name, not the idea" };
        }
        // for a body, two sources that each have exactly one exact record of the name, and no other, name the same body and vote:
        // ULAN's "National Gallery" and LoC's "National Gallery (Great Britain)" for "National Gallery London" (finding 40; the
        // owner's policy, 2026-10-22, ceiling 35). Bodies only: a person's namesakes are too common. As finding 16 does with dates.
        // In a context that reads bodies among other things (keywords: "Galleria Spada (Rome, Italy)", ULAN's and LoC's one record each,
        // both bodies by their own vocabularies), the records' own classes say it is a body (finding 78, 2026-10-23)
        var bodyContext = reading.Classes.Count > 0 && reading.Classes[0] == EntityClass.Group;
        if (!reading.HasLifespan && reading.Classes.Contains(EntityClass.Group))
        {
            var exactBySource = list.Select((m, i) => (m, i))
                .Where(x => !x.m.Veto && !x.m.SetAside && x.m.JoinedTo is null && x.m.Candidate.Source != "store" && x.m.Candidate.NameScore >= 0.97
                    && (bodyContext ? x.m.Candidate.Class is null or EntityClass.Group : x.m.Candidate.Class == EntityClass.Group))
                .GroupBy(x => x.m.Candidate.Source, StringComparer.Ordinal).ToList();
            var anyOtherExact = !bodyContext && list.Any(m => !m.Veto && !m.SetAside && m.Candidate.Source != "store" && m.Candidate.NameScore >= 0.97 && m.Candidate.Class != EntityClass.Group);
            if (exactBySource.Count == 2 && exactBySource.All(g => g.Count() == 1) && !anyOtherExact)
            {
                var (a, _) = exactBySource[0].Single(); var (b, at) = exactBySource[1].Single();
                if (!SameThing(a, b)) list[at] = b with { JoinedTo = a.Candidate.Uri, Reason = b.Reason + $"; {b.Candidate.Source}'s one record of the body, as {a.Candidate.Source}'s one is" };
            }
        }
        // a record of another source whose placement sits inside a holding record's is the same place at less depth (LoC's
        // "Princes Risborough (England)", the store's entity labelled from it, under TGN's Princes Risborough in Buckinghamshire,
        // England): it is placed through that record and grouped with it. The holders are the candidates the string's qualifier
        // placed, or, when the string carries no qualifier, every exact place record that says where it is: LoC's "Abbeyleix
        // (Ireland)" is TGN's Abbeyleix in Laois, Ireland whether the string says so or not, and TGN and LoC then agree. Inside
        // two holders of one source (a city and its district with the same parent string) it is placed through neither; the
        // desk tells them apart.
        var placed = list.Where(m => m.QualifierAgrees == true && !m.Veto && !m.SetAside).ToList();
        var holders = reading.Qualifier is not null ? placed
            : list.Where(m => !m.Veto && !m.SetAside && m.Candidate.Placement is not null && m.Candidate.Class is EntityClass.Place or null && m.Candidate.NameScore >= 0.97).ToList();
        if (holders.Count == 0) return list;
        var inside = new HashSet<int>();
        for (var i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (b.Veto || b.SetAside || b.Candidate.Placement is null || b.JoinedTo is not null) continue;
            if (reading.Qualifier is null && (b.Candidate.Class is not (EntityClass.Place or null) || b.Candidate.NameScore < 0.85)) continue;
            // a heading's placement may be several parentheticals ("Great Britain; London, England", since finding 58): each is a
            // placement of its own, and the record is inside a holder when any one of them is
            var parts = b.Candidate.Placement.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Qualified.Tokens).Where(w => w.Count > 0).ToList();
            if (parts.Count == 0) continue;
            var through = holders.Where(a => a.Candidate.Uri != b.Candidate.Uri && a.Candidate.Source != b.Candidate.Source && parts.Any(words => Qualified.Contains(a.Candidate.Placement, words))).ToList();
            // a holder this loop has already joined to another holder is that holder (the project's "Cornwall (England : County)"
            // placed through TGN's Cornwall is not a second Cornwall for LoC's heading to be "inside"): read the joins made so far
            through = through.Select(a => list.First(m => m.Candidate.Uri == a.Candidate.Uri)).ToList();
            through = through.Where(a => a.JoinedTo is null || !through.Any(o => o.Candidate.Uri == a.JoinedTo)).ToList();
            if (through.Count == 0) continue;
            inside.Add(i);
            if (through.Count == 1) { list[i] = b with { JoinedTo = through[0].Candidate.Uri, Reason = b.Reason + $"; placed through {through[0].Candidate.Source} {through[0].Candidate.Label}" }; continue; }
            // inside several holders (TGN's New York the state and New York the city, both in New York): the heading's level decides.
            // LoC's type word after " : " names it ("County", "Township"); a qualified heading without one is the inhabited place,
            // LoC's default; TGN's placeType says each holder's level, and the heading joins the one holder at its level
            // (the TGN level preference, doc 10 section 7.6, built 2026-10-21)
            var level = HeadingLevel(b.Candidate);
            var atLevel = through.Where(a => level is not null && Qualified.LevelOf(a.Candidate.PlaceType) == level).ToList();
            // no holder at the heading's level (LoC's "Cumbria (England)" has no type word, but Cumbria is a county and so is Westmorland,
            // which answers through its variant): the holder whose own name the heading is, if one (finding 49, 2026-10-22)
            var byName = atLevel.Count == 1 ? atLevel : (atLevel.Count > 1 ? atLevel : through).Where(a => NameMatcher.Score(reading.Text, [a.Candidate.Label ?? ""]) >= 0.97).ToList();
            // two inhabited places of the name (Portsmouth the city and a hamlet in Calderdale): the one that gives its name to the
            // division above it, whose parent string begins with the name ("Portsmouth, England, ..."), is the known one (finding 61)
            var namesake = byName.Count > 1 ? byName.Where(a => a.Candidate.Placement is { } p && NameMatcher.Score(reading.Text, [p.Split(',')[0].Trim()]) >= 0.97).ToList() : [];
            list[i] = atLevel.Count == 1
                ? b with { JoinedTo = atLevel[0].Candidate.Uri, Reason = b.Reason + $"; placed through {atLevel[0].Candidate.Source} {atLevel[0].Candidate.Label}, the {Qualified.LevelName(level!.Value)} among {through.Count}" }
                : byName.Count == 1
                ? b with { JoinedTo = byName[0].Candidate.Uri, Reason = b.Reason + $"; placed through {byName[0].Candidate.Source} {byName[0].Candidate.Label}, whose own name it is among {through.Count}" }
                : namesake.Count == 1
                ? b with { JoinedTo = namesake[0].Candidate.Uri, Reason = b.Reason + $"; placed through {namesake[0].Candidate.Source} {namesake[0].Candidate.Label}, the one of {through.Count} that names the division above it" }
                : b with { Reason = b.Reason + $"; inside {through.Count} placed candidates" };
        }
        // an unqualified LoC heading of a place ("China", "Canada") is one LoC gives to a place unique in its world: among TGN
        // namesakes of differing level it is the nation, or the highest level present when there is no nation and that level is
        // held by one record (the TGN level preference; hamlets called China and Canada lose)
        if (reading.Qualifier is null)
            for (var i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b.Veto || b.SetAside || b.JoinedTo is not null || b.Candidate.Placement is not null || b.Candidate.Source is not ("loc" or "lcsh") || b.Candidate.Class != EntityClass.Place || b.Candidate.NameScore < 0.97) continue;
                var levelled = holders.Where(a => a.Candidate.Source == "tgn" && Qualified.LevelOf(a.Candidate.PlaceType) is not null).Select(a => (Holder: a, Level: Qualified.LevelOf(a.Candidate.PlaceType)!.Value)).ToList();
                if (levelled.Select(x => x.Level).Distinct().Count() < 2) continue;
                var top = levelled.Max(x => x.Level);
                if (top < 4) continue;
                var highest = levelled.Where(x => x.Level == top).ToList();
                if (highest.Count != 1) continue;
                list[i] = b with { JoinedTo = highest[0].Holder.Candidate.Uri, Reason = b.Reason + $"; an unqualified heading: the {Qualified.LevelName(top)} among {levelled.Count} TGN namesakes" };
            }
        if (reading.Qualifier is null) return list;
        // the project's one entity of the name with no placement of its own ("Adelaide" beside "Adelaide, South Australia") is the
        // placed record's own entity: it joins the placed group and gains the id, as the project's LoC-labelled entities do
        var anchors = list.Where(m => !m.Veto && m.QualifierAgrees == true && m.JoinedTo is null).ToList();
        var own = list.Select((m, i) => (m, i)).Where(x => !x.m.Veto && x.m.Candidate.EntityId is not null && x.m.Candidate.Placement is null && x.m.Candidate.NameScore >= 0.97 && x.m.JoinedTo is null && !x.m.Candidate.ProvisionalQueued).ToList();
        // several placed records of the name ("N.Y." places TGN's New York the city and the county alike): the one a heading joined,
        // by its level or its placement, is the place the string names, and the project's one bare entity joins it there (2026-10-22)
        ScoredCandidate? anchor = anchors.Count == 1 ? anchors[0] : null;
        if (anchor is null && anchors.Count > 1)
        {
            var ranked = anchors.Select(a => (a, joined: list.Count(m => m.JoinedTo == a.Candidate.Uri))).OrderByDescending(x => x.joined).ToList();
            if (ranked[0].joined > ranked[1].joined) anchor = ranked[0].a;
        }
        if (anchor is not null && own.Count == 1)
            list[own[0].i] = own[0].m with { JoinedTo = anchor.Candidate.Uri, Reason = own[0].m.Reason + $"; the project's one {reading.Text} joins the placed {anchor.Candidate.Source} record" + (anchors.Count > 1 ? $", the one of {anchors.Count} a heading joined" : "") };
        // when the qualifier places one candidate, the namesakes it does not place are not what the string names
        for (var i = 0; i < list.Count; i++)
            if (list[i].QualifierAgrees == false && !inside.Contains(i) && !list[i].Veto) list[i] = list[i] with { Veto = true, Reason = list[i].Reason + "; the qualifier places another candidate" };
        return list;
    }

    /// <summary>
    /// A vocabulary's own sorting of its records decides among its namesakes: LCSH holds "Catalogs" as a topic heading (in
    /// its collection LCSHAuthorizedHeadings), a topical subdivision (TopicSubdivisions) and a form subdivision
    /// (GenreFormSubdivisions, both also in Subdivisions), and the context says which it wants, in order (the subdivisions
    /// of a heading: GenreFormSubdivisions then Subdivisions; the heads: LCSHAuthorizedHeadings). Among one source's exact
    /// matches that carry collections, those in the earliest wanted collection stand and the others are vetoed; a lone
    /// record, records without collections and sources without them are left alone. Runs before the home rule, so the
    /// home vocabulary's one exact record is the one wanted here.
    /// </summary>
    private static void PreferCollections(IReadOnlyList<string> wanted, List<ScoredCandidate> list)
    {
        int Rank(Candidate c) { for (var i = 0; i < wanted.Count; i++) if (c.Collections!.Contains(wanted[i], StringComparer.OrdinalIgnoreCase)) return i; return wanted.Count; }
        foreach (var source in list.Where(m => m.Candidate.Collections is { Count: > 0 }).Select(m => m.Candidate.Source).Distinct(StringComparer.Ordinal).ToList())
        {
            var exact = list.Select((m, i) => (m, i)).Where(x => !x.m.Veto && !x.m.SetAside && x.m.Candidate.Source == source && x.m.Candidate.Collections is { Count: > 0 } && x.m.Candidate.NameScore >= 0.97).ToList();
            if (exact.Count < 2) continue;
            var best = exact.Min(x => Rank(x.m.Candidate));
            if (best == wanted.Count) continue;
            foreach (var (m, i) in exact)
                if (Rank(m.Candidate) > best) list[i] = m with { SetAside = true, Reason = m.Reason + $"; {source}'s {wanted[best]} record of the name is the one wanted here" };
        }
    }

    /// <summary>
    /// The context's home vocabulary decides: when exactly one of its records matches the name exactly, another vocabulary's
    /// one exact term on the same name (or the store's own entities under that label) is that concept's equivalent, not a
    /// rival group: "Juvenile literature" is LCSH sh85071184 carrying AAT's "children's literature", where the two were
    /// rival groups and the string was queued. A vocabulary with several exact terms of the name (AAT's two "private
    /// collections") carries none of them, and they are set aside: the home record stands whatever the other holds, so
    /// indexing AAT's bare terms cannot make a string the home vocabulary knows ambiguous. Two exact home records stay
    /// rivals for the desk, and so does a record of another class: LCSH's topic "Dance" carries AAT's concept, not ULAN's
    /// George Dance or LoC's Dance Association, which match the name exactly through a variant and are rivals of another
    /// kind. A record the home record's own links say is the same thing is left as it is: it is in the group already.
    /// </summary>
    private static void JoinToHome(string home, List<ScoredCandidate> list)
    {
        var exact = list.Where(m => !m.Veto && !m.SetAside && m.Candidate.Source == home && m.Candidate.NameScore >= 0.97).ToList();
        if (exact.Count != 1) return;
        var h = exact[0];
        bool Eligible(ScoredCandidate b) => !b.Veto && !b.SetAside && b.Candidate.Source != home && b.Candidate.NameScore >= 0.97 && b.JoinedTo is null
            && !(b.Candidate.Class is { } cls && h.Candidate.Class is { } homeClass && cls != homeClass);
        // another source's exact record of another class is another thing of the name: LoC's "Hutchinson (Firm)" beside LCSH's topic,
        // ULAN's "Dance" beside LCSH's "Dance" (finding 41, 2026-10-22: finding 39 had made such bodies exact and queued thirty
        // headings); set aside, not vetoed, as the "Dance" class check of 2026-10-20 intended. Not the project's own entities
        for (var i = 0; i < list.Count; i++)
        {
            var b = list[i];
            if (b.Veto || b.SetAside || b.JoinedTo is not null || b.Candidate.Source == home || b.Candidate.Source == "store" || b.Candidate.NameScore < 0.97 || SameThing(b, h)) continue;
            if (b.Candidate.Class is { } cls && h.Candidate.Class is { } homeClass && cls != homeClass)
                list[i] = b with { SetAside = true, Reason = b.Reason + $"; {home} {h.Candidate.Label} decides: a {cls.ToString().ToLowerInvariant()} of the name, not the {homeClass.ToString().ToLowerInvariant()}" };
        }
        foreach (var source in list.Where(Eligible).Select(m => m.Candidate.Source).Distinct(StringComparer.Ordinal).ToList())
        {
            var terms = list.Select((m, i) => (m, i)).Where(x => Eligible(x.m) && x.m.Candidate.Source == source).ToList();
            foreach (var (b, i) in terms)
            {
                if (SameThing(b, h)) continue;
                list[i] = terms.Count == 1 || source == "store"
                    ? b with { JoinedTo = h.Candidate.Uri, Reason = b.Reason + $"; carried by {home} {h.Candidate.Label}" }
                    : b with { SetAside = true, Reason = b.Reason + $"; {home} {h.Candidate.Label} decides: {source} has {terms.Count} exact terms of the name, none carried" };
            }
        }
        // and another vocabulary's inexact record of the name is a lookalike, not a rival: LoC's "Porcelain (Musical group)" beside
        // LCSH's "Porcelain" is set aside, so the home record is an exact-single (finding 21; the owner's policy, 2026-10-21, ceiling
        // 158 strings in the keywords family). The project's own entities are left as they are: a lookalike of ours is desk work
        for (var i = 0; i < list.Count; i++)
        {
            var b = list[i];
            // the home vocabulary's own inexact headings too ("Drugs" beside LCSH's "Medicine", through a variant): its headings are unique
            if (b.Veto || b.SetAside || b.JoinedTo is not null || b.Candidate.Source == "store" || b.Candidate.NameScore >= 0.97 || SameThing(b, h)) continue;
            list[i] = b with { SetAside = true, Reason = b.Reason + $"; {home} {h.Candidate.Label} decides: not the exact name" };
        }
    }

    /// <summary>A qualified heading's level: its type word's, or an inhabited place when it has none (LoC's default); null for a record that is not a heading with a placement (TGN's own records say their level in <see cref="Candidate.PlaceType"/>).</summary>
    private static int? HeadingLevel(Candidate c) =>
        c.Source is "loc" or "lcsh" or "store" && c.Placement is not null ? Qualified.LevelOf(c.PlaceType) ?? 2 : null;

    private static bool SameThing(ScoredCandidate a, ScoredCandidate b) =>
        a.Candidate.SameAs.Contains(b.Candidate.Uri, StringComparer.Ordinal) || b.Candidate.SameAs.Contains(a.Candidate.Uri, StringComparer.Ordinal);

    /// <summary>
    /// Joins candidates through their URIs and sameAs links (union-find) and ranks the groups by their best non-vetoed
    /// member. A group whose members are all vetoed, or in which a vetoed member is linked by sameAs to a live one (the
    /// authorities say they are one thing, and that thing's dates or type conflict), is marked vetoed and ranks last.
    /// </summary>
    public static IReadOnlyList<CandidateGroup> Groups(IReadOnlyList<ScoredCandidate> scored)
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        string Find(string x) { if (!parent.TryGetValue(x, out var p)) { parent[x] = x; return x; } if (p == x) return x; var root = Find(p); parent[x] = root; return root; }
        void Union(string a, string b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }
        foreach (var c in scored)
        {
            Find(c.Candidate.Uri);
            foreach (var same in c.Candidate.SameAs) Union(c.Candidate.Uri, same);
            if (c.JoinedTo is not null) Union(c.Candidate.Uri, c.JoinedTo);
        }
        return scored.GroupBy(c => Find(c.Candidate.Uri))
            .Select(g =>
            {
                var members = g.OrderByDescending(m => m.Score).ToList();
                // set-aside members are not live, but unlike vetoed ones they do not poison a group the authorities' links put them in
                var live = members.Where(m => !m.Veto && !m.SetAside).ToList();
                var uris = members.Select(m => m.Candidate.Uri).Concat(members.SelectMany(m => m.Candidate.SameAs)).Distinct(StringComparer.Ordinal).ToArray();
                var liveUris = live.Select(m => m.Candidate.Uri).ToHashSet(StringComparer.Ordinal);
                var linkedVeto = members.Any(m => m.Veto && (m.Candidate.SameAs.Any(liveUris.Contains) || live.Any(l => l.Candidate.SameAs.Contains(m.Candidate.Uri, StringComparer.Ordinal))));
                var vetoed = live.Count == 0 || linkedVeto;
                // the store's own entities are not an independent source: their ids came from earlier votes
                var sourcesAgreeing = live.Select(m => m.Candidate.Source).Where(s => s != "store").Distinct(StringComparer.Ordinal).ToArray();
                return new CandidateGroup(members, vetoed ? 0 : live.Max(m => m.Score), sourcesAgreeing, uris, vetoed);
            })
            .OrderBy(g => g.Vetoed).ThenByDescending(g => g.Score).ThenByDescending(g => g.Sources.Count)
            .ToList();
    }

    private static CandidateGroup Group(IReadOnlyList<ScoredCandidate> members) => Groups(members)[0];

    /// <summary>
    /// What carries an acceptable-scoring group, as the decision method it is recorded under: independent sources agreeing, or an
    /// entity already in the store with authority behind it (vote); one source with an exact name carried by the string's own
    /// dates or placed qualifier (single); or nothing (null).
    /// </summary>
    private string? CarriedBy(CandidateGroup group)
    {
        if (group.Sources.Count >= _options.MinIndependentSources) return DecisionMethod.Vote;
        if (group.Members.Any(m => !m.Veto && !m.SetAside && m.Candidate.EntityId is not null && !m.Candidate.Provisional)) return DecisionMethod.Vote;
        var best = group.Best;
        return _options.AcceptSingleSourceWithDates && (best.DatesAgree || best.QualifierAgrees == true) && best.Candidate.NameScore >= 0.97 ? DecisionMethod.Single : null;
    }

    // ---- entities ------------------------------------------------------------------------------------------------

    private async Task<Entity> EntityForGroupAsync(string dataset, StringReading reading, CandidateGroup group, string? runId, CancellationToken ct, string method = DecisionMethod.Vote)
    {
        // one writer at a time per authority identity, so two reconcilers (or a reconcile and the desk) do not both mint an entity for the same ids
        var lockKey = dataset + "|" + group.Uris.OrderBy(u => u, StringComparer.Ordinal).First();
        await using var _ = await store.LockAsync(lockKey, ct);
        var now = DateTimeOffset.UtcNow;
        var live = group.Members.Where(m => !m.Veto).ToList();
        Entity? entity = null;
        foreach (var m in live.Where(m => m.Candidate.EntityId is not null))
            if (await store.ResolveEntityAsync(m.Candidate.EntityId!, ct) is { } found) { entity = found; break; }
        if (entity is null)
            foreach (var uri in group.Uris)
            {
                var found = (await store.FindEntitiesByEquivalentAsync(uri, ct)).FirstOrDefault(e => e.Dataset == dataset && e.MergedInto is null);
                if (found is not null) { entity = found; break; }
            }
        var best = group.Best.Candidate;
        var external = live.Where(m => m.Candidate.EntityId is null).Select(m => m.Candidate).ToList();
        if (entity is null)
        {
            var cls = best.Class ?? reading.Classes[0];
            var labels = external.Select(c => c.Label).OfType<string>().Append(reading.Text).Distinct(StringComparer.Ordinal).ToList();
            // the record's dates when they date it; otherwise the string's own, since a record's loose window (ULAN's 1850-2080) is not a lifespan to publish
            var lifespan = best.Lifespan is { IsDated: true } ? best.Lifespan : reading.Lifespan ?? best.Lifespan;
            // a placed qualified name keeps its qualifier as the label ("Heaton, Bradford"), which is also how the store stage tells it from namesakes later
            var label = reading.Qualifier is not null && group.Best.QualifierAgrees == true ? reading.Label : best.Label ?? reading.Text;
            entity = await store.AddEntityAsync(dataset, new NewEntity(cls, label, labels, lifespan, reading.Nationalities, reading.Roles, EntityStatus.Aligned, RunId: runId), scheme, ct);
        }
        var present = entity.Equivalents.Select(q => q.Uri).ToHashSet(StringComparer.Ordinal);
        foreach (var c in external)
        {
            if (present.Add(c.Uri)) await store.AddEquivalentAsync(entity.Id, new Equivalent(c.Uri, c.Source, method, group.Score, now, RunId: runId), ct);
            foreach (var same in c.SameAs)
                if (!scheme.Owns(same) && present.Add(same)) await store.AddEquivalentAsync(entity.Id, new Equivalent(same, AuthorityUris.SourceOf(same), method, group.Score, now, RunId: runId), ct);
        }
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>The entity for a URI: one of ours (resolved, or created under that identifier), or the entity that already has the authority URI as an equivalent, or a new confirmed entity built from the cached authority record with that URI as its equivalent. The review desk and overrides use this.</summary>
    public Task<Entity> EntityForUriAsync(string dataset, string uri, StringReading reading, string? runId = null, CancellationToken ct = default) => EntityForTargetAsync(dataset, uri, reading, runId, ct);

    private async Task<Entity> EntityForTargetAsync(string dataset, string target, StringReading reading, string? runId, CancellationToken ct)
    {
        if (scheme.Owns(target))
        {
            if (await store.ResolveEntityAsync(target, ct) is { } own) return own;
            var cls = scheme.ClassOf(target) ?? reading.Classes[0];
            return await store.AddEntityAsync(dataset, new NewEntity(cls, reading.Text, [reading.Text], reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Confirmed, Id: target, RunId: runId), scheme, ct);
        }
        await using var _ = await store.LockAsync(dataset + "|" + target, ct);
        if ((await store.FindEntitiesByEquivalentAsync(target, ct)).FirstOrDefault(e => e.Dataset == dataset && e.MergedInto is null) is { } existing) return existing;
        var record = await store.GetAuthorityAsync(target, ct);
        var recordClass = record is not null && Enum.TryParse<EntityClass>(record.Type, true, out var rc) ? rc : reading.Classes[0];
        var labels = (record is null ? [] : record.Labels).Append(reading.Text).Distinct(StringComparer.Ordinal).ToList();
        var entity = await store.AddEntityAsync(dataset, new NewEntity(recordClass, record?.Label ?? reading.Text, labels, record?.Lifespan ?? reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Confirmed, RunId: runId), scheme, ct);
        var now = DateTimeOffset.UtcNow;
        await store.AddEquivalentAsync(entity.Id, new Equivalent(target, AuthorityUris.SourceOf(target), DecisionMethod.Override, 1.0, now, RunId: runId), ct);
        // the record's own links come along, as a vote's would
        foreach (var same in record?.SameAs ?? [])
            if (!scheme.Owns(same) && same != target) await store.AddEquivalentAsync(entity.Id, new Equivalent(same, AuthorityUris.SourceOf(same), DecisionMethod.Override, 1.0, now, RunId: runId), ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    /// <summary>The provisional entity for a queued or minted string, under the earlier pipeline's identifier for the string when it had one (<see cref="OwnEntityAsync"/>).</summary>
    private Task<Entity> MintAsync(string dataset, StringReading reading, string? runId, CancellationToken ct) =>
        OwnEntityAsync(dataset, reading, new NewEntity(reading.Classes[0], reading.Label, [reading.String.Raw], reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Provisional, RunId: runId), ct);

    // ---- evidence and parsers ----------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions EvidenceJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static string Evidence(string stage, StringReading reading, IReadOnlyList<CandidateGroup> groups, object? extra) =>
        JsonSerializer.Serialize(new
        {
            stage,
            reading = new { reading.Text, reading.Key, reading.Qualifier, classes = reading.Classes.Select(c => c.ToString()), lifespan = reading.Lifespan?.Text, nationalities = reading.Nationalities.Count == 0 ? null : reading.Nationalities, roles = reading.Roles.Count == 0 ? null : reading.Roles },
            groups = groups.Take(5).Select(g => new
            {
                g.Score, g.Sources, vetoed = g.Vetoed ? true : (bool?)null,
                members = g.Members.Take(8).Select(m => new { m.Candidate.Uri, m.Candidate.Source, m.Candidate.Label, m.Candidate.Stage, m.Score, veto = m.Veto ? true : (bool?)null, setAside = m.SetAside ? true : (bool?)null, m.Reason }),
            }),
            extra,
        }, EvidenceJson);

    private AgentStringParser? ParserFor(string? profileName)
    {
        if (profileName is null) return null;
        if (_parsers.TryGetValue(profileName, out var p)) return p;
        var profile = AgentProfileCatalog.BuiltIn.TryGet(profileName, out var found) ? found : AgentProfile.Default;
        return _parsers[profileName] = new AgentStringParser(profile, datesProfiles);
    }
}
