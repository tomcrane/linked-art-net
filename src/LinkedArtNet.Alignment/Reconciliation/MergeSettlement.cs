using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>
/// A person merging a placeholder into another entity has settled the placeholder's strings (the owner, 2026-11-07, at the Ackermanns:
/// "once I was in the various Ackermanns it seemed the easiest flow to go back and search for more"), but a merge re-points the loser's
/// valid decisions and leaves their method, so a queued string stayed in the queue and a minted one kept saying nothing was found. Before
/// the merge, each of the loser's strings whose standing decision is queued or minted is decided onto the survivor, method human, with the
/// merge as the evidence; undo of that decision puts the earlier one back.
/// </summary>
public static class MergeSettlement
{
    public static async Task<int> SettleAsync(IAlignmentStore store, Reconciler reconciler, Entity loser, Entity survivor, string reviewer, long? exceptStringId = null, CancellationToken ct = default)
    {
        var n = 0;
        foreach (var s in await store.StringsForEntityAsync(loser.Id, 1000, ct))
        {
            if (s.String.Id == exceptStringId || s.Decision.Method is not (DecisionMethod.Queued or DecisionMethod.Minted)) continue;
            await store.DecideAsync(s.String.Id, survivor.Id, DecisionMethod.Human,
                Reconciler.Evidence("same-as-by-merge", reconciler.Read(s.String), [], new { entity = survivor.Id, merged = loser.Id, was = s.Decision.Method }), reviewer, ct: ct);
            n++;
        }
        return n;
    }
}
