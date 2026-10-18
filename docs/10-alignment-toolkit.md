# Design: transformation and alignment toolkit (`LinkedArtNet.Alignment`, `LinkedArtNet.Agents`)

Status: design, 2026-10-17. Supersedes the notes in [07-later-alignment-toolkit.md](07-later-alignment-toolkit.md),
which stay as the record of what the PMC work and the LUX survey taught. Nothing here is built yet; section 12 is the
order to build it in and section 13 the questions the owner decides first.

## 1. What this is for

The owner's framing (2026-10-17), in priority order:

1. **Transform a collection's data to Linked Art, with as much alignment as possible.** The Paul Mellon Centre (PMC)
   library, archive and photo archive are the first instance, to be re-run from a fresh dump and aligned with what
   LUX already holds. Wellcome Collection, whose Catalogue API shape sits close to Linked Art, is the second candidate.
2. **Prepare data for import into LUX, and not only LUX.** The output is a set of Linked Art records with stable
   identifiers and `equivalent` links to the authorities, usable by any consumer.
3. **Later, a Linked Art backed discovery engine of the owner's own**, probably on QLever, as the back end for
   cross-collection interfaces. The toolkit's outputs must load into it without a second transformation.
4. **Useful to anyone doing this from .NET**: libraries, museums and archives each have their quirks, but the stages
   are common. Collection-specific code is an instance of the toolkit, never part of it.

The one non-negotiable property is **repeatability**. Alignment is expensive (human judgement, rate-limited services,
large dumps) and must be done once per entity, not once per run. That means the toolkit keeps its own local data: the
entities it has minted, with their own identifiers; every source string it has seen and what was decided about it; and
cached authority records. A re-run of a collection dump reads those first and sends only the new or changed strings to
reconciliation.

Non-goals: a LUX replacement; running live against web services in the main path; any PMC-specific mapping inside the
toolkit; a general-purpose entity resolution framework. Where a sound, narrow choice exists, take it.

## 2. What to keep and what to drop

The owner asked that nothing be kept because it exists, and nothing dropped because it was improvised. Judged on the
pmc-2 branch ([01-current-state.md](01-current-state.md) section 5) and the LUX pipeline:

| Keep, because it is sound | Why |
|---|---|
| The stage separation: filter, map, micro-parse, collect strings, reconcile, assign, write | It is the same shape LUX uses (harvest, map, collect, reconcile, reidentify, load). Stages can be run, cached and tested alone. |
| Curated overrides consulted before any automatic match | A person's decision outranks a heuristic; the `pmc-lux` JSON files are the seed of the decision store. |
| Reconciliation keyed on the source string, not on the record | Collections repeat strings heavily; one decision serves thousands of records. |
| Voting across independent authorities | A single-source match is a guess; agreement of two or three is evidence. |
| A local ULAN dump in Postgres | Local dumps are the only way to be fast, repeatable and offline. LUX loads every authority this way. |
| `ParsedAgent`: one string yields name, dates, honorific and role | A general problem (section 6), and the pmc-2 tests are its first corpus. |
| One JSON file per identifier as the output form | Simple, diffable, loadable by anything. |

| Drop or change | Replace with |
|---|---|
| Live web services inside the transformation run (rate limits, no response cache, not thread-safe) | Reconciliation is its own offline, incremental stage over the store; every remote answer is cached by URI with a fetched-at time. |
| Mappings as code (`Media.cs`, `NotesField.cs`, role tables) | JSON mapping tables under `vocabulary/mappings/`, loaded by the `VocabularyMap` doc 02 promised; the toolkit is its first customer. |
| Hard-coded `C:\pmc` paths and dump dates in `Program.cs` | A pipeline manifest (section 5) naming the dump, its date and where it lives. |
| Postgres as an implicit dependency | `IAlignmentStore` with SQLite as the zero-install default and Postgres as the option; the owner's existing Postgres data imports into either. |
| A string-to-authority cache with no provenance | Decisions with method, evidence, reviewer, timestamp and dataset version (section 4). |
| Identifiers minted at write time, differently each run | Entities minted once, stored, and reused; the published URIs never change for the same entity. |
| LUX search as one more vote | LUX as a *mapping* source whose self-echoes are detected (section 7.4). |
| The known bugs: `LocClient.Suggest` reading index 0, `WikidataClient.GetName` ignoring language | Rewritten clients with tests against recorded responses. |

From LUX ([project-lux/data-pipeline](https://github.com/project-lux/data-pipeline)): the IdMap (internal id to
external ids and back), the Reconciler adding matches to `equivalent` and iterating until no new equivalents appear, the
Reidentifier rewriting external URIs to internal ones, and source reconcilers over local indexes keyed by URI or
lower-cased label. These are the four moving parts of section 4 under other names.

From Wellcome's Catalogue API: a merged "Works" model with `contributors` (agent, roles), `production` (places, agents,
dates as labels), `subjects` and `genres` as concepts with identifiers, and a nightly line-delimited JSON snapshot
(`works.json.gz`, 1.5 GB). Their production dates are labels only, exactly what the dates parser exists for.

From Leeds (University of Leeds Library, Digital Library Infrastructure Project; private repositories read from the
owner's checkout on 2026-10-17: `linked-art-docs`, `linked-art-schemas`, `linked-art-transformations`, `source-schemas`,
`linked-art-api`; Python, Pydantic, by Edward Anderson). Their pipeline caches EMu and Alma exports in PostgreSQL,
validates source records against **data contracts** with a public/full split (a `PublicRecord` the `Record` extends, so
private fields can never reach a public representation), transforms them with per-field mapper functions into Pydantic
Linked Art models named after the Linked Art API partitions, and loads a quad store (QLever) behind a FastAPI gateway;
the components are a `stdin`/`stdout` CLI toolchain with LMDB as the intermediate store. Reconciliation is designed as
SPARQL queries over the integrated graph (same-name unaligned persons, later place and work overlap, with
`SERVICE` calls to Wikidata or ULAN) whose findings are written as `la:equivalent` triples into an authority record
that only the Authority Server owns. Their **persistent identifier policy** separates the thing
(`https://pid.library.leeds.ac.uk/id/{noid}`, 303 to), the document (`/doc/{noid}`, content negotiation by profile)
and the representation (`/doc/{noid}/la/about.json`), with eight-character NOIDs from an identifier service; facets
without their own records (an EMu drop-down value) get **content-based identities**, a hash of path and value. Their
`eparties` design lists the date forms found in EMu party records ("fl.ca.1737-1751", "b.ca.1441", "d.17??",
"1690(ca.)"), which is a ready-made test set for the dates parser.

What this design takes from Leeds: the thing/document/representation identifier structure (section 8); content-based
identities for facets that have no record of their own; the public/full boundary as a stage before mapping, so
redaction is never the mapper's job; and the observation that once our output is in QLever, reconciliation across our
own datasets can be SPARQL over the graph as well as lookups in the store. What it does differently: alignment to
external authorities happens before loading, in the store, so the published records already carry `equivalent`.

## 3. Shape of the code

```
src/LinkedArtNet.Agents/          agent-string parser: "Name [dates] (role)" and its relatives -> Person/Group with
                                  lifespan, role and nationality; profiles and a corpus, like the dates parser
src/LinkedArtNet.Alignment/       the toolkit: the PostgreSQL store, entities, decisions, authority sources, reconciler,
                                  reidentifier, pipeline runner, source adapter contracts, writers
tools/Align/                      CLI: run a pipeline manifest, import decisions, review queue export, reports
pipelines/pmc/                    the PMC instance: DScribe adapters, mappers, manifest; moves to its own repository
                                  once the packages are published
pipelines/wellcome/               later
```

`LinkedArtNet.Agents` depends on `LinkedArtNet.Dates` and the model library. `LinkedArtNet.Alignment` depends on all
three. The dates service gains `/v1/agent` for the agent parser (deterministic text in, structure out, same caching
rules); the examples site gains a playground page and, in time, a review page that is the trainer generalised.

## 4. The store: entities, strings, decisions, authority cache

Status 2026-10-17: built as described, with the additions of an `overrides` table (7.1's first step) and a
`Lifespan` of four instants in place of a TimeSpan pair; doc 11 has the schema, the API, the importers for the
pmc-lux files and the earlier PMC tables, the `tools/Align` command line and the test setup.

Everything repeatable lives in `IAlignmentStore`. Five kinds of row.

**Entity**: something the toolkit has minted an identifier for.
- `id`: our URI, minted once from a configured base (`https://<base>/<dataset>/<class>/<ulid>`; question 1 in section 13).
  Never reused, never changed. A merge records the survivor on the loser (`merged_into`) and keeps the loser resolvable.
- `class`: Person, Group, Place, Type, Period, Event.
- `label`, `labels` (language-tagged alternates), `lifespan` (a `LinkedArtTimeSpan` pair when known), `nationality`,
  `roles` (AAT terms seen with this entity), all as the current best values with their provenance.
- `equivalents`: authority URIs (ULAN, AAT, TGN, LCNAF, VIAF, Wikidata, LUX, PeriodO...) each with `source`,
  `method`, `confidence`, `decided_at`, `dataset_version`. These become Linked Art `equivalent` on output.
- `status`: provisional (minted from a string, no external match), aligned (at least one accepted equivalent),
  confirmed (a person signed it off), merged.

**SourceString**: a string as it occurred in a dataset.
- `dataset`, `context` (which field or role: "library.author", "archive.creator", "subject"), `raw`, `normalised`,
  `parsed` (the agent parser's structure, or the date parser's result, as JSON), `first_seen`, `last_seen`,
  `occurrences`.
- The key is (`dataset`, `context`, `normalised`). The same name in a different context ("Smith, John" as an author
  and as a subject) may be a different person; the context is part of the identity until a decision says otherwise.

**Decision**: what a SourceString refers to.
- `entity_id` (or `unresolved`), `method` (override, exact-uri, exact-name, vote, human, llm-proposed-then-human),
  `evidence` (the candidates considered and their scores, as JSON), `reviewer`, `decided_at`, `dataset_version`,
  `valid` (false once an authority dump update or a merge invalidates it; re-run reconsiders only invalid ones).
- A string with no decision yet is work for the reconciler; a string with a valid decision is settled.

**AuthorityRecord**: a cached copy of an external record by URI, with `fetched_at`, `source_version` and the parsed
labels, dates, type and sameAs links. Lookups during reconciliation read this first; a miss fetches once. Bulk dumps
(ULAN, AAT, TGN, LCNAF) are loaded into the same table or into per-source indexes, so "fetch" is a local read.

**Run**: one execution of a pipeline manifest: dataset, dump identity (file hashes, dates), counts per stage, started
and finished, the git SHA of the pipeline, and the report. Decisions carry the run that made them.

One implementation: PostgreSQL (decision 2 in section 13), in `LinkedArtNet.Alignment` itself. The owner's existing
`source_string_map` and `authorities` tables import as decisions and entities (section 9), preserving the identifiers
already published. Tests run against a disposable database.

## 5. The pipeline

A **manifest** (JSON, checked in with the instance) names the dataset, its dumps (paths, dates, hashes), the adapter
and mapper to use, the profiles for dates and agents, the authority sources and their order, thresholds, the output
folder and the identifier base. Nothing about a run lives in code.

Stages, each a class with one job, each runnable alone from the CLI, each writing a count and a sample to the report:

1. **Acquire**: locate the dump files the manifest names; verify hashes; later, harvest by IIIF Change Discovery or
   OAI-PMH as LUX does. (PMC: DScribe XML exports. Wellcome: the snapshot.)
2. **Read**: a source adapter (`IRecordSource`) yields source records with stable source identifiers, streaming (the
   photo archive EAD is 1.5 GB; nothing loads whole files into memory).
3. **Map**: a per-source mapper builds `LinkedArtNet` objects with **placeholder references** wherever the source gives
   a string for a person, group, place, concept or period: the raw string, its context, and nothing else. Mappers are
   deterministic and testable from fixture records. This is where `WithTimeSpan("c. 1850")` and the agent parser run.
4. **Collect**: gather the placeholders into SourceStrings (new ones are inserted; known ones get `last_seen` and
   `occurrences`).
5. **Reconcile** (section 7): for SourceStrings without a valid decision only. Produces decisions, provisional
   entities, and a review queue for the uncertain.
6. **Assign**: the reidentifier replaces every placeholder with the entity URI its decision points to, adds the
   entity's `equivalent`s, and fills `_label` from the entity's best label. Unresolved strings become provisional
   entities so the output is complete and the gap is visible, never silently dropped.
7. **Validate**: the model library's schema checks and the corpus-style rules (ids unique, every reference resolvable,
   no empty TimeSpans).
8. **Write**: one JSON file per record and per entity; an Activity Streams change log for the run (created, updated,
   deleted since the last run) so a consumer like LUX, or our own QLever loader, can take deltas; the report.

Because stages 5 and 6 read the store, a re-run over an unchanged dump is pure reads and completes in minutes; a run
over a fresh dump reconciles only what is new.

## 6. The agent-string parser (`LinkedArtNet.Agents`), first deliverable

Status 2026-10-17: built as a first version (`src/LinkedArtNet.Agents`): `AgentStringParser` with six profiles (default,
dscribe, marc, ulan, ead, wellcome), the tables (honorifics, qualifiers, corporate words, nationalities and roles, with
AAT identifiers only where the registry has the term), `AgentBuilder.ToPerson/ToGroup`, `AgentResultDto`, the agent
corpus (`corpus/agents/`, four files, 60 entries, evaluated by `AgentCorpusEvaluator` with the nested dates
expectation), `GET /v1/agent` and `/v1/agent/profiles` on the service, the `/agents` playground, and `tools/Corpus
agents`. The trainer's agent item kind (decision 5) followed the same day: agent strings are queued, reviewed as
parts, and saved as agent entries with the date part also entering the dates corpus (doc 08 section 8). Not yet:
the Wellcome and PMC strings as corpus files, an agent corpus schema and `promote` for agent entries.

Library and archive data pack a person or group into one string: a name, life or activity dates, an honorific, a role
or occupation, sometimes a nationality. The conventions differ by source but the task is one task:

| Source convention | Example |
|---|---|
| DScribe / CALM (PMC) | `Grierson, Herbert John Clifford [Sir, 1866-1960] (preface by)` |
| MARC / RDA headings | `Leconte de Lisle, Charles Marie, 1818-1894`; `Smith, John, active 1640-1660`; `Rembrandt Harmenszoon van Rijn, 1606-1669, artist` |
| ULAN display | `Dutch painter, 1606-1669`; `French illuminator, active mid-15th century` (already in the dates corpus) |
| EAD / ISAAR authority names | `Smith; John (1900-1980); artist`; corporate bodies with dates of existence |
| Wellcome contributors | `agent.label` plus `roles[].label`, dates sometimes inside the label |

Design, mirroring the dates parser because that shape has worked:

- `AgentStringParser.Parse(text, AgentProfile) -> ParsedAgent`: `Name` (as written, and normalised to a sort form and
  a display form), `Honorifics`, `Dates` (the dates parser's result for the date part with `Intent.Pair`, so a
  `LinkedArtTimeSpan` pair with the usual confidence and flags), `Roles` (phrases as written and the AAT terms a mapping
  table gives them, through `VocabularyMap`), `Nationality` (adjective and the AAT nationality term), `Kind` (person,
  group, uncertain), `Flags` (approximate, floruit, pseudonym marker, "and others"), `Unconsumed`, `Confidence`, `Notes`.
- `AgentProfile`: what the brackets and parentheses mean in this source (dates, honorifics, roles, qualifiers),
  the separator conventions, the role vocabulary, and the dates profile to use. Shipped profiles: `dscribe`, `marc`,
  `ulan`, `ead`, `default`.
- `ToPerson()` / `ToGroup()` build model objects: `Person` with `identified_by` Name parts, `born`/`died` from the
  pair, `classified_as` nationality and occupation terms, and `_label`. The role is not on the person; it is the
  relationship to the record and goes to the mapper.
- A corpus (`corpus/agents/`) in the same format as the dates corpus, seeded from the pmc-2 `ParsedAgentTests`, PMC
  and Wellcome strings, and ULAN display names; `tools/Corpus` grows to cover it. A playground page and
  `/v1/agent` on the service. The trainer's proposal mechanism applies unchanged if an interpreter is ever wanted here.

This is first because it is general, small, needs no store or network, and every later stage calls it.

## 7. Reconciliation

### 7.1 Candidate generation

For a SourceString without a decision, in order, stopping at the first that yields an accepted decision:

1. **Overrides**: a curated table of (dataset, context, normalised string) to entity or authority URI. The `pmc-lux`
   JSON files import here. Always wins.
2. **Store**: an existing entity whose labels match (normalised) and whose lifespan, if both have one, overlaps. The
   common case on a re-run: the string is new but the person is not.
3. **Local authority indexes**: ULAN (the Postgres dump), AAT, TGN, PeriodO, LCNAF, each queried by normalised label
   and by label-with-dates; results carry type and dates where the authority has them.
4. **Remote, cached**: VIAF, Wikidata, LoC suggest, LUX search; one call per string per source, answers cached by
   URI, rate-limited by a shared client, never called from inside the mapper.

### 7.2 Scoring and the vote

Each candidate is scored on name similarity (normalised exact, then token overlap, then a bounded edit distance),
date agreement (both lifespans known and overlapping; one known and the other silent is neutral; a conflict is a
veto), type agreement (a Person candidate for a corporate-body string is a veto), and hints (role or nationality
matching the authority's occupation or nationality fields). Candidates that are the same real-world entity across
sources are grouped by their mutual `sameAs` links before voting, so "ULAN says X, Wikidata says the Wikidata item
whose sameAs is X" counts as agreement, not as two different answers.

Decision rule, numeric thresholds in the manifest: accept when a curated override or an exact URI match exists, or
when two independent authority groups agree and nothing vetoes; queue for review when one authority matches, or the
groups disagree, or only a weak name match exists; otherwise mint a provisional entity. Every decision stores the
evidence it was made on.

### 7.3 Review

The uncertain cases go to a review queue with the same shape as the dates trainer: the string in its contexts (with
example records), the candidates with their evidence, a one-click accept per candidate, "none of these" (new entity),
"same as this other string" (merge), and an optional LLM proposal under the same rules as the dates interpreter: a
proposal, confidence-capped, confirmed by a person, logged as such. The dates trainer's store, login and export code
are the starting point, generalised to item kinds (date, agent, place, concept).

### 7.4 LUX, and the self-echo problem

LUX already holds the earlier PMC export under PMC's own identifiers, before alignment was settled, so LUX search
would return our own records back to us and "confirm" them. The rule: a LUX hit is a **self-echo** when its record
carries one of our identifiers (the published PMC URIs, or any URI under our identifier base) in its `equivalent` or
identifiers, or when LUX attributes it to the PMC source. Self-echoes never vote. They are kept as a mapping: the LUX
URI of the record that already represents this entity, stored as an `equivalent` with method `lux-self`, so the
re-export carries the same identifier and `equivalent` set that lets LUX merge rather than duplicate. A non-echo LUX
hit is a candidate like any other, with the extra value that LUX has already done the alignment to the authorities it
loads; its `equivalent` list is read as a candidate group.

Whether to ask LUX to withdraw or merge the premature records is the owner's conversation and independent of this
design; the design makes a clean re-export possible either way.

## 8. Identifiers and publishing

- Entities and records get URIs under one configurable hostname per dataset (PMC keeps its current one), minted once
  and stored. A re-run never mints a second URI for an entity it already has; `merged_into` keeps old URIs resolvable.
  The structure follows Leeds: an identifier for the thing that 303-redirects, a document identifier that negotiates
  by profile, and representation identifiers per format, so one opaque reference serves all three.
- Every authority match is written as Linked Art `equivalent` on the entity; the record references the entity by our
  URI. Consumers that prefer an authority URI follow `equivalent`; consumers that need stability use ours.
- Each run writes an Activity Streams collection (Create, Update, Delete by URI) alongside the files, which is what
  LUX harvests and what a QLever loader can apply as a delta.
- The output folder is the contract: `records/<class>/<id>.json`, `entities/<class>/<id>.json`, `activity/<run>.json`,
  `report.md`. A publishing step that serves this folder over HTTP with content negotiation is the owner's hosting
  decision, as with the dates service.

## 9. The PMC instance

What a re-run needs, and where it comes from:

- **Dumps.** Fresh DScribe exports of the library, archive descriptions and authorities, and the photo archive (EAD and
  LIDO). On the owner's machine today: the March 2024 set under `C:\pmc\2024-03-18` (library 71 MB, archive 28 MB and
  3 MB, photo archive 1.5 GB and 860 MB), the keywords CSV and the cleaned keywords JSON; the October 2024 library dump
  the pmc-2 code expects is not present. The manifest names whichever set is current.
- **Mapping.** The pmc-2 `Library`, `Archive` and `Leeds` processors become adapters and mappers over the stage
  contracts, with the code-as-data tables moved to JSON. The mapping logic itself is mostly sound and ports.
- **Existing decisions.** The owner's Postgres `source_string_map` and `authorities` tables import as SourceStrings,
  Decisions (method `imported`) and Entities with their published identifiers, so the first run reconciles only what
  those tables do not cover and the identifiers already in LUX are the ones re-exported.
- **Overrides.** The `digirati-co-uk/pmc-lux` JSON files import as overrides.
- **LUX.** Self-echo handling as in 7.4; the run's report lists how many PMC entities LUX already holds under our ids,
  and how many under other ids (the duplicates to raise with Yale).
- **Deliverable.** The output folder of section 8 for the three PMC sources, a report with per-stage counts and the
  review queue's size, and the Activity Streams delta against the earlier export.

## 10. The Wellcome instance, sketched

The daily snapshot (`https://data.wellcomecollection.org/catalogue/v2/works.json.gz`, 1.5 GB, line-delimited, the same
serialisation as the API) is the dump; a streaming adapter reads one Work per line. Two real records
(`works/vaqg68j6`, a born-digital archive item, and the Rembrandt etchings `yzdmrzhc` and `ka8vy93d`, read 2026-10-17)
show the shape:

- **Identifiers** name their scheme: source-system ids (`sierra-system-number`, `sierra-identifier`, `calm-ref-no`,
  `calm-altref-no`, `axiell-guid`, `mets`) and authority ids on agents and concepts (`lc-names` such as `n79142935`
  for Rembrandt, `lc-subjects` such as `sh85038258`), with `label-derived` for concepts that have no authority yet.
  The `lc-names`/`lc-subjects` values map straight to LCNAF and LCSH URIs and become `equivalent`; `label-derived`
  concepts are SourceStrings for the reconciler.
- **Contributors**: `agent` with `type` (Person, Organisation...), `label`, `identifiers`, and `roles[].label`. Where
  the label carries dates the agent-string parser applies; where it is a bare heading the identifiers do the work.
- **Production**: `label` plus `dates[].label` only ("1635.", "April 2006"), `places[].label`, `agents[].label`; the
  dates parser reads the labels, the places and agents are SourceStrings.
- **Subjects** can be compound (`Selling - Drugs` with concepts Selling and Drugs), so a subject is one SourceString
  whose parts are also concepts; **genres** are concepts (`Etching`) usually `label-derived`, which AAT resolves.
- **Archive hierarchy**: `partOf` (Section, Collection with ids), `parts`, `precededBy`, `succeededBy`; `referenceNumber`
  is the CALM reference; `workType` (`Format`: "Born-digital archives", "Pictures") decides the Linked Art class.
- **Items and locations**: `DigitalLocation` with `locationType` (`iiif-presentation`), `license` (`inc`, `cc-by`...) and
  access status, mapping to `DigitalObject` and rights statements.

Wellcome's own data is CC0; licensing is per item for digitised content. Open points for the instance: the mapping of
`workType` onto `HumanMadeObject`, `LinguisticObject` and `Set`, and whether `createdDate` or `production` is
authoritative when both exist.

## 11. Towards the discovery engine

Out of scope here, noted so the outputs fit: QLever loads N-Triples or N-Quads fast and answers SPARQL; LUX itself
runs a `qlever-middletier`. The writer should therefore be able to emit the same records as JSON-LD (the contract) and
as N-Quads with a graph per dataset (one `jsonld` expansion away), and the Activity Streams delta should map to
QLever's update path. A later document covers the engine, its indexing and its interfaces.

## 12. Order of work

1. **Agents parser** (`LinkedArtNet.Agents`): parser, profiles, corpus seeded from pmc-2 tests, PMC and ULAN strings and
   the Leeds `eparties` date forms, tests, `/v1/agent`, playground page, and the trainer's agent item kind with the
   nested dates expectation (decision 5). No store, no network. Small.
2. **Store and model** (built 2026-10-17, doc 11): the PostgreSQL store; Entity, SourceString, Decision,
   AuthorityRecord, Run; identifier minting under a per-dataset hostname; importer for the owner's existing tables
   and the `pmc-lux` overrides. Tests on a disposable database.
3. **Authority sources and reconciler**: ULAN local index first (the data exists), then AAT, TGN, PeriodO (reuse),
   LCNAF; cached remote clients for VIAF, Wikidata, LoC, LUX with the self-echo rule; scoring, voting, thresholds;
   review queue export; tests against recorded responses. *Local half built 2026-10-18 (doc 11, section 8): Getty
   N-Triples loader, name matching, scoring and grouping, the decision ladder, `tools/Align load-getty|reconcile`.
   Remote half (3b) next.*
4. **Pipeline runner and the PMC instance**: manifest, stages, DScribe adapters and mappers ported from pmc-2, the
   writer with the Activity Streams delta; a full run over the current dumps; the report.
5. **Review UI**: the trainer generalised to agents, places and concepts, with LLM proposals as in the dates trainer.
6. **Wellcome adapter** and a second full run, which is the test that the toolkit is general.
7. **Publishing and the QLever loader**; the discovery engine design.

Steps 1 to 3 do not touch PMC code and have no network dependency in their tests.

## 13. Decisions (owner, 2026-10-17)

1. **Identifier base: a configurable hostname per dataset.** PMC keeps the hostname its published identifiers already
   use, so the re-export changes nothing LUX has stored. The base is a manifest setting; new datasets choose their own.
   The thing/document/representation structure from Leeds is adopted as the shape under that hostname.
2. **PostgreSQL is the store.** SQLite is dropped from the design: one implementation, one schema, the owner's existing
   data already there. Tests run against a disposable database (a local instance or a service container in CI).
3. **The LUX duplicates: leave for now.** The self-echo mapping (7.4) keeps a clean re-export possible either way.
4. **Leeds: read** (section 2). Anything further the owner finds in those repositories is folded in as it comes.
5. **Agents get the same playground and trainer as dates, and the two train each other.** The agent-string parser
   delegates the date substring to the dates parser, so an agent corpus entry's expectation nests a dates expectation
   for that substring, in the same shape the dates corpus uses, and a failure says which part failed. In the trainer an
   item has a kind (date, agent, later place and concept); reviewing an agent shows the parsed parts (name, dates,
   honorific, roles, nationality) for correction, and saving writes an agent corpus entry and, when the date part was
   corrected or confirmed, a dates corpus entry for the substring as well (de-duplicated against the existing corpus).
   Agent strings are thus one of the largest sources of dated expectations: every library heading with dates feeds the
   dates corpus without anyone typing a date on its own.
6. **LLM escalation, as for dates, with graded autonomy for large datasets.** The dates rule stays for anything that
   enters the verified corpus: a person confirms. For pipeline output over hundreds of thousands of strings a stricter
   human-per-string rule would never finish, so the reconciler uses three tiers, all logged with evidence:
   - *Deterministic*: parser or authority match at or above the accept threshold; no model call.
   - *Corroborated proposal*: the interpreter's canonical form is realisable and agrees with an independent signal (an
     authority candidate whose name and dates agree, or the deterministic parser's partial reading). Accepted into the
     output automatically at the interpreter's capped confidence, flagged `Interpreted`, decision method
     `llm-corroborated`; a random sample (manifest setting, say 5%) plus every case where the signals disagree goes to
     the review queue for audit, and any correction found there invalidates the matching decisions.
   - *Uncorroborated proposal*: queued for a person; the output carries the deterministic reading or a provisional
     entity meanwhile.
   Bulk runs use the Message Batches API (half price, asynchronous); the manifest carries a per-run token budget and
   the run stops asking when it is spent. Nothing a model proposed becomes `verified` corpus or a `confirmed` entity
   without a person; the autonomy is in what the pipeline outputs, with its provenance stating exactly that.

Still open: whether the review UI stays in the examples site (as the trainer generalised) or moves beside the store as
part of `tools/Align`; the owner's instinct for the former is noted, and the first agent work will show which fits.
