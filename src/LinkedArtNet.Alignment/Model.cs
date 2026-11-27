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
    /// <summary>One source alone, carried by what the string says about itself: dates that agree with the record's, or a qualifier the record's placement bears out ("Hammersmith, London" on TGN's Hammersmith under London). Kept apart from vote so carried singles can be reviewed as a set.</summary>
    public const string Single = "single";
    /// <summary>The string names several things ("Dublin; New York"): decided as its parts, which are strings of their own (source_string_parts); no entity of its own.</summary>
    public const string List = "list";
    /// <summary>
    /// The string is not a name: a cataloguer's statement that nothing is known ("s.n.", "S.l.", "[publisher not identified]") or what is
    /// left of a slip ("S", "21"). No entity; the evidence says which (<see cref="Reconciliation.CurationService.NothingKnown"/> or
    /// <see cref="Reconciliation.CurationService.RecordWantsFixing"/>). The owner, 2026-11-22.
    /// </summary>
    public const string NotAName = "not-a-name";
    /// <summary>The string means different things in different records, and every record it is in has a decision of its own (record_decisions): no entity for the string itself.</summary>
    public const string ByRecord = "by-record";
    /// <summary>A compound heading ("Painting--Canada") decided as a concept coordinated from its parts, which are strings of their own (source_string_parts); the entity is the heading's own Type (doc 10, section 7.5).</summary>
    public const string Composite = "composite";
    /// <summary>An equivalent a legacy alignment gained through its Wikidata item: the item's sameAs names the local record (the pass ThroughWikidata, 2026-11-06), reversible by undo-run.</summary>
    public const string ThroughWikidata = "through-wikidata";
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
    /// <summary>A bound is tight when both its edges are given and lie within about a decade: a year, or a short range. ULAN's windows of possibility (1850-2080) have one edge each and are loose.</summary>
    public static bool Tight(DateTimeOffset? earliest, DateTimeOffset? latest) => earliest is not null && latest is not null && (latest.Value - earliest.Value).TotalDays <= 366 * 10;

    /// <summary>Whether this lifespan dates its subject: a tight begin or a tight end. Loose outer bounds can rule a match out but never confirm one.</summary>
    public bool IsDated => Tight(BeginEarliest, BeginLatest) || Tight(EndEarliest, EndLatest);

    /// <summary>
    /// Working dates ("fl. 1775-1795", "active 1640-1660", "op 1739", a lone "1730?"): born no later than the start, died no earlier than
    /// the end, and nothing more. The shape is a latest begin and an earliest end with no outer edges (finding 26, 2026-10-23).
    /// </summary>
    public bool IsWorking => BeginEarliest is null && EndLatest is null && BeginLatest is not null && EndEarliest is not null;

    /// <summary>
    /// The lifespan as a person should see it: the source's own text when both sides are dated or the dates are working dates; a dated side
    /// alone when the other is a window of possibility ("1952-", "-1748"); nothing for a window alone, since ULAN's "1200~-~2080" says only
    /// that the dates are unknown (the desk, 2026-10-26: every undated ULAN owner wore it as if it were a life).
    /// </summary>
    public string? Display
    {
        get
        {
            if (IsWorking) return Text;
            bool begin = Tight(BeginEarliest, BeginLatest), end = Tight(EndEarliest, EndLatest);
            if (begin && end) return Text;
            if (begin) return Year(BeginEarliest!.Value, BeginLatest!.Value) + "-";
            if (end) return "-" + Year(EndEarliest!.Value, EndLatest!.Value);
            return null;
        }
    }

    private static string Year(DateTimeOffset earliest, DateTimeOffset latest) =>
        earliest.Year == latest.Year ? earliest.Year.ToString(System.Globalization.CultureInfo.InvariantCulture) : "c. " + ((earliest.Year + latest.Year) / 2).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The longest working life allowed between two sets of working dates of the same person: "fl. 1775-1795" and "active 1840-1876" are
    /// two people, though a lifetime of 120 years could hold both. Eighty years is the assistant's judgement (a working life from the
    /// teens to the nineties), not a sourced figure.
    /// </summary>
    public const int MaxWorkingYears = 80;

    /// <summary>
    /// A near miss: both lifespans dated on both sides, one side the same year and the other a year or two out ("1815-1882" against
    /// "1813-1882"). Painters' birth years wobble between references, and a catalogue's year a little out from the authorities' names the
    /// same person (finding 115, 2026-11-08, "Hall, Harry, 1815-1882" against ULAN's and LoC's painter of 1813-1882). Not agreement, which
    /// would carry a single source; not a conflict, which would veto; the candidate stands at its name score.
    /// </summary>
    public bool NearMiss(Lifespan other, int years = 2)
    {
        if (IsWorking || other.IsWorking) return false;
        if (!Tight(BeginEarliest, BeginLatest) || !Tight(other.BeginEarliest, other.BeginLatest) || !Tight(EndEarliest, EndLatest) || !Tight(other.EndEarliest, other.EndLatest)) return false;
        var begin = Math.Abs(BeginEarliest!.Value.Year - other.BeginEarliest!.Value.Year);
        var end = Math.Abs(EndEarliest!.Value.Year - other.EndEarliest!.Value.Year);
        return (begin is >= 1 && begin <= years && end == 0) || (end is >= 1 && end <= years && begin == 0);
    }

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
        if (CrossConflict(BeginEarliest, BeginLatest, other.EndEarliest, other.EndLatest, maxLifetimeYears)) return DateAgreement.Conflict;
        if (CrossConflict(other.BeginEarliest, other.BeginLatest, EndEarliest, EndLatest, maxLifetimeYears)) return DateAgreement.Conflict;
        if (IsWorking && other.IsWorking)
        {
            // two sets of working dates: too far apart for one working life is a conflict; starts and ends each within a decade agree,
            // as two birth years within a decade do (finding 26, 2026-10-23)
            var hull = Math.Max(EndEarliest!.Value.Year, other.EndEarliest!.Value.Year) - Math.Min(BeginLatest!.Value.Year, other.BeginLatest!.Value.Year);
            if (hull > MaxWorkingYears) return DateAgreement.Conflict;
            if (Tight(Earlier(BeginLatest, other.BeginLatest), Later(BeginLatest, other.BeginLatest)) && Tight(Earlier(EndEarliest, other.EndEarliest), Later(EndEarliest, other.EndEarliest))) return DateAgreement.Agree;
            return DateAgreement.Incomparable;
        }
        // agreement needs a tight bound on both sides: a window of possibility (ULAN's 1850-2080) overlaps anything and proves nothing
        var beginsDated = Tight(BeginEarliest, BeginLatest) && Tight(other.BeginEarliest, other.BeginLatest);
        var endsDated = Tight(EndEarliest, EndLatest) && Tight(other.EndEarliest, other.EndLatest);
        return beginsDated || endsDated ? DateAgreement.Agree : DateAgreement.Incomparable;
    }

    /// <summary>
    /// A begin after the other's latest possible end, or a lifetime longer than the longest allowed even between the latest possible
    /// begin and the earliest possible end, cannot be the same person. An outer bound alone ("died no later than ~2090", ULAN's window
    /// for a living artist) never makes a lifetime: "Gilbert, 1943-" against ULAN's 1943-~2090 is the same Gilbert (finding 52, 2026-10-22).
    /// </summary>
    private static bool CrossConflict(DateTimeOffset? beginEarliest, DateTimeOffset? beginLatest, DateTimeOffset? endEarliest, DateTimeOffset? endLatest, int? maxLifetimeYears)
    {
        var begin = beginEarliest ?? beginLatest;
        if (begin is not null && endLatest is not null && begin > endLatest) return true;
        if (begin is not null && endLatest is null && endEarliest is not null && begin > endEarliest) return true;
        var latestBegin = beginLatest ?? beginEarliest;
        return maxLifetimeYears is { } max && latestBegin is not null && endEarliest is not null && endEarliest.Value.Year - latestBegin.Value.Year > max;
    }

    private static DateTimeOffset? Earlier(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a < b ? a : b;
    private static DateTimeOffset? Later(DateTimeOffset? a, DateTimeOffset? b) => a is null ? b : b is null ? a : a > b ? a : b;

    private static DateTimeOffset? At(LinkedArtDate? d) => d is { } date && date.TryToDateTimeOffset(out var r) ? r : null;

    /// <summary>A lifespan from two Linked Art TimeSpans, as the dates parser yields for a pair.</summary>
    public static Lifespan? From(LinkedArtTimeSpan? begin, LinkedArtTimeSpan? end, string? text = null)
    {
        if (begin is null && end is null) return null;
        var l = new Lifespan(At(begin?.BeginOfTheBegin), At(begin?.EndOfTheEnd), At(end?.BeginOfTheBegin), At(end?.EndOfTheEnd), text);
        return l.IsEmpty && text is null ? null : l;
    }

    /// <summary>
    /// Working dates from the spans the dates parser yields for a floruit ("fl. 1775-1795": the first and last years; "op 1739": the
    /// one span twice): alive through the whole of it, so born no later than the last day of the start and died no earlier than the
    /// first day of the end (<see cref="IsWorking"/>).
    /// </summary>
    public static Lifespan? Working(LinkedArtTimeSpan? start, LinkedArtTimeSpan? end, string? text = null)
    {
        start ??= end; end ??= start;
        if (start is null) return null;
        var l = new Lifespan(null, At(start.EndOfTheEnd), At(end!.BeginOfTheBegin), null, text);
        return l.IsWorking ? l : null;
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
    string? RunId)
{
    /// <summary>Names a source gives the entity that a person has set aside: no string is matched on them, and no update, pull or merge puts them back among <see cref="Labels"/>.</summary>
    public IReadOnlyList<string> SetAsideLabels { get; init; } = [];
}

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

/// <summary>A record behind a string, for the desk: where the string was seen, with the record's title and date, the element it came from, and the record's naming fields as JSON (field to values).</summary>
public sealed record StringExample(long StringId, string RecordId, string Source, string? Title, string? Date, string Field, string? RecordJson, long? ParentStringId = null, int Position = 0)
{
    /// <summary>The record's naming fields in the order the export gives them, the string's own field first.</summary>
    public IEnumerable<(string Name, IReadOnlyList<string> Values)> NamingFields()
    {
        if (RecordJson is null) yield break;
        using var doc = System.Text.Json.JsonDocument.Parse(RecordJson);
        var all = doc.RootElement.EnumerateObject().Select(p => (p.Name, (IReadOnlyList<string>)p.Value.EnumerateArray().Select(v => v.GetString() ?? "").ToList())).ToList();
        foreach (var f in all.Where(f => f.Name == Field)) yield return f;
        foreach (var f in all.Where(f => f.Name != Field && f.Name is not ("title" or "year"))) yield return f;
    }
}

/// <summary>What a source string refers to. A null entity means it was looked at and left unresolved; <see cref="Valid"/> false means a later event (merge, authority update) reopened it.</summary>
/// <param name="Supersedes">The decision this one replaced when it was made, so an undo restores exactly that one.</param>
public sealed record Decision(long Id, long SourceStringId, string? EntityId, string Method, string? EvidenceJson, string? Reviewer, DateTimeOffset DecidedAt, string? DatasetVersion, string? RunId, bool Valid, long? Supersedes = null);

/// <summary>A cached copy of an external authority's record, so reconciliation reads locally and fetches once.</summary>
public sealed record AuthorityRecord(string Uri, string Source, string? Label, IReadOnlyList<string> Labels, string? Type, Lifespan? Lifespan, IReadOnlyList<string> SameAs, string? DataJson, DateTimeOffset FetchedAt, string? SourceVersion);

/// <summary>One execution of a pipeline: what dataset and dumps, when, which code, with what counts and report.</summary>
public sealed record Run(string Id, string Dataset, string? ManifestJson, string? DumpsJson, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string? GitSha, string? CountsJson, string? Report);

/// <summary>A curated answer for a string: this normalised text in this dataset (and context, or any context when null) is this entity or authority URI. Always wins.</summary>
/// <summary>A name written with a hyphen that a compound heading's split broke, joined at the desk: read joined wherever a heading of the dataset carries it.</summary>
public sealed record HyphenatedName(string Dataset, string Written, string? Reviewer, DateTimeOffset AddedAt)
{
    /// <summary>How a name is compared: lower case, no surrounding space.</summary>
    public static string Key(string written) => written.Trim().ToLowerInvariant();
}

public sealed record Override(string Dataset, string? Context, string Normalised, string Target, string Source, string? Note = null);

/// <summary>A string with the decision a run made about it.</summary>
/// <summary>A queued string that bears an entity's name, with what tells whether it is that entity: its records, one of them, its own qualifier if it has one, and where the entity stands among the candidates the rules found for the string (<paramref name="Rank"/>: 1 for first; null when it is not among them), of how many, and what the first candidate is called.</summary>
public sealed record Waiting(SourceString String, int Records, StringExample? Example, string? Qualifier, int? Rank, int Candidates, string? First);

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
    public const string Gnd = "https://d-nb.info/gnd/";
    public const string LocNames = "http://id.loc.gov/authorities/names/";
    public const string LocSubjects = "http://id.loc.gov/authorities/subjects/";
    public const string Lux = "https://lux.collections.yale.edu/data/";
    public const string PeriodO = "http://n2t.net/ark:/99152/";

    private static readonly (System.Text.RegularExpressions.Regex Form, string To)[] PageForms =
    [
        (new(@"^https?://(?:www\.|m\.)?wikidata\.org/(?:wiki|entity)/(Q\d+)(?:[/?#].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase), Wikidata + "$1"),
        (new(@"^https?://(?:www\.)?viaf\.org/(?:viaf/|en/viaf/)(\d+)(?:[/?#].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase), Viaf + "$1"),
        (new(@"^https?://vocab\.getty\.edu/(?:page/)?(ulan|aat|tgn)/(\d+)(?:[/?#.].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "http://vocab.getty.edu/$1/$2"),
        (new(@"^https?://id\.loc\.gov/authorities/(names|subjects)/([a-z]+\d+(?:-781)?)(?:\.html)?(?:[?#].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "http://id.loc.gov/authorities/$1/$2"),
        (new(@"^https?://d-nb\.info/gnd/([0-9X-]+)(?:[/?#].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase), Gnd + "$1"),
        (new(@"^https?://lux\.collections\.yale\.edu/(?:view|data)/([a-z]+/[0-9a-f-]{36})(?:[/?#].*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase), Lux + "$1"),
    ];

    /// <summary>
    /// The identifier a pasted address stands for: a person copies what the browser shows, a source's page for the thing
    /// ("https://www.wikidata.org/wiki/Q2216536"), and the identifier is the URI the source gives it ("http://www.wikidata.org/entity/Q2216536").
    /// The owner, 2026-11-14, keeping "Brockley (London, England)" with Wikidata's page as "known elsewhere" and refused: "not in a
    /// recognised namespace". Wikidata, VIAF, Getty, LoC, GND and LUX pages are read so; anything else is returned as given, trimmed.
    /// </summary>
    public static string Canonical(string uri)
    {
        uri = uri.Trim();
        foreach (var (form, to) in PageForms)
            if (form.IsMatch(uri)) return form.Replace(uri, to);
        return uri;
    }

    /// <summary>A Library of Congress identifier's URI: subject headings begin "sh", everything else is a name.</summary>
    public static string Loc(string id) => (id.StartsWith("sh", StringComparison.OrdinalIgnoreCase) ? LocSubjects : LocNames) + id;

    /// <summary>
    /// An ArchivesSpace agent on any instance: the public URL carries the record's own identity, the agents path with its kind and
    /// number ("https://manuscripts.nls.uk/agents/corporate_entities/1476", the National Library of Scotland's record of the
    /// Edinburgh auctioneers Dowell's; 2026-10-26, the desk). The instance is the host; a host change is the identifier's risk, and an
    /// institutional or SNAC id on the record, when it carries one, is the better identifier beside it.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex ArchivesSpaceAgent = new(@"^https?://[^/]+(?:/[^/?#]+)*?/agents/(?:people|corporate_entities|families|software)/\d+/?$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool IsArchivesSpaceAgent(string uri) => ArchivesSpaceAgent.IsMatch(uri);

    /// <summary>The source name for an authority URI ("ulan", "viaf", ...), or "other".</summary>
    public static string SourceOf(string uri)
    {
        if (uri.StartsWith(Ulan, StringComparison.Ordinal)) return "ulan";
        if (uri.StartsWith(Aat, StringComparison.Ordinal)) return "aat";
        if (uri.StartsWith(Tgn, StringComparison.Ordinal)) return "tgn";
        if (uri.StartsWith(Viaf, StringComparison.Ordinal)) return "viaf";
        if (uri.StartsWith(Wikidata, StringComparison.Ordinal)) return "wikidata";
        if (uri.StartsWith(Gnd, StringComparison.Ordinal)) return "gnd";
        if (uri.StartsWith("http://id.loc.gov/", StringComparison.Ordinal)) return "loc";
        if (uri.StartsWith("https://lux.collections.yale.edu/", StringComparison.Ordinal)) return "lux";
        if (uri.StartsWith(PeriodO, StringComparison.Ordinal)) return "periodo";
        if (IsArchivesSpaceAgent(uri)) return "archivesspace";
        return "other";
    }
}

/// <summary>
/// A record of a source system as one collect read it (design doc 12 section 14.1): the system's key, the record's identifier there,
/// its title and date, its naming fields as JSON (field to values), and a fingerprint of what it says apart from its identifier.
/// </summary>
public sealed record SourceRecord(string System, string RecordId, string? Title, string? Date, string? FieldsJson, string? Fingerprint);

/// <summary>The records a string occurs in that share one value of a field, for the desk: the value (null when the records have none), how many records, and the first few.</summary>
/// <summary>
/// A place entity whose imported Wikidata item may be a namesake of the place meant (a check on the store's entities; the owner,
/// 2026-11-15): the item and what it says of itself, and the most written-about place of the same name in the project's home place.
/// </summary>
public sealed record AwayRow(string EntityId, string Label, string Status, int Strings, string Item, string? ItemDescription, int ItemSitelinks, string HomeUri, string HomeChain, int HomeSitelinks, string? HomeItem, bool Holds = false, string? HomeKind = null)
{
    /// <summary>The entity's own label, when <see cref="Label"/> is the name the row is about and not the entity's label ("Milano (Tex.)" for the name "Milan"): the second kind of row, a bare name on a place labelled as another.</summary>
    public string? EntityLabel { get; init; }

    /// <summary>The nearest place the home place is in: the first of its chain that is not its own name ("Aberdeenshire" of "Bridgend, Aberdeenshire, Scotland, United Kingdom"); null when the chain has none.</summary>
    public string? Within => HomeChain.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .FirstOrDefault(x => x is not ("Europe" or "World") && !string.Equals(x, Label, StringComparison.OrdinalIgnoreCase));

    /// <summary>Where the import's item is, when its record or a pull says: for a distance in words, and a map when the two are far apart.</summary>
    public (double Lat, double Long)? ItemAt { get; init; }
    /// <summary>Where the place at home is, by TGN's record.</summary>
    public (double Lat, double Long)? HomeAt { get; init; }

    /// <summary>The distance between two points on the earth, in kilometres (the haversine formula; a sphere of 6,371 km).</summary>
    public static double Km((double Lat, double Long) a, (double Lat, double Long) b)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var h = Math.Pow(Math.Sin(Rad(b.Lat - a.Lat) / 2), 2) + Math.Cos(Rad(a.Lat)) * Math.Cos(Rad(b.Lat)) * Math.Pow(Math.Sin(Rad(b.Long - a.Long) / 2), 2);
        return 2 * 6371 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }
}

/// <summary>A string found by the project's search: the string, how it stands (its valid decision's method and entity, null when undecided) and how many records it is in.</summary>
public sealed record StringHit(SourceString String, string? Method, string? EntityId, int Records);

/// <summary>A record found by the project's search, with up to twelve of the strings in it (id, field, as written), each of which has a card.</summary>
public sealed record RecordHit(string System, string RecordId, string? Title, string? Date, IReadOnlyList<(long Id, string Context, string Raw)> Strings);

/// <summary>A longer form of a string's name, in the same field, that is already decided onto an entity ("Arno Press" for "Arno"): evidence for the reviewer, not a decision.</summary>
public sealed record LongerForm(long StringId, string Raw, int Records, string EntityId, string? EntityLabel);

/// <summary>What of the project already stands on an authority record: the entity that holds it, and how many decided strings and records that entity carries.</summary>
public sealed record Standing(string EntityId, string? EntityLabel, int Strings, int Records);

/// <summary>
/// Something a source record could say better, on the cataloguers' list (design doc 12 section 14.4): the record by its system and
/// identifier, the field and what it said when the finding was raised, the kind, and what became of it. <see cref="Title"/> and
/// <see cref="Date"/> are the record's, for the list.
/// </summary>
public sealed record Finding(long Id, string Dataset, string System, string RecordId, string? Field, string? Value, string Kind, string? Suggestion, string? Why, long? StringId,
    string? RaisedBy, DateTimeOffset RaisedAt, string Status, string? StatusBy, DateTimeOffset? StatusAt, string? Note, string? Title = null, string? Date = null)
{
    public const string Open = "open", Sent = "sent", Mended = "mended", Declined = "declined";
    public static readonly IReadOnlyList<string> Statuses = [Open, Sent, Mended, Declined];
    /// <summary>The kind of a finding raised when a string is decided as no name and its record wants fixing.</summary>
    public const string NotAName = "not a name";
    /// <summary>The kinds a person may give a finding raised at the desk ("the record is wrong"): a short list, from design doc 12 section 14.4.</summary>
    public static readonly IReadOnlyList<string> DeskKinds = ["spelling", "two values in one field", "truncated", "wrong field", "other"];
}

/// <summary>A decision for one record: what a string means in that record, where it differs from what the string means elsewhere (design doc 12 section 14.3).</summary>
/// <summary>A record decided for itself onto an entity: the record, the field, and the string (with its card) whose text it is.</summary>
public sealed record EntityRecord(long Id, string System, string RecordId, string Field, long? StringId, string? Raw, string? Reviewer, DateTimeOffset DecidedAt);

public sealed record RecordDecision(long Id, string System, string RecordId, string Field, string? EntityId, string? EntityLabel, string? Reviewer, DateTimeOffset DecidedAt);

/// <summary>A string a reviewer set aside at the desk, with the latest note on it: why, when one was given.</summary>
public sealed record ParkedString(long Id, string Context, string Raw, string Method, DateTimeOffset ParkedAt, int Records, string? Note, string? NoteBy);

/// <summary>
/// An entity of a project every one of whose strings looks like no name at all ("S.l.", "Publisher not identified", "S"): a placeholder
/// or a minted entity that stands for nothing. <see cref="Queued"/> is how many of its strings wait at the desk.
/// </summary>
/// <summary>A live place of the project with no gazetteer's and no Wikidata identifier: what the store knows of it before the indexes are asked.</summary>
public sealed record ThinPlaceRow(string EntityId, string? Label, IReadOnlyList<string> Keys, IReadOnlyList<string> Sources, int Strings);

/// <summary>
/// A place that says too little (the fourth check, 2026-11-24): it holds a library's heading or the import's identifiers and no
/// gazetteer record, so nothing says where it is; the gazetteer has records of its name.
/// </summary>
/// <param name="Named">The gazetteer's records bearing one of the entity's names, those in the home place first.</param>
/// <param name="AtHome">How many of them are in the project's home place.</param>
/// <param name="Offer">The record to offer in one press: the only one of the name, or the only one at home. Null when there are several to choose among.</param>
/// <param name="Holder">The entity of the project that already holds the offered record, when one does: the two may be one place.</param>
public sealed record ThinPlace(ThinPlaceRow Row, IReadOnlyList<AuthorityRecord> Named, int AtHome, AuthorityRecord? Offer, Entity? Holder)
{
    /// <summary>The entity's name without its qualifier, for searching the indexes for it.</summary>
    public string Name { get; init; } = "";
    /// <summary>The record offered is in the home place, and not where the entity's own label says the entity is: offered apart, to be read before it is given.</summary>
    public bool Doubt { get; init; }
    /// <summary>What kind of place each of <see cref="ThinPlace.Named"/> is, by its URI, as the gazetteer says ("inhabited places", "counties"), where it says.</summary>
    public IReadOnlyDictionary<string, string> Kinds { get; init; } = new Dictionary<string, string>();
    /// <summary>How much is written of each of <see cref="ThinPlace.Named"/>, by its URI: the sitelinks of the Wikidata item that names it, where one does. Counted only where there are several to choose among.</summary>
    public IReadOnlyDictionary<string, int> Written { get; init; } = new Dictionary<string, int>();
}

/// <summary>An identifier on a person the namesake check lists: the record's own name and the year its source says the person was born, when it says.</summary>
public sealed record NamesakeIdentifier(string Uri, string Source, string? Label, int? Born);

/// <summary>
/// A person an identifier of whose says was born after, or too few years before, the earliest of the records the entity's strings stand in:
/// aligned to a namesake (the fifth check, 2026-11-27; the desk run of 2026-11-25 found James Thomson, on a record of 1824, aligned to an
/// actress born in 1989). <see cref="FirstYear"/> is that earliest record's year.
/// </summary>
public sealed record NamesakePerson(string EntityId, string? Label, int FirstYear, IReadOnlyList<long> StringIds, IReadOnlyList<string> Strings, IReadOnlyList<NamesakeIdentifier> Identifiers)
{
    /// <summary>The fewest years a person is before the first record that names them as its author or its subject: a birth later than this before it is another person's.</summary>
    public const int YearsBefore = 15;
    public bool BornTooLate(NamesakeIdentifier i) => i.Born is { } b && b > FirstYear - YearsBefore;
    /// <summary>The identifiers the check would keep: where one at least was born in time, all but those born too late; where none was, none (the cluster is another person's).</summary>
    public IReadOnlyList<NamesakeIdentifier> Kept => Identifiers.Any(i => i.Born is not null && !BornTooLate(i)) ? Identifiers.Where(i => !BornTooLate(i)).ToList() : [];
}

public sealed record NotNameEntity(string EntityId, string? Label, string Class, IReadOnlyList<long> StringIds, IReadOnlyList<string> Strings, int Records, int Queued);

/// <summary>
/// Two of a project's entities that may be one thing: their authority records say so (<see cref="Linked"/>: a record one holds
/// names a record the other holds as the same), or they bear one label (<see cref="SameLabel"/>). With how many records stand on each.
/// </summary>
public sealed record PossibleDuplicate(string AId, string? ALabel, string AClass, int ARecords, string BId, string? BLabel, string BClass, int BRecords, bool Linked, bool SameLabel, string? AAbout = null, string? BAbout = null);

public sealed record RecordGroup(string? Value, int Records, IReadOnlyList<StringExample> First);
