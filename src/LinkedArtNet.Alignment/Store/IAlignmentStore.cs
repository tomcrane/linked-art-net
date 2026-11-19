namespace LinkedArtNet.Alignment.Store;

/// <summary>
/// Everything repeatable about a transformation (design doc 10, section 4): the entities we minted, the strings we
/// saw, what we decided each string refers to and on what evidence, the authority records we fetched, and the runs.
/// One implementation, PostgreSQL; this interface is what pipeline stages and tests program against.
/// </summary>
public interface IAlignmentStore
{
    /// <summary>Creates or upgrades the schema. Safe to call every start.</summary>
    Task InitialiseAsync(CancellationToken ct = default);

    // ---- entities

    /// <summary>Stores a new entity. With <see cref="NewEntity.Id"/> null a URI is minted under the scheme and checked unused; with an id, that id is kept (an import of published identifiers).</summary>
    Task<Entity> AddEntityAsync(string dataset, NewEntity entity, IdentifierScheme scheme, CancellationToken ct = default);

    /// <summary>The entity by URI, or null. A merged entity is returned as stored; <see cref="ResolveEntityAsync"/> follows merges.</summary>
    Task<Entity?> GetEntityAsync(string id, CancellationToken ct = default);

    /// <summary>The entity by URI following <see cref="Entity.MergedInto"/> to the survivor, or null.</summary>
    Task<Entity?> ResolveEntityAsync(string id, CancellationToken ct = default);

    /// <summary>Entities whose label or alternate labels normalise to the key, optionally within a dataset and class. Merged entities are left out.</summary>
    Task<IReadOnlyList<Entity>> FindEntitiesByLabelAsync(string normalisedLabel, string? dataset = null, EntityClass? cls = null, CancellationToken ct = default);

    /// <summary>A page of the dataset's entities by id, optionally only those with an equivalent from one source ("ulan"), starting after an id; merged entities are left out.</summary>
    Task<IReadOnlyList<Entity>> ListEntitiesAsync(string dataset, string? equivalentSource = null, string? afterId = null, int limit = 500, CancellationToken ct = default);

    /// <summary>Entities of the dataset with a label containing the normalised text; merged entities are left out.</summary>
    Task<IReadOnlyList<Entity>> FindEntitiesByLabelContainingAsync(string dataset, string containing, int limit = 20, CancellationToken ct = default);

    /// <summary>Entities with this authority URI among their equivalents.</summary>
    Task<IReadOnlyList<Entity>> FindEntitiesByEquivalentAsync(string uri, CancellationToken ct = default);

    /// <summary>Replaces the entity's current best values (label, labels, lifespan, nationalities, roles, status). Equivalents are managed separately.</summary>
    Task UpdateEntityAsync(Entity entity, CancellationToken ct = default);

    /// <summary>
    /// Sets a name of the entity aside (out of its labels and lookup keys, kept in <see cref="Entity.SetAsideLabels"/> so that nothing
    /// puts it back), or restores it. The entity's own label cannot be set aside. False when there was nothing to change.
    /// </summary>
    Task<bool> SetLabelAsideAsync(string entityId, string name, bool aside, CancellationToken ct = default);

    /// <summary>Adds or replaces an equivalent; an entity with any equivalent that is still provisional becomes aligned.</summary>
    Task AddEquivalentAsync(string entityId, Equivalent equivalent, CancellationToken ct = default);

    /// <summary>Removes one equivalent. True when it was there.</summary>
    Task<bool> RemoveEquivalentAsync(string entityId, string uri, CancellationToken ct = default);

    /// <summary>A page of the dataset's entities for browsing: optionally of one status and class, by id after a cursor. Merged entities are left out.</summary>
    Task<IReadOnlyList<Entity>> BrowseEntitiesAsync(string dataset, EntityStatus? status = null, EntityClass? cls = null, string? afterId = null, int limit = 50, CancellationToken ct = default);

    /// <summary>Strings whose valid decision points at the entity, with those decisions.</summary>
    Task<IReadOnlyList<DecidedString>> StringsForEntityAsync(string entityId, int limit = 200, CancellationToken ct = default);
    /// <summary>Of the entities given, those that are a compound heading's own concept (a valid decision of method composite points at them), for a list to tag.</summary>
    Task<IReadOnlySet<string>> CompositeEntityIdsAsync(IReadOnlyCollection<string> entityIds, CancellationToken ct = default);

    /// <summary>Strings of the dataset whose normalised text contains the word.</summary>
    Task<IReadOnlyList<SourceString>> FindStringsAsync(string dataset, string containing, int limit = 200, CancellationToken ct = default);

    /// <summary>Invalidates a string's valid decision with no replacement, so it is undecided again. True when there was one.</summary>
    Task<bool> ReopenStringAsync(long stringId, CancellationToken ct = default);

    /// <summary>Removes provisional entities of the dataset that no valid decision points at, nothing merged into and that have no equivalents: the leavings of re-pointed strings. Returns how many went.</summary>
    Task<int> RemoveOrphanProvisionalsAsync(string dataset, CancellationToken ct = default);

    /// <summary>The dataset's live entities whose class and label (case folded) another live entity shares, with their equivalents, ordered by class, label and age: the duplicate pass's input (<see cref="Reconciliation.DuplicateMerger"/>).</summary>
    Task<IReadOnlyList<Entity>> EntitiesSharingLabelAsync(string dataset, CancellationToken ct = default);

    /// <summary>Merges the loser into the survivor: equivalents move, valid decisions re-point, the loser is marked merged and keeps resolving.</summary>
    Task MergeAsync(string loserId, string survivorId, string? reviewer, CancellationToken ct = default);

    // ---- source strings

    /// <summary>Records a string occurrence: inserts it or bumps last_seen and occurrences. The parsed JSON, when given, replaces what was stored.</summary>
    /// <param name="count">Whether this is an occurrence of the string in the data (the default); a part derived from another string is not counted again on every run.</param>
    Task<SourceString> CollectAsync(string dataset, string context, string raw, string? parsedJson = null, CancellationToken ct = default, bool count = true);
    /// <summary>Records that a string is made of parts: <paramref name="partId"/> is the part at <paramref name="position"/> of <paramref name="parentId"/> (doc 10, section 7.6).</summary>
    Task LinkPartAsync(long parentId, int position, long partId, string? role = null, CancellationToken ct = default);
    /// <summary>Removes every part row of a heading, before it is split again (finding 112's repair); the part strings themselves stay.</summary>
    Task ClearPartsAsync(long parentId, CancellationToken ct = default);
    /// <summary>Strings of the part contexts given that no heading carries any more: fragments of a split since repaired, never a string of the catalogue's own.</summary>
    Task<IReadOnlyList<SourceString>> OrphanPartStringsAsync(string dataset, IReadOnlyCollection<string> partContexts, CancellationToken ct = default);
    /// <summary>Deletes strings outright, with their decisions, examples and part rows (the cascade); for fragments only, never for a string the catalogue wrote.</summary>
    Task<int> DeleteStringsAsync(IReadOnlyCollection<long> stringIds, CancellationToken ct = default);
    /// <summary>The parts of a string, in order; empty when it has none.</summary>
    Task<IReadOnlyList<SourceString>> PartsAsync(long parentId, CancellationToken ct = default);
    /// <summary>The strings a string is a part of.</summary>
    Task<IReadOnlyList<SourceString>> ParentsAsync(long partId, CancellationToken ct = default);

    Task<SourceString?> GetSourceStringAsync(long id, CancellationToken ct = default);

    // ---- the records behind the strings (design doc 12 section 5; 2026-10-26) ----
    /// <summary>Every string of the dataset by (context, normalised text), for a collector that reads an export once.</summary>
    Task<IReadOnlyDictionary<(string Context, string Normalised), long>> StringKeysAsync(string dataset, CancellationToken ct = default);
    /// <summary>
    /// Writes occurrences (a string in a field of a record; the example's source is the source system's key), replacing any for the
    /// same string and record, and makes the record's row when a collect has not written it. Since schema version 11 these are
    /// the <c>occurrences</c> and <c>records</c> tables (design doc 12 section 14.2), every occurrence and not a sample.
    /// </summary>
    Task PutExamplesAsync(IReadOnlyList<StringExample> examples, CancellationToken ct = default);
    /// <summary>Removes the occurrences of a source system for the dataset, before it is collected again; how many. The records stay.</summary>
    Task<int> ClearExamplesAsync(string dataset, string source, CancellationToken ct = default);
    /// <summary>The records behind a string, at most the limit, by record identifier.</summary>
    Task<IReadOnlyList<StringExample>> ExamplesForAsync(long stringId, int limit = 3, CancellationToken ct = default);
    /// <summary>Writes the records a collect read from a source system: new ones are first seen at this collect, known ones are seen again and no longer gone.</summary>
    Task PutRecordsAsync(string dataset, string collect, IReadOnlyList<SourceRecord> records, CancellationToken ct = default);
    /// <summary>Marks as gone the records of a source system that the named collect did not read; how many. The rows are kept.</summary>
    Task<int> MarkRecordsGoneAsync(string dataset, string system, string collect, CancellationToken ct = default);
    /// <summary>How many records a string occurs in.</summary>
    Task<int> RecordCountAsync(long stringId, CancellationToken ct = default);
    /// <summary>
    /// The records a string occurs in, grouped by what one of their naming fields says (the field's values joined), the largest
    /// groups first, each with its count and its first few records (design doc 12 section 14.6).
    /// </summary>
    /// <summary>
    /// Place entities of the dataset, not confirmed by a person, with a label that has no qualifier and a Wikidata item given by an
    /// import, beside each TGN place of exactly that name whose chain is in the home place: one row a pair, for the check that reads them.
    /// </summary>
    Task<IReadOnlyList<AwayRow>> ImportedPlacesWithHomeNamesakesAsync(string dataset, string homePlace, CancellationToken ct = default);

    /// <summary>The most written-about Wikidata place of exactly this name in the local index, other than the item given: its URI, what it says it is, and its sitelinks; null when the index has no other place of the name.</summary>
    Task<(string Uri, string? Description, int Sitelinks)?> BestKnownNamesakeAsync(string label, string? exceptUri, CancellationToken ct = default);

    /// <summary>The dataset's strings whose text contains the text given, whatever their field: one written exactly so first, then the shorter. For the project's search box.</summary>
    Task<IReadOnlyList<StringHit>> SearchStringsAsync(string dataset, string text, int limit = 30, CancellationToken ct = default);

    /// <summary>The strings of the same field that begin or end with this string as whole words and are decided onto an entity (not queued, not a placeholder), the most used first.</summary>
    Task<IReadOnlyList<LongerForm>> LongerFormsAsync(long stringId, int limit = 8, CancellationToken ct = default);

    /// <summary>By authority URI, the project entity that holds it and how many decided strings and records already stand on that entity (queued strings and placeholders not counted). URIs nothing stands on are absent.</summary>
    Task<IReadOnlyDictionary<string, Standing>> StandingOnAsync(string dataset, IReadOnlyCollection<string> uris, CancellationToken ct = default);

    /// <summary>The dataset's records whose title contains the text given, or whose identifier is it, with the strings in each.</summary>
    Task<IReadOnlyList<RecordHit>> SearchRecordsAsync(string dataset, string text, int limit = 15, CancellationToken ct = default);

    /// <summary>What one field says in the records where another field has a value ("place" where "publisher" is "Cambridge University Press"), each value with its count of records, the commonest first.</summary>
    Task<IReadOnlyList<(string Value, int Records)>> FieldValuesWhereAsync(string dataset, string whereField, string whereValue, string field, int limit = 40, CancellationToken ct = default);

    Task<IReadOnlyList<RecordGroup>> RecordGroupsAsync(long stringId, string byField, int groups = 8, int perGroup = 3, CancellationToken ct = default);
    /// <summary>The dataset's records by source system: how many, and how many of them are gone.</summary>
    Task<IReadOnlyList<(string System, int Records, int Gone, string? LastCollect)>> RecordSystemsAsync(string dataset, CancellationToken ct = default);

    Task<SourceString?> FindSourceStringAsync(string dataset, string context, string raw, CancellationToken ct = default);

    /// <summary>Strings in the dataset with no valid decision at all, oldest first: the reconciler's work. A decision that left a string unresolved counts as looked at until it is invalidated.</summary>
    Task<IReadOnlyList<SourceString>> UndecidedAsync(string dataset, int limit = 1000, CancellationToken ct = default);

    // ---- decisions

    /// <summary>Records what a string refers to, invalidating any earlier decision for it. A null entity records "looked at, unresolved".</summary>
    Task<Decision> DecideAsync(long sourceStringId, string? entityId, string method, string? evidenceJson = null, string? reviewer = null, string? datasetVersion = null, string? runId = null, CancellationToken ct = default);

    /// <summary>Whether any valid decision of the method points at the entity ("queued": the entity is a lookalike awaiting review).</summary>
    Task<bool> HasValidDecisionAsync(string entityId, string method, CancellationToken ct = default);

    /// <summary>The valid decision for a string, or null.</summary>
    Task<Decision?> DecisionForAsync(long sourceStringId, CancellationToken ct = default);

    /// <summary>The entity a string in a dataset and context currently resolves to, following merges; null when unknown or undecided.</summary>
    Task<Entity?> ResolveAsync(string dataset, string context, string raw, CancellationToken ct = default);

    /// <summary>Strings of the dataset with a valid decision to an entity, by string id after <paramref name="afterStringId"/>, optionally of one method and context; <paramref name="sample"/> returns a random page instead.</summary>
    Task<IReadOnlyList<DecidedString>> DecidedStringsAsync(string dataset, string? method = null, string? context = null, long afterStringId = 0, int limit = 500, bool sample = false, CancellationToken ct = default);
    /// <summary>The desk's next string: the unskipped in id order, then the skipped in the order they were skipped; <paramref name="afterStringId"/> is the string the desk is leaving, so the next is the one after it in that order.</summary>
    Task<DecidedString?> NextForDeskAsync(string dataset, string method, string? context = null, long afterStringId = 0, CancellationToken ct = default);
    /// <summary>A reviewer skipped the string: it goes to the back of the desk's order (schema version 9).</summary>
    Task SkipAsync(long stringId, CancellationToken ct = default);
    /// <summary>The skip taken back: the string is in its place by id again.</summary>
    Task UnskipAsync(long stringId, CancellationToken ct = default);
    /// <summary>When the string was skipped, or null.</summary>
    Task<DateTimeOffset?> SkippedAtAsync(long stringId, CancellationToken ct = default);
    /// <summary>The queued strings of a dataset whose normalised text is one of the keys, in id order: a string's siblings under its name, for the desk to finish a name before it leaves it (2026-11-06).</summary>
    Task<IReadOnlyList<DecidedString>> QueuedByKeysAsync(string dataset, IReadOnlyList<string> keys, int limit = 20, CancellationToken ct = default);

    /// <summary>The valid decisions a run made, oldest first, optionally of one method ("queued", "minted"), with their strings.</summary>
    Task<IReadOnlyList<DecidedString>> DecisionsForRunAsync(string runId, string? method = null, int limit = 50, string? context = null, CancellationToken ct = default);

    /// <summary>Takes a run back: its decisions are invalidated so the strings are undecided again, and provisional entities it minted that nothing else points at are removed (they were never published). Aligned and confirmed entities stay.</summary>
    Task<UndoResult> UndoRunAsync(string runId, CancellationToken ct = default);

    /// <summary>A run's valid decisions counted by the string's context and the method.</summary>
    Task<IReadOnlyList<ContextMethodCount>> RunBreakdownAsync(string runId, CancellationToken ct = default);

    /// <summary>Marks every valid decision pointing at the entity invalid, so the reconciler reconsiders those strings.</summary>
    Task<int> InvalidateDecisionsForEntityAsync(string entityId, CancellationToken ct = default);

    /// <summary>Removes a provisional entity that nothing points at any more (no valid decision, no equivalents, nothing merged into it): the placeholder of a queued string the second pass settled. True when it went.</summary>
    Task<bool> RemoveProvisionalIfUnreferencedAsync(string entityId, CancellationToken ct = default);

    // ---- overrides

    Task PutOverrideAsync(Override item, CancellationToken ct = default);

    /// <summary>Every override of the dataset.</summary>
    Task<IReadOnlyList<Override>> OverridesAsync(string dataset, CancellationToken ct = default);

    /// <summary>Removes one override (context null means the any-context one). True when something was removed.</summary>
    Task<bool> RemoveOverrideAsync(string dataset, string? context, string normalised, CancellationToken ct = default);

    /// <summary>The override for a string: the one for its context first, then the one for any context.</summary>
    Task<Override?> OverrideForAsync(string dataset, string context, string normalised, CancellationToken ct = default);

    // ---- authority records

    Task<AuthorityRecord?> GetAuthorityAsync(string uri, CancellationToken ct = default);

    Task PutAuthorityAsync(AuthorityRecord record, CancellationToken ct = default);
    /// <summary>
    /// Caches what a live source said about a record under <c>pulled</c> in its <c>data</c> (Authorities.SourcePull), making the row when the
    /// cache has none (a LUX record, a Wikidata item outside the dump); an existing row keeps its labels and dates. A dump reload replaces
    /// <c>data</c> whole and so drops the pull, which the next "Pull in" restores.
    /// </summary>
    Task PutPulledAsync(string uri, string source, string? label, string pulledJson, CancellationToken ct = default);

    /// <summary>Cached authority records whose labels normalise to the key, optionally from one source ("ulan"). Not for the hot path: without a source literal the partial label indexes cannot be used and the table is scanned; the reconciler uses <see cref="FindAuthoritiesByLabelsAsync"/>.</summary>
    Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesByLabelAsync(string normalisedLabel, string? source = null, CancellationToken ct = default);

    /// <summary>Authority records matching any of the keys (normalised or diacritic-folded labels), optionally within one source and one type ("Person"), at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesByLabelsAsync(IReadOnlyCollection<string> keys, string? source = null, string? type = null, int limit = 50, CancellationToken ct = default);

    /// <summary>Authority records that declare the URI as the same thing (skos:exactMatch and the like).</summary>
    Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesBySameAsAsync(string uri, CancellationToken ct = default);

    /// <summary>Removes a source's records not refreshed since the instant given (a load's start): ids the new dump no longer has. Returns how many went.</summary>
    Task<int> RetireAuthoritiesAsync(string source, DateTimeOffset notFetchedSince, CancellationToken ct = default);

    /// <summary>Recomputes every entity's and authority record's lookup keys from its labels, for after the key rules change.</summary>
    /// <summary>Recomputes lookup keys with the current rules. <paramref name="source"/> limits the authority records to one source (entities are always done); rows whose keys are already current are not written.</summary>
    Task<(int Entities, int AuthorityRecords)> ReindexLabelsAsync(string? source = null, IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>A lock on a key held until disposed, so two writers that would create the same thing take turns.</summary>
    Task<IAsyncDisposable> LockAsync(string key, CancellationToken ct = default);

    /// <summary>Stores many authority records at once (a vocabulary load), replacing by URI. Returns how many were written.</summary>
    Task<int> PutAuthoritiesAsync(IAsyncEnumerable<AuthorityRecord> records, CancellationToken ct = default);

    // ---- runs

    Task<Run> StartRunAsync(string dataset, string? manifestJson = null, string? dumpsJson = null, string? gitSha = null, CancellationToken ct = default);

    Task FinishRunAsync(string runId, string? countsJson, string? report, CancellationToken ct = default);

    Task<Run?> GetRunAsync(string runId, CancellationToken ct = default);

    Task<IReadOnlyList<Run>> RunsAsync(string dataset, int limit = 20, CancellationToken ct = default);

    // ---- projects, users, memberships, notes (the workbench)

    Task UpsertProjectAsync(Project project, CancellationToken ct = default);
    Task<Project?> GetProjectAsync(string dataset, CancellationToken ct = default);
    Task<IReadOnlyList<Project>> ListProjectsAsync(CancellationToken ct = default);
    Task UpsertUserAsync(string login, string? displayName, CancellationToken ct = default);
    Task<User?> GetUserAsync(string login, CancellationToken ct = default);
    Task SetMembershipAsync(string dataset, string login, string role, CancellationToken ct = default);
    Task RemoveMembershipAsync(string dataset, string login, CancellationToken ct = default);
    Task<IReadOnlyList<Membership>> MembershipsAsync(string dataset, CancellationToken ct = default);
    Task<IReadOnlyList<Membership>> MembershipsForAsync(string login, CancellationToken ct = default);
    Task<Note> AddNoteAsync(string dataset, string target, string? author, string text, CancellationToken ct = default);
    Task<IReadOnlyList<Note>> NotesAsync(string dataset, string target, CancellationToken ct = default);

    /// <summary>The targets of the dataset's notes whose text begins with one of the prefixes, each with the latest such note: how something left undecided with a note is found again.</summary>
    Task<IReadOnlyDictionary<string, string>> NotedAsync(string dataset, IReadOnlyList<string> prefixes, CancellationToken ct = default);
    /// <summary>The project's latest notes across every target, newest first: the audit log's recent changes (doc 12 section 13, item 1).</summary>
    Task<IReadOnlyList<Note>> RecentNotesAsync(string dataset, int limit = 50, CancellationToken ct = default);
    /// <summary>The project's latest decisions made by a person (a reviewer named), newest first, each with its string.</summary>
    Task<IReadOnlyList<DecidedString>> RecentHumanDecisionsAsync(string dataset, int limit = 50, CancellationToken ct = default);

    /// <summary>One string with its valid decision, or null when it has none.</summary>
    Task<DecidedString?> DecidedStringAsync(long stringId, CancellationToken ct = default);

    /// <summary>Strings with a valid decision of one method, counted by context: the review queue's size.</summary>
    Task<IReadOnlyList<ContextMethodCount>> QueueCountsAsync(string dataset, string method, CancellationToken ct = default);

    /// <summary>Takes back the latest valid decision for a string and makes the one it superseded valid again, so an undo restores exactly what stood before. Returns the decision now valid, or null when none is.</summary>
    Task<Decision?> RevertDecisionAsync(long stringId, CancellationToken ct = default);

    // ---- overview

    Task<StoreCounts> CountsAsync(string? dataset = null, CancellationToken ct = default);

    /// <summary>The project's live entities counted by status and class.</summary>
    Task<IReadOnlyList<EntityCount>> EntityCountsAsync(string dataset, CancellationToken ct = default);

    /// <summary>The authority sources loaded, with record counts.</summary>
    Task<IReadOnlyList<AuthoritySource>> AuthoritySourcesAsync(CancellationToken ct = default);
}
