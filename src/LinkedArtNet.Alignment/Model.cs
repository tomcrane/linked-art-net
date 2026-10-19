namespace LinkedArtNet.Alignment;

/// <summary>The classes the store mints identifiers for. Type, Material and Language share the concept path.</summary>
public enum EntityClass { Person, Group, Place, Type, Material, Language, Period, Event }

/// <summary>
/// Provisional: minted from a string, no external match. Aligned: at least one accepted equivalent. Confirmed: a
/// person signed it off. Merged: superseded by <see cref="Entity.MergedInto"/>, kept so the old URI still resolves.
/// </summary>
public enum EntityStatus { Provisional, Aligned, Confirmed, Merged }

/// <summary>How a decision or an equivalent came about; free text in the store, these are the names the toolkit uses.</summary>
public static class DecisionMethod
{
    public const string Override = "override";
    public const string ExactUri = "exact-uri";
    public const string ExactName = "exact-name";
    public const string Vote = "vote";
    /// <summary>One source alone, an exact name, no rival candidate at any score, and no dates on the string to check: accepted by policy (ReconcilerOptions.AcceptUniqueExactSingleSource), kept apart so these can be reviewed or reversed as a set.</summary>
    public const string ExactSingle = "exact-single";
    /// <summary>A person decided it in the workbench; the evidence says what they chose and why.</summary>
    public const string Human = "human";
    public const string LlmProposedThenHuman = "llm-proposed-then-human";
    /// <summary>Carried over from an earlier system's tables, identifiers preserved.</summary>
    public const string Imported = "imported";
    /// <summary>No match anywhere: a provisional entity was minted for the string.</summary>
    public const string Minted = "minted";
    /// <summary>Candidates were found but none accepted: the string is in the review queue, with a provisional entity meanwhile and the candidates in the evidence.</summary>
    public const string Queued = "queued";
    /// <summary>A LUX record that already represents one of our entities (design doc 10, 7.4).</summary>
    public const string LuxSelf = "lux-self";
}

/// <summary>An authority URI an entity is the same as, with how and how surely that was decided.</summary>
public sealed record Equivalent(string Uri, string Source, string Method, double Confidence, DateTimeOffset DecidedAt, string? DatasetVersion = null, string? RunId = null);

/// <summary>
/// When something began and ended, each as a range of possible instants: a person's birth lies between
/// <see cref="BeginEarliest"/> and <see cref="BeginLatest"/>, their death between <see cref="EndEarliest"/> and
/// <see cref="EndLatest"/>. <see cref="Text"/> is the dates as written. Any bound may be unknown.
/// </summary>
public enum DateAgreement { Agree, Conflict, Incomparable }

public sealed record Lifespan(DateTimeOffset? BeginEarliest, DateTimeOffset? BeginLatest, DateTimeOffset? EndEarliest, DateTimeOffset? EndLatest, string? Text = null)
{
    public bool IsEmpty => BeginEarliest is null && BeginLatest is null && EndEarliest is null && EndLatest is null;

    /// <summary>Whether two lifespans could belong to the same thing: anything but a <see cref="DateAgreement.Conflict"/>.</summary>
    public bool Compatible(Lifespan other, int? maxLifetimeYears = null) => Compare(other, maxLifetimeYears) != DateAgreement.Conflict;

    /// <summary>
    /// How two lifespans relate. <b>Agree</b>: a side both know (begin or end) overlaps and nothing contradicts.
    /// <b>Conflict</b>: a side both know is disjoint; or one's begin is after the other's end; or, given a longest
    /// lifetime, one's begin is more than that many years before the other's end. <b>Incomparable</b>: the two know
    /// different sides and neither check bites, so nothing can be said either way.
    /// </summary>
    public DateAgreement Compare(Lifespan other, int? maxLifetimeYears = null)
    {
        static bool Known(DateTimeOffset? a, DateTimeOffset? b) => a is not null || b is not null;
        static bool Overlap(DateTimeOffset? a1, DateTimeOffset? a2, DateTimeOffset? b1, DateTimeOffset? b2)
        {
            var aFrom = a1 ?? DateTimeOffset.MinValue; var aTo = a2 ?? DateTimeOffset.MaxValue;
            var bFrom = b1 ?? DateTimeOffset.MinValue; var bTo = b2 ?? DateTimeOffset.MaxValue;
            return aFrom <= bTo && bFrom <= aTo;
        }
        var beginsKnown = Known(BeginEarliest, BeginLatest) && Known(other.BeginEarliest, other.BeginLatest);
        var endsKnown = Known(EndEarliest, EndLatest) && Known(other.EndEarliest, other.EndLatest);
        if (beginsKnown && !Overlap(BeginEarliest, BeginLatest, other.BeginEarliest, other.BeginLatest)) return DateAgreement.Conflict;
        if (endsKnown && !Overlap(EndEarliest, EndLatest, other.EndEarliest, other.EndLatest)) return DateAgreement.Conflict;
        if (CrossConflict(BeginEarliest ?? BeginLatest, other.EndLatest ?? other.EndEarliest, maxLifetimeYears)) return DateAgreement.Conflict;
        if (CrossConflict(other.BeginEarliest ?? other.BeginLatest, EndLatest ?? EndEarliest, maxLifetimeYears)) return DateAgreement.Conflict;
        return beginsKnown || endsKnown ? DateAgreement.Agree : DateAgreement.Incomparable;
    }

    /// <summary>A begin after the other's end, or more than a lifetime before it, cannot be the same person.</summary>
    private static bool CrossConflict(DateTimeOffset? begin, DateTimeOffset? end, int? maxLifetimeYears)
    {
        if (begin is null || end is null) return false;
        if (begin > end) return true;
        return maxLifetimeYears is { } max && end.Value.Year - begin.Value.Year > max;
    }

    /// <summary>A lifespan from two Linked Art TimeSpans, as the dates parser yields for a pair.</summary>
    public static Lifespan? From(LinkedArtTimeSpan? begin, LinkedArtTimeSpan? end, string? text = null)
    {
        if (begin is null && end is null) return null;
        static DateTimeOffset? At(LinkedArtDate? d) => d is { } date && date.TryToDateTimeOffset(out var r) ? r : null;
        var l = new Lifespan(At(begin?.BeginOfTheBegin), At(begin?.EndOfTheEnd), At(end?.BeginOfTheBegin), At(end?.EndOfTheEnd), text);
        return l.IsEmpty && text is null ? null : l;
    }
}

/// <summary>Something the toolkit has minted an identifier for. The id is a URI under the dataset's identifier scheme, minted once and never reused.</summary>
public sealed record Entity(
    string Id,
    string Dataset,
    EntityClass Class,
    string? Label,
    IReadOnlyList<string> Labels,
    Lifespan? Lifespan,
    IReadOnlyList<string> Nationalities,
    IReadOnlyList<string> Roles,
    EntityStatus Status,
    string? MergedInto,
    IReadOnlyList<Equivalent> Equivalents,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? RunId);

/// <summary>What is known about an entity before it has an identifier. <see cref="Id"/> keeps an identifier already published (an import); null mints one.</summary>
public sealed record NewEntity(
    EntityClass Class,
    string? Label,
    IReadOnlyList<string>? Labels = null,
    Lifespan? Lifespan = null,
    IReadOnlyList<string>? Nationalities = null,
    IReadOnlyList<string>? Roles = null,
    EntityStatus Status = EntityStatus.Provisional,
    string? Id = null,
    string? RunId = null);

/// <summary>A string as it occurred in a dataset. The key is (dataset, context, normalised); the same name in another context may be another person.</summary>
public sealed record SourceString(long Id, string Dataset, string Context, string Raw, string Normalised, string? ParsedJson, DateTimeOffset FirstSeen, DateTimeOffset LastSeen, int Occurrences);

/// <summary>What a source string refers to. A null entity means it was looked at and left unresolved; <see cref="Valid"/> false means a later event (merge, authority update) reopened it.</summary>
/// <param name="Supersedes">The decision this one replaced when it was made, so an undo restores exactly that one.</param>
public sealed record Decision(long Id, long SourceStringId, string? EntityId, string Method, string? EvidenceJson, string? Reviewer, DateTimeOffset DecidedAt, string? DatasetVersion, string? RunId, bool Valid, long? Supersedes = null);

/// <summary>A cached copy of an external authority's record, so reconciliation reads locally and fetches once.</summary>
public sealed record AuthorityRecord(string Uri, string Source, string? Label, IReadOnlyList<string> Labels, string? Type, Lifespan? Lifespan, IReadOnlyList<string> SameAs, string? DataJson, DateTimeOffset FetchedAt, string? SourceVersion);

/// <summary>One execution of a pipeline: what dataset and dumps, when, which code, with what counts and report.</summary>
public sealed record Run(string Id, string Dataset, string? ManifestJson, string? DumpsJson, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string? GitSha, string? CountsJson, string? Report);

/// <summary>A curated answer for a string: this normalised text in this dataset (and context, or any context when null) is this entity or authority URI. Always wins.</summary>
public sealed record Override(string Dataset, string? Context, string Normalised, string Target, string Source, string? Note = null);

/// <summary>A string with the decision a run made about it.</summary>
public sealed record DecidedString(SourceString String, Decision Decision);

/// <summary>What undoing a run removed.</summary>
public sealed record UndoResult(int DecisionsInvalidated, int ProvisionalEntitiesRemoved);

/// <summary>How many strings of one context a run decided by one method.</summary>
public sealed record ContextMethodCount(string Context, string Method, long Count);

/// <summary>How many of a project's live entities (not merged) are in each status and class.</summary>
public sealed record EntityCount(EntityStatus Status, EntityClass Class, long Count);

/// <summary>An authority source loaded into the store: its name, how many records it has, and when it was last loaded.</summary>
public sealed record AuthoritySource(string Source, long Records, DateTimeOffset LoadedAt);

/// <summary>A project: a dataset with its name, identifier base, manifest (context rules, thresholds, sources) and members (design doc 12, section 4).</summary>
public sealed record Project(string Dataset, string Name, string? Description, string IdentifierBase, string? ManifestJson, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>A signed-in person; the login is the reviewer name on decisions.</summary>
public sealed record User(string Login, string? DisplayName, DateTimeOffset CreatedAt, DateTimeOffset? LastSeen);

public static class ProjectRole
{
    public const string Reviewer = "reviewer";
    public const string Operator = "operator";
    public const string Owner = "owner";
    public static readonly string[] All = [Reviewer, Operator, Owner];
    /// <summary>Whether a role includes another's rights: owner includes operator includes reviewer.</summary>
    public static bool Covers(string role, string required) => Array.IndexOf(All, role) >= Array.IndexOf(All, required) && Array.IndexOf(All, role) >= 0;
}

public sealed record Membership(string Dataset, string Login, string Role, DateTimeOffset AddedAt);

/// <summary>Free text a reviewer attached to something: a string ("string:123"), an entity (its URI) or a run.</summary>
public sealed record Note(long Id, string Dataset, string Target, string? Author, string Text, DateTimeOffset CreatedAt);

public sealed record StoreCounts(long Entities, long SourceStrings, long Decided, long Unresolved, long Overrides, long AuthorityRecords, long Runs);

/// <summary>The authority namespaces the toolkit recognises, with the URI each identifier lives under.</summary>
public static class AuthorityUris
{
    public const string Ulan = "http://vocab.getty.edu/ulan/";
    public const string Aat = "http://vocab.getty.edu/aat/";
    public const string Tgn = "http://vocab.getty.edu/tgn/";
    public const string Viaf = "http://viaf.org/viaf/";
    public const string Wikidata = "http://www.wikidata.org/entity/";
    public const string LocNames = "http://id.loc.gov/authorities/names/";
    public const string LocSubjects = "http://id.loc.gov/authorities/subjects/";
    public const string Lux = "https://lux.collections.yale.edu/data/";
    public const string PeriodO = "http://n2t.net/ark:/99152/";

    /// <summary>A Library of Congress identifier's URI: subject headings begin "sh", everything else is a name.</summary>
    public static string Loc(string id) => (id.StartsWith("sh", StringComparison.OrdinalIgnoreCase) ? LocSubjects : LocNames) + id;

    /// <summary>The source name for an authority URI ("ulan", "viaf", ...), or "other".</summary>
    public static string SourceOf(string uri)
    {
        if (uri.StartsWith(Ulan, StringComparison.Ordinal)) return "ulan";
        if (uri.StartsWith(Aat, StringComparison.Ordinal)) return "aat";
        if (uri.StartsWith(Tgn, StringComparison.Ordinal)) return "tgn";
        if (uri.StartsWith(Viaf, StringComparison.Ordinal)) return "viaf";
        if (uri.StartsWith(Wikidata, StringComparison.Ordinal)) return "wikidata";
        if (uri.StartsWith("http://id.loc.gov/", StringComparison.Ordinal)) return "loc";
        if (uri.StartsWith("https://lux.collections.yale.edu/", StringComparison.Ordinal)) return "lux";
        if (uri.StartsWith(PeriodO, StringComparison.Ordinal)) return "periodo";
        return "other";
    }
}
