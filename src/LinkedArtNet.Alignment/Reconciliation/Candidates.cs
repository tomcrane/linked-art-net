using LinkedArtNet.Agents;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>How strings in one context are read: what they may name, and whether the agent-string parser separates the name from dates, roles and honorifics first.</summary>
/// <param name="Classes">The entity classes a string in this context may name, in order of preference; the first is what a new entity is minted as, and the agent parser's reading of the kind moves that class to the front.</param>
/// <param name="AgentProfile">The agent parser profile to read the string with, or null to take the text as the whole name.</param>
/// <param name="Qualified">Strings here name one thing with where it is or what tells it from namesakes ("Hammersmith, London"): the name is looked up and the qualifier checked against the records (see <see cref="Reconciliation.Qualified"/>).</param>
public sealed record ContextRule(IReadOnlyList<EntityClass> Classes, string? AgentProfile = null, bool Qualified = false)
{
    public static readonly ContextRule Anything = new([EntityClass.Person, EntityClass.Group, EntityClass.Place, EntityClass.Type]);
}

/// <summary>
/// What the reconciler understood of a source string before looking anywhere: the name part, its lookup keys, the
/// classes it may be, and the lifespan, nationalities and roles the string itself carried.
/// </summary>
public sealed record StringReading(
    SourceString String,
    string Text,
    IReadOnlyList<string> Keys,
    IReadOnlyList<EntityClass> Classes,
    Lifespan? Lifespan,
    IReadOnlyList<string> Nationalities,
    IReadOnlyList<string> Roles,
    ParsedAgent? Agent,
    string? Qualifier = null,
    bool IsQualified = false)
{
    public string Key => Keys.Count > 0 ? Keys[0] : "";
    public bool HasLifespan => Lifespan is { IsEmpty: false };
    /// <summary>The label a new entity gets: the name with its qualifier, so a minted "Heaton, Bradford" is not a bare "Heaton" that captures every other Heaton.</summary>
    public string Label => Qualifier is null ? Text : $"{Text}, {Qualifier}";

    /// <summary>Reads a string under its context's rule, through the agent parser when the rule names a profile.</summary>
    public static StringReading Read(SourceString s, ContextRule rule, AgentStringParser? parser)
    {
        ParsedAgent? agent = null;
        var text = s.Raw;
        var classes = rule.Classes;
        Lifespan? lifespan = null;
        IReadOnlyList<string> nationalities = [];
        IReadOnlyList<string> roles = [];
        string? qualifier = null;
        if (rule.Qualified && parser is null) (text, qualifier) = Qualified.Split(s.Raw);
        if (parser is not null)
        {
            agent = parser.Parse(s.Raw);
            if (agent.HasName) text = agent.Name!;
            // the parser's reading of the kind puts that class first; the rule still says what is allowed at all
            if (agent.Kind == AgentKind.Group && classes.Contains(EntityClass.Group)) classes = [EntityClass.Group, .. classes.Where(c => c != EntityClass.Group)];
            else if (agent.Kind == AgentKind.Person && classes.Contains(EntityClass.Person)) classes = [EntityClass.Person, .. classes.Where(c => c != EntityClass.Person)];
            if (agent.Dates is { Kind: Dates.ResultKind.BirthDeath or Dates.ResultKind.FormationDissolution } d)
                lifespan = Lifespan.From(d.Begin, d.End, agent.DateText);
            nationalities = agent.Nationalities.Select(n => n.AatUri ?? n.Label).Distinct(StringComparer.Ordinal).ToArray();
            roles = agent.Roles.Where(r => r.IsOccupation).Select(r => r.Label).Distinct(StringComparer.Ordinal).ToArray();
        }
        return new StringReading(s, text, NameMatcher.Keys(text), classes, lifespan, nationalities, roles, agent, qualifier, rule.Qualified);
    }
}

/// <summary>Something a string might refer to, from one source: an authority record, or an entity already in the store.</summary>
/// <param name="Stage">Where it came from: override, store, local, remote.</param>
/// <param name="NameScore">How well the string's name matched this candidate's labels, 0..1.</param>
/// <param name="EntityId">Set when the candidate is an entity in the store rather than an external record.</param>
/// <param name="Provisional">A store entity minted for an earlier string without any authority behind it: a lookalike that must not make a known entity ambiguous or contradict a vote.</param>
/// <param name="ProvisionalQueued">A provisional entity whose own string is awaiting review: it never answers another string silently.</param>
public sealed record Candidate(
    string Uri,
    string Source,
    string? Label,
    EntityClass? Class,
    Lifespan? Lifespan,
    IReadOnlyList<string> SameAs,
    string Stage,
    double NameScore,
    string? EntityId = null,
    IReadOnlyList<string>? Nationalities = null,
    bool Provisional = false,
    bool ProvisionalQueued = false,
    bool Disambiguated = false,
    string? Placement = null);

/// <summary>A candidate with the reconciler's verdict on it.</summary>
/// <param name="DatesAgree">The string's dates and the candidate's were both known on the same side and overlapped; what the single-source rule needs.</param>
/// <param name="QualifierAgrees">The string's qualifier was found in the record's placement (true), looked for and not found (false), or there was nothing to compare (null).</param>
public sealed record ScoredCandidate(Candidate Candidate, double Score, bool Veto, string Reason, bool DatesAgree = false, bool? QualifierAgrees = null);

/// <summary>Candidates that are the same real-world thing across sources, joined through their mutual sameAs links, voting as one.</summary>
/// <param name="Vetoed">Every member is vetoed, or a vetoed member is the same thing as a live one by the authorities' own links: the group cannot be the answer.</param>
public sealed record CandidateGroup(IReadOnlyList<ScoredCandidate> Members, double Score, IReadOnlyList<string> Sources, IReadOnlyList<string> Uris, bool Vetoed = false)
{
    /// <summary>The highest-scoring member that was not vetoed, or the highest-scoring member of a vetoed group.</summary>
    public ScoredCandidate Best => (Members.Where(m => !m.Veto).OrderByDescending(m => m.Score).FirstOrDefault() ?? Members.OrderByDescending(m => m.Score).First());
}

/// <summary>What a source found, and whether it had more than it was allowed to return.</summary>
public sealed record CandidateSet(IReadOnlyList<Candidate> Candidates, bool Truncated)
{
    public static readonly CandidateSet Empty = new([], false);
}

/// <summary>A place to look for what a string names. Local sources read the store's authority records; remote ones call a service through a cache and a budget.</summary>
public interface IAuthoritySource
{
    string Name { get; }
    bool IsRemote { get; }
    Task<CandidateSet> FindAsync(StringReading reading, CancellationToken ct = default);
}

/// <summary>A local index: the authority records of one source (ulan, aat, tgn, loc) already in the store, looked up by the reading's keys and limited to the classes the reading allows. When more records match than the limit, the set says so and the reconciler sends the string to review rather than accept a candidate it may not have seen the rival of.</summary>
public sealed class LocalIndexSource(IAlignmentStore store, string source, int limit = 200) : IAuthoritySource
{
    public string Name => source;
    public bool IsRemote => false;

    public async Task<CandidateSet> FindAsync(StringReading reading, CancellationToken ct = default)
    {
        if (reading.Keys.Count == 0) return CandidateSet.Empty;
        var found = new Dictionary<string, AuthorityRecord>(StringComparer.Ordinal);
        var truncated = false;
        foreach (var cls in reading.Classes)
        {
            var page = await store.FindAuthoritiesByLabelsAsync(reading.Keys, source, cls.ToString(), limit + 1, ct);
            if (page.Count > limit) truncated = true;
            foreach (var r in page.Take(limit)) found[r.Uri] = r;
        }
        return new CandidateSet(found.Values.Select(r => ToCandidate(r, reading, "local")).ToList(), truncated);
    }

    public static Candidate ToCandidate(AuthorityRecord r, StringReading reading, string stage)
    {
        var cls = Enum.TryParse<EntityClass>(r.Type, true, out var c) ? c : (EntityClass?)null;
        IReadOnlyList<string>? nationalities = null;
        var disambiguated = false;
        string? parentString = null;
        if (r.DataJson is not null)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(r.DataJson);
                if (doc.RootElement.TryGetProperty("parentString", out var ps) && ps.ValueKind == System.Text.Json.JsonValueKind.String) parentString = ps.GetString();
                if (doc.RootElement.TryGetProperty("nationalities", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.Array)
                    nationalities = n.EnumerateArray().Select(e => e.GetString()).OfType<string>().ToArray();
                // a LoC heading carries dates ("Taylor, Desmond, 1930-") when the cataloguer had namesakes to tell apart: the record is disambiguated
                disambiguated = doc.RootElement.TryGetProperty("headingDates", out var hd) && hd.ValueKind == System.Text.Json.JsonValueKind.String;
            }
            catch (System.Text.Json.JsonException) { }
        }
        var labels = r.Label is null ? r.Labels : r.Labels.Prepend(r.Label);
        return new Candidate(r.Uri, r.Source, r.Label, cls, r.Lifespan, r.SameAs, stage, NameMatcher.Score(reading.Text, labels), null, nationalities, Disambiguated: disambiguated,
            Placement: reading.Qualifier is null ? null : Qualified.PlacementOf(parentString, labels));
    }
}
