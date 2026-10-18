using System.Text;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>What an entity's ULAN record says about the entity.</summary>
public enum AuditFlag
{
    /// <summary>The entity's ULAN id is not in the loaded index: retired, merged or mistyped.</summary>
    UlanMissing,
    /// <summary>None of the entity's labels resembles any of the record's.</summary>
    LabelMismatch,
    /// <summary>Both sides have dates and they cannot be the same lifetime.</summary>
    DatesConflict,
    /// <summary>The entity says Person and the record says Group, or the reverse.</summary>
    TypeMismatch,
    /// <summary>The entity has an id in a source (LoC, Wikidata, VIAF) and ULAN's record links to a different id in that source: one of the two clusters is wrong.</summary>
    LinkConflict,
    /// <summary>ULAN links to an id in a source the entity has no id for: an equivalent the entity could gain.</summary>
    LinkGain,
}

public sealed record AuditItem(Entity Entity, string UlanUri, AuthorityRecord? Record, IReadOnlyList<AuditFlag> Flags, string Detail);

public sealed record AuditReport(int Entities, int WithUlan, IReadOnlyDictionary<AuditFlag, int> Flags, int Clean)
{
    public override string ToString() =>
        $"entities {Entities}, with a ULAN id {WithUlan}: clean {Clean}; " + string.Join(", ", Enum.GetValues<AuditFlag>().Select(f => $"{f} {Flags.GetValueOrDefault(f)}"));
}

/// <summary>
/// Checks the store's entities against the ULAN index now held locally: for every entity with a ULAN id, does the
/// record exist, do the labels resemble each other, do dates and type agree, and do the entity's other authority ids
/// agree with the links ULAN asserts. A link conflict is the strongest signal of a wrong cluster and needs no human
/// to find. Nothing is written; flagged entities are handed to the caller.
/// </summary>
public sealed class IndexAudit(IAlignmentStore store)
{
    public async Task<AuditReport> RunAsync(string dataset, Func<AuditItem, Task>? onItem = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var flags = new Dictionary<AuditFlag, int>();
        int entities = 0, withUlan = 0, clean = 0;
        string? after = null;
        while (true)
        {
            var page = await store.ListEntitiesAsync(dataset, "ulan", after, 500, ct);
            if (page.Count == 0) break;
            foreach (var e in page)
            {
                ct.ThrowIfCancellationRequested();
                after = e.Id;
                entities++;
                var ulan = e.Equivalents.FirstOrDefault(q => q.Source == "ulan")?.Uri;
                if (ulan is null) continue;
                withUlan++;
                var record = await store.GetAuthorityAsync(ulan, ct);
                var item = Check(e, ulan, record);
                if (item.Flags.Count == 0 || item.Flags.All(f => f == AuditFlag.LinkGain)) clean++;
                foreach (var f in item.Flags) flags[f] = flags.GetValueOrDefault(f) + 1;
                if (item.Flags.Count > 0 && onItem is not null) await onItem(item);
                if (withUlan % 2000 == 0) progress?.Report($"{withUlan} checked: link conflicts {flags.GetValueOrDefault(AuditFlag.LinkConflict)}, missing {flags.GetValueOrDefault(AuditFlag.UlanMissing)}");
            }
        }
        return new AuditReport(entities, withUlan, flags, clean);
    }

    public static AuditItem Check(Entity e, string ulan, AuthorityRecord? record)
    {
        var flags = new List<AuditFlag>();
        var detail = new StringBuilder();
        if (record is null) return new AuditItem(e, ulan, null, [AuditFlag.UlanMissing], "no record for " + ulan);

        var entityLabels = (e.Label is null ? e.Labels : e.Labels.Prepend(e.Label)).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var recordLabels = (record.Label is null ? record.Labels : record.Labels.Prepend(record.Label)).ToList();
        var best = entityLabels.Count == 0 ? 1.0 : entityLabels.Max(l => NameMatcher.Score(l, recordLabels));
        if (best < 0.8) { flags.Add(AuditFlag.LabelMismatch); detail.Append($"label {e.Label} vs {record.Label} ({best:0.00}); "); }

        if (e.Lifespan is { IsEmpty: false } el && record.Lifespan is { IsEmpty: false } rl && !el.Compatible(rl))
        { flags.Add(AuditFlag.DatesConflict); detail.Append($"dates {el.Text} vs {rl.Text}; "); }

        if (Enum.TryParse<EntityClass>(record.Type, true, out var rc) && rc != e.Class && (rc is EntityClass.Person or EntityClass.Group) && (e.Class is EntityClass.Person or EntityClass.Group))
        { flags.Add(AuditFlag.TypeMismatch); detail.Append($"type {e.Class} vs {rc}; "); }

        var mine = e.Equivalents.Where(q => q.Source != "ulan").GroupBy(q => q.Source).ToDictionary(g => g.Key, g => g.Select(q => q.Uri).ToHashSet(StringComparer.Ordinal));
        foreach (var link in record.SameAs)
        {
            var source = AuthorityUris.SourceOf(link);
            if (source == "other") continue;
            if (mine.TryGetValue(source, out var have))
            {
                if (!have.Contains(link)) { if (!flags.Contains(AuditFlag.LinkConflict)) flags.Add(AuditFlag.LinkConflict); detail.Append($"{source}: entity {string.Join("|", have)} vs ulan {link}; "); }
            }
            else if (!flags.Contains(AuditFlag.LinkGain)) { flags.Add(AuditFlag.LinkGain); detail.Append($"could gain {link}; "); }
        }
        return new AuditItem(e, ulan, record, flags, detail.ToString().TrimEnd(' ', ';'));
    }

    public static string CsvHeader => "entity,class,label,ulan,ulan_label,flags,detail";

    public static string CsvLine(AuditItem item)
    {
        static string Q(string? s) => s is null ? "" : "\"" + s.Replace("\"", "\"\"") + "\"";
        return string.Join(",", Q(item.Entity.Id), item.Entity.Class.ToString(), Q(item.Entity.Label), Q(item.UlanUri), Q(item.Record?.Label), Q(string.Join(" ", item.Flags)), Q(item.Detail));
    }
}
