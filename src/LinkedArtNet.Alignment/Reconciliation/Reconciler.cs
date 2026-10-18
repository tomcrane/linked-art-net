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
    /// <summary>Accept one source alone when the name is exact and both the string and the record carry compatible dates. Design doc 10 sends a lone source to review; this is the pragmatic default while ULAN is the only local index, and a manifest can turn it off.</summary>
    public bool AcceptSingleSourceWithDates { get; init; } = true;
    /// <summary>Reading rules by context (the field a string came from); contexts not listed use <see cref="DefaultContext"/>.</summary>
    public IReadOnlyDictionary<string, ContextRule> Contexts { get; init; } = new Dictionary<string, ContextRule>(StringComparer.OrdinalIgnoreCase);
    public ContextRule DefaultContext { get; init; } = ContextRule.Anything;
    /// <summary>Mint a provisional entity when nothing was found; off, such strings are left undecided for a later run.</summary>
    public bool MintWhenNothingFound { get; init; } = true;
}

public enum Outcome { Skipped, Overridden, MatchedStore, Accepted, Queued, Minted, Deferred }

public sealed record ReconcileReport(int Considered, int Overridden, int MatchedStore, int Accepted, int Queued, int Minted, int Deferred, int Skipped)
{
    public override string ToString() => $"considered {Considered}: overridden {Overridden}, matched in store {MatchedStore}, accepted {Accepted}, queued {Queued}, minted {Minted}, deferred {Deferred}, skipped {Skipped}";
}

/// <summary>
/// What the reconciler would do with a string, before anything is written: the outcome, how the string was read, the
/// candidate groups, and whichever of an override, a store entity or an accepted group decided it.
/// </summary>
public sealed record Evaluation(Outcome Outcome, StringReading Reading, IReadOnlyList<CandidateGroup> Groups, Override? Override = null, Entity? StoreMatch = null, CandidateGroup? Accepted = null, ScoredCandidate? StoreChoice = null)
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

    public async Task<ReconcileReport> RunAsync(string dataset, int limit = 1000, string? runId = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var strings = await store.UndecidedAsync(dataset, limit, ct);
        int overridden = 0, matched = 0, accepted = 0, queued = 0, minted = 0, deferred = 0, skipped = 0, n = 0;
        foreach (var s in strings)
        {
            ct.ThrowIfCancellationRequested();
            switch (await ReconcileOneAsync(dataset, s, runId, ct))
            {
                case Outcome.Overridden: overridden++; break;
                case Outcome.MatchedStore: matched++; break;
                case Outcome.Accepted: accepted++; break;
                case Outcome.Queued: queued++; break;
                case Outcome.Minted: minted++; break;
                case Outcome.Deferred: deferred++; break;
                default: skipped++; break;
            }
            if (++n % 500 == 0) progress?.Report($"{n} of {strings.Count}: accepted {accepted}, queued {queued}, minted {minted}");
        }
        return new ReconcileReport(strings.Count, overridden, matched, accepted, queued, minted, deferred, skipped);
    }

    public async Task<Outcome> ReconcileOneAsync(string dataset, SourceString s, string? runId = null, CancellationToken ct = default)
    {
        var evaluation = await EvaluateAsync(dataset, s, includeStore: true, ct);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await CommitAsync(dataset, s, evaluation, runId, ct);
        Time("commit", sw);
        return evaluation.Outcome;
    }

    /// <summary>Reads the string and finds what it would be decided as, writing nothing. With <paramref name="includeStore"/> false the store's own entities are not consulted, so an old decision cannot answer itself.</summary>
    public async Task<Evaluation> EvaluateAsync(string dataset, SourceString s, bool includeStore = true, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rule = _options.Contexts.TryGetValue(s.Context, out var r) ? r : _options.DefaultContext;
        var reading = StringReading.Read(s, rule, ParserFor(rule.AgentProfile));
        Time("read", sw);
        if (reading.Key.Length == 0) return new Evaluation(Outcome.Skipped, reading, []);

        // 1. overrides, by the whole string and by the name part
        var over = await store.OverrideForAsync(dataset, s.Context, s.Normalised, ct);
        if (over is null && reading.Key != s.Normalised) over = await store.OverrideForAsync(dataset, s.Context, reading.Key, ct);
        Time("overrides", sw);
        if (over is not null) return new Evaluation(Outcome.Overridden, reading, [], Override: over);

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
            {
                var labels = e.Label is null ? e.Labels : e.Labels.Prepend(e.Label);
                candidates.Add(new Candidate(e.Id, "store", e.Label, e.Class, e.Lifespan, e.Equivalents.Select(q => q.Uri).ToArray(), "store", NameMatcher.Score(reading.Text, labels), e.Id, e.Nationalities, e.Status == EntityStatus.Provisional));
            }
            Time("store", sw);
            // one acceptable entity, or one acceptable entity with authority behind it among provisional lookalikes
            var storeScored = Score(reading, candidates).Where(c => !c.Veto && c.Score >= _options.AcceptScore).ToList();
            var firm = storeScored.Where(c => !c.Candidate.Provisional).ToList();
            var chosen = storeScored.Count == 1 ? storeScored[0] : firm.Count == 1 ? firm[0] : null;
            if (chosen is not null)
                return new Evaluation(Outcome.MatchedStore, reading, [Group([chosen])], StoreMatch: storeMatches[chosen.Candidate.EntityId!], StoreChoice: chosen);
        }

        // 3. local indexes (remote sources come in step 3b)
        foreach (var source in sources.Where(x => !x.IsRemote))
        {
            candidates.AddRange(await source.FindAsync(reading, ct));
            Time("source:" + source.Name, sw);
        }

        var groups = Groups(Score(reading, candidates));
        Time("score", sw);
        var top = groups.FirstOrDefault();
        // a rival group counts as a disagreement only when something other than a provisional lookalike is in it
        var second = groups.Skip(1).FirstOrDefault(g => g.Members.Any(m => !m.Veto && !m.Candidate.Provisional));
        if (top is not null && top.Score >= _options.AcceptScore && Carries(reading, top) && (second is null || second.Score < _options.AcceptScore))
            return new Evaluation(Outcome.Accepted, reading, groups, Accepted: top);
        if (top is not null && top.Score >= _options.ReviewScore)
            return new Evaluation(Outcome.Queued, reading, groups);
        return new Evaluation(_options.MintWhenNothingFound ? Outcome.Minted : Outcome.Deferred, reading, groups);
    }

    /// <summary>Writes an evaluation: the decision with its evidence and, where needed, the entity.</summary>
    public async Task CommitAsync(string dataset, SourceString s, Evaluation e, string? runId = null, CancellationToken ct = default)
    {
        var reading = e.Reading;
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
                var entity = await EntityForGroupAsync(dataset, reading, e.Accepted!, runId, ct);
                await store.DecideAsync(s.Id, entity.Id, DecisionMethod.Vote, Evidence("vote", reading, e.Groups, null), runId: runId, ct: ct);
                break;
            }
            case Outcome.Queued:
            {
                var provisional = await MintAsync(dataset, reading, runId, ct);
                await store.DecideAsync(s.Id, provisional.Id, DecisionMethod.Queued, Evidence("queued", reading, e.Groups, null), runId: runId, ct: ct);
                break;
            }
            case Outcome.Minted:
            {
                var fresh = await MintAsync(dataset, reading, runId, ct);
                await store.DecideAsync(s.Id, fresh.Id, DecisionMethod.Minted, Evidence("minted", reading, e.Groups, null), runId: runId, ct: ct);
                break;
            }
        }
    }

    // ---- scoring and grouping ----------------------------------------------------------------------------------

    /// <summary>Name score, then dates (agreement lifts, conflict vetoes), type (a mismatch vetoes), nationality (agreement lifts).</summary>
    public static IReadOnlyList<ScoredCandidate> Score(StringReading reading, IEnumerable<Candidate> candidates)
    {
        var list = new List<ScoredCandidate>();
        foreach (var c in candidates)
        {
            var score = c.NameScore;
            var reasons = new List<string> { $"name {c.NameScore:0.00}" };
            var veto = false;
            if (c.Class is { } cls && !reading.Classes.Contains(cls)) { veto = true; reasons.Add($"type {cls} not allowed here"); }
            if (reading.Lifespan is { IsEmpty: false } rl && c.Lifespan is { IsEmpty: false } cl)
            {
                if (rl.Compatible(cl)) { score = Math.Min(1, score + 0.05); reasons.Add("dates agree"); }
                else { veto = true; reasons.Add($"dates conflict ({rl.Text} vs {cl.Text})"); }
            }
            if (reading.Nationalities.Count > 0 && c.Nationalities is { Count: > 0 } cn && cn.Intersect(reading.Nationalities, StringComparer.Ordinal).Any())
            { score = Math.Min(1, score + 0.03); reasons.Add("nationality agrees"); }
            list.Add(new ScoredCandidate(c, Math.Round(score, 3), veto, string.Join("; ", reasons)));
        }
        return list;
    }

    /// <summary>Joins candidates through their URIs and sameAs links (union-find) and ranks the groups by their best non-vetoed member. Groups whose members are all vetoed are dropped.</summary>
    public static IReadOnlyList<CandidateGroup> Groups(IReadOnlyList<ScoredCandidate> scored)
    {
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
        string Find(string x) { if (!parent.TryGetValue(x, out var p)) { parent[x] = x; return x; } if (p == x) return x; var root = Find(p); parent[x] = root; return root; }
        void Union(string a, string b) { var ra = Find(a); var rb = Find(b); if (ra != rb) parent[ra] = rb; }
        foreach (var c in scored)
        {
            Find(c.Candidate.Uri);
            foreach (var same in c.Candidate.SameAs) Union(c.Candidate.Uri, same);
        }
        return scored.GroupBy(c => Find(c.Candidate.Uri))
            .Select(g =>
            {
                var members = g.OrderByDescending(m => m.Score).ToList();
                var live = members.Where(m => !m.Veto).ToList();
                if (live.Count == 0) return null;
                return new CandidateGroup(members, live.Max(m => m.Score), live.Select(m => m.Candidate.Source).Distinct(StringComparer.Ordinal).ToArray(),
                    members.Select(m => m.Candidate.Uri).Concat(members.SelectMany(m => m.Candidate.SameAs)).Distinct(StringComparer.Ordinal).ToArray());
            })
            .OfType<CandidateGroup>()
            .OrderByDescending(g => g.Score).ThenByDescending(g => g.Sources.Count)
            .ToList();
    }

    private static CandidateGroup Group(IReadOnlyList<ScoredCandidate> members) => Groups(members)[0];

    /// <summary>Whether an acceptable-scoring group has enough behind it: independent sources agreeing, or one source with an exact name and compatible dates, or an entity already in the store.</summary>
    private bool Carries(StringReading reading, CandidateGroup group)
    {
        if (group.Sources.Count >= _options.MinIndependentSources) return true;
        if (group.Members.Any(m => !m.Veto && m.Candidate.EntityId is not null)) return true;
        var best = group.Best;
        return _options.AcceptSingleSourceWithDates && reading.HasLifespan && best.Candidate.Lifespan is { IsEmpty: false } && best.Candidate.NameScore >= 0.97;
    }

    // ---- entities ------------------------------------------------------------------------------------------------

    private async Task<Entity> EntityForGroupAsync(string dataset, StringReading reading, CandidateGroup group, string? runId, CancellationToken ct)
    {
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
            entity = await store.AddEntityAsync(dataset, new NewEntity(cls, best.Label ?? reading.Text, labels, best.Lifespan ?? reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Aligned, RunId: runId), scheme, ct);
        }
        var present = entity.Equivalents.Select(q => q.Uri).ToHashSet(StringComparer.Ordinal);
        foreach (var c in external)
        {
            if (present.Add(c.Uri)) await store.AddEquivalentAsync(entity.Id, new Equivalent(c.Uri, c.Source, DecisionMethod.Vote, group.Score, now, RunId: runId), ct);
            foreach (var same in c.SameAs)
                if (!scheme.Owns(same) && present.Add(same)) await store.AddEquivalentAsync(entity.Id, new Equivalent(same, AuthorityUris.SourceOf(same), DecisionMethod.Vote, group.Score, now, RunId: runId), ct);
        }
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    private async Task<Entity> EntityForTargetAsync(string dataset, string target, StringReading reading, string? runId, CancellationToken ct)
    {
        if (scheme.Owns(target))
        {
            if (await store.ResolveEntityAsync(target, ct) is { } own) return own;
            var cls = scheme.ClassOf(target) ?? reading.Classes[0];
            return await store.AddEntityAsync(dataset, new NewEntity(cls, reading.Text, [reading.Text], reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Confirmed, Id: target, RunId: runId), scheme, ct);
        }
        if ((await store.FindEntitiesByEquivalentAsync(target, ct)).FirstOrDefault(e => e.Dataset == dataset && e.MergedInto is null) is { } existing) return existing;
        var record = await store.GetAuthorityAsync(target, ct);
        var recordClass = record is not null && Enum.TryParse<EntityClass>(record.Type, true, out var rc) ? rc : reading.Classes[0];
        var entity = await store.AddEntityAsync(dataset, new NewEntity(recordClass, record?.Label ?? reading.Text, [reading.Text], record?.Lifespan ?? reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Confirmed, RunId: runId), scheme, ct);
        await store.AddEquivalentAsync(entity.Id, new Equivalent(target, AuthorityUris.SourceOf(target), DecisionMethod.Override, 1.0, DateTimeOffset.UtcNow, RunId: runId), ct);
        return (await store.GetEntityAsync(entity.Id, ct))!;
    }

    private Task<Entity> MintAsync(string dataset, StringReading reading, string? runId, CancellationToken ct) =>
        store.AddEntityAsync(dataset, new NewEntity(reading.Classes[0], reading.Text, [reading.String.Raw], reading.Lifespan, reading.Nationalities, reading.Roles, EntityStatus.Provisional, RunId: runId), scheme, ct);

    // ---- evidence and parsers ----------------------------------------------------------------------------------

    private static readonly JsonSerializerOptions EvidenceJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static string Evidence(string stage, StringReading reading, IReadOnlyList<CandidateGroup> groups, object? extra) =>
        JsonSerializer.Serialize(new
        {
            stage,
            reading = new { reading.Text, reading.Key, classes = reading.Classes.Select(c => c.ToString()), lifespan = reading.Lifespan?.Text, nationalities = reading.Nationalities.Count == 0 ? null : reading.Nationalities, roles = reading.Roles.Count == 0 ? null : reading.Roles },
            groups = groups.Take(5).Select(g => new
            {
                g.Score, g.Sources,
                members = g.Members.Take(8).Select(m => new { m.Candidate.Uri, m.Candidate.Source, m.Candidate.Label, m.Candidate.Stage, m.Score, veto = m.Veto ? true : (bool?)null, m.Reason }),
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
