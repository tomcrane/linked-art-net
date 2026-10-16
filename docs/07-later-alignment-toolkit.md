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

## Accommodations to make now (reviewed 2026-10-16 against what was built)

- The model library's registry type is `VocabularyTerm(Name, Id, Label, Kind, Notation, AliasOf)`, one per
  source class (`Aat`, `Tgn`, `Ulan`), and `Aat.Term(id, label)`, `Tgn.Place(id, label)`, `Ulan.Person(id, label)`
  turn an id and label into model objects. An alignment result that carries source, id and label therefore maps
  straight onto `classified_as` or a reference; the aligner should return that triple rather than a new type.
  The curated-mapping loader (`VocabularyMap` over `vocabulary/mappings/`, doc 02 §6) was not built; the
  toolkit will need it, so build it there or when the examples first need a mapping table.
- The date parser's `INamedPeriodSource` exists (`JsonNamedPeriodSource`, `CompositePeriodSource`) and is the
  plug-in shape for period authorities; a PeriodO-backed implementation is the natural first authority source
  and is still open in Phase 2. `IDateInterpreter` is designed (doc 03, doc 04) but not yet in code; when it is
  written it should be the same shape the toolkit uses for LLM assistance.
- Statelessness did not hold, deliberately: the trainer keeps a file-backed queue and submission store and the
  service writes a capture JSONL, both on a mounted volume (`/srv/linkedart/data`, see 09-hosting.md). A future
  Postgres is therefore not "only for alignment"; decide then whether the trainer store and capture move into it
  or stay as files. Either way they need a backup before the toolkit is started.

## What the trainer already proves

The trainer (`/dates/train`, doc 08) is a working prototype of the review loop this toolkit needs: a source
string with its context, a queue, a reviewer's decision with status, tags and notes, reviewer identity and
timestamps, and export as a pull request against the corpus. `IAlignmentStore` has the same columns and the
reviewer UI the same flow (candidates to pick from instead of a parse to confirm), so Phase 5 should start from
the trainer's store, GitHub login and export code rather than from scratch.
