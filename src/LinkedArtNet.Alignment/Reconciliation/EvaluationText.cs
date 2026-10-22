using System.Text;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>
/// An evaluation as a line or two of text, for the tool's verbose mode and anywhere else a person watches the
/// reconciler work: the string, what became of it and what carried it, then (asked for) the top candidates in the
/// shape the decisions command prints them. A decomposed string's parts follow it, indented.
/// </summary>
public static class EvaluationText
{
    /// <param name="prefix">Goes before the string's own line only ("1234/46299 "), so the run's progress shows on every line; a decomposed string's parts are indented under it without one.</param>
    public static string Narrate(Evaluation e, bool candidates = false, int indent = 0, string? prefix = null)
    {
        var sb = new StringBuilder();
        var pad = new string(' ', indent);
        var s = e.Reading.String;
        sb.Append(prefix).Append(pad).Append('[').Append(s.Context).Append("] ").Append(s.Raw).Append(" -> ").Append(Outcome(e));
        sb.AppendLine();
        if (candidates)
            foreach (var g in e.Groups.Take(3))
                foreach (var m in g.Members.Take(3))
                    sb.Append(pad).Append("   ").Append(m.Score.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(m.Candidate.Source.PadRight(6)).Append(' ').Append(m.Candidate.Label)
                      .Append("  <").Append(m.Candidate.Uri).Append(">  ").Append(m.Reason).AppendLine();
        foreach (var part in e.Parts ?? []) sb.Append(Narrate(part, candidates, indent + 2 + (prefix?.Length ?? 0)));
        return sb.ToString();
    }

    private static string Outcome(Evaluation e)
    {
        switch (e.Outcome)
        {
            case Reconciliation.Outcome.Overridden: return $"overridden {e.Override?.Target}";
            case Reconciliation.Outcome.MatchedStore: return $"matched in store {e.StoreMatch?.Id} \"{e.StoreMatch?.Label}\"" + (e.StoreMatch?.Status == EntityStatus.Provisional ? " (provisional)" : "");
            case Reconciliation.Outcome.Accepted when e.Accepted is { } g:
            {
                var best = g.Best;
                return $"accepted {e.AcceptedMethod} {best.Candidate.Source} {best.Candidate.Label} <{best.Candidate.Uri}> {g.Score:0.00}" + (g.Sources.Count > 1 ? $"; sources {string.Join(", ", g.Sources)}" : "");
            }
            case Reconciliation.Outcome.Queued:
            {
                var live = e.Groups.Where(g => !g.Vetoed).ToList();
                return $"queued ({live.Count} group{(live.Count == 1 ? "" : "s")}: {string.Join(", ", live.Take(3).Select(g => $"{string.Join("+", g.Sources.DefaultIfEmpty("store"))} {g.Score:0.00}"))}{(live.Count > 3 ? ", ..." : "")})";
            }
            case Reconciliation.Outcome.Minted: return e.Groups.Count == 0 ? "minted (nothing found)" : $"minted (best {e.Groups[0].Score:0.00} {string.Join("+", e.Groups[0].Sources.DefaultIfEmpty("store"))})";
            case Reconciliation.Outcome.Decomposed: return $"{(e.Reading.Shape == PartsShape.Compound ? "composite" : "list")} ({e.Reading.Parts?.Count ?? 0} parts, {e.Parts?.Count ?? 0} new)";
            case Reconciliation.Outcome.Deferred: return "deferred";
            default: return e.Outcome.ToString().ToLowerInvariant();
        }
    }
}
