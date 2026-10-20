using System.Globalization;
using System.Text;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>How a shadow evaluation of an old decision compares with it.</summary>
public enum Verdict
{
    /// <summary>The new answer points at the old entity or one of its authority ids.</summary>
    Agree,
    /// <summary>The new answer is accepted and names a different record from the same source as one the old entity has: one of them is wrong.</summary>
    Disagree,
    /// <summary>The new run would accept something the old entity has no id in common with, in a source the old entity never had: new information, not a contradiction.</summary>
    Unverifiable,
    /// <summary>The new run would send it to review; the old answer is among the candidates.</summary>
    QueuedWithOld,
    /// <summary>The new run would send it to review; the top candidates share a source with the old entity's ids but name different records.</summary>
    QueuedConflict,
    /// <summary>The new run would send it to review and its candidates' ids and the old entity's have no source in common (a ULAN person against LoC and VIAF ids, say): nothing to compare.</summary>
    QueuedUnlinked,
    /// <summary>The new run found nothing; the old run had an answer with authority ids.</summary>
    OldOnly,
    /// <summary>The new run reads the string as a list of parts, each decided on its own; the old run had one answer for the whole.</summary>
    Decomposed,
    /// <summary>The old entity has no authority ids, so there is nothing to compare: a local entity then, and a provisional one now.</summary>
    OldLocal,
    /// <summary>The string could not be read.</summary>
    Skipped,
}

/// <summary>One string's shadow result: the old decision and entity, the new evaluation, and the verdict.</summary>
public sealed record ShadowItem(SourceString String, Decision OldDecision, Entity? OldEntity, Evaluation New, Verdict Verdict);

public sealed record ShadowReport(int Considered, IReadOnlyDictionary<Verdict, int> Totals, IReadOnlyList<(string Context, Verdict Verdict, int Count)> ByContext)
{
    public int this[Verdict v] => Totals.GetValueOrDefault(v);

    public override string ToString()
    {
        var sb = new StringBuilder($"considered {Considered}: ");
        sb.Append(string.Join(", ", Enum.GetValues<Verdict>().Where(v => this[v] > 0).Select(v => $"{v} {this[v]}")));
        return sb.ToString();
    }
}

/// <summary>
/// The shadow run: re-judges decisions made earlier (the imported ones, by default) as if from scratch, with the
/// store's own entities hidden so an old decision cannot answer itself, and compares each new evaluation with the old
/// entity's authority ids. Nothing is written except the run record and the report; the disagreements are written to
/// a file for review.
/// </summary>
public sealed class ShadowRun(IAlignmentStore store, Reconciler reconciler)
{
    public async Task<ShadowReport> RunAsync(string dataset, string method = DecisionMethod.Imported, string? context = null, int limit = 100_000, bool sample = false,
        Func<ShadowItem, Task>? onItem = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var totals = new Dictionary<Verdict, int>();
        var byContext = new Dictionary<(string, Verdict), int>();
        var considered = 0;
        long after = 0;
        while (considered < limit)
        {
            var page = await store.DecidedStringsAsync(dataset, method, context, after, sample ? limit : Math.Min(500, limit - considered), sample, ct);
            if (page.Count == 0) break;
            foreach (var item in page)
            {
                ct.ThrowIfCancellationRequested();
                after = item.String.Id;
                var old = item.Decision.EntityId is null ? null : await store.ResolveEntityAsync(item.Decision.EntityId, ct);
                var evaluation = await reconciler.EvaluateAsync(dataset, item.String, includeStore: false, ct);
                var verdict = Judge(old, evaluation);
                totals[verdict] = totals.GetValueOrDefault(verdict) + 1;
                byContext[(item.String.Context, verdict)] = byContext.GetValueOrDefault((item.String.Context, verdict)) + 1;
                considered++;
                if (onItem is not null) await onItem(new ShadowItem(item.String, item.Decision, old, evaluation, verdict));
                if (considered % 500 == 0) progress?.Report($"{considered}: agree {totals.GetValueOrDefault(Verdict.Agree)}, disagree {totals.GetValueOrDefault(Verdict.Disagree)}, old only {totals.GetValueOrDefault(Verdict.OldOnly)}");
            }
            if (sample) break; // one random page is the sample
        }
        return new ShadowReport(considered, totals, byContext.Select(kv => (kv.Key.Item1, kv.Key.Item2, kv.Value)).OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList());
    }

    /// <summary>Compares a new evaluation with the old entity's identifiers.</summary>
    public static Verdict Judge(Entity? old, Evaluation e)
    {
        if (e.Outcome == Outcome.Skipped) return Verdict.Skipped;
        var oldUris = old is null ? [] : old.Equivalents.Select(q => q.Uri).Append(old.Id).ToHashSet(StringComparer.Ordinal);
        var oldExternal = old is null ? new HashSet<string>(StringComparer.Ordinal) : old.Equivalents.Select(q => q.Uri).ToHashSet(StringComparer.Ordinal);
        var target = e.TargetUris;
        switch (e.Outcome)
        {
            case Outcome.Overridden:
            case Outcome.Accepted:
            case Outcome.MatchedStore:
            {
                if (target.Any(oldUris.Contains)) return Verdict.Agree;
                if (oldExternal.Count == 0) return Verdict.OldLocal;
                // the same source on both sides, different ids: a contradiction
                var oldSources = oldExternal.Select(AuthorityUris.SourceOf).Where(s => s != "other").ToHashSet(StringComparer.Ordinal);
                return target.Select(AuthorityUris.SourceOf).Any(oldSources.Contains) ? Verdict.Disagree : Verdict.Unverifiable;
            }
            case Outcome.Queued:
            {
                if (oldExternal.Count == 0) return Verdict.OldLocal;
                if (target.Any(oldUris.Contains)) return Verdict.QueuedWithOld;
                var oldSources = oldExternal.Select(AuthorityUris.SourceOf).Where(s => s != "other").ToHashSet(StringComparer.Ordinal);
                return target.Select(AuthorityUris.SourceOf).Any(oldSources.Contains) ? Verdict.QueuedConflict : Verdict.QueuedUnlinked;
            }
            case Outcome.Decomposed:
                return Verdict.Decomposed;
            default:
                return oldExternal.Count == 0 ? Verdict.OldLocal : Verdict.OldOnly;
        }
    }

    /// <summary>One CSV line per item, for the review spreadsheet: the string, the old answer, the new verdict and the new top candidates.</summary>
    public static string CsvHeader => "context,string,verdict,old_entity,old_label,old_ids,new_outcome,new_read_as,new_lifespan,new_top_uri,new_top_label,new_top_score,new_top_sources,new_second_uri,new_second_label,new_second_score";

    public static string CsvLine(ShadowItem item)
    {
        static string Q(string? s) => s is null ? "" : "\"" + (s.Length > 0 && "=+-@\t\r".Contains(s[0]) ? "'" + s : s).Replace("\"", "\"\"") + "\""; // a leading formula character would run when the report is opened in a spreadsheet
        var top = item.New.Groups.FirstOrDefault();
        var second = item.New.Groups.Skip(1).FirstOrDefault();
        var ids = item.OldEntity is null ? "" : string.Join(" ", item.OldEntity.Equivalents.Select(q => q.Uri));
        return string.Join(",",
            Q(item.String.Context), Q(item.String.Raw), item.Verdict.ToString(), Q(item.OldEntity?.Id), Q(item.OldEntity?.Label), Q(ids),
            item.New.Outcome.ToString(), Q(item.New.Reading.Text), Q(item.New.Reading.Lifespan?.Text),
            Q(top?.Best.Candidate.Uri), Q(top?.Best.Candidate.Label), top is null ? "" : top.Score.ToString("0.00", CultureInfo.InvariantCulture), Q(top is null ? null : string.Join(" ", top.Sources)),
            Q(second?.Best.Candidate.Uri), Q(second?.Best.Candidate.Label), second is null ? "" : second.Score.ToString("0.00", CultureInfo.InvariantCulture));
    }
}
