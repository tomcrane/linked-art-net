using System.Text;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>Entities of one class sharing a label: the one to keep, the ones to merge into it, and the ones kept apart with the reason.</summary>
public sealed record DuplicateTwins(EntityClass Class, string Label, Entity Survivor, IReadOnlyList<Entity> Merge, IReadOnlyList<(Entity Entity, string Reason)> Kept)
{
    public int Entities => 1 + Merge.Count + Kept.Count;
}

/// <summary>What a duplicate pass would do, or did: one line of twins per shared label.</summary>
public sealed record DuplicateReport(IReadOnlyList<DuplicateTwins> Twins)
{
    public int Merges => Twins.Sum(t => t.Merge.Count);
    public int KeptApart => Twins.Sum(t => t.Kept.Count);

    /// <summary>Every label with its entities, then a count by class; what the owner reads before saying go.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var t in Twins)
        {
            sb.Append(t.Class).Append(" \"").Append(t.Label).Append("\": keep ").Append(Describe(t.Survivor));
            if (t.Merge.Count > 0) sb.Append("; merge ").Append(string.Join(", ", t.Merge.Select(Describe)));
            sb.AppendLine();
            foreach (var (e, reason) in t.Kept) sb.Append("  kept apart ").Append(Describe(e)).Append(": ").Append(reason).AppendLine();
        }
        sb.AppendLine();
        sb.AppendLine("class    labels  entities  merges  kept apart");
        foreach (var g in Twins.GroupBy(t => t.Class).OrderBy(g => g.Key))
            sb.AppendLine($"{g.Key,-8} {g.Count(),6}  {g.Sum(t => t.Entities),8}  {g.Sum(t => t.Merge.Count),6}  {g.Sum(t => t.Kept.Count),10}");
        sb.AppendLine($"{"all",-8} {Twins.Count,6}  {Twins.Sum(t => t.Entities),8}  {Merges,6}  {KeptApart,10}");
        return sb.ToString();
    }

    private static string Describe(Entity e)
    {
        var ids = e.Equivalents.Count == 0 ? "no ids" : string.Join(" ", e.Equivalents.GroupBy(q => q.Source, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Count() == 1 ? g.Key : $"{g.Key} x{g.Count()}"));
        var dates = e.Lifespan is { IsEmpty: false } l ? $"; {l.Text ?? $"{l.BeginEarliest?.Year}-{l.EndEarliest?.Year}"}" : "";
        return $"{e.Id} ({e.Status.ToString().ToLowerInvariant()}; {ids}{dates})";
    }
}

/// <summary>
/// The one-off pass over a dataset's duplicate entities (plan, resume point decision 5, the owner's decision 2026-10-21):
/// the earlier pipeline minted two entities for one label more than once (two "England", two "Evanston"), and the twins
/// block the rule that reuses the project's one bare entity for a place. Twins of one class and label (case folded) are
/// merged where their external ids do not conflict: none on one side, or an id shared and no authoritative source (one record
/// per thing: TGN, ULAN, AAT, LoC, LCSH, Wikidata) with differing ids on the two sides. VIAF and LUX are clusters and hold
/// duplicates, so differing VIAF or LUX ids beside a shared id are not a conflict (the dry run of 2026-10-21 showed the legacy
/// import's twins of "Evanston" and forty other places with the same Wikidata id and two VIAF clusters); differing VIAF or LUX
/// ids with nothing shared leave the twins apart. For a person a shared label is weak evidence, so the twins must also share an id or carry the same
/// dates; a bare "Smith, John" twice stays two entities for the desk. The survivor is a confirmed entity first, then
/// the one with the most ids, then the oldest. The merged-away identifiers stay on the entities table as
/// <c>merged_into</c> and the export writes them as equivalents of the survivor so LUX merges its copies.
/// </summary>
public sealed class DuplicateMerger(IAlignmentStore store)
{
    /// <summary>What the pass would do for the dataset, for the classes given (every class when none): nothing is changed.</summary>
    public async Task<DuplicateReport> PlanAsync(string dataset, IReadOnlyCollection<EntityClass>? classes = null, CancellationToken ct = default)
    {
        var entities = await store.EntitiesSharingLabelAsync(dataset, ct);
        var twins = entities
            .Where(e => classes is null || classes.Count == 0 || classes.Contains(e.Class))
            .GroupBy(e => (e.Class, Label: Fold(e.Label)))
            .Where(g => g.Count() > 1)
            .Select(g => Plan(g.ToList()))
            .OrderBy(t => t.Class).ThenBy(t => t.Label, StringComparer.Ordinal)
            .ToList();
        return new DuplicateReport(twins);
    }

    /// <summary>Carries out a plan: every entity to merge goes into its survivor through the store's merge (its decisions re-pointed, its ids and labels carried). Returns how many merged.</summary>
    public async Task<int> ApplyAsync(DuplicateReport report, string reviewer, CancellationToken ct = default)
    {
        var n = 0;
        foreach (var t in report.Twins)
            foreach (var loser in t.Merge)
            {
                await store.MergeAsync(loser.Id, t.Survivor.Id, reviewer, ct);
                n++;
            }
        return n;
    }

    /// <summary>The plan for one set of twins: the survivor, and each other entity merged or kept apart with the reason.</summary>
    public static DuplicateTwins Plan(IReadOnlyList<Entity> twins)
    {
        if (twins.Count < 2) throw new ArgumentException("Twins are two or more entities.", nameof(twins));
        var ordered = twins
            .OrderByDescending(e => e.Status == EntityStatus.Confirmed)
            .ThenByDescending(e => e.Equivalents.Count)
            .ThenBy(e => e.CreatedAt).ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();
        var survivor = ordered[0];
        var merge = new List<Entity>();
        var kept = new List<(Entity, string)>();
        foreach (var other in ordered.Skip(1))
        {
            if (Compatible(survivor, other, out var reason)) merge.Add(other); else kept.Add((other, reason!));
        }
        return new DuplicateTwins(survivor.Class, survivor.Label ?? "", survivor, merge, kept);
    }

    /// <summary>Sources that are clusters of other sources' records and hold duplicates of one thing; a difference there is not a conflict when an id is shared elsewhere.</summary>
    private static readonly HashSet<string> Clustered = new(StringComparer.Ordinal) { "viaf", "lux" };

    /// <summary>Whether two entities of one label are the same thing by their ids (and, for a person, dates); the reason when not.</summary>
    public static bool Compatible(Entity a, Entity b, out string? reason)
    {
        reason = null;
        var idsA = a.Equivalents.Select(q => q.Uri).ToHashSet(StringComparer.Ordinal);
        var idsB = b.Equivalents.Select(q => q.Uri).ToHashSet(StringComparer.Ordinal);
        var shared = idsA.Overlaps(idsB);
        if (idsA.Count > 0 && idsB.Count > 0)
        {
            var both = a.Equivalents.Select(q => q.Source).Intersect(b.Equivalents.Select(q => q.Source), StringComparer.Ordinal).OrderBy(s => Clustered.Contains(s)).ThenBy(s => s, StringComparer.Ordinal).ToList();
            var clustersDiffer = new List<string>();
            foreach (var source in both)
            {
                var ofA = a.Equivalents.Where(q => q.Source == source).Select(q => q.Uri).ToHashSet(StringComparer.Ordinal);
                var ofB = b.Equivalents.Where(q => q.Source == source).Select(q => q.Uri);
                if (ofA.SetEquals(ofB)) continue;
                if (!Clustered.Contains(source)) { reason = $"different {source} ids"; return false; }
                clustersDiffer.Add(source);
            }
            if (!shared) { reason = clustersDiffer.Count > 0 ? $"different {string.Join(" and ", clustersDiffer)} ids, nothing else shared" : "ids from different sources, none shared"; return false; }
        }
        if (a.Class == EntityClass.Person && !shared && !SameDates(a.Lifespan, b.Lifespan))
        {
            reason = "a person twice without a shared id or the same dates";
            return false;
        }
        return true;
    }

    private static bool SameDates(Lifespan? a, Lifespan? b)
    {
        if (a is null || b is null || a.IsEmpty || b.IsEmpty) return false;
        if (a.BeginEarliest is null || b.BeginEarliest is null) return false;
        return a.BeginEarliest.Value.Year == b.BeginEarliest.Value.Year && a.EndEarliest?.Year == b.EndEarliest?.Year;
    }

    /// <summary>The label as the owner's count folds it: lower case, trimmed.</summary>
    public static string Fold(string? label) => (label ?? "").Trim().ToLowerInvariant();
}
