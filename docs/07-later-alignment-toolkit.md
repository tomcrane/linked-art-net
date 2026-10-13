# Later: transformation and alignment toolkit (notes, not a design)

Out of scope for the current phases. These notes capture what the PMC work and the survey of Yale's
pipeline teach, so the model library and parser make the right accommodations now.

## What pmc-2 did (see [01-current-state.md](01-current-state.md) §5)

Filter -> map -> micro-parse -> collect strings -> reconcile (curated overrides, then LUX + ULAN local
dump + VIAF + LoC + Wikidata with a voting heuristic) -> assign -> write files. Postgres as the
string->authority cache and ULAN store. Pain: live web services (rate limits, no response cache,
not thread-safe), code-as-data mappings, hard-coded paths, no update story.

## What Yale LUX does (`project-lux/data-pipeline`, Python)

Components: Harvester (IIIF Change Discovery, OAI-PMH), Cache (Postgres or filesystem), IdMap
(Redis or in-memory), Collector, Reconciler, Merger, Reidentifier, ReferenceManager, UpdateManager.
External sources are downloaded as **dumps** (Getty NTriples synced via Change Discovery, LoC, Wikidata,
VIAF, DNB, Geonames, Who's on First, ORCID, ROR, BNF, BNE, Japan NL, GBIF) and loaded into LMDB /
Postgres; an `IndexLoader` builds inverted name indexes per dataset for reconciliation. Output goes to
MarkLogic and QLever. `make_datetime` is memoised and cheap-first (see prior art). Configs are open in
`pipeline-configs`. This is the reference architecture to borrow from.

## Shape of a .NET toolkit (`LinkedArtNet.Alignment`, later)

- `IAuthoritySource` (Getty AAT/TGN/ULAN, LoC NAF/SH, Wikidata, VIAF, PeriodO) with two
  implementations each: **local** (QLever or Postgres over a dump; the default) and **remote** (for
  small jobs), behind a shared response cache.
- `IAlignmentStore` (Postgres): source string + context -> decision, candidates, scores, reviewer,
  timestamps; dataset versions so re-runs are incremental and re-alignable when a dump updates.
- `IReconciler` strategies and a voting/combination step, with the PMC heuristics as the first
  implementation and room for embedding-based candidate generation.
- Curated overrides and mappings as JSON (the `pmc-lux` files), loaded through the vocabulary
  registry's `VocabularyMap`.
- A pipeline runner with stages, checkpoints and reports; sources plug in by implementing
  `IRecordSource` and a mapper that builds `LinkedArtNet` objects.
- Composite subject headings (LCSH `Topic--Subdivision--Place--Period`) parsed into components and
  aligned individually; the period subdivision uses the date parser.

## Accommodations to make now

- The model library's `Type` registry and `KnownTerm` (uri, label, source) should be the same type
  the aligner returns, so `classified_as` can be filled from an alignment result directly.
- The date parser's `INamedPeriodSource` and `IDateInterpreter` should be the same plug-in shapes
  the toolkit will use for period authorities and LLM assistance.
- Keep the service and site stateless so a future Postgres is only for alignment.
