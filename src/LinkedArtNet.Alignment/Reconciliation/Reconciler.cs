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
}

public enum Outcome { Skipped, Overridden, MatchedStore, Accepted, Queued, Minted, Deferred, Decomposed }

/// <param name="Lists">Strings decided as lists of parts; their parts' outcomes are counted with everything else, so the outcome counts add up to Considered minus Lists plus Parts.</param>
/// <param name="Parts">Parts reconciled for the first time in this run (a part already decided by an earlier string is not counted again).</param>
/// <param name="Single">Accepted on one source carried by the string's own dates or placed qualifier (DecisionMethod.Single).</param>
/// <param name="Compounds">Compound headings decided as concepts coordinated from their parts (DecisionMethod.Composite); counted like lists.</param>
/// <param name="DecidedEarlier">Strings in the undecided list that were decided before the run reached them, as parts of an earlier string (part strings outlive an undo as rows), and so were passed over rather than decided twice.</param>
public sealed record ReconcileReport(int Considered, int Overridden, int MatchedStore, int Accepted, int Queued, int Minted, int Deferred, int Skipped, int ExactSingle = 0, int Lists = 0, int Parts = 0, int Single = 0, int Compounds = 0, int DecidedEarlier = 0)
{
    /// <summary>Accepted by independent sources agreeing, as opposed to one source carried by the string or the single-source policy.</summary>
    public int Voted => Accepted - ExactSingle - Single;
    public override string ToString() => $"considered {Considered}: overridden {Overridden}, matched in store {MatchedStore}, accepted {Accepted} (vote {Voted}, single {Single}, exact-single {ExactSingle}), queued {Queued}, minted {Minted}, deferred {Deferred}, skipped {Skipped}" + (Lists + Compounds > 0 ? $", lists {Lists}, compounds {Compounds} (between them {Parts} part strings)" : "") + (DecidedEarlier > 0 ? $"; {DecidedEarlier} of those considered were decided as parts before the run reached them" : "");
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

    /// <summary>Works through the dataset's undecided strings: all of them when <paramref name="limit"/> is 0 or less.</summary>
    public async Task<ReconcileReport> RunAsync(string dataset, int limit = 0, string? runId = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var strings = await store.UndecidedAsync(dataset, limit > 0 ? limit : int.MaxValue, ct);
        int overridden = 0, matched = 0, accepted = 0, exactSingle = 0, single = 0, queued = 0, minted = 0, deferred = 0, skipped = 0, lists = 0, compounds = 0, parts = 0, decidedEarlier = 0, n = 0;
        void Count(Evaluation e)
        {
            switch (e.Outcome)
            {
                case Outcome.Overridden: overridden++; break;
                case Outcome.MatchedStore: matched++; break;
                case Outcome.Accepted: accepted++; if (e.AcceptedMethod == DecisionMethod.ExactSingle) exactSingle++; else if (e.AcceptedMethod == DecisionMethod.Single) single++; break;
                case Outcome.Queued: queued++; break;
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
            else Count(await ReconcileOneDetailedAsync(dataset, s, runId, ct));
            if (n % 500 == 0) progress?.Report($"{n} of {strings.Count}: accepted {accepted} (exact-single {exactSingle}), queued {queued}, minted {minted}" + (lists + compounds > 0 ? $", lists {lists}, compounds {compounds}" : ""));
        }
        return new ReconcileReport(strings.Count, overridden, matched, accepted, queued, minted, deferred, skipped, exactSingle, lists, parts, single, compounds, decidedEarlier);
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

    /// <summary>The heading's own concept: the project's existing Type entity under that label when it has one (the imported headings keep their identifiers), else a new one under the project's identifiers. Its parts are the decisions of its part strings, read at export.</summary>
    private async Task<Entity> CompositeEntityAsync(string dataset, StringReading reading, string? runId, CancellationToken ct)
    {
        foreach (var key in reading.Keys)
            foreach (var found in await store.FindEntitiesByLabelAsync(key, dataset, EntityClass.Type, ct))
                if (found.MergedInto is null && NameMatcher.Score(reading.Text, found.Label is null ? found.Labels : found.Labels.Prepend(found.Label)) >= 0.97) return found;
        return await store.AddEntityAsync(dataset, new NewEntity(EntityClass.Type, reading.Text, [reading.String.Raw], null, [], [], EntityStatus.Provisional, RunId: runId), scheme, ct);
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
        var reading = Read(s);
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
        // a rival at acceptance score is a disagreement: another authority, or another provisional lookalike when the top is only a lookalike itself
        var topIsFirm = top is not null && top.Members.Any(m => !m.Veto && !m.Candidate.Provisional);
        var second = live.Skip(1).FirstOrDefault(g => !topIsFirm || g.Members.Any(m => !m.Veto && !m.Candidate.Provisional));
        var carried = top is null ? null : CarriedBy(top);
        // outvoted rivals (the owner's policy, 2026-10-21, doc 10 section 7.6): for a place or a concept, a group two independent sources
        // carry beats rivals one source carries at the same score (TGN's Scottish Aberdeen with LoC's "Aberdeen (Scotland)" joined,
        // against four TGN namesakes no LoC heading reaches); never over one of the project's own aligned entities or another
        // two-source group, and never for people and bodies, where sameness of name is weak evidence and the desk decides
        var rivals = top is null ? [] : live.Skip(1).Where(g => g.Score >= _options.AcceptScore).ToList();
        var outvoted = top is not null && second is not null && second.Score >= _options.AcceptScore && top.Sources.Count >= 2
            && (top.Best.Candidate.Class ?? reading.Classes[0]) is EntityClass.Place or EntityClass.Type or EntityClass.Period
            && rivals.All(g => g.Sources.Count <= 1 && !g.Members.Any(m => !m.Veto && !m.SetAside && m.Candidate.EntityId is not null && !m.Candidate.Provisional));
        if (outvoted)
            groups = groups.Select(g => rivals.Contains(g) ? g with { Members = g.Members.Select(m => m with { Reason = m.Reason + $"; outvoted by a group of {top!.Sources.Count} sources" }).ToList() } : g).ToList();
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
        if (_options.AcceptUniqueExactSingleSource && top is not null && !truncated && groups.Count(g => g.Members.Any(m => !m.SetAside)) == 1 && reading.Lifespan is null
            && (reading.Classes[0] is not (EntityClass.Person or EntityClass.Group) || StringKey.Normalise(reading.Text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            && top.Score >= _options.AcceptScore && top.Best.Candidate.NameScore >= 0.97
            && top.Members.All(m => !m.Veto && !m.Candidate.Provisional && m.Candidate.EntityId is null)
            // a heading that carries dates was disambiguated from namesakes by its cataloguer: an undated string cannot claim it alone
            && !top.Members.Any(m => m.Candidate.Disambiguated)
            // a qualifier that the record does not bear out ("Princeton, N.J." against a Princeton elsewhere) is for the desk
            && top.Best.QualifierAgrees != false)
        {
            if (includeStore && await HoldersAsync(dataset, top.Uris, ct) > 1) return new Evaluation(Outcome.Queued, reading, groups, Truncated: truncated);
            return new Evaluation(Outcome.Accepted, reading, groups, Accepted: top, AcceptedMethod: DecisionMethod.ExactSingle);
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
        var placement = reading.IsQualified && e.Label is not null ? Qualified.Split(e.Label).Qualifier : null;
        return new Candidate(e.Id, "store", e.Label, e.Class, lifespan ?? e.Lifespan, e.Equivalents.Select(q => q.Uri).ToArray(), "store", NameMatcher.Score(reading.Text, labels), e.Id, e.Nationalities, provisional, queued, Placement: placement);
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
        foreach (var c in candidates)
        {
            var score = c.NameScore;
            var reasons = new List<string> { $"name {c.NameScore:0.00}" };
            var veto = false;
            var datesAgree = false;
            if (c.Class is { } cls && !reading.Classes.Contains(cls)) { veto = true; reasons.Add($"type {cls} not allowed here"); }
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
            if (reading.Nationalities.Count > 0 && c.Nationalities is { Count: > 0 } cn && cn.Intersect(reading.Nationalities, StringComparer.Ordinal).Any())
            { score = Math.Min(1, score + 0.03); reasons.Add("nationality agrees"); }
            var qualifierAgrees = Qualified.Agrees(reading.Qualifier, c.Placement);
            switch (qualifierAgrees)
            {
                case true: score = Math.Min(1, score + 0.05); reasons.Add($"qualifier places it ({c.Placement})"); break;
                case false: reasons.Add($"qualifier not placed ({reading.Qualifier} vs {c.Placement})"); break;
            }
            list.Add(new ScoredCandidate(c, Math.Round(score, 3), veto, string.Join("; ", reasons), datesAgree, qualifierAgrees));
        }
        // a string that carries dates and a record of a source that agrees with them: that source's undated records of the name
        // are namesakes the cataloguer could not date, not the dated person (LoC's headings are unique, so its undated "Doyle,
        // Richard" beside "Doyle, Richard, 1824-1883" is another Richard Doyle by LoC's own rule); the owner's policy, 2026-10-21
        if (reading.HasLifespan)
            foreach (var source in list.Where(m => m.DatesAgree && !m.Veto).Select(m => m.Candidate.Source).Distinct(StringComparer.Ordinal).ToList())
                for (var i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    if (b.Veto || b.Candidate.Source != source || b.Candidate.Lifespan is { IsEmpty: false } || b.Candidate.EntityId is not null) continue;
                    list[i] = b with { Veto = true, Reason = b.Reason + $"; {source}'s dated record of the name agrees with the string's dates: this undated one is a namesake" };
                }
        // an undated record that is its source's only exact record of the name is the person another source dates: "LoC knows one
        // Marit Guinness Aschan; ULAN dates her", and the two vote (doc 10, section 7.6 finding 14; the owner's policy, 2026-10-21)
        if (reading.HasLifespan && list.FirstOrDefault(m => m.DatesAgree && !m.Veto && !m.SetAside) is { } dated)
            foreach (var source in list.Select(m => m.Candidate.Source).Distinct(StringComparer.Ordinal).ToList())
            {
                if (source == dated.Candidate.Source || source == "store") continue;
                var exact = list.Select((m, i) => (m, i)).Where(x => x.m.Candidate.Source == source && x.m.Candidate.NameScore >= 0.97).ToList();
                if (exact.Count != 1) continue;
                var (lone, at) = exact[0];
                if (lone.Veto || lone.SetAside || lone.JoinedTo is not null || lone.Candidate.Lifespan is { IsEmpty: false }) continue;
                if (lone.Candidate.Class is { } cls && dated.Candidate.Class is { } datedClass && cls != datedClass) continue;
                list[at] = lone with { JoinedTo = dated.Candidate.Uri, Reason = lone.Reason + $"; {source}'s one record of the name, which {dated.Candidate.Source} dates" };
            }
        if (reading.Collections is { Count: > 0 } wanted) PreferCollections(wanted, list);
        if (reading.Home is not null) JoinToHome(reading.Home, list);
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
            var words = Qualified.Tokens(b.Candidate.Placement);
            if (words.Count == 0) continue;
            var through = holders.Where(a => a.Candidate.Uri != b.Candidate.Uri && a.Candidate.Source != b.Candidate.Source && Qualified.Contains(a.Candidate.Placement, words)).ToList();
            if (through.Count == 0) continue;
            inside.Add(i);
            list[i] = through.Count == 1
                ? b with { JoinedTo = through[0].Candidate.Uri, Reason = b.Reason + $"; placed through {through[0].Candidate.Source} {through[0].Candidate.Label}" }
                : b with { Reason = b.Reason + $"; inside {through.Count} placed candidates" };
        }
        if (reading.Qualifier is null) return list;
        // the project's one entity of the name with no placement of its own ("Adelaide" beside "Adelaide, South Australia") is the
        // placed record's own entity: it joins the placed group and gains the id, as the project's LoC-labelled entities do
        var anchors = list.Where(m => !m.Veto && m.QualifierAgrees == true && m.JoinedTo is null).ToList();
        var own = list.Select((m, i) => (m, i)).Where(x => !x.m.Veto && x.m.Candidate.EntityId is not null && x.m.Candidate.Placement is null && x.m.Candidate.NameScore >= 0.97 && x.m.JoinedTo is null && !x.m.Candidate.ProvisionalQueued).ToList();
        if (anchors.Count == 1 && own.Count == 1)
            list[own[0].i] = own[0].m with { JoinedTo = anchors[0].Candidate.Uri, Reason = own[0].m.Reason + $"; the project's one {reading.Text} joins the placed {anchors[0].Candidate.Source} record" };
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
    }

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

    private Task<Entity> MintAsync(string dataset, StringReading reading, string? runId, CancellationToken ct) =>
        store.AddEntityAsync(dataset, new NewEntity(reading.Classes[0], reading.Label, [reading.String.Raw], reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Provisional, RunId: runId), scheme, ct);

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
