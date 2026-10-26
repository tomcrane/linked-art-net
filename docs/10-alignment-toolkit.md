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

### 7.5 Compound subjects (designed 2026-10-19; built 2026-10-20 as the compound shape of 7.6)

The PMC library's `keywords` field holds precoordinated LCSH-style headings: "Art, Modern--18th century--Great
Britain", "Cotman, Frederick George, 1850-1920--Exhibitions". The 2024 export treated each heading as one opaque
concept: LUX today holds, for the stem "Art, Modern--19th century" alone, 53 PMC concepts (`unrec_…` identifiers)
whose only content is the whole heading as a label, with no parts and no external ids, so a LUX search for
"Exhibitions influenced by Cotman" finds nothing (checked against the LUX API on 2026-10-19). The LUX records the
2024 notes pointed at are 404: LUX reminted its UUIDs, while the PMC identifiers inside the records survived.

Linked Art's rule for a coordinated concept (linked.art/model/concept, "Creation and Influences") is a `Type` whose
`created_by` `Creation` is `influenced_by` the component entities, which may be of any class; components are not
`broader` terms, since "France" is a Place, not a narrower kind of "History of France".

The design:

1. **Split, then reconcile the parts.** A string in a context marked `compound` (the manifest says which contexts and
   the separator, `--` for LCSH) is split into part strings, collected in derived contexts: `keywords:head` for the
   first segment, `keywords:sub` for each subdivision. The head may name a type, person, group or place and is read
   with the LoC heading profile (so "Cotman, Frederick George, 1850-1920" meets the LoC names index as a person); a
   subdivision may name a type, place or period. Parts are strings like any other: they go through the ladder, the
   desk and the variants page, and overrides apply to them. One decision on "Art, Modern" settles every heading that
   contains it; thousands of headings decompose into a few hundred distinct parts.
2. **The heading stays an entity** with its existing PMC identifier (LUX already holds those ids; the self-echo pass
   of 7.4 maps them). The heading entity's class is Type; its label is the heading. Its parts are not stored as a
   second truth: at export time the parts are the current decisions of its part strings, in order, so a reviewer's
   later correction of a part flows into every heading. The export writes the pattern above, with each resolved part
   as the entity its decision names and an unresolved part as the project's own provisional entity for that part.
3. **Indexes.** LCSH (the LoC subjects file, same MADS/RDF shape as names, same loader with the subject classes) for
   topics, forms and chronological subdivisions; AAT for forms and materials; TGN and LoC names for places; the LoC
   names index for name headings. Period subdivisions ("18th century") resolve to LCSH's own period headings or to a
   local period authority minted once per project.
4. **Workbench.** The entity page of a heading lists its parts with their lozenges and links to the part entities; the
   desk shows a part string with the headings it occurs in as context; the project page counts parts separately.

The `unrec_` identifiers are kept: the owner's decision 2026-10-21 ("keep"; the owner recalls the 2024 outputs
reached LUX before the alignment was settled, and the LUX check above found them there). Built that day: a string
the earlier pipeline gave an unreconciled identifier carries it in its parsed JSON as `legacyUnreconciled` (the
import keeps it), and `Reconciler.OwnEntityAsync` mints the string's own entity under that identifier, placed under
the class's segment as the import places the `authorities` identifiers (`…/concept/unrec_…` for a heading's
composite, the string's class for a queued or minted string); the same placeholder on a second string reuses the
entity, and an identifier that would not make a path segment is ignored. Part strings carry no legacy identifier and
mint fresh. One assumption to check against LUX before the export is designed: that the 2024 export placed the
`unrec_` identifiers under the same class segments. Export of the coordinated concept itself waits for 7.5 item 2.

### 7.6 Strings made of parts (designed and built 2026-10-20; generalises 7.5)

The first full corpus runs with all five indexes loaded (ulan, loc, aat, lcsh, tgn; 14.5M records) left 26,594 of
36,823 PMC strings minted. Samples by context (run-20261019-193430-gm5vtp and run-20261020-090207-x7q3sw) show that
the residue is not missing authority records but strings that name more than one thing, or one thing plus where it
is:

| context | strings | minted | what the minted ones are |
|---|---|---|---|
| keywords | 21,721 | 18,477 before LCSH, ~17,400 after | compound headings: "Painting--Canada", "Art, Modern--20th century--France--Exhibitions", "Tuke, Henry Scott, 1858-1929--Exhibitions" |
| place | 1,402 | 934 | lists, "Dublin; New York", "Paris ; Aix-en-Provence"; a place and its parent, "Hammersmith, London", "Piltown, County Kilkenny", "Heaton, Bradford"; a few misspellings, "Farborough" |
| publisher, corpauthor | 9,285 | 5,748 | mostly genuine residue: small galleries, printers and firms absent from ULAN and LoC |
| persauthorfull | 4,415 | 2,089 | genuine residue, to be measured against VIAF (3b) |

TGN holds Hammersmith, with a parent string containing London; LCSH holds "Painting" and "Canada" and some
precoordinated headings whole, but a free-floating subdivision (Exhibitions, Catalogs, History) combines with anything
and is never a record. Section 7.5 designed the split for keywords; the same mechanism serves places, and it is one
mechanism with three shapes:

- **Compound** (7.5): parts coordinate into one concept. Separator `--`. The whole is an entity (Type) whose parts
  are the decisions of its part strings; export as a coordinated concept.
- **List**: the string names several entities, each complete in itself. Separator `;` in `place` (publication
  places), and in any context the manifest marks so. The whole is not an entity: the parent string's decision is
  `list` and the record that cited it gets every member's entity.
- **Qualified**: one entity named with its container or a disambiguator, "Hammersmith, London", "Kent Town, S.
  Australia", "Fountains Hall (Yorkshire)". No split into part strings: the reading is the name plus a qualifier, and
  the qualifier scores against the candidate record, for a place against TGN's parent string (`data.parentString`,
  already loaded) and LoC's geographic qualifier, for a group against the LoC parenthetical. Today a qualifier is
  stripped and scored at 0.85 (the qualifier-stripped match); checking it instead gives agreement or conflict, and
  "Heaton, Bradford" stops tying with the Heaton in Newcastle.

Shape in the store and the reconciler:

1. **Derived strings.** `source_strings` gains `parent_id` and `position` (and `role`: head, sub, member). A context
   rule gains `Parts`: `{ separator: "--", shape: compound, contexts: { head: "keywords:head", sub: "keywords:sub" } }`
   or `{ separator: ";", shape: list, context: "place" }`. Collecting a parent string collects its parts as strings in
   the derived contexts, de-duplicated by key like any other string, so "Exhibitions" is one string with thousands of
   parents. Parts go through the ladder, the desk and the variants page unchanged; an override on a part applies
   everywhere it occurs.
2. **The parent's decision.** Method `composite` (compound) or `list`, entity null for a list and the heading's own
   Type entity for a compound; evidence lists the part string ids in order. A parent is "decided" when every part is;
   the project page counts parents and parts separately, and the desk shows a part with the parents it serves.
3. **Reading parts.** The head of a compound heading may be a person or group: it is read with the agent parser's
   LoC heading profile, so "Tuke, Henry Scott, 1858-1929" meets ULAN and LoC as a dated person and the vote has its
   dates. Subdivisions read as Type, Place or Period; a period part ("20th century") resolves to LCSH's period heading
   or a project period entity minted once. A list member in `place` reads as a qualified place.
4. **Home vocabulary per context** (plan open item, 2026-10-19 shadow): a derived context names the vocabulary whose
   exact term decides, keywords:head and keywords:sub say lcsh, and the other vocabulary's exact term on the same
   string is carried as an equivalent rather than a rival group.
5. **Export.** A compound heading exports as 7.5 says; a list exports as its members on the citing record; a
   qualified place exports as the TGN place with its parent chain available from the record.
6. **Parser gaps seen in the samples**: "McSloy, Tom, op 1946" (a working date written "op"; dates grammar), "The
   Hunterian" and "Hunting Art prizes" read as Person (no corporate word; a definite article or "prizes" should lean
   Group), "Mainz/Rhein" (a slash variant of "Mainz am Rhein"; name-forms).

Order of work: the qualified reading first (no schema change, measurable on the 934 places at once), then derived
strings with the list shape for places, then the compound shape for keywords with the home-vocabulary rule. Each step
is measured by the by-context table of a fresh corpus run and a shadow sample.

Built 2026-10-20, the qualified shape: `ContextRule.Qualified` (the PMC `place` rule sets it; the manifest writes it
as `qualified`), `Qualified.Split` (name before the first comma, parentheticals as qualifiers), `Candidate.Placement`
(TGN's `parentString`, else the parentheticals of a LoC or LCSH label), `ScoredCandidate.QualifierAgrees` (a word of
three letters or more of the qualifier among the placement's words; since the evening of the same day the cataloguers' abbreviations for states, provinces and
territories, the AACR2 Appendix B forms LoC uses, are read as their names, "N.J." as New Jersey, and a name of several
words is one word, so "Jersey" is not New Jersey; the table is in `Qualified.Regions`, written from the assistant's
knowledge of the list). A placed candidate lifts
by 0.05 and vetoes the namesakes the qualifier does not place; a placed exact single-source match carries like one
with agreeing dates; an unplaced qualifier bars the exact-single policy; a minted or aligned qualified name keeps its
qualifier as the entity label, and in a qualified context the store stage answers only when qualifiers agree or
neither side has one, so a bare "Heaton" meets both Heatons in the indexes rather than the one aligned first.

Built 2026-10-20, the list shape: schema v7 `source_string_parts (parent_id, position, part_id, role)`;
`ContextRule.Parts` = `PartsRule(shape, separator, context)` (the PMC `place` rule: list, `;`; the manifest writes
`"parts": { "shape": "list", "separator": ";" }`); a string that splits into two or more parts reads as
`StringReading.Parts` and evaluates to `Outcome.Decomposed` (an override on the whole string still wins first);
`Reconciler.DecomposeAsync` collects each part as a string in the parts' context without counting an occurrence,
links it in order, reconciles it when it has no decision yet, and decides the parent with method `list` and no
entity. The report counts lists and newly reconciled parts; the shadow verdict for a decomposed string is
`Decomposed`. Also here: the exact-single policy's two-word rule now applies to people and bodies only, since a
one-word place or subject ("Lampeter", "Sculpture") is whole.

Measured 2026-10-20 (run-20261020-100906-ccv3h8, all five sources, both shapes): accepted 1,632 (vote 531,
exact-single 1,101), queued 7,005, minted 25,866, lists 404 as 160 newly reconciled parts; against the baseline
run x7q3sw, 728 strings left minted. The place context: minted 931 -> 491 (qualified shape alone, run zwc3gb) ->
203; list 404; vote 3 -> 120; exact-single 14 -> 32; queued 302 -> 626, the namesakes the qualifier does not
settle ("Princeton, N.J.") now reaching the desk instead of being minted. Keywords exact-single 327 -> 353 from the
one-word rule; the rest of keywords waits on the compound shape. A fifteen-string sample of the placed votes was
right in every case, and showed two things to settle:

1. A coarser placement that agrees is treated as a namesake. "Princes Risborough, Buckinghamshire" is placed on
   TGN 1030371 (parent string "Buckinghamshire, England, ..."); LoC n82118681 "Princes Risborough (England)" and
   the store's own entity labelled from it fail the qualifier ("Buckinghamshire vs England") and are vetoed as
   namesakes the qualifier does not place. They are the same village: LoC qualifies with the country unless a
   namesake forces the county, so "(England)" is a claim of uniqueness at that level, not a different place. The
   cost is a second entity for one place (the new TGN-placed one beside the legacy LoC-identified one) and a
   lost equivalent. Proposed rule, "placed through": when the qualifier places candidate A, any candidate B whose
   placement words (three letters or more, not noise) are all found in A's placement is the same thing as A and
   joins A's group, so the entity carries both ids and an existing store entity is matched rather than
   duplicated; B's placement that is not contained ("Me.", "Dumfries and Galloway") stays a namesake. The store
   stage needs the same courtesy: a store entity whose qualifier is coarser than the string's is not refused
   outright but held for the index stage to place through. Owner's decision before building; the shadow of
   this run will show how many old LoC-identified places it touches.
2. A single-source match carried by a placed qualifier (or by agreeing dates) is recorded under method `vote`,
   which it is not; the desk cannot review carried singles as a set the way it reviews `exact-single`. A method
   of its own (`single`) for both carriers, written by `Carries()`.

Built 2026-10-20 (the owner's "yes" to both), placed through: in `Reconciler.Score`, after the qualifier has placed
one or more candidates, a candidate of another source whose placement words all sit inside a placed candidate's
placement (`Qualified.Contains`) is `ScoredCandidate.JoinedTo` that candidate and `Groups` unions the two, so
LoC's "Princes Risborough (England)" and the store's legacy entity labelled from it join TGN's record: two sources
agree, the decision is a vote, and the legacy entity gains the TGN id instead of a duplicate being minted. Inside
two placed candidates of one source with the same parent string (a city and its district) the coarser record is
placed through neither and left unvetoed; those rivals go to the desk. Records of one source never place through
each other. The method `single` (`DecisionMethod.Single`, `CarriedBy()`) now records a single-source match carried
by agreeing dates or a placed qualifier; the report prints "accepted N (vote V, single S, exact-single E)". From the
same day's shadow: a LoC names id ending `-781` (the heading's geographic-subdivision form, MARC field 781) is given
the base id as a sameAs when read as a candidate, so "Massachusetts" and "South Africa" no longer meet themselves as
rivals; and the agent parser leaves a body's words alone ("Queen Victoria Museum" had lost its "Queen" and matched
the Victoria Museum).

Measured 2026-10-20 (run-20261020-112520-sxyxbc, placed through and `single` in play): accepted 1,658 (vote 441,
single 110, exact-single 1,102; the report line printed "single 0" because the count was never handed to the
report record, fixed), queued 7,063, minted 25,858. Place: single 44 and vote 98 against 120 votes before, so 22
more places accepted; minted 198; queued 598; exact-name 136. The vote sample showed the legacy LoC-identified
entities ("Princes Risborough (England)", "Saxmundham (England)", "Billingshurst (England)") matched and given
their TGN ids rather than duplicated, and LoC's finer headings ("Godshill (Hampshire, England)", "Tal-y-bont
(Ceredigion, Wales)") placed through TGN's. It also showed "Secaucus, New Jersey" minting a new entity beside the
legacy "Secaucus (N.J.)", because "N.J." placed nothing: hence the abbreviation table above. The next corpus run
measures it.

Built 2026-10-20 (evening), the compound shape and the home vocabulary. `PartsShape.Compound`; the PMC `keywords`
rule is `parts: { shape: compound, separator: "--", context: "keywords:sub", headContext: "keywords:head" }` with
`home: lcsh`, and the two derived contexts have rules of their own: `keywords:head` may name a type, person, group or
place, reads through the `marc` agent profile only when the head carries dates (`parseDatedOnly`, so "Tuke, Henry
Scott, 1858-1929" meets ULAN and LoC as a dated person while "Art, Modern" stays a topic), home lcsh; `keywords:sub`
may name a type, place or period, home lcsh. A compound heading is looked up whole first, since LCSH holds some
precoordinated headings as records ("Art, Modern--20th century" is accepted whole), and evaluates to `Decomposed`
only when nothing holds it; `DecomposeAsync` then collects the head and subdivisions as strings in their contexts
(roles `head` and `sub` in `source_string_parts`), reconciles the new ones, and decides the heading as `composite`
with the heading's own Type entity (the project's existing entity under that label when it has one, so imported
headings keep their identifiers, else one minted under the project's scheme). The home vocabulary rule
(`Reconciler.JoinToHome`): when exactly one record of the context's home source matches the name exactly, another
source's exact term on the same name, or the store's entity under that label, is joined to it (`ScoredCandidate.JoinedTo`,
the field placed-through also uses) and grouped with it, so "Juvenile literature" is LCSH sh85071184 carrying AAT's
"children's literature" as an equivalent and the vote has two sources; two exact home records stay rivals for the
desk. The report counts compounds beside lists. Export (item 5) is still to come: it reads a composite's parts from
`source_string_parts` and the parts' current decisions.

Also that evening, from the owner's question "how often do I need to re-press the manifest button": the Align tool
now reads the context rules from the project's saved manifest, as it already read the sources (the PMC default only
when the project has none), prints a "manifest:" line saying whether the saved manifest still equals the library
rules and in what it differs (`ProjectManifest.Differences`), and `manifest --use-default` saves the library rules
from the command line; the project page shows the same "In effect now" sentence above the manifest editor, and the
button reads "Replace with the PMC library rules". The answer is: after any commit that changes `PmcDefault`, which
the line now tells you.

Measured 2026-10-20 (run-20261020-124120-aqn6dz, compound shape and home vocabulary in play): considered 36,930 and
9,437 new part strings; accepted 4,827 (vote 2,059, single 647, exact-single 2,121), queued 8,657, minted 12,287,
lists 404, compounds 15,749. Keywords: composite 15,749, minted 3,033 (from 17,849), queued 2,086, exact-name 269,
exact-single 346, vote 235. keywords:head, 7,741 strings: exact-name 1,944 (heads that are already the project's
entities: "Painting", "Portraits", "Art, Modern"), exact-single 948, vote 1,497, single 549, queued 2,050, minted
738, override 15. keywords:sub, 1,536 strings: exact-name 269, exact-single 79, vote 46, queued 632, minted 510.
Place as before (minted 198, vote 116, single 31). Store stage 968 s. The forecast "a few hundred part strings" was
wrong by an order of magnitude: PMC's subject headings are mostly about people ("Tuke, Henry Scott,
1858-1929--Exhibitions"), so keywords:head is the corpus's second-largest name context and reconciles as
persauthorfull does (Tuke and Hewlett voted ULAN with LoC on dates; "Clanbrassill, James Hamilton, Earl of, ca.
1691-1758" is a single on LoC's "approximately 1691-1758"). Findings from the samples:

1. Bug, fixed the same evening: "Dance" (keywords:head) was accepted as a *person*. LCSH's topic "Dance" was the one
   exact home record and carried ULAN's George Dance the younger, ULAN's Nathaniel Dance-Holland and LoC's Dance
   Association for North Carolina Educators, all exact on the name through a variant, and the entity took the class
   of the group's best member. `JoinToHome` now carries only records of the home record's own class; "Dance" goes to
   the desk as four rivals.
2. LCSH holds a subdivision as a record of its own, beside the heading of the same words: "Catalogs" is sh85020898
   (the topic), sh99001246 (the form subdivision) and sh2005003411; "Diaries" sh85037601 and sh99001602; "20th
   century" meets "Twentieth century" sh85139020 and the chronological subdivision sh2002012476. In keywords:sub the
   subdivision record is the right one, in keywords:head the heading; today they are rivals and the string is
   queued, which is most of the 632 queued subdivisions. LCSH says which is which in `madsrdf:isMemberOfMADSCollection`
   (collection_Subdivisions and its kinds, collection_TopicSubdivisions, collection_FormSubdivisions,
   collection_ChronSubdivisions, collection_GeographicSubdivisions; headings are in collection_LCSHAuthorizedHeadings;
   collection names from the assistant's knowledge of id.loc.gov, to check against the file), which the loader does
   not keep. Design: keep the memberships in the record's data at load (an LCSH reload), and let a context rule name
   the collection its strings belong to (`collection: subdivisions` on keywords:sub); a home record outside the named
   collection is then vetoed when one inside it matches as well. Not built.
3. "Doyle, Richard, 1824-1883" (keywords:head) queued although ULAN and LoC n79071133 agree on the dates: LoC's
   undated no2020029689 "Doyle, Richard" is a rival at 1.00 (the 1948- record is vetoed by its dates). The analogue
   of the qualifier veto, for the owner: when the string carries dates and a record agrees with them, a record of the
   same source with no dates is a namesake the cataloguer could not date, not the dated person, and should be
   vetoed. The same rule would reach persauthorfull. Measure first (the SQL in the plan item).
4. "Stage--setting and scenery" decomposed into "Stage" (two TGN hamlets and LCSH "Acting", queued) and the
   subdivision "setting and scenery", minted. LCSH's heading is "Stage-setting and scenery" with one hyphen; the data's
   double hyphen is a mis-keyed separator, and the sign is a part beginning in lower case (LCSH subdivisions are
   capitalised). Design: before decomposing, rejoin a lower-case part to the one before it with a single hyphen and
   look the whole up again; count the lower-case subdivisions first. Not built.
5. AAT's qualified terms score 0.85 against the bare word: "dance (performing arts genre)", "twentieth century
   (dates CE)", where "diaries" scores 1.00, so the home rule cannot carry them. The AAT loader could index the term
   without its parenthetical qualifier, keeping the qualifier as the record's placement as the LoC labels already
   are read (`Qualified.Split`). Loader change and reload; not built.
6. "Canada" (keywords:sub): TGN's nation, LoC n79007233 and two TGN namesakes at 1.00, queued: the open place-type
   preference item; the home rule does not reach it because the geographic name is in LoC names, not LCSH.
7. The report line read "compounds 15749 (as 9437 new parts)" though the parts count covers the list parts too (160
   of them); it now reads "between them N new part strings".

The owner's two counts (2026-10-20, run aqn6dz): finding 3's ceiling is 754 queued keywords:head strings, 29
persauthorfull and 50 publisher whose dates agree with some record; finding 4's lower-case subdivisions are 101 of
1,536. The assistant's recommendation on finding 3, for the owner: make it policy. LoC headings must be unique, so
when LoC holds both "Doyle, Richard, 1824-1883" and an undated "Doyle, Richard" it is saying they are two people;
vetoing the undated same-source namesake when the string's dates agree with the dated record applies LoC's own rule.
The risk is a wrongly dated string, which every date agreement already carries, and the decision is recorded as
single or vote and reviewable as a set. Same source only: an undated record of a source with no dated record of the
name stays a rival.

Built 2026-10-20 (night), finding 2: the LCSH loader keeps a subject record's `madsrdf:isMemberOfMADSCollection`
memberships in its data as `collections`, by short name, checked against id.loc.gov that day: "Catalogs" sh85020898 is
in LCSHAuthorizedHeadings, sh2005003411 in Subdivisions and TopicSubdivisions, sh99001246 in Subdivisions and
GenreFormSubdivisions; "20th century" sh2002012476 in Subdivisions and TemporalSubdivisions. The record's collections
reach the candidate (`Candidate.Collections`), and a context rule's `collections` lists the ones its strings mean, in
order of preference: `keywords` and `keywords:head` want `LCSHAuthorizedHeadings`; `keywords:sub` wants
`GenreFormSubdivisions` then `Subdivisions`, since a library's subdivisions are more often forms ("--Catalogs",
"--Exhibitions") than topics where LCSH has both. `Reconciler.PreferCollections`, before the home rule: among one
source's exact matches that carry collections, those in the earliest wanted collection stand and the rest are vetoed
("lcsh's GenreFormSubdivisions record of the name is the one wanted here"); a lone record, records without
collections and sources without them are left alone. So the home rule then sees one exact LCSH record and carries
AAT's term. The rule is general: any vocabulary whose loader keeps collections can be sorted by them. It takes an
lcsh reload (the loader change) and a manifest refresh (PmcDefault changed) to measure.

The first measuring run (run-20261020-140820-q7jhh7, after the lcsh reload) could not be read: it considered 46,207
strings, the 36,930 of the corpus and the 9,277 part strings of the run before, which outlive an undo as rows and were
undecided when the list was made. Each was decided by its parent heading on the way and then again when the loop
reached it, the second decision superseding the first (a minted part met its own entity in the store and became
exact-name), and the counts doubled (matched in store 4,371 -> 10,204, store stage 1,310 s). Fixed the same evening:
`RunAsync` passes over a string that has a decision by the time it is reached and reports them ("N of those
considered were decided as parts before the run reached them"). Undo q7jhh7 and run again to measure.

Measured 2026-10-20 (run-20261020-150054-tzm64p, the collections rule and the "Dance" class fix in play, against
aqn6dz): accepted 4,827 -> 4,836 (vote 2,059 -> 2,068), queued 8,657 -> 8,518, minted 12,287 -> 12,290; store stage
788 s. keywords:sub: queued 632 -> 581, vote 46 -> 88, exact-name 269 -> 273, minted 510 -> 515. keywords:head: vote
1,497 -> 1,462 and queued 2,050 -> 2,078, the class fix undoing 35 cross-class joins like "Dance", which is now queued
as a concept with its four rivals. The collections rule does what it was built for where it applies: "Diaries" is a
vote on sh99001602 (the form subdivision) carrying AAT's "diaries", the topic heading vetoed; "Biography" is down to one
live LCSH record of three. Why the fall in queued subdivisions is 51 and not hundreds, from the sample of 15 still
queued:

- 5 of 15 are blocked only by AAT's qualified terms near but not at 1.00 ("Catalogs" by "catalogs (documents)" at 0.85,
  "20th century", "21st century", "Biography", "Private collections" by "private collections (repositories)" at 0.93):
  the one live LCSH record is not an exact-single because a near rival stands, and not a vote because AAT's term is not
  exact. Finding 5 (AAT qualifier indexing) is now the main blocker for subdivisions. Its design gains a refinement:
  the home rule should carry another source's term only when that source too has exactly one exact record, or AAT's
  two "private collections" concepts would both become one entity's equivalents.
- 8 of 15 are places with TGN namesakes ("Canada", "England", "Cornwall", "Orkney", "London", "Kent", "Norfolk",
  "Austria"): the open place-type preference item, now the larger half of what remains in keywords:sub, with the
  measuring query in the plan. "England" also meets two of the project's own legacy entities under that label
  (xscu7bkr, ppaajaj9), duplicates in the imported data.
- "Stage" (finding 4, the mis-keyed separator) and, finding 8, "Stage setting and scenery": LCSH's heading is
  "Stage-setting and scenery" and the keys keep hyphens (`StringKey.Normalise` folds dash forms to "-" but does not
  equate a hyphen with a space), so the record is not retrieved; a key with hyphens as spaces on both sides needs the
  index rebuilt, so this waits for the next full reload.
- The `decisions` printer shows three groups of three members, and tied groups keep the sources' consulting order, so
  the home vocabulary's record ("Dance", "Catalogs") can be the fourth group and unprinted; the evidence holds five.

Place moved slightly with no rule touching it (exact-name 146 -> 128, vote 116 -> 125, minted 198 -> 204, single 31 ->
32), not yet explained: the undo and redo of a run are not exactly idempotent on the store side (legacy entities
reverted and realigned), to look at with the store-stage work.

Built 2026-10-20 (night), finding 5 with the home rule made decisive. The AAT loader indexes each term without its
parenthetical qualifier as well (`GettyVocabLoader.BareTerm`: "catalogs" beside "catalogs (documents)"), so a bare
"catalogs" is exact on it and the qualifier is read as the record's placement by the qualified shape. The name
scorer had put such a term at 0.85 ("equal only once parenthesised qualifiers go"), a near rival that blocked the
exact-single policy without being able to vote. And `JoinToHome` now decides outright: when the home vocabulary has
exactly one exact record, another vocabulary's one exact term of the same class is carried as its equivalent, and a
vocabulary with several exact terms of the name (AAT's two "private collections") carries none of them and they are
set aside with the reason recorded ("lcsh Private collections decides: aat has 2 exact terms of the name, none
carried"), so indexing AAT's bare terms cannot make a string LCSH knows ambiguous; a term the home record's own links
say is the same thing is left in the group; the store's own entities under the label are carried as before. AAT
records are Type class and only the keyword contexts allow Type, so no other context sees the change. It takes an aat
reload (57,322 records) to measure; PmcDefault is unchanged.

Measured 2026-10-20 (run-20261020-160811-e2xu8x, AAT bare terms and the decisive home rule, against tzm64p):
accepted 4,836 -> 4,862 (vote 2,068 -> 2,085, exact-single 2,121 -> 2,130), queued 8,518 -> 8,483, minted 12,290
unchanged; store stage 873 s. A gain of 26 strings, as small as the sample of 15 foretold: the AAT near rival was
the only block on a few dozen subdivisions, and the places are the larger remainder (finding 9). The sample read:
"Catalogs" and "Diaries" are votes on the LCSH form subdivision carrying AAT's term, as designed. But the gain fell
in keywords (exact-single 346 -> 352, vote 237 -> 241, queued 2,075 -> 2,062) and keywords:head (vote 1,462 -> 1,479,
queued 2,078 -> 2,058), hardly in keywords:sub (queued 581 -> 579), and the sample says why (finding 11): "20th
century" is still queued because LCSH's sh2002012476 is Temporal, read as a Period, while AAT's "twentieth century
(dates CE)", now exact, is a Type like every AAT concept, and the home rule carries only a record of the home
record's own class. The chronological subdivisions ("19th century", "20th century", "21st century") are a good part
of what remains queued in keywords:sub. Design, as first written: type the Styles and Periods facet as Period. Checked
against vocab.getty.edu on 2026-10-21 before building, and wrong: "twentieth century (dates CE)" (aat 300404514) is
not under Styles and Periods (300264088) but under "dates (spans of time)" in the Associated Concepts facet, as its
parent string says. Built 2026-10-21 from that: the AAT loader types a concept whose parent string contains "dates
(spans of time)" as a Period, so AAT's centuries meet LCSH's chronological subdivisions as one class and the home rule
carries them; styles and periods proper (Baroque, Georgian) stay types, as Linked Art classifies them. Needs an aat
reload (57,322 records, minutes) before the rerun. "Stage" now also meets AAT's "stages (performance
spaces)" exactly, which changes nothing: it is finding 4's mis-keyed heading.

Built 2026-10-21, findings 9 and 10 and the owner's decision on finding 3 (the owner's answers that morning: finding 3
yes; "outvoted rivals" deferred until measured; `unrec_` deferred until LUX's needs are seen; users by GitHub id agreed
in principle). Finding 9: a place record always carries its placement (`LocalIndexSource.ToCandidate` computes it for
every Place-class record, TGN's parent string or a LoC heading's parenthetical; other classes only when the string has
a qualifier to check), and the placed-through step of `Reconciler.Score` holds for every string: when the string has a
qualifier the holders are the records it placed, as before; when it has none, every exact place record that says where
it is, so LoC's "Abbeyleix (Ireland)" at 0.85 joins TGN's Abbeyleix in Laois, Ireland and the two sources agree,
whereas inside two TGN namesakes of the name it joins neither, and a record of another class ("Abbeyleix, John
(Clergyman)") holds nothing. Finding 10: in a qualified reading whose placed records form one group, the project's one
entity exact on the name with no placement of its own (not awaiting review) joins that group ("the project's one
Adelaide joins the placed tgn record"), so "Adelaide, South Australia" reuses the project's "Adelaide" and gives it
the TGN id; two such entities, or two placed groups, and nothing is chosen for it. Finding 3 as policy: when the
string carries dates and a record of a source agrees with them, that source's undated records of the name are vetoed
("loc's dated record of the name agrees with the string's dates: this undated one is a namesake"); another source's
undated record stays a rival, the store's entities are untouched. One rerun measures all three: expect place queued
(584) and keywords:sub queued (579) to fall by the placement shapes, and keywords:head queued (2,058),
persauthorfull (1,584) and publisher (1,257) by the undated namesakes (ceilings 754, 29, 50).

Built 2026-10-21, finding 4: in `StringReading.Read`, a compound's part that begins in lower case is the tail of a
hyphenated heading whose hyphen was keyed as the separator (LCSH's subdivisions begin with a capital), so it is
rejoined to the part before it with one hyphen and the whole read from the rejoined text: "Stage--setting and
scenery" is "Stage-setting and scenery", one heading looked up whole; "Theaters--Stage--setting and scenery--Great
Britain" is three parts; "Art, Modern--20th century" is untouched, a digit not being lower case. 101 of 1,536
subdivision strings began in lower case in run tzm64p; the rerun says how many headings LCSH then holds whole. No
reload. Finding 11 built the same morning (see its paragraph above): an aat reload, then the one rerun measures
findings 9, 10, 3, 4 and 11 together, each readable in its own context's rows.

Measured 2026-10-21 (run-20261021-093102-pqe98w, after the aat reload, against e2xu8x): accepted 4,862 -> 5,492
(vote 2,085 -> 2,533, single 647 -> 773, exact-single 2,130 -> 2,186), queued 8,483 -> 7,889, minted 12,290 ->
12,361, compounds 15,749 -> 15,675 over 9,328 part strings; store stage 823 s. By context: place queued 584 -> 499
and vote 125 -> 207 (finding 9: "Piltown, County Kilkenny" a vote with LoC's "Piltown (Ireland)" placed through TGN);
keywords:sub queued 579 -> 508 and vote 84 -> 151 (the collections rule and finding 11); keywords:head queued 2,078
-> 1,659, vote 1,479 -> 1,772, single 549 -> 635 (finding 3 as policy: 419 of its ceiling of 754); persauthorfull
queued 1,584 -> 1,576 and publisher 1,257 -> 1,217 (ceilings 29 and 50); keywords exact-single 352 -> 388 and
composite 15,749 -> 15,675 (finding 4: 74 headings rejoined, most held whole by LCSH). The samples, read:

- "20th century" was still queued, and it is a flaw in the collections rule (finding 2's build): AAT's "twentieth
  century (dates CE)" is `skos:exactMatch` to LCSH's topic "Twentieth century" (checked at vocab.getty.edu), the
  record the rule had vetoed for the subdivision "20th century"; the group's union-find put all three together and
  a vetoed member linked to a live one marks the whole group vetoed, so the Express train at 0.85 ranked first.
  Fixed 2026-10-21: a preference is not a contradiction. `ScoredCandidate.SetAside` is the new state for a record
  not wanted here (outside the wanted collections, or another vocabulary's second exact term), distinct from a veto
  (dates or type conflict, a namesake the qualifier rejects): a set-aside member is not live and cannot be the
  answer, but it does not poison its group, and a group of set-aside records is not a namesake for the exact-single
  policy ("Evaluation" with AAT's two exact terms set aside is an exact-single on LCSH). The evidence writes
  `setAside` beside `veto`, and the desk dims both.
- "Hammersmith, London" was queued: LoC's "Hammersmith (London, England : Parish)" was placed by the string's
  qualifier but not placed through TGN, because its placement carried "Parish", which TGN's parent string does not.
  Finding 12, fixed the same day: LoC's type after " : " (Parish, County, Town) is not a place; `Qualified` drops it
  from a parenthetical when reading a placement or a qualifier.
- "Cornwall (England : County)", "Istanbul (Turkey)", "St. Mary (Lambeth, England : Parish)" and "National Gallery
  of Canada (Ottawa, Canada)" (keywords:head) were queued: the heading's own LoC-style parenthetical was part of the
  name, so LoC's heading matched at 1.00 and every other record at 0.85, and nothing could be a vote. Finding 13,
  built the same day: in a context that is not qualified, a heading ending in a parenthetical is read with the
  parenthetical as its qualifier and commas left alone ("Art, Modern" is itself), the entity keeping the whole
  heading as its label (`StringReading.LabelOverride`); so TGN's Cornwall is placed by England and LoC's heading
  joins it, and the project's "National Gallery of Canada" answers from the store.
- "Oppé, A. P. (Adolf Paul), 1878-1957" was queued on LoC alone at 0.90: the heading's "(Adolf Paul)" is MARC's
  fuller form of the name (100 $q), not a qualifier, and the scorer had put it at 0.85. Finding 15, built the same
  day in `NameMatcher.Score`: a parenthetical right after initials is dropped before comparing, and the names are
  the same at the folded level (0.97), so the dates then carry a single.
- "Pre" was a head: "Pre--Raphaelitism" keyed with the separator, which finding 4 did not catch because
  "Raphaelitism" is capitalised. Finding 4 extended: a part that is only a prefix (pre, post, anti, neo, non, proto,
  pseudo, semi, mid, ex; from the assistant's knowledge, not a file) is rejoined to the next with a hyphen.
- "Aschan, Marit Guinness, 1919-2004": ULAN agrees on the dates, LoC's one record of the name is undated, and the
  two are not linked, so two single-source groups tie and the string is queued. Finding 14, a policy question for
  the owner: an undated record that is its source's only exact record of the name joins the record of another
  source whose dates agree with the string's ("LoC knows one Marit Guinness Aschan; ULAN dates her"), and the two
  vote. The owner's yes and built 2026-10-21 (`Reconciler.Score`, after the undated-namesake veto): the joining
  record must be its source's only exact record of the name, dated or not, undated, of the dated record's class, and
  not the store's; "Smith, John" with many LoC records does not qualify. Ceiling from the query in the plan.
- Outvoted rivals, the owner's decision 2026-10-21 after the ceiling was measured on pqe98w (510 queued strings with
  one two-source group at acceptance score beside single-source rivals: persauthorfull 144, keywords:head 115,
  keywords:sub 83, place 68, publisher 51, keywords 34, corpauthor 15): yes for places and concepts, not for people
  and bodies. Built that day in `EvaluateAsync`: when the top group's class is Place, Type or Period, two independent
  sources carry it, and every rival at acceptance score is carried by one source and is not one of the project's own
  aligned entities, the string is accepted as a vote and the rivals' evidence reads "outvoted by a group of 2
  sources"; a second two-source group ("Cambridge" in England and in Massachusetts) still queues it. For a person
  or a body ("Smith, John" with a ULAN-linked LoC record beside another John Smith LoC knows) the tie goes to the
  desk as before: sameness of name is weak evidence there, and two vocabularies knowing a name is prominence, not
  identity. Up to 185 strings; people to revisit against the 2024 shadow.

Measured 2026-10-21 (run-20261021-112513-uhwjq5, the seven changes above, against pqe98w): accepted 5,492 -> 6,088
(vote 2,533 -> 2,921, single 773 -> 881, exact-single 2,186 -> 2,286), queued 7,889 -> 6,969, matched in store 4,399
-> 4,730, minted 12,361 unchanged; store stage 800 s. By context: keywords queued 2,071 -> 1,648 and exact-name 294
-> 537 (finding 13: whole headings with a parenthetical answering from the store, and 79 singles placed by their own
parenthetical); keywords:sub queued 508 -> 330, vote 151 -> 237, exact-single 80 -> 149 (set-aside and finding 11:
the centuries); keywords:head queued 1,659 -> 1,448; place queued 499 -> 438 and vote 207 -> 264 (outvoted rivals
and finding 12); persauthorfull queued 1,576 -> 1,542 (findings 14 and 15). The samples, read:

- "Cornwall (England : County)", "Istanbul (Turkey)", "National Gallery of Canada (Ottawa, Canada)" were still
  queued, and the reading showed why: finding 13 ran only when the context had no agent parser, and keywords:head
  keeps the MARC parser for dated strings, so an undated heading there was never split. The unit test had passed
  with no parser. Fixed the same day: the split runs after the parser step, whenever the parser has not read the
  string; the store test reads "Cornwall (England : County)" through the real rule.
- "Friedmann, Gloria, 1950-": ULAN and LoC each have one record of the name, both agreeing with the string's dates,
  unlinked, so two single-source groups tied. Finding 16, built the same day: two sources that each date one record
  of the name as the string does date the same person, and the record joins ("dated as ulan's record is") for a
  vote; two agreeing records in one source still go to the desk.
- "Kent Town, S. Australia": the string's own half-abbreviation "S. Australia" placed nothing against LoC's "S.A."
  and "S. Aust.", which the table expands. "S. Australia" and "W. Australia" added to it the same day.
- "Paris", "Dublin", "Athens", "Cornwall", "london" (place): several TGN namesakes each carrying its own LoC heading
  placed through ("Paris (Ont.)", "Paris (Idaho)", "Paris (Ill.)" and, unprinted, "Paris (France)"), so several
  two-source groups tie and outvoting rightly stands aside. What tells Paris from Paris is prominence, which no rule
  so far reads. Finding 17, a design for the owner: among tied two-source groups of places, prefer the TGN record
  with the most names (TGN gives the famous city dozens of names in as many languages, a hamlet one or two), or the
  LoC heading with the most variants; crude, measurable from the exports, and only among groups two sources already
  carry. Not built.
- "Evanston, Illinois": TGN placed and LoC placed through it, but two of the project's own entities "Evanston"
  stand beside them, so finding 10 (the project's one bare entity) does not apply and the string queues; "England"
  had two as well. The legacy import carries duplicates. A merge pass for the project's own entities of one label
  and class with no conflicting equivalents is the remedy, with a count first (query in the plan). Not built.
- "Ionia (Turkey and Greece)": once split, LCSH's heading is placed by its own parenthetical and TGN's Ionia by
  "Turkey", but the heading's two-part placement is not contained in TGN's parent string ("Turkey, Asia, World"
  has no Greece), so the two are not joined. A known gap of placed-through's containment test; few strings.
- "Drury", "Paintings, European", "Donnington", "Heaton, Bradford" (TGN has no Heaton in Bradford), "Bottrop,
  Essen" (the data's qualifier is a neighbouring city): the desk's work.

- "Paris", "Aix-en-Provence", "Bottrop, Essen", "China", "Canada": TGN namesakes of differing level (the city and
  its arrondissement or Kreis, the nation and a hamlet), LoC's placed heading "inside 3 placed candidates" and so
  joining none: the TGN level preference, next on the list, now with its shape clear from these. LoC's own
  heading says the level: "Aix-en-Provence (France)" is the city, "(France : Arrondissement)" would be the
  arrondissement, so the heading's type after " : " (finding 12 drops it from the placement, but it can be kept
  beside it) and TGN's `placeType` decide which namesake the heading holds.

Measured 2026-10-21 (run-20261021-142134-epenbw, the finding 13 fix, finding 16, the abbreviations and the `unrec_`
identifiers in play, against uhwjq5): accepted 6,088 -> 6,333 (vote 2,921 -> 3,217, single 881 -> 888, exact-single
2,286 -> 2,228), queued 6,969 -> 6,647, matched in store 4,730 -> 4,763, minted 12,361 -> 12,395. By context:
keywords:head queued 1,448 -> 1,142 and vote 1,772 -> 2,184 (the finding 13 fix: the parenthetical headings, split
at last, placed through and voted; some exact-singles became votes, hence the fall there); keywords:sub queued 330
-> 325; place 438 -> 433; keywords 1,648 -> 1,646; persauthorfull 1,542 -> 1,540. The minted sample's "Sporting
Gallery" took `group/unrec_t85jgf5s`, so the placeholders are kept as decided. The keywords:head queue, read:

- "Loire River (France)", "Grand tours (Education)": the split heading met LCSH's heading of the very same label and
  scored it 0.85 (the name against a label with a parenthetical), placed to 0.90, which neither the single rule nor
  exact-single carries (both want 0.97). Finding 19, fixed the same day in `Reconciler.Score`: a candidate whose
  label is the whole heading is exact ("the whole heading"). "Grand tours" also met AAT's bare "grand tours", which
  the qualifier "Education" does not place, so AAT is vetoed and LCSH's heading is a single.
- "Carlton House (London)": LCSH's "Carlton House (London, England)" placed by the qualifier, 0.90 again, LoC's New
  York house vetoed, and still queued for want of 0.97. Finding 20, fixed the same day: a qualified heading the
  string's qualifier places is exact on its name (the parenthetical is the placement, already checked), so the
  single rule carries it.
- "St. Mary (Lambeth, England : Parish)": three LoC churches in Prestwich, Tewkesbury and Tadcaster all "placed" by
  the qualifier, because `Qualified.Agrees` asked for one shared word and "England" is in every English placement.
  "Ionia (Turkey and Greece)" likewise placed TGN's Ilía (in Greece) by "Greece". Finding 22, fixed the same day:
  agreement is one side's words all among the other's (the coarser side inside the finer); a word on each side the
  other lacks is another place. St. Mary's three are not placed and the string stays with the desk; Ionia's LCSH
  heading, exact by finding 19 and placed by its own parenthetical, is a single once TGN's Ilía is vetoed.
- "Porcelain": LCSH's "Porcelain" exact, AAT's two exact terms set aside by the home rule, and LoC's "Porcelain
  (Musical group)" at 0.85 the one live rival, which blocks exact-single (the policy wants no other group at all).
  Finding 21, a policy for the owner: when the home vocabulary has exactly one exact record of the name, another
  source's inexact record (below 0.97) is set aside as a lookalike, not a rival; the home record is then an
  exact-single. Ceiling query in the plan (decision 6): 158 strings in the keywords family, the only contexts with
  a home. The owner's yes and built that evening in `Reconciler.JoinToHome` ("lcsh Porcelain decides: not the exact
  name"); the project's own entities are left as rivals, since a lookalike of ours is the desk's to merge or part.
- "Coventry, Terence, 1938-": LoC's one "Coventry, Terence", undated, the only candidate; exact-single stands aside
  for a dated string by design (the dates are there to check and nothing checks them). Finding 24, a policy for the
  owner: a dated string whose only candidate is a source's one exact undated record is carried as an exact-single,
  LoC's uniqueness of headings being the evidence (as finding 14 reasons). Ceiling query in the plan (decision 7):
  45 strings, 42 of them the dated name headings of the keywords field. The owner's yes and built that evening in
  `EvaluateAsync`: the exact-single rule, which stood aside for every dated string, now takes one whose only
  candidate anywhere is one undated exact record; a second record of the name, dated or not, is a second group.
- "China": TGN's nation ("Zhongguo") beside two TGN hamlets and LoC's unqualified "China", with LoC's Mexican and
  Maine Chinas placed through the hamlets: the TGN level preference, next on the list. It also shows two LoC
  records of the exact name, `n79091151` and `n79091151-781`. Finding 23: id.loc.gov publishes a "-781" resource
  beside a geographic heading, the heading's form as a geographic subdivision (MARC field 781), typed Geographic
  with the same label (this is the assistant's reading of the identifier pattern, not yet checked against the
  site, which refuses automated fetches; the owner's query in the plan confirms it from the local index). Every
  such twin makes LoC's one heading two exact records, which blocks exact-single and finding 14's uniqueness test
  for every place LoC uses as a subdivision. Confirmed by the owner from the index the same evening: 2,392 such
  records in the names file, typed Place, labelled as headings ("Kosovo (Republic)", "Riau-Lingga (Sultanate)",
  "Pueblo of Isleta, New Mexico"), and 45,258 in the subjects file, LCSH's own geographic headings each written
  under its country as a subdivision ("Oregon--Trail Creek Watershed (Jackson County)"), every one beside its
  parent record. Built that evening: `LocNamesLoader` leaves "-781" resources out of both files, and the owner's
  one-off deletes (the same evening) cleaned the index without a reload.
- "Drury" (a ULAN person and two TGN places under a bare surname), "Paintings, European" (ULAN's "European
  Paintings", a department, at 0.93): the desk's work.

Measured 2026-10-21 (run-20261021-165615-qr7fbx: findings 19 to 24, the "-781" deletes, the duplicate pass and the
two hand merges, against epenbw): accepted 6,333 -> 6,633 (vote 3,217 -> 3,130, single 888 -> 1,176, exact-single
2,228 -> 2,327), queued 6,647 -> 5,763, matched in store 4,763 -> 5,388 (the duplicate pass: twins no longer block
the store's silent match), minted 12,395 -> 12,359. By context: keywords:head queued 1,142 -> 777, single 652 ->
780 (findings 19 and 20), exact-single 958 -> 1,040 (21 and 24); keywords queued 1,646 -> 1,376 and single 78 ->
223; keywords:sub 325 -> 273; corpauthor 359 -> 274 and publisher 1,202 -> 1,112 (the duplicate pass, through the
store); place 433 -> 418; persauthorfull 1,540 -> 1,533. The samples, read:

- "St. Mary (Lambeth, England : Parish)": finding 22 did its work (the three churches elsewhere are gone, and LoC's
  own "Saint Mary (Lambeth, England : Parish)" is placed), but "St. Mary" against "Saint Mary" scored 0.85, so
  finding 20 could not call the name exact and the single rule did not carry it. Finding 25, built the same night:
  "St." and "Saint", "Ste." and "Sainte", "Mt." and "Mount" are one word to `NameMatcher` (the assistant's own
  list of the English place-name abbreviations).
- "Medicine": LCSH's exact heading beside LCSH's own "Drugs" at 0.85 through a variant; finding 21 set aside other
  vocabularies' inexact records only. Extended the same night to the home vocabulary's own inexact headings, whose
  headings are unique.
- "Scots": LoC's "Shoreline Cleanup Oversight Team" exact through its acronym variant "SCOTS", and ULAN's "unknown
  Scottish" through a variant of its own, beside LCSH's "Scots". Finding 28, built: a name matched only through an
  all-capitals acronym variant scores 0.85 unless the string is itself written in capitals ("RIBA" still meets the
  Royal Institute of British Architects). ULAN's attribution record stays a rival; the desk.
- "Athens" (place) was queued to the aligned legacy Athens, not a provisional: the earlier pipeline's
  `unreconciled_authority` column held tentative mappings to real authorities as well as `unrec_` placeholders, and
  the `unrec_` build reused whatever the column named. Finding 29, fixed: an aligned or confirmed entity under a
  legacy identifier is not reused for a queued or minted string, which would make a tentative decision look
  settled; a provisional one is reused and an absent one created, as before.
- "Piper, John, 1903-1992" (keywords:head): ULAN and LoC agree on the dates and join (finding 16), but two of the
  project's own entities of the name, "Piper, John" and "John Piper", both aligned (LUX and VIAF ids from the legacy
  import, no ULAN or LoC), stand beside them: two acceptable firm entities, so neither answers silently in the store
  stage, and the firm rival queues the vote. The duplicate pass folded labels only. Built: people and bodies are
  twins by the words of the name in any order as well, under the same rules (a person's twins need a shared id or
  the same dates). The next dry run says how many.
- "Bolton, James, fl. 1775-1795" and "Goldsmith, Oliver, 1730?": the string's dates were not read ("fl." and a lone
  "1730?" fall out of the MARC profile), and LoC's "active 1840-1876" is not read by the loader either, so namesakes
  whose working dates cannot overlap did not conflict. Finding 26, parser and loader gaps, in the plan's open items;
  not built.
- "Kirby, John", "Drury", "Hughs", "Paintings, European": the desk's.
- Place: "New York, N.Y" (TGN's two New Yorks both placed by "N.Y.", LoC's heading inside both), "Pisa" (three TGN
  Pisas), "Cornwall" (LoC's county heading inside two TGN Cornwalls in England), "China": the TGN level preference,
  built that night (below). "Paris", "Dublin", "Athens": finding 17, the owner's decision 4. "Evanston, Illinois":
  the two legacy Evanstons carry different Wikidata ids and stand, so finding 10 does not apply; the desk. "Bottrop,
  Essen", "Heaton, Bradford", "Donnington": the data's.

Built 2026-10-21 night, the TGN level preference (`Candidate.PlaceType`, `Qualified.TypeOf`, `Qualified.LevelOf`,
`LocalIndexSource.FindAsync`, `Reconciler.Score`): TGN's `placeType` (an AAT concept; its label is read from the local
AAT index once per concept) says a record's level, and a LoC heading's type after " : " says the heading's, with no
type meaning the inhabited place, LoC's practice. A heading inside several holders joins the one holder at its level
("New York (N.Y.)" the city, not the county; "Cornwall (England : County)" the county, not the hamlet in Devon), and
the vote then outvotes the other TGN namesake. An unqualified LoC heading of a place, which LoC gives to a place
unique in its world, joins the nation among TGN namesakes of differing level, or the highest level present when one
record holds it and it is a state or above ("China" the nation, not the hamlet in Nuevo León). The level ladder
(nation 5; state, province, region, département, Land, canton 4; county, district, arrondissement, Kreis 3; city,
town, village, borough, township, parish 2; rivers, islands and continents off it) is the assistant's own table of
the AAT place-type labels TGN uses and LoC's type words, not a published list. Left for the owner (decision 9): a
bare string with TGN namesakes of differing level and no LoC heading at all ("Pisa": the city and the province),
where the ladder alone would have to prefer the inhabited place; ceiling query in the plan.

Measured 2026-10-22 (run-20261022-085117-v9wawv: the level preference, findings 25, 28, 29, the home's own
lookalikes, the second duplicate pass, against qr7fbx): accepted 6,633 -> 6,743 (vote 3,130 -> 3,163, single 1,176
-> 1,185, exact-single 2,327 -> 2,395), queued 5,763 -> 5,586, matched in store 5,388 -> 5,491, minted 12,359 ->
12,323. By context: keywords:head queued 777 -> 701 and exact-single 1,040 -> 1,086; keywords 1,376 -> 1,360;
keywords:sub 273 -> 250; place 418 -> 401 and vote 268 -> 286; corpauthor 274 -> 260; publisher 1,112 -> 1,082;
persauthorfull 1,533 -> 1,532. The decision 9 query returned 0 rows: every bare string among TGN namesakes of
differing level has a LoC heading in play. The samples, read:

- "New York, N.Y": LoC's heading joined the city by its level ("the inhabited place among 2") and the string was
  still queued, because the project's own bare "New York" (aligned, VIAF only) stood beside two placed anchors and
  finding 10 asks for one. Fixed the same morning: among several anchors, the one a heading joined (by its level or
  its placement) is the place, and the project's one bare entity joins it there.
- "Cornwall": LoC's "Cornwall (England : County)" was "inside 2 placed candidates" though only one TGN Cornwall is
  in England: the project's own entity labelled from that heading, placed through TGN's county and joined to it,
  counted as a second holder. Fixed: holders the loop has already joined to one another are one holder.
- "Fine Art Society (London)": ULAN's three Fine Art Societies "not placed (London vs Corporate Bodies)", LoC's
  "not placed (London vs Gallery)". Finding 30, fixed: for a person, a body or a concept, checked against the
  string's qualifier, the placement is only a LoC heading's "City, Country" parenthetical ("Tate Gallery (London,
  England)"); ULAN's parent string is its facet and a LoC type qualifier is not a place, and neither vetoes a
  record as not placed. The string itself stays with the desk (three ULAN records of the name, one of them ULAN's
  "unidentified" record).
- "Gilbert, 1943-": a dated heading met TGN's Gilbert Rivers at 1.00. Finding 31, fixed: a reading that carries a
  lifespan vetoes place records; a dated heading names a person or a body.
- "Watson, J.N.P. (author)", "Christianson, C Paul (author)" (persauthorfull): LoC's "Watson, J. N. P." and
  "Christianson, C. Paul" at 0.95, the only candidates, short of exact-single's 0.97 for a dot or a space. Finding
  34, fixed: the same letters with punctuation or spacing alone between them are one name (0.97); an abbreviation
  ("Thos.") still is not.
- "Piper, John, 1903-1992" (keywords:head): still queued. The second duplicate pass kept "Piper, John" and "John
  Piper" apart (no shared id, no dates on either), so the firm legacy entity with LUX and VIAF ids alone still
  stands beside the ULAN and LoC group that agrees with the string's dates. Finding 32, a policy for the owner: a
  firm entity of the project's own whose ids are all cluster sources (VIAF, LUX) and whose name is exact, beside a
  group of two sources agreeing with the string's dates, joins that group (and gains its ids) rather than queuing
  it; the entity is the earlier pipeline's identification of the same name, and the dates are the evidence it
  lacked. Ceiling query in the plan (decision 10).
- "Scots": LoC's "Shoreline Cleanup Oversight Team" still at 1.00: the label it matched through is "SCOTs", an
  acronym with a plural's lower-case s (the owner's query, 2026-10-22). Finding 28 widened that morning to allow
  the trailing letter.
- persauthorfull, read for the first time since pqe98w: "Gay, John (author)", "Ford, John (author)", "Russell,
  James (author)", "Gray, John (author)", "Jenkins, David (author)": undated strings with one ULAN artist and
  several LoC namesakes, or several of each; the desk's, and the shape a Wikidata extract or the 2024 shadow would
  help with. "S. W. [(Simon Wilson)] (editor)": the data's.
- Place: "Paris", "Dublin", "Athens", "Winchester": prominence (decision 1, waiting for Wikidata). "Evanston,
  Illinois": two legacy Evanstons with different Wikidata ids; the desk. "Bottrop, Essen", "Heaton, Bradford",
  "Donnington": the data's.

Measured 2026-10-22 (run-20261022-093942-evt3ut: the morning's five fixes and the widened acronym rule, against
v9wawv): accepted 6,743 -> 7,132 (vote 3,163 -> 3,184, single 1,185 -> 1,108, exact-single 2,395 -> 2,840),
queued 5,586 -> 5,296, matched in store 5,491 -> 5,510, minted 12,323 -> 12,299. By context: persauthorfull queued
1,532 -> 1,396 and exact-single 352 -> 480 (finding 34: the initials); keywords exact-single 371 -> 532 and
compounds 15,668 -> 15,574 (whole headings now exact without decomposing); corpauthor queued 260 -> 223; publisher
1,082 -> 1,015; place 401 -> 373 and vote 286 -> 309 (the two level fixes); keywords:head 701 -> 693; keywords:sub
250 -> 235. The samples, read:

- "Cornwall": LoC's county heading now joins TGN's county through the project's own entity, so the top group is
  tgn, loc and the store together; but TGN's Cornwalls in Ontario and Connecticut each carry a LoC heading placed
  through too, so three two-source groups tie and outvoting stands aside. The difference between them is that the
  project's own aligned entity sits in one: the earlier pipeline identified "Cornwall" with that county, and a
  bare "Cornwall" in the same project is the same place. Finding 35, a policy for the owner: among tied two-source
  place groups, the group that holds one of the project's own aligned entities wins (decision 7, ceiling query in
  the plan). Never over a group the authorities and the project disagree about, only a tie among them. Ceiling 26;
  the owner's yes and built the same day in `EvaluateAsync`.
- "Norfolk?": read with the question mark, so TGN's Norfolks scored 0.97 and LoC's "Norfolk (Mass.)" was placed by
  its level all the same. Finding 36, fixed: a trailing question mark is the cataloguer's doubt, not the name.
- "Rowman and Littlefield" (publisher): LoC's "Rowman and Littlefield, Inc." at 0.95, the only candidate, short of
  exact-single for a legal suffix. "E & H Lumley", "Remondini (Firm) (Printer)" (corpauthor): LoC's "E. & H.
  Lumley (Firm)" and "Remondini (Firm)" at 0.85, the type qualifier read as a disambiguator. Findings 37 and 39,
  fixed (`NameMatcher.SameBody`): for a body, a name is exact without its legal suffix, a courtesy prefix
  ("Messrs"), connectives and a type qualifier without a comma ("(Firm)", "(Gallery)"), which tells the firm from a
  person of the name, not from another firm; abbreviations are not expanded ("Thos." is still not "Thomas"), a
  place qualifier stays, and a person's record ("Clark, Art") stays short of "Clark Art Ltd".
- "England & Co", "Department of the Environment" (publisher): the project's own provisional entity, queued for the
  same name in another context, stood beside ULAN's record as a second group and blocked exact-single. Finding 38,
  fixed: a provisional entity of the project's own is a lookalike, not a rival group; ULAN's one "England & Co." is
  an exact-single. "Department of the Environment" has two ULAN records and stays with the desk.
- "National Gallery London" (corpauthor): ULAN's "National Gallery" and LoC's "National Gallery (Great Britain)"
  each exact through a variant, each its source's only record of the name, unlinked, two single-source groups.
  Finding 40, a policy for the owner: for a body, two sources that each have exactly one exact record of the name
  and no other join and vote, as finding 16 does for a dated person (decision 8, ceiling query in the plan).
  Ceiling 35 (publisher 22, corpauthor 13); the owner's yes and built the same day in `Reconciler.Score`, bodies
  only (a reading whose first class is Group), Group-class records only.
- "Murray", "Sutton", "Hutchinson", "König", "Garland" (publisher), "Mr Abbott", "Mr Hodgson" (corpauthor): bare
  surnames meeting ULAN's painters through variant names (ULAN's "Koninck, Jacob, the younger" carries "König").
  A publisher named by a surname is a firm, and the desk's. "Waltham Forest: Libraries & Arts Department" against
  LoC's "Waltham Forest (London, England). Libraries and Arts Department" (a subordinate body written with a colon)
  and "Musée de l'Ermitage" against LoC's romanised Hermitage: the desk's, both right at 0.85.
- "Nationalmuseum" (corpauthor): ULAN's Stockholm museum, the project's own "Nationalmuseum (Sweden)" with LoC's
  heading, and ULAN's National Museum of Denmark through a variant; the desk's.
- Place: "Paris", "Dublin", "Athens", "Winchester": prominence (waiting for Wikidata). "Evanston, Illinois": the
  desk. "Bottrop, Essen", "Heaton, Bradford", "Donnington": the data's.

Measured 2026-10-22 (run-20261022-104922-gde6k3: findings 35 to 40 and the morning's others, against evt3ut):
accepted 7,132 -> 7,394 (vote 3,184 -> 3,272, single 1,108 -> 1,084, exact-single 2,840 -> 3,038), queued 5,296 ->
5,009, matched in store 5,510 -> 5,542. By context: publisher queued 1,015 -> 823 and exact-single 320 -> 459
(findings 37 to 39); corpauthor 223 -> 189; place 373 -> 344 and vote 309 -> 337 (findings 35 and 36); keywords
1,361 -> 1,311; keywords:sub 235 -> 224; keywords:head 693 -> 723, a regression (finding 41, below). The publisher,
keywords and keywords:sub queues, read:

- keywords:head up by thirty: finding 39 made LoC's bodies with a type qualifier exact ("Hutchinson (Firm)"), and
  beside LCSH's exact topic of the name they were a second exact group of another class, which the home rule
  neither joins (the class check of 2026-10-20) nor set aside. Finding 41, fixed: the home vocabulary's one exact
  record sets aside another source's exact record of another class ("a group of the name, not the type"), as the
  "Dance" case always wanted.
- "Garland" met LoC's "Garland (Destroyer)" at 0.97 and "Sutton" a clipper ship, and "King's College" LoC's three
  colleges through "(University of Cambridge)": finding 39 dropped every parenthetical without a comma. Tightened
  the same day: only a qualifier naming a kind of body goes (firm, publisher, gallery, museum, society, press and
  the like; the assistant's own list of LoC's usual ones); a vessel or an institution stays.
- "Williams, Peter, 1914-1995", "Reuss, Albert, 1889-1975", "Thomas, Thomas, 1817-1888" (keywords, whole headings):
  LoC's dated heading exact at 1.00, and ULAN's undated namesakes at 0.40, or LoC's at 0.83, counted as rival
  groups. Finding 44, fixed: a group below the review score is noise, not a rival, for exact-single (a vetoed group
  still counts, as the policy says). Finding 45, fixed: a string equal to LoC's dated heading, dates and all, is not
  an undated claim on a disambiguated heading. Finding 46, built: the keywords field reads its dated name headings
  through the MARC profile as its head context does ("Atken, J, fl 1783", "Horlor, George W, op 1849-1890" still
  wait on finding 26 for their working dates), so "Thomas, Thomas" with a lifespan meets its namesakes by dates;
  needs `manifest --use-default` before the run.
- "Leeds Castle (Kent)": TGN's castle placed, LCSH's "Leeds Castle (England)" a topic with no placement, two
  classes, no join. Finding 47, fixed: an LCSH heading's parenthetical places it whatever its class, and the two
  meet by placement. "Grimsthorpe Castle (Lincolnshire)" against LCSH's "(England)" alone stays the desk's: a
  coarser placement than the qualifier, with no TGN record to bridge them.
- "Cumbria" (keywords:sub): LoC's "Cumbria (England)" inside TGN's Cumbria and Westmorland (through its variant),
  both counties, and the heading has no type word, so no holder at its level. Finding 49, fixed: with no holder at
  the level, the holder whose own name the heading is, if one.
- "Thames River" (keywords:sub): TGN's Waihou in New Zealand at 1.00 through its variant "Thames River" outranked
  TGN's Thames with LCSH's heading placed through at 0.97, so the owner's outvoted-rivals policy, written for an
  equal score, did not fire. Finding 50, fixed: at acceptance score a two-source group outranks one source's
  higher-scoring record.
- "Italy" (keywords:sub): decision 7 fired and still queued, rightly: the two legacy "Italy" entities (TGN on one,
  LoC and LUX on the other) both hold the group's ids and the store disagrees with itself; the hand merge on the
  owner's list. "Cornwall" (keywords:sub) likewise queued with the county group unprinted; the owner's query in
  the plan (decision 10) shows which entities hold the county's ids.
- "Decorated style" (keywords:sub): AAT's exact term, and LCSH's "Architecture, Gothic--Decorated style" at 0.85
  as the one rival, the home having no exact record. Finding 48, a policy for the owner: exact-single tolerates
  rivals below acceptance score when the exact record is the only one at or above it (decision 9, ceiling query
  in the plan; the shape behind much of the residue, so measure before deciding). Ceiling 322: concepts and places
  180, people 125, bodies 17. The owner's yes for concepts and places, built the same day in `EvaluateAsync`;
  people and bodies keep the review-score floor.
- "Murray", "König", "Sutton", "Hutchinson" (publisher): bare surnames, ULAN's painters through variants and LoC's
  firms; the one-word rule holds them for the desk, rightly. "Macmillan" against LoC's one "Macmillan, Inc." is the
  same rule's cost (finding 42, not built: a one-word firm name against a record that is plainly a firm). "Peter
  Lang": two LoC records of the publisher; the desk. "Clark Art Ltd": LoC's person; the desk.
- "Norfolk", "Sheffield", "Vienna", "Yorkshire" (keywords:sub): prominence. "Dating": TGN's Chinese places through
  pinyin variants; the desk. "Anglesey Abbey", "Newcastle (Bridgend, Wales)", "Hall, ?": the desk's or the data's.

Measured 2026-10-22 (run-20261022-121623-t8wzjm: findings 41, 44 to 50 and 48, with the keywords field's parser
through a manifest refresh, against gde6k3): accepted 7,394 -> 8,435 (vote 3,272 -> 3,500, single 1,084 -> 1,731,
exact-single 3,038 -> 3,204), queued 5,009 -> 4,828, matched in store 5,542 -> 5,826, minted 12,297 -> 11,164. By
context: keywords single 181 -> 818, vote 357 -> 607, exact-single 593 -> 721, minted 3,028 -> 1,920 and queued
1,311 -> 1,244 (finding 46: a thousand dated name headings read and carried by their dates); keywords:head queued
723 -> 609 (the regression undone and more); keywords:sub 224 -> 183; place 344 -> 334; publisher 823 -> 867 and
corpauthor 189 -> 198 (the ships and colleges back to the desk, as expected); persauthorfull 1,393. The samples,
read, all three queues now mostly dated names:

- "Gilbert, 1943-": ULAN's Gilbert Proesch, born 1943, "dates conflict (1943- vs 1943-~2090)". The lifetime check
  read ULAN's window of possibility for a living artist (died no later than about 2090) as a death date, and 147
  years is more than a lifetime. Finding 52, fixed in `Lifespan.Compare`: a lifetime too long needs the record's
  earliest possible end against the latest possible begin; an outer bound alone never makes one. With LoC's
  "Gilbert, 1943-" agreeing too, the two vote.
- "Clarke, Geoffrey, 1924-", "Adams, Robert, 1917-1984": ULAN and LoC agree and join, and a third record of the
  same source stands beside them, dated but not in conflict (LoC's "Clarke, Geoffrey, -1950") or dated only by a
  window (ULAN's 1871~-~2030). Finding 3 vetoed a source's undated records once one of its records agreed; finding
  54, built, widens it to every other record of that source, by the same rule of uniqueness.
- "Thomas, Thomas, 1817-1888", "Chandra, Mohini, 1964-": LoC agrees; ULAN's one live record is undated, or dated
  only by a window, and finding 14 did not join it, because a second ULAN record vetoed by its dates still counted
  as a record of the name, or because a window counted as dates. Finding 55, fixed: finding 14 counts live exact
  records only, and reads a window of possibility as undated.
- "Fountains Hall (Yorkshire)", "Grimsthorpe Castle (Lincolnshire)" (keywords): LCSH's heading with the coarser
  "(England)" as the only candidate, "not placed". Finding 56, built: the qualifier's own place in TGN, when it is
  a county or above and TGN has one such place of the name, lends its ancestors ("Yorkshire" is in "England, United
  Kingdom, Europe, World"), so the coarser parenthetical agrees and finding 20 makes the heading exact; a town's
  name brings nothing, having too many namesakes.
- "Jope, E M (author)": LoC's "Jope, E. M. (Edward Martyn)" at 0.85: the fuller form dropped, the names differed by
  dots alone. Finding 57, fixed.
- "Wheatley, John, 1892-1955", "Morgan, John, 1823-1886" (keywords): ULAN agrees; LoC's exact records of the name are
  undated or two, and finding 14 did or did not join them; both read right on the rules as they stand after 55.
- "Portsmouth (England)": three TGN Portsmouths placed by "England", the city, its district and a hamlet; LoC's
  heading has no type word, and two holders are inhabited places. Prominence, or the city within its own district;
  not built.
- persauthorfull, the ten read again: the undated names ("Gay, John", "Ford, John", "Gray, John", "Jenkins, David")
  with one ULAN artist and several LoC namesakes, exactly as before. The desk's, or the Wikidata extract's.
- "Fine Art Society (London)", "Anglesey Abbey", "Hall, ?", "S. W. [(Simon Wilson)]": the desk's or the data's.

Measured 2026-10-22 (run-20261022-131239-sfny58: findings 52 to 57 and the hand merges of Italy and Kolkata, against
t8wzjm): accepted 8,435 -> 8,737 (vote 3,500 -> 3,682, single 1,731 -> 1,792, exact-single 3,204 -> 3,263), queued
4,828 -> 4,572, matched in store 5,826 -> 5,859. By context: keywords queued 1,244 -> 1,122 and vote 607 -> 670;
keywords:head 609 -> 553 and vote 2,093 -> 2,223 (the dates rules); persauthorfull 1,393 -> 1,325; place 334 ->
331; the rest within a few. The samples, read:

- "Raglan Castle (Wales)": LCSH's "Raglan Castle (Raglan, Wales)" "not placed (Wales vs Raglan, Wales)", which
  agrees on "Wales" as written. Finding 56's ancestors had been appended to the qualifier before the check, and
  "United Kingdom, Europe, World" is not in LCSH's parenthetical. Corrected the same day: the qualifier as written
  first, the ancestors only to widen agreement.
- "Cuddington, Buckinghamshire?", "[Warwick]" (place): the cataloguer's doubt inside the qualifier and the brackets
  of a supplied place were read as the name. Finding 59, fixed, with finding 36's trailing question mark.
- "Hunterian Art Gallery (Glasgow)": LoC's "Hunterian Art Gallery (University of Glasgow)" at 0.85 with no placement,
  since finding 30 took a body's placement only from a parenthetical with a comma. Finding 58, built: for a body, any
  LoC parenthetical that is not a type of body places it, so "Glasgow" is found in "University of Glasgow", and
  finding 20 makes the name exact.
- "Portsmouth (England)": TGN's city, its district and a hamlet in Calderdale all placed by "England", LoC's heading
  inside all three, two of them inhabited places. Finding 61, built: among inhabited places of the name, the one
  that gives its name to the division above it (the city's parent string begins "Portsmouth, England") is the known
  one; the vote then outvotes the hamlet and the district.
- "National Maritime Museum (London)": TGN's museum placed, LoC's heading placed through it, and ULAN's three records
  of the museum unplaced beside them; a body, so the outvoted-rivals policy stands aside and the string queues.
  Finding 60, a policy for the owner (decision 11): when the string's qualifier places the winning group, the tie is
  not a name-only tie, and bodies could be outvoted as places are. Ceiling 18; the owner's yes and built the same
  evening in `EvaluateAsync`. Building it showed that finding 58 had joined a heading's several parentheticals into
  one placement ("Great Britain; London, England"), which no parent string contains whole; each part is now a
  placement of its own in the placed-through check.
- "Peers" (keywords): AAT's "peers (aristocrats)" exact beside two TGN hamlets called Peers; "Dating", "Hall" and
  "Cross" are the same shape. Finding 63, a policy for the owner (decision 12): in the keywords family, an exact
  AAT or LCSH concept against TGN hamlets alone is the concept; a bare common noun in a subject field names the idea,
  and a place would carry its qualifier. Ceiling 20; the owner's yes and built the same evening in
  `Reconciler.Score`, for the one exact AAT or LCSH concept against TGN's inhabited places alone; a nation or a
  county of the name ("Turkey") is not set aside, nor is any record of another source.
- "Thomas, Thomas, 1817-1888" (keywords): LoC agrees, ULAN's second record is vetoed by its dates, and ULAN's first,
  undated, still does not join by finding 14 though finding 55 made it eligible. The one condition left is the
  class: if ULAN types that record as a body, the join rightly stops. The owner's query in the plan (decision 13)
  shows the two records' types: it is a Group, a firm. The join was right to stop, and finding 64, built that
  evening, vetoes a body of the name against a heading read as a person with a lifespan unless its own dates
  agree, as finding 31 does for places; the string is LoC's single.
- "Summers, S, op 1739": LoC's "Summers, S. (Steve)" and "(Samuel)", both exact through their fuller forms; the
  desk's, and "op 1739" is finding 26. "Hampton Court (Herefordshire)": TGN has no Hampton Court in Herefordshire,
  and the ancestors rule rightly placed neither the Jamaican nor the Richmond one; minted. "Christian church", "Hall,
  ?", "Anglesey Abbey", "Newcastle (Bridgend, Wales)", "Morgan, John": as before.
- Place: "Dublin", "Athens", "Winchester": prominence; "Norfolk?" now read as Norfolk, still a tie. "Evanston",
  "Bottrop", "Heaton", "Donnington": the desk's or the data's.

Measured 2026-10-22 (run-20261022-141247-hspbdg: findings 56 corrected, 58 to 61, 63, 64 and the placement parts,
against sfny58): accepted 8,737 -> 8,836 (vote 3,682 -> 3,766, single 1,792 -> 1,819), queued 4,572 -> 4,463. By
context: keywords queued 1,122 -> 1,074 and vote 670 -> 714; keywords:head 553 -> 515; place 331 -> 319 and vote
329 -> 344; keywords:sub 180 -> 171; publisher 863 -> 861; corpauthor 198; persauthorfull 1,325. The samples, read:

- "E & H Lumley" (corpauthor): LoC's "E. & H. Lumley (Firm)" exact by finding 39, and still queued, because the
  project's own provisional placeholder for the name stood above it at 1.00 and the exact-single rule looked at the
  placeholder. Finding 68, fixed: the rule reads the best group that is not a provisional placeholder of the
  project's own.
- "Remondini (Firm) (Printer)" (corpauthor), "Macmillan" (publisher, the run before): LoC's one record, exact, held
  by the one-word rule for people and bodies. Finding 42, built: a record plainly a firm by its own label (a type
  qualifier, "Remondini (Firm)", or a legal suffix, "Macmillan, Inc.") is a firm's name, and the rule stands aside.
- "MacDonald, Biddy, 1871-1952" (keywords): ULAN's one record, dated only by a window (1855~-~1968) that holds the
  string's dates, "not comparable", and the only candidate. Finding 65, fixed: a window of possibility is undated
  for finding 24, as it is for finding 14 since finding 55, so the string is an exact-single.
- "Hampton Court (Herefordshire)" (keywords): TGN's Jamaican and Richmond Hampton Courts, neither placed, neither
  vetoed, since the veto wanted a placed rival. Finding 66, built: a qualifier TGN knows as a county or above
  (finding 56's ancestors) names a real place, and a record placed elsewhere is vetoed whether or not another is
  placed; the string is minted as the Herefordshire house. A town's name ("Bridgend") resolves to nothing and
  vetoes nothing.
- Checking finding 66 showed that "Yorkshire" did not agree with "West Yorkshire", since the regions table makes the
  compound one word. Finding 67, fixed: a compound of a direction and the word holds the word; "York" is still not
  "New York".
- "Peers" (keywords): still queued though finding 63 was built for it, which means a fourth group the printer does
  not show, of another source. The owner's query in the plan (decision 14) prints the evidence.
- "Morgan, John, 1823-1886", "Summers, S, op 1739", "Messrs Royce" against "Royce, John, active 1773-1789": the
  working dates of finding 26, now the commonest shape among the dated names left; next builds, item 6.
- "City Museum and Art Gallery, Portsmouth" against LoC's "Portsmouth City Museum and Art Gallery (Portsmouth,
  England)" at 0.93: the same words in another order, which for a body may or may not be the same body ("Museum of
  London" is not the "London Museum"); the desk's. "Christie, Sharp and Harper": three ULAN records, one the firm
  and two through variants; the desk's. "Nationalmuseum", "Musée de l'Ermitage", "Waltham Forest: Libraries & Arts
  Department", "Mr Abbott", "Mr Hodgson", "Christian church", "De profundis": as before.
- keywords:sub: "Cornwall" (run order, finding 51), "Norfolk", "Yorkshire", "Sheffield", "Vienna", "Parma", "Camden",
  "Hampshire", "Cambridge": the famous-name ties, prominence; waiting for the Wikidata extract.

Measured 2026-10-22 (run-20261022-145615-gh3u6n: findings 42, 63 narrowed, 65 to 68, against hspbdg): accepted 8,836
-> 8,919 (exact-single 3,251 -> 3,344), queued 4,463 -> 4,294, minted 11,093 -> 11,169 (finding 66 clearing
namesakes). By context: keywords queued 1,074 -> 977; publisher 861 -> 823 and exact-single 419 -> 452 (the one-word
firms); corpauthor 198 -> 190; keywords:head 515 -> 502; place 319 -> 310; keywords:sub 171 -> 168; persauthorfull
1,324. The day's path: 5,009 -> 4,828 -> 4,572 -> 4,463 -> 4,294. The samples, read:

- "Newcastle (Bridgend, Wales)": finding 66 vetoed TGN's Canadian Newcastles, "Bridgend" being a county borough TGN
  knows, and LoC's "Captain Newcastle, approximately 1701-1756" stood alone through its variant. Finding 69, built:
  a string placed in a known place is a place, a building or a body there, not a person.
- "Hampton Court (Herefordshire)": TGN's two vetoed, and ULAN's "Hampton Court (Richmond upon Thames, London,
  England)" with the project's own entity of that label at 0.85 kept it queued, neither having a placement. Finding
  70, built: a ULAN label's own place parenthetical, and a store entity's, place it whenever the string has a
  qualifier; both are now elsewhere, and the house is minted.
- "Audley End (Essex)": LoC's "Audley End (England)" "not placed", because finding 56 found "Essex" as a county in
  England, Ontario and five American states and lent nothing. Finding 71, built: the project's manifest names its
  home place (`homePlace`, "United Kingdom" for PMC), and a county name TGN knows in several countries resolves to
  the one there. Needs `manifest --use-default` before the run.
- "Carwitham, John, c 1700- c1760": ULAN's one record dated by a window holding the dates, LoC's one record undated,
  neither tight, so neither finding 14 nor 16 joined them. Finding 72, built: a dated string that no record dates
  tightly, with two sources each holding exactly one live exact record of the name, neither at odds with the dates,
  is their vote, as finding 24 takes one such record alone.
- "Adams & Dart" (publisher): LoC's "Adams, Dart, 1975-" at 0.97, the person, through finding 34's bare equality
  ("adams dart" either way); the disambiguated-heading check held it. Finding 73, fixed: a name with an ampersand
  and one without are not the same name (0.93).
- "Christopher Cock": ULAN's "Cock, Christopher", the project's own aligned "Christopher Cock" and LoC's "Cock, Mr.
  (Christopher), active 1720-1749" at 0.93; the entity's ids are the clusters' (finding 32, the desk's). "Peter
  Lang": LoC's two records of the publisher. "Hutchinson": LoC's person, LoC's firm, ULAN's. "Murray", "König",
  "Sutton", "Mr Farebrother", "Mr Hoard": bare surnames, the desk's, the one-word rule holding as it should.
- "Morgan, John", "Summers, S, op 1739", "Messrs Royce": finding 26 (working dates), next. "Christie, Sharp and
  Harper", "Nationalmuseum", "Musée de l'Ermitage", "Waltham Forest", "City Museum and Art Gallery, Portsmouth",
  "Hall, ?", "Anglesey Abbey", "Ogmore Castle", "Christian church": as before, the desk's or the data's.

Built 2026-10-23, finding 26 (working dates), while the owner's run after gh3u6n was going. The dates parser already
read every form ("fl. 1775-1795", "active 1773-1789", "op 1739", "op 1946" as a floruit; "1730?" as a single
uncertain year) under both the default and the marc profiles; the reading dropped them, because only a birth-death or
formation-dissolution pair became a `Lifespan`, and the LoC loader returned no lifespan for "active 1640-1660" by
design ("active dates are not a lifespan"). Built:

- `Lifespan.IsWorking`: a latest begin and an earliest end with no outer edges, "born no later than the start, died no
  earlier than the end"; `Lifespan.Working(start, end, text)` makes one from the parser's spans. The store needs no
  change: the shape is the four columns as they are. `IsDated` stays false for a working span, so every rule that
  takes an undated record (findings 14, 24, 55, 65) treats a record with working dates as before, and the entity
  publishes the record's lifespan only when it is a lifespan.
- The reading: a floruit becomes a working span; a lone year or decade ("Goldsmith, Oliver, 1730?", a Single result
  no wider than a decade) becomes one too, since it says no more than that the person was alive then. Oliver
  Goldsmith was born about 1728 and the string's "1730?" was a birth year with the death dropped; "alive in 1730" is
  the weaker statement and does not contradict a record of 1728-1774.
- The LoC loader: "active 1640-1660" is a working span, "active 1635" one of a year, "fl." and "flourished" the
  same; the RWO's birth and death still win when LoC has them.
- `Compare`: the existing checks already make "Morgan, John, 1823-1886" against "Morgan, John, active 1635" a
  conflict (a begin after the other's earliest end). Two working spans add two rules: a hull longer than
  `Lifespan.MaxWorkingYears` (80, the assistant's judgement of the longest working life, not a sourced figure) is a
  conflict ("fl. 1775-1795" against "active 1840-1876", which the 120-year lifetime bound alone allows), and starts
  and ends each within a decade agree ("fl. 1775-1790" against "active 1773-1789"), as two birth years within a
  decade do; otherwise working spans are incomparable, with each other and with lifespans that hold them.
- The duplicate pass: two people with the same working dates are twins as two with the same lifespan are.

The LoC index holds its working dates only after a reload, or after the SQL in the plan's section 0, which writes
them from the `headingDates` the loader kept in each record's data; the owner runs it before the next reconcile.
Expected: persauthorfull's and keywords' dated namesakes ("Bolton, James", "Morgan, John", "Summers, S", "Messrs
Royce" against "Royce, John, active 1773-1789") resolve or veto by their dates; nothing accepted on a working span
alone, since it is not dated.

Measured 2026-10-23 (run-20261023-103032-ygjx75: findings 69 to 73 and the manifest's home place, against gh3u6n;
finding 26 not yet in the index): accepted 8,919 -> 8,962 (vote 3,756 -> 3,745, single 1,819 -> 1,875, exact-single
3,344 -> 3,342), queued 4,294 -> 4,193, minted 11,169 -> 11,242, matched in store 5,871 -> 5,856. By context:
keywords queued 977 -> 887 (the county-qualified houses and the dated names), keywords:head 502 -> 493, place 310 ->
302, corpauthor 190 -> 191, keywords:sub 168 -> 171, publisher 823 -> 825, persauthorfull 1,324. The path: 5,009 ->
4,828 -> 4,572 -> 4,463 -> 4,294 -> 4,193. The samples, read against what finding 26 will do:

- "Morgan, John, 1823-1886": ULAN agrees; LoC's "Morgan, John, active 1635" will be vetoed by its working dates; and
  LoC's "Morgan, John Lester" at 1.00, exact only through its variant "Morgan, John", would then have been LoC's one
  undated record of the name and joined ULAN (finding 14), which it is not: it is LoC's record of John Lester Morgan.
  Finding 74, built before the rerun: a record exact only through a variant label (`Candidate.ByVariant`; a LoC
  heading without its dates is still the heading, a body's heading without its suffix still the heading) is not its
  source's record of the name for the lone-record rules (findings 14, 24, 55, 65, 72). The same shape: ULAN's
  "Foster, Charles B." for "Foster, Charles, 1850-1931" (its dates agree, so it still counts), LoC's "Kirby, John C."
  for "Kirby, John". Morgan stays queued, a 1.00 rival; the desk's.
- "Dodd, Phyllis, op 1959": LoC's one "Dodd, Phyllis, 1899-1995", whose lifespan holds the working year; finding 24
  took only an undated lone record, and the disambiguated-heading check held an "undated" string off. Finding 75,
  built: a string with working dates against a source's one record of the name whose lifespan does not conflict is
  that record, and such a string is not undated for the disambiguated check. "Easton, Timothy, active 1972-1999"
  (ULAN's one, LoC's one, both undated) becomes finding 72's vote once the reading carries the dates; "Bolton,
  James, fl. 1775-1795" becomes ULAN's lone record once LoC's "active 1840-1876" and "1960-" are vetoed.
- "Hall, ?": read as "Hall," with the comma. Finding 76, fixed: the "?" and its comma go together. Still queued: three
  TGN Halls.
- "Ogmore Castle" (LCSH's "Ogmore Castle (Wales)" at 0.85, the only candidate) and "Anglesey Abbey" (ULAN's body,
  the project's own provisional group, LCSH's "Anglesey Abbey (Lode, England)" at 0.85): the string is bare and the
  heading is qualified by a place inside the project's home place. A rule would say: a heading whose only qualifier
  places it in the home place, its source's one record of the bare name, is exact on that name; "Hall (Germany)" and
  "Dublin (Calif.)" are elsewhere, and "Richmond (England)" beside "Richmond (London, England)" is two records. Not
  built; decision 16 in the plan, with the count query.
- "Summers, S, op 1739" (LoC's two fuller forms), "Kirby, John", "Hughs" (TGN's "Hughes" exact through a variant),
  "Fine Art Society (London)" (ULAN's three, none placed), "Piper, John, 1903-1992" and "Blake, Peter, 1932-" (the
  legacy twins "Piper, John" and "John Piper", both aligned; finding 32), "Art, Primitive" (AAT's concept exact
  beside two LCSH headings carrying it as a variant), "Paintings, European", "Christian church", "Galleria Spada (Rome,
  Italy)" (ULAN's and LoC's records, unlinked and unplaced), "Stafford House (London)" (LCSH's "Lancaster House", the
  later name, at 0.90): the desk's.
- Place: "Dublin", "Athens", "Winchester", "Norfolk?", "Pisa", "[Warwick]", "Donnington": the famous-name ties,
  prominence; "York (England)": city beside county; "Bottrop, Essen" and "Heaton, Bradford": TGN does not place
  them so; "Evanston, Illinois": the two store twins. All as before.

Decision 16's count, run by the owner against ygjx75 (2026-10-23): 43 queued keywords strings whose only live
candidate is a LoC or LCSH heading at 0.85 placed in the United Kingdom (lcsh 25 and loc 13 in keywords, 2 and 2 in
keywords:sub, loc 1 in keywords:head). Read, most are not the bare shape the rule was proposed for but qualified
strings whose county qualifier should have agreed through finding 56's ancestors and did not: "Arlington Court
(Devonshire)" against "(Arlington, Devon, England)", "Buckland Abbey (Devonshire)" against "(England)", "A-la-Ronde
(Exmouth, Devonshire)" against "(Devon, England)", "Ashburnham Place (Sussex)" and "Bignor Park (Sussex)" against
"(England)", "Callendar House (Stirlingshire)" against "(Falkirk, Scotland)", "Barrogill Castle (Caithness)" against
"Castle of Mey (Scotland)", "Broughton House (Dumfries & Galloway)" against "(Kirkcudbright, Scotland)". Finding 77,
built: the ancestors lookup scored the qualifier against TGN's preferred label only ("Devonshire" is TGN's variant of
"Devon"), read only the first segment ("Exmouth" is a town and brought nothing), and did not read "&" as "and". Now
every segment is tried until one is a county or above, variants count, and when the match is a variant the preferred
name leads the ancestors ("Devon, England, United Kingdom, Europe, World"), so a heading placed by the preferred name
agrees. "Sussex", "Caithness" and "Stirlingshire" are TGN's former counties and should resolve the same way; if
they still do not after the rerun, the owner's query on their TGN records (`select r.label, t.label as place_type,
r.data->>'parentString' from authority_records r left join authority_records t on t.uri = r.data->>'placeType' where
r.source = 'tgn' and r.normalised_labels && array['sussex', 'caithness', 'stirlingshire'];`) says why. The bare shape
is left: "Brotherhood of Ruralists" against LoC's "Brotherhood of Ruralists (Great Britain)", "Pwllmeyric (Wales)",
"South Staffordshire (England)", "Tees River Valley (England)", "West Country (England)", "Ogmore Castle (Wales)",
"Camberwell School of Arts and Crafts" against "Camberwell School of Arts & Crafts (Camberwell, London, England)": about
eight strings. The owner's decision (2026-10-23): the desk, not a rule, at that count.

Measured 2026-10-23 (run-20261023-115139-p9g534: finding 26 with the LoC index's 38,754 working spans, findings 74 to
76, against ygjx75; finding 77 not in it): accepted 8,962 -> 9,017 (vote 3,745 -> 3,803, single 1,875 -> 1,907,
exact-single 3,342 -> 3,307), queued 4,193 -> 4,109, minted 11,242 -> 11,295, matched in store 5,856 -> 5,832. By
context: persauthorfull queued 1,324 -> 1,306 (the first movement there in a week: the working dates), keywords 887 ->
843, publisher 825 -> 811, keywords:head 493 -> 487, corpauthor 191 -> 189, place 302, keywords:sub 171. The path:
4,828 -> 4,572 -> 4,463 -> 4,294 -> 4,193 -> 4,109. Exact-single fell by 35 while single and vote rose: finding 74
took lone records matched through variants out of the single-record rules, and some of those were right (below). The
samples, read:

- "Horlor, George W, op 1849-1890" (keywords): ULAN's one "Horlor, George William", 1819~-~1920, "dates not
  comparable" and queued, where finding 75 should have accepted it: the name matched through ULAN's variant "Horlor,
  George W.", and finding 74 counted that as a variant-only match. "Carwitham, John, c 1700- c1760": the same, LoC's
  "Carwitham, J. (John)" at 1.00 through its variant, so finding 72 no longer joined it to ULAN. Finding 78, built: a
  name that is the heading written shorter (initials against its words, `NameMatcher.Abbreviates`) is the heading,
  not another name; and the matcher expands a fuller form into the initials it follows ("Carwitham, J. (John)" is
  "Carwitham, John", 0.97), beside finding 57's dropping it. "Morgan, John Lester" and "Kirby, John C." stay variant-
  only, as finding 74 meant.
- "Galleria Spada (Rome, Italy)" (keywords): ULAN's one record and LoC's one, both bodies by their own vocabularies,
  unlinked and unplaced; finding 40 joins such a pair for a body context only, and keywords reads concepts first.
  Finding 78 also: in a context that allows bodies among other things, the two join when both records are bodies by
  their own class and no record of another class is exact beside them.
- "Morgan, John, 1823-1886": now three live groups (ULAN agreeing, LoC's "John Lester", LoC's "active 1851", not
  comparable; "active 1635" vetoed and off the printer). The desk's, as finding 74 intended.
- "S. W. [(Simon Wilson)] (editor)" (persauthorfull): read as "S. W.", the bracketed fuller form dropped, and three
  wrong exact records (ULAN's Monogrammist S. W. and "W., S.", LoC's John Sergeant, who signed S. W.). A parser gap: a
  bracketed parenthetical of two capitalised words after a name of initials is the name's fuller form. In the plan's
  parser gaps (section 7), for the Agent parser work; the string is queued, so the desk sees it.
- persauthorfull: "Gay, John", "Waters, William", "Bennett, Mary", "Ford, John", "Russell, James", "Gray, John",
  "Jenkins, David", "Little, Bryan": undated namesakes, two to four records each, the queue's residue as expected.
  "Robertson, G H" against LoC's one "Robertson, G. H. (George Harcourt), 1943-": held by the disambiguated-heading
  check, as finding 45 says it should be.
- keywords: "Hall, ?" now read as "Hall" (finding 76) and still three TGN Halls; "Anglesey Abbey", "Ogmore Castle"
  (decision 16, the desk); "Summers, S, op 1739" (two fuller forms); "Christian church"; "Stafford House (London)":
  as before.

Built 2026-10-26, findings 98 to 101, from the four pieces of evidence k22erz's samples asked for (below, "Read
2026-10-26") and the fifth they led to:

- Finding 98: a qualifier that resolves to a known place names that place, and a record whose whole chain holds the
  qualifier's word but not the place's ancestors is in a namesake elsewhere. "Marble Hill (Middlesex)" resolved
  "Middlesex" to England (the home place deciding among TGN's nineteen records of the name, finding 71), and then
  TGN's Marble Hill in Middlesex County, Massachusetts, agreed on the word alone, since the qualifier as written is
  read before its ancestors, which only widen (finding 56); the other American Marble Hills were vetoed as elsewhere.
  Now an agreement with a whole chain (TGN's parent string, ending in "World") is checked against the ancestors and
  vetoed when they are missing: "the qualifier names a known place (England, ...) and this Middlesex is a namesake
  elsewhere". A heading's parenthetical "(Middlesex)" is not a chain and agrees as before. TGN has no Marble Hill in
  Twickenham, so the string will mint, which is right. Expect the qualified houses placed in American namesake
  counties (New England has England's county names) to move single -> minted or queued; the method diff will count
  them.
- Finding 99: a name whose given part is a rank and its particle and nothing else ("Yarmouth, Earl of") is a title,
  which several men have borne, not a personal name, and is no more exact than one word (finding 42's ground).
  "Yarmouth, Earl of (author)" was an exact-single on ULAN's "Hertford, Francis Charles Seymour-Conway, 3rd Marquess
  of", which bears "Yarmouth, Earl of" as a variant. The parser marks such a name (ParsedAgent.TitleOnly; the rank
  stays in the name, so "Hertford, Marquess of" reads so and no longer as "Hertford, of"; a lone forename the data
  uses as one, "Smith, Dean", is a name), and the exact-single rule declines it; the single rule with dates and a
  vote still carry it. A lone title in a comma segment of its own ("Byron, Lord") comes off as before and leaves one
  word, finding 42's. Expect "Yarmouth, Earl of (author)" exact-single -> queued, the peerage headings finding 94
  brought to the desk staying there.
- Finding 100: the second pass goes round again while it settles something. The two "Gibson, Jean, 1935-" strings:
  keywords stayed queued between LoC's agreeing heading (nb2010009060, 1935-1991; ULAN's linked 500001913, 1927-1991,
  set aside by finding 96) and ULAN's undated 500375601 ("1800~-~2100", not comparable); keywords:head was accepted
  single in the second pass, because by its turn the pass had accepted another string onto the project's gjq63j55
  ("Gibson, Jean", 1930-) with that undated ULAN record and LoC's undated nb2010009063 as its equivalents, and the
  namesake's group went down with the entity's dates conflict. The keywords string had been reconsidered before that
  acceptance, and one round left it where the order of the pass found it. Each further round reconsiders only the
  strings still queued, stops when a round settles nothing, three rounds at most; the report says "over N rounds" and
  the progress line reports each round. Expect some tens settled by the further rounds; the second pass took 161 s in
  k22erz, so each further round adds about that. (The keywords Gibson string itself will not be among them, finding 101.)
- Finding 101: the string's own placeholder dates no record for it. The decisions onto gjq63j55 (the owner's query,
  the same day) showed how "Gibson, Jean, 1930-" in keywords:head was accepted in k22erz's second pass: queued in the
  first pass between ULAN's undated "Gibson, Jean" (500375601, a window of 1800~-~2100) and LoC's undated one
  (nb2010009063), with LoC's dated namesakes vetoed; in the second pass its own placeholder came back as a store
  candidate agreeing on the dates, and the rule of findings 14 and 16 took it as the record that dates a source's one
  undated record of the name, joining both to it ("ulan's one record of the name, which store dates"; "loc's one ...");
  two sources in one group, a vote, onto a placeholder that carries nothing. Finding 95 closed this hole in the single
  rule; the same ground closes it in the three date rules that read an agreeing record (finding 54's namesake veto,
  findings 14/16/79's join, finding 72's two lone records): a placeholder awaiting review agrees with nothing. The
  string returns to the desk, and so does the keywords "Gibson, Jean, 1935-" question: with gjq63j55 no longer firm,
  its undated ULAN rival stands again for both "1935-" strings, which is right (ULAN's 500375601 may be the sculptor
  or a namesake; the desk's). Expect "Gibson, Jean, 1930-" vote -> queued, the keywords:head "Gibson, Jean, 1935-"
  single -> queued, and some tens of second-pass votes onto placeholders with them (the diff will count "which store
  dates" reversals as vote -> queued).

Read 2026-10-26, the evidence of k22erz's four samples (the owner's three queries, run that morning):

- "Orovida, 1877-1970" -> ULAN's "Knight, Laura" (500017204) is what the data says: the record's labels include
  "Orovida" (beside "Knight, Dame Laura", "Johnson, Laura", "Knight-Johnson, Laura" and the rest) and its lifespan is
  1877-1970, the string's. The string carries Laura Knight's dates under a name ULAN lists as hers; whether ULAN is
  right to (Orovida Pissarro, 1893-1968, is 500008117, and was vetoed on the dates) is Getty's question, not the
  toolkit's, and a candidate for the upstream list if the owner wants to raise it. Nothing to build.
- "Marble Hill (Middlesex)": nineteen TGN records bear the name; the English Middlesex (7012288, under England) is
  typed aat 300387179 where the American counties are 300000771. The resolution reached England all the same (the
  Indiana, Missouri and Tennessee records were vetoed "the qualifier names a known place and this is elsewhere", and
  only the home place can have chosen one chain among nineteen), and the Massachusetts record agreed on its word.
  Finding 98.
- "Yarmouth, Earl of (author)": ULAN 500436285 at name 1.00 through the variant, nothing else. Finding 99.
- "Gibson, Jean, 1935-": the keywords decision has ULAN 500001913 set aside and LoC nb2010009060 agreeing (finding
  96 at work), ULAN 500375601 the live rival, ULAN 500783658 (1957-), LoC n88111181 (active 1755) and LoC nb2010009063
  (finding 75's namesake) vetoed; the head string's two decisions, queued then single, the second with 500375601 in
  one group with the project's gjq63j55 (1930-, conflicting) and LoC nb2010009063, and the single onto unrec_b9u3ugps,
  the keywords string's placeholder, LoC's heading joined to it. Finding 100 for the order; and the decisions onto
  gjq63j55, read the same day, showed the acceptance itself was a hole: finding 101.

Built 2026-10-26, finding 97, from k22erz's single sample: an approximate or uncertain year in a lifespan is widened by
four years each way on its own side ("c.1716-1799" the birth, "1859-c.1927" the death, "c 1816- c 1875" both), as the
lone year has been since finding 82; the dates parser flags the whole result, so the side is read from the text
(StringReading.ApproximateSides). "Cavaceppi, Bartolomeo, c.1716-1799" had LoC's "Cavaceppi, Bartolomeo, 1717-1799"
vetoed as a conflict and was a single on ULAN's record where it should have been a vote. Takes effect at the next run.

Measured 2026-10-26 (run-20261026-090659-k22erz: findings 93 to 96 and the Wikidata subclass walk, against wy2aqs):
accepted 9,822 -> 9,426 (vote 4,020 -> 4,024, single 2,504 -> 2,101, exact-single 3,298 -> 3,301), queued 3,567 ->
3,989, matched in store 5,957 -> 5,956, minted 10,907 -> 10,882; the second pass reconsidered 4,121 (4,163) and
settled 132 (596). By context queued: persauthorfull 1,282, keywords 947, publisher 783, keywords:head 452, place 216,
corpauthor 183, keywords:sub 126. Time by stage: store 939 s, second pass 161 s, commit 150 s, source:loc 118 s, ulan
103 s, tgn 84 s, lcsh 84 s, aat 79 s, read 22 s; twenty-nine minutes. The method diff against wy2aqs: single -> queued
494, queued -> single 79, minted -> single 12, minted -> queued 10, queued -> exact-name 4, exact-name -> queued 3,
minted -> vote 2, minted -> exact-single 2, exact-name -> single 2, single -> vote 2, queued -> exact-single 1, queued
-> minted 1. Read:

- Finding 95 took back 494 placeholder self-acceptances, not the 295 expected: the hole was wider than the dated
  persons, since a placeholder minted with the string's qualifier agreed with it the same way ("Arundel Castle
  (Sussex)", "Apethorpe Hall (Northamptonshire)", "Abby Aldrich Rockefeller Folk Art Center (Williamsburg, Virginia,
  USA)"), and bp8unt's 96 houses "reached" by finding 90 were mostly this: queued by finding 90's narrowing and then
  accepted onto themselves. Every queue count since the second pass was built (finding 51, 2026-10-23) understated by
  up to some 230 of these older ones; 3,989 is the first honest count since then, and the path reads 3,567 -> 3,989
  with that said. The keywords queued sample shows the rule at work: "Anglesey Abbey" with its placeholder marked "a
  placeholder awaiting review: carries nothing", "Newcastle (Bridgend, Wales)" back on the desk.
- Finding 96 brought 79 queued strings to single ("Agar, Eileen, 1899-1991", "Angus, Peggy, 1904-1993", "Arbuthnot,
  Malcolm, 1874-1967", "Baldinucci, Filippo, 1625-1696"), a share of the 12 minted -> single (Jan van Eyck 1390-1440,
  William Kent 1685-1748, John Frederick Lewis), the 2 minted -> vote (Mary Cameron, Sir George Reid), and the 4 queued
  -> exact-name (Bellotto, Kelly, Mednikoff, Rice: the twin string's entity, now made, answering).
- Finding 94: "Keeling, William Knight, 1807-1886" minted -> single, read whole and found; "Browne, Hablot Knight
  [Phiz], 1815-1882" single -> vote; "Mandle, Earl Roger" queued -> exact-single; "Arnaud, Dean Leopold" queued ->
  minted (nothing of the whole name; right by the name). And an effect not foreseen: "Earl" kept at the head of the
  given names makes the peerage headings "Jersey, Earl of (author)", "Rosse, Earl of", "Shaftesbury, Earl of",
  "Crawford and Balcarres, Earl of" read as names, so they went minted -> queued with candidates (the desk's, better
  than mints) and "Yarmouth, Earl of (author)" minted -> exact-single, which wants its evidence: a title with no
  personal name may not be exact enough to carry a record alone (finding 42's ground).
- The walk's retyping shows nowhere in the diff that can be told apart from findings 93 to 96 (vote +4).
- The keywords single sample, three to check: "Marble Hill (Middlesex)" is placed in TGN's Marble Hill in Middlesex
  County, Massachusetts, the two other American Marble Hills vetoed as elsewhere, when the house is in Twickenham
  (the qualifier "Middlesex" resolved to the American county, or to several with the American one agreeing; the TGN
  records of the name and the reading's ancestors wanted); "Orovida, 1877-1970" accepted single to ULAN's "Knight,
  Laura" at name 1.00 with dates agreeing (1877-1970 are Laura Knight's; how a one-word name matched that record is
  the record's labels to show; finding 42 kept one-word names from exact-single, not from the single rule);
  "Cavaceppi, Bartolomeo, c.1716-1799" a single where a vote was due (finding 97, above). The rest right: Fountains
  Hall, Leeds Castle, Grimsthorpe Castle, Houghton Hall on LCSH's placed headings; Williams, Peter and Thomas, Thomas
  on LoC's dated headings against ULAN's windows; Lismore Castle in Waterford. Also to read: "Gibson, Jean, 1935-" in
  keywords:head was accepted single in the second pass onto unrec_b9u3ugps, the keywords string's placeholder, which is
  still queued; the ground was the authority record joined to it, printed as the placeholder, and the entity chosen is
  the placeholder: whether that is the right entity is for the evidence.
- keywords queued otherwise the desk's: "Hall, ?" three TGN Halls with no sitelinks; "Wheatley, John, 1892-1955" ULAN
  agreeing beside LoC's undated namesakes; "Nibes, Richard Henry, 1954-" against ULAN's one Richard Henry Nibbs
  (1806-1893), the catalogue's spelling and dates both the desk's; "Harcourt, George, 1868-1947" two one-record
  conflicts (ULAN's 1869-1923, LoC's "Horsford, George" through a variant).

Built 2026-10-24 night, finding 96, from the evidence of two of the ninety strings finding 93 left queued in wy2aqs
("Agar, Eileen, 1899-1991", "Kelly, Felix, 1917-1994"; the owner's policy, asked and given the same night): a linked
cluster that disagrees with itself about the dates stands when one of its records agrees with the string's dates (the
whole heading as written agrees by its own; an undated linked record vouches for nothing, so finding 44's undated LoC
record still goes down with ULAN's conflicting one). Agar: LoC's heading is the string verbatim and agrees, ULAN's linked record says
1904-1991, and finding 44's linked veto had taken the group down with it. Kelly: ULAN agrees, LoC's linked record
carries finding 88's bare 1916. The link is the authorities' own claim that these are one person and the conflict their
disagreement among themselves, so the conflicting record is set aside rather than vetoed (set aside, it is not live and
does not poison the group), the agreeing record carries the string as a single, and both records join the entity. A
record vetoed for its class as well stays vetoed; a cluster none of whose records agrees stays vetoed and is finding
93's. With it, CarriedBy's ground is the best live member that is not a placeholder awaiting review (finding 95), since
the placeholder a string's dates had joined to ULAN's record (the "dated as store's record is" join) stood first in the
group. Finding 75's namesake rule leaves set-aside members alone. The test is LinkedClusterTests. Expect about ninety
of the next run's queued back among the singles.

Built 2026-10-24 night, finding 95, from wy2aqs's second pass (below): the placeholder of a string awaiting review
carries nothing. A queued string's placeholder is minted with the string's own lifespan and qualifier (MintAsync), so
when the second pass evaluated the string again the placeholder came back as a store candidate whose dates agreed, by
construction, and the single-source rule (CarriedBy) accepted the string onto it: "Cozens, John Robert, 1752-1798 ->
accepted single store Cozens, John Robert", 265 strings in wy2aqs, finding 93's seventeen among them ("Hadid, Zaha,
1950-2006" onto unrec_nr4bazcp again, "Papworth, John, 1775-1847", "Broderick, Laurence, 1953-", "Rice, Anne Estelle"),
and about thirty in every run since the second pass was built (bp8unt's "Phillips, Claude [1848-1924]", "Kindersley,
David [1903-1987.]", the Liverpool Biennial editions onto "Liverpool Biennial of Contemporary Art"). The store stage had
already refused such a placeholder as a silent answer (ProvisionalQueued, finding 51); now CarriedBy refuses it as a
ground too, and the candidate's reason says "a placeholder awaiting review: carries nothing". The test is
SecondPassTests "A queued string's own placeholder never settles it"; the test store's "Dürer, Albrecht, 1800-1870" had
not shown the hole because the project's own Dürer stood in its way. Expect the next run's queue near 3,830: the 265
back on the desk and the older thirty with them, single near 2,239 again.

Measured 2026-10-24 night (run-20261024-161624-wy2aqs: finding 93 and the vacuum, against bp8unt): accepted 9,556 ->
9,822 (vote 4,020 -> 4,020, single 2,239 -> 2,504, exact-single 3,297 -> 3,298), queued 3,477 -> 3,567, matched in store
5,992 -> 5,957, minted 11,228 -> 10,907; the second pass reconsidered 4,163 (3,839) and settled 596 (362). By context
queued: persauthorfull 1,265, keywords 621, publisher 782, keywords:head 386, place 215, corpauthor 174, keywords:sub
124. Time by stage: store 975 s, second pass 176 s, commit 158 s, source:ulan 105 s, source:loc 97 s (6,741 s in bp8unt:
the vacuum was the remedy, as the EXPLAIN had said), lcsh 85 s, tgn 84 s, aat 83 s, read 22 s; the whole run about
thirty minutes against bp8unt's hours. The method diff against bp8unt (each run's last decision per string): minted ->
single 233, minted -> queued 87, exact-name -> single 32, exact-name -> queued 3, and "Duranty, Charles" (two strings)
swapping exact-name and exact-single with minted. Read:

- Finding 93 reached some 355 strings, not seventeen: every earlier mint on a dates conflict with a source's one exact
  record of the name, which the diff had never shown because those strings were minted in every run. Most are the
  catalogue's own dates against the authority's: "Cozens, John Robert, 1752-1798" against 1752-1797, "Emerson, Peter
  Henry, 1956-1936" and "Purvis, Tom, 1988-1959" (typed years), "Chippendale, Thomas, Jr., 1718-1779" (the father's
  dates), "Fullard, George, 1925-1973" against 1923-1974. Each had been a silent second entity under the string's legacy
  identifier; finding 93's policy is that they are the desk's, which fixes the date and joins the record.
- But the second pass took 265 of them straight back off the desk, onto their own placeholders, as singles at 1.00
  (minted -> single 233, exact-name -> single 32): finding 95, built, above. "Hadid, Zaha, 1950-2006" went that way in
  keywords and the head string followed it: the second Zaha Hadid stood at the end of this run as it had at the end of
  bp8unt, by another route. Ninety stayed queued (minted -> queued 87, exact-name -> queued 3: "Agar, Eileen,
  1899-1991", "Angus, Peggy, 1904-1993", "Bellotto, Bernardo (Michiel), 1721-1780", "Kelly, Felix, 1917-1994"); why their
  placeholders did not carry them is not read (two of their evidence rows are under plan section 0).
- The samples: keywords queued is the desk's, "Hall, ?" three TGN Halls with no sitelinks, "Stafford House (London)"
  against LCSH's "Lancaster House (London, England)" at 0.90 through the variant (the same house under its old name,
  the desk's to accept), "Kasmin Limited (London)" against LoC's "Kasmin Limited" with "Kasmin" and John Kasmin beside
  it, "Wheatley, John, 1892-1955" ULAN agreeing beside LoC's undated "Wheatley, John" (another source's undated record is
  not finding 75's namesake; left as it is). persauthorfull queued is bare names among dated namesakes ("Gay, John",
  "Ford, John", "Jenkins, David" three LoC headings) and "Robertson, G H" against LoC's one dated heading (finding 45).
  Nothing to build from the samples.
- Finding 94 is not in this run (built while it ran); "Keeling, William Knight, 1807-1886" reads whole from the next.

Built 2026-10-24 night, finding 94, from the evidence below: off the end of the given names in an inverted heading only
a post-nominal comes ("Smith, John Esq", "Reynolds, Joshua RA"; the table's new `postnominals` list: Bt, Bart, Baronet,
Esq, Esquire, Kt, KBE, CBE, OBE, MBE, RA, ARA, FRS, FSA, MP, QC, KC). The parser had taken any word of the honorifics
table off the end, and "Knight" is in it for "Smith, John, Knight", so "Keeling, William Knight, 1807-1886" read as
"Keeling, William" with the honorific Knight and, in rkjzag, was accepted exact-single to LoC's one "Keeling, William"
(n82073934, the sea captain, whom finding 88 then dated 1578-1620 and whose conflict minted the string). A title that
precedes a name (Dean, Earl, Knight, Major, King) is a forename or a middle name when it stands last among the given
names; as a comma segment of its own it is still read. The leading position, counted the same night over the PMC strings
(the owner's query, every string whose first given name or first direct-order word is a title the table knows that is
also a forename): nine inverted personal names, seven of them titles ("Cecil, Lord David", "Kerr, Lord John", "Bellenger,
Dom Aidan", "Houédard, Dom Sylvester", "Barnes, Major R M", "Singh, Prince Frederick Duleep"), two of them forenames,
"Arnaud, Dean Leopold" and "Mandle, Earl Roger"; the direct-order rows all bodies and places ("Dean Clough Galleries",
"Duke University Press", "Bishop Auckland", "Prince Edward Island"), which the corporate guard and the unparsed contexts
already keep whole. No shape separates "Earl Roger" from "Dom Aidan", so the table says it: a `forenamesToo` list (Dean,
Earl) that is not taken off the front of the given names in an inverted heading, still a title in direct order and as a
comma segment. The owner's caution stands in the table's note: this is the PMC data's usage, and another dataset may
use these words otherwise. Takes effect at the run after the one running when it was built.

Read 2026-10-24 night, the evidence of five of bp8unt's decisions (plan section 0 item (3)): finding 91 as built, three
of finding 88's seventeen as finding 93 expects, and one thing finding 93 does not reach:

- "Lincoln (England)" in keywords:head is finding 91's vote: TGN's Lincoln with LoC's "Lincoln (England)" and the two
  diocese headings carry "holds the whole heading as written (Lincoln (England)): breaks the tie", Lincolnshire's group
  "the whole heading as written is Lincoln (England), elsewhere", Missouri's Lincoln vetoed by the resolved qualifier.
- "Hadid, Zaha, 1950-2006" in keywords was minted over the project's own entity (akku6exr, "dates conflict (1950-2006 vs
  1950-2016)", its lifespan from its LoC equivalent, dated by finding 88), ULAN's and LoC's Zaha Hadid the same, "Zaha
  Hadid Limited" a body. Then the same string in keywords:head went exact-name, "dates agree", to unrec_nr4bazcp "Hadid,
  Zaha": the entity the keywords mint had just made under the string's legacy identifier, carrying the catalogue's wrong
  death year. The second Zaha Hadid, with a string already joined to it. Finding 93 queues the keywords string (the
  project's own entity of the name, conflicting), and with no mint the head string meets the same entity and the same
  rule.
- "Papworth, John, 1775-1847" in keywords:head: LoC's one "Papworth, John" (n84070333) is dated 1921-2020 by finding 88,
  which is John Papworth the activist and priest (my recollection of who the dates fit; LoC's heading does not say).
  rkjzag's exact-single acceptance to that record was wrong; the architect John Buonarotti Papworth's heading, if LoC
  has it, is not of this name as written. Finding 93 queues it (the source's one exact record, conflicting), the desk's.
- "Keeling, William Knight, 1807-1886" in keywords: read as "Keeling, William" (finding 94 above); LoC's two "Keeling,
  William" are 1578-1620 and 1964-, both conflicting, so the string mints as finding 93 means several conflicting records
  to. rkjzag's acceptance to the sea captain was wrong, so this mint is finding 88 correcting a wrong turn, and the next
  run reads the whole name.
- "Charlton, Alan, 1948" in keywords:head, a vote in rkjzag, queued: two live groups at 1.00 with dates agreeing, ULAN's
  Alan Charlton with LoC's "Charlton, Alan, 1948-" (n87803436), and LoC's undated heading "Charlton, Alan" (nb2007025681)
  alone, which finding 88 dated from its Wikidata item to agree with 1948. Before the write that heading fell to finding
  75's veto, as "Charlton, Alan (Local historian)" still does ("loc's dated record of the name agrees with the string's
  dates: this undated one is a namesake"); the 1952- and 1970-2018 headings are vetoed on their dates. The outvoted-rivals
  rule never runs for people, so the one-source group holds the string for the desk. Either Wikidata's item for the
  painter points at the wrong LoC record, or LoC holds two records for him: either way the desk should see it, and the
  string is rightly queued. Noted, not built: finding 75's veto could still fall on a record whose only dates are
  finding 88's when a sibling record of the same source agrees on its heading's own dates; three strings in bp8unt's
  diff (Charlton, Clatworthy, Richards) went vote -> queued this way, too few to build on tonight.

Built 2026-10-24 night, finding 93, from bp8unt's method diff (below): a namesake vetoed on a dates conflict is the
desk's, not a mint, when it is the project's own entity of the name or its source's one exact record of the name, both
lifespans proper; several exact records of a source all conflicting are other people and the string mints as before,
and so do working dates against a lifespan, the project's own entity included ("Dodd, Phyllis, fl. 2001-2005" after the
death of the one Phyllis Dodd is another person: finding 75's policy stands). The reasons say which ("the project's own
entity of the name, its dates conflicting: the desk's, not a second entity"; "loc's one record of the name, its dates
conflicting: the desk's, not a mint"). Finding 88 brought the cases to light: the catalogue's "Hadid, Zaha, 1950-2006"
against the project's own Zaha Hadid, whose LoC record now says 1950-2016, had minted a second entity for her. The
store test's "Dürer, Albrecht, 1800-1870" against the project's Dürer, minted since that test was written, is queued now
for the same reason. Expect bp8unt's seventeen mints back as queued strings and a few older mints with them (a rare
name's one dated record conflicting), counted in the diff.

Measured 2026-10-24 night (run-20261024-130904-bp8unt: findings 88 to 92, against rkjzag): accepted 9,447 -> 9,556
(vote 4,008 -> 4,020, single 2,124 -> 2,239, exact-single 3,315 -> 3,297), queued 3,488 -> 3,477, matched in store
5,996 -> 5,992, minted 11,322 -> 11,228; the second pass reconsidered 3,839 (3,757) and settled 362 (269): the first
pass left 82 more queued and the second settled 93 more. By context queued: persauthorfull 1,265 (1,264), keywords 584
(590), publisher 782, keywords:head 333 (342), place 215 (208), corpauthor 174, keywords:sub 124 (128). The path: ...
-> 3,563 -> 3,488 -> 3,477. The samples and the method diff, read:

- Finding 90 reached further than the 24: minted -> single 96, the county-qualified houses whose LCSH heading names the
  town ("Hever Castle (Kent)", "Kelmscott Manor (Oxfordshire)", "Auckland Castle (Durham)", "Arundel Castle (Sussex)"),
  so finding 66's veto had been minting them since each county began to resolve, not only since Sussex did; minted ->
  queued 9 ("Enfield, Middlesex", "Ulverston, Lancashire", "Tattershall Castle (England)") and minted -> vote 5
  ("Waddesdon Manor (Buckinghamshire)", "St. George's Hall (Liverpool)"). Minted 11,322 -> 11,228.
- Finding 91 at work: queued -> vote 20, "Bedford (England)", "Chester (England)", "Agrigento (Italy)", "Bebington,
  Wirral", "Freemasons (Bath, England)"; and the bare names then join the entities those votes made, in the second pass
  (queued -> exact-name 7: "Agrigento", "Chester", "Parma", "St Ives (Cornwall)"). "Lincoln (England)" is beyond the
  diff's eight examples; its line is read with the next queries (plan section 0).
- Finding 89: "Orkney" in keywords:sub left the legacy "Orkney (Sask.)" and the second pass matched it to the project's
  "Orkney (Scotland)" (place/aqzss6fj), "Gloucester" and "Hove" the same way (vote -> exact-name 3); vote -> queued 8
  holds "Dedham", "Somerset", "Wickford" and "Gloucester" in another context, the desk's by finding 89, and three dated
  persons ("Charlton, Alan, 1948", "Clatworthy, Robert, 1928-", "Richards, Albert, 1919-1945") that are finding 88's: a
  vote whose LoC member is now dated and no longer agrees, to be read in their evidence.
- Finding 92 says what it was built to say: "Sheffield", "Cambridge", "Durham" and "Williamsburg" carry "prominence
  stands aside: the project's own entity Sheffield is among the live groups, the desk's". "Washington (D.C.)" is
  queued between the project's two "Washington" entities (the duplicates, a desk item). "Camden", "Quebec" and
  "Trent)" declined by prominence as before.
- Finding 88, both ways. For it: exact-name -> vote 10 ("Bevan, Tony, 1951-", "Bowyer, William, 1926-", "Green,
  Anthony, 1939-", "Reynolds, Alan, 1926-2014"), LoC's newly dated record agreeing and joining the vote. Against it,
  the wrong turn finding 93 corrects (above): seventeen accepted strings minted on conflicts the new dates raised,
  single -> minted 8 ("Biegel, Peter, 1913-1987", "Broderick, Laurence, 1953-", "Keeling, William Knight, 1807-1886"),
  exact-name -> minted 5 ("Hadid, Zaha, 1950-2006", "Kindersley, David [1903-1987.] (author)", "Hilton, Matthew,
  1948-"), exact-single -> minted 3 ("Carstairs, John Paddy, 1916-1970", "Papworth, John, 1775-1847"), vote -> minted 1
  ("Rice, Anne Estelle, 1879-1959"). By the assistant's recollection, to be read against the evidence: Zaha Hadid died
  in 2016, David Kindersley lived 1915-1995, Laurence Broderick was born in 1935 and Anne Estelle Rice in 1877, so
  there the catalogue's dates are the ones wrong and the project's own entities were being duplicated over them;
  Keeling's 1807-1886 and Papworth's 1775-1847 are right, so there the record's new dates, or the Wikidata link that
  brought them, are wrong (LoC has several John Papworths, and the undated one may be linked to another man's item).
  Also vote -> single 15 ("Dodd, Daniel, op 1760-1791", "Eshun, Kodwo, 1966-", "Hay, Jane Benham, 1829-c.1862"): the
  LoC member's new dates conflict and ULAN's record stands alone with the dates agreeing, an acceptance still, the
  conflict in the reasons.
- Time by stage: source:loc 124 s -> 6,741 s, the whole of the slowdown (store 1,033 -> 728 s; the second pass 194 ->
  508 s, 3,839 strings each going through the LoC lookup again; read 176 -> 333 s; score, where finding 90's walk
  sits, 9.9 -> 8.7 s). The only change on LoC's side between the runs is finding 88's write of 354,991 rows to
  `authority_records`, and the store's own loader knows the effect (PostgresAlignmentStore, the vacuum after a load:
  reloading ULAN's 404k rows had made each lookup twenty times slower until a vacuum folded the GIN index's pending
  entries in; every load of five thousand records or more has vacuumed since). The SQL write was staged without that
  step. Run 2026-10-25 with the lookup timed before and after: 2,901,626 dead tuples (15 per cent, under autovacuum's
  trigger); before, the bitmap index scan on `authority_records_labels_loc` read 423 pages for one key in 11.9 ms, the
  pending list's signature; after, 5 pages in 0.11 ms, the lookup 13.9 ms -> 1.6 ms. Every staged mass write to the
  table ends with the vacuum from now on, as the loaders' do.

Corrected 2026-10-24 evening, finding 90's cost, and corrected again at night: the evening's diagnosis was wrong. The
first run carrying the walk ran visibly slower and the walk was blamed; bp8unt's timing line (above) puts the walk
under ten seconds in the `score` bucket in both runs and the slowdown in LoC's index after finding 88's write. The
evening's narrowing stands as harmless: a placement that agrees with the qualifier by any of Score's three readings can
never be vetoed, and `Elsewhere` reads only the candidate's own segments, so agreeing placements are skipped before the
walk; the set of known places a disagreeing candidate is judged by is unchanged.

Built 2026-10-24 late afternoon, findings 91 and 92, from the two pieces of evidence rkjzag's measurement had asked for
(the stored evidence of its "Sheffield" and "Lincoln (England)" decisions):

- "Lincoln (England)" in keywords:head was queued on a tie of two two-source groups at 1.00: TGN's Lincoln, placed by
  the qualifier, with LoC's "Lincoln (England)" through it and two diocese headings; and TGN's Lincolnshire, exact
  through its variant "Lincoln", with LoC's "Lincolnshire (England)" through it (Missouri's Lincoln vetoed by the
  resolved qualifier, finding 66). Nothing on a qualified string separated them: prominence is for bare names, finding
  35 wants a project entity, and the outvoted rule wants a one-source rival. The string is LoC's heading as written.
  Finding 91: among two-source place groups tied at the top score on a qualified string, when exactly one holds a
  record whose label is the whole heading as written (0.97 against the raw string), its qualifier agreeing, that group
  wins as a vote ("holds the whole heading as written (Lincoln (England)): breaks the tie"); the others carry "the
  whole heading as written is ..., elsewhere". Not when any live group holds a project entity (finding 35's ground).
  t629cj's vote on this string had come through the widened store entity, since unplaced (the lone-nation rule); this
  is the rule that should have carried it.
- "Sheffield" in keywords:sub was queued with no word of why. Its evidence showed five two-source groups at 1.00, all
  American (Illinois, Massachusetts, Ohio, Pennsylvania, Texas), and nothing English: the evidence takes the first five
  groups, and the English group, like whatever stopped prominence, lay beyond them. By the code, prominence stands aside
  only when a live group at acceptance score holds a firm project entity outside the tie (in a tied group, finding 35
  would have taken it), so the project has an aligned "Sheffield" entity in a group one source carries, or two of them;
  the owner's query confirmed it the same hour: two aligned Place entities both labelled "Sheffield", one carrying
  Wikidata's Q42448 (the English city) with a VIAF id, the other another VIAF id, neither with a TGN or LoC equivalent,
  so each is a lone store group and the store stage declines to choose between two firm entities. Duplicates for
  merge-duplicates or the desk, as "Washington" is; and the crosswalk use of the index (plan section 0, item 3 (c))
  would join the first to the English TGN and LoC group through Q42448's ids. Finding 92, two changes to the evidence rather than a rule: the tied groups
  carry "prominence stands aside: the project's own entity <label> is among the live groups, the desk's" when that is
  what happened; and the stored evidence lists the first five groups, every live group at the top score and every
  group holding a project entity, twelve at most, so that a tie of six namesakes shows all six and the entity that
  decided. The next run's "Sheffield" line will say which entity; the desk, or merge-duplicates, takes it from there.

Measured 2026-10-24 afternoon (run-20261024-111420-rkjzag: findings 85, 86, 87 and the corrections to 83 and 84,
against t629cj): accepted 9,370 -> 9,447 (vote 3,936 -> 4,008, single 2,119 -> 2,124, exact-single 3,315), queued
3,563 -> 3,488, matched in store 6,003 -> 5,996, minted 11,317 -> 11,322; the second pass reconsidered 3,757 (3,869)
and settled 269 (306), so the first pass took 112 more than before. By context queued: persauthorfull 1,264, keywords
590, publisher 782, keywords:head 342, place 208 (258), corpauthor 174, keywords:sub 128 (152). The path: ... -> 3,603
-> 3,563 -> 3,488. The samples and the method diff, read:

- Prominence (85) works as meant where it runs: "Cornwall" in keywords:sub is a vote with the Ontario, Connecticut and
  Pennsylvania groups marked "less prominent: 46 sitelinks against 126"; the diff's queued -> vote 68 is mostly places,
  "Berlin", "Barcelona", "Antrim", "Bradford", "Braunschweig" among them. Declined as meant: "Parma" (124, 80, 50, 40:
  Italy's against Ohio's, under the ratio), "Camden" (62, 58, ...), "Quebec" (202 against 149, the province and the
  city), "Trent)" (116, 90, a fragment anyway). Not run at all on "Sheffield", "Cambridge", "Durham" and
  "Williamsburg", whose queued lines carry no prominence reason: a guard held (a firm project entity in some live
  group, or no two-source group at the top score), to be read from their evidence (plan section 0).
- Finding 35 against a legacy entity: "Orkney" in keywords:sub went to the project's "Orkney (Sask.)" over Scotland's
  Orkney, "the project's own aligned entity breaks the tie". Finding 89, built: prominence checks the project's entity
  before it breaks a tie; when another tied group's item is the prominent one by prominence's own margins, the string
  is queued for the desk with both sides' counts in the reasons. "Washington (D.C.)" is queued between two project
  entities both labelled "Washington": duplicates for merge-duplicates or the desk.
- The corrections held: "Bell, Trevor, B. 1930" and "Statens Museum for Kunst (Copenhagen, Denmark)" are votes again
  (minted -> vote), the 17 Royal Academy Winter Exhibition headings are back to queued (minted -> queued), "Münster" is
  queued, "Venice (Italy)" is no longer a person.
- A new wrong turn from finding 87 (queued -> minted 19; exact-name -> minted 5): "Arundel Castle (Sussex)", "Firle
  Place (Sussex)", "Cardiff Castle (Glamorganshire)", "Parham Park (Sussex)", "Powderham Castle (Devonshire)". Sussex now
  resolves (two counties under England, so the shared chain), and finding 66's veto fell on every candidate whose
  placement did not agree, LCSH's "Arundel Castle (Arundel, England)" among them, whose placement names the town and
  the country and not the county: not elsewhere. Finding 90, built: the veto needs a known place on the candidate's
  side outside the qualifier's chain (a TGN parent string that disagrees is one; a heading's parenthetical only when
  one of its segments outside the chain is a county or above the walk knows: Kildare against Dublin yes, Arundel
  against Sussex no); the known places are gathered per string from the candidates' placements through the cached
  walk, only when the string's qualifier resolved. Expect the 24 back.
- Also from the diff: 14 queued -> single, "Ashburnham Place (Sussex)", "Basildon Park (Berkshire)", "Cowdray House
  (Sussex)", the decision-16 shape, now carried by LCSH's placed heading since the county resolves (the right answer
  that decision 16 declined to force; here the rule's own conditions reached it); 11 vote -> queued, "Bedford
  (England)", "Lincoln (England)", "Chester (England)", "Munster (Ireland)", "Newbridge (Dublin)": t629cj's votes on
  these came through the widened store entity, which the lone-country rule now leaves unplaced, so they are the
  desk's again or wait for their evidence (plan section 0); "Sketchley, R. E. D. 1875- [1875-] (author)" single ->
  queued, two date segments after finding 83's split (parser gaps). Time by stage: store 1,033 s, commit 234 s, the
  second pass 194 s.

Staged 2026-10-24, finding 88, dates for LoC's undated person records from the Wikidata item that links them (the
index's second use; plan section 0 item 2 has the SQL). Measured first: 1,318,802 LoC person records are linked from
a Wikidata item with a birth or a death date, 374,822 of them undated in LoC (the query ran 7 min 15 s beside a
reconcile). The lever: a dated string against LoC's undated record of the name is today a guess the lone-record rules
make (findings 24, 75, 79); dated, it is a comparison, agreeing or conflicting. The risk: a Wikidata item linked to
the wrong LoC record writes a wrong lifespan onto it, which then vetoes the right person's strings. Two checks cut
it: the item carries one LoC id only (a merged item spreads nothing), and the LoC heading's surname (its text before
the first comma, lower-cased) appears in one of the item's labels. Through the filter 354,991 pass (about five per
cent dropped as doubtful), and the owner's sample of twenty read right to the last row (2026-10-24 afternoon), so the
write is cleared for after the run in progress; the write records the item in the record's data as `datesFrom`, so the dates' provenance is
on the record and the step is reversible (`update ... set begin_earliest = null, ... where data ? 'datesFrom'`). The
LoC loader does not yet do this at load time, so a reload loses the dates until it does (open item). Written
2026-10-24 evening, between runs: 354,991 rows in 8 min 54 s, and the `datesFrom` count is 354,991, the measurement's
figure to the row. The next run measures it.

The write as run (2026-10-24 evening; the one-off SQL, kept here because the LoC loader has to learn the step before the next
reload, and because every staged mass write to the table now ends with `vacuum (analyze) authority_records`, which this one did
not and cost bp8unt its hours):

       with links as (
         select w.uri as item, w.normalised_labels as wlabels, w.begin_earliest, w.begin_latest, w.end_earliest, w.end_latest, w.lifespan_text, u.loc_uri
         from authority_records w cross join lateral unnest(w.same_as) as u(loc_uri)
         where w.source = 'wikidata' and w.type = 'Person' and u.loc_uri like 'http://id.loc.gov/%'
           and (w.begin_earliest is not null or w.end_latest is not null)
           and cardinality(array(select x from unnest(w.same_as) x where x like 'http://id.loc.gov/%')) = 1)
       update authority_records a
          set begin_earliest = l.begin_earliest, begin_latest = l.begin_latest, end_earliest = l.end_earliest, end_latest = l.end_latest,
              lifespan_text = l.lifespan_text, data = coalesce(a.data, '{}'::jsonb) || jsonb_build_object('datesFrom', l.item)
         from links l
        where a.uri = l.loc_uri and a.source = 'loc' and a.type = 'Person'
          and a.begin_earliest is null and a.begin_latest is null and a.end_earliest is null and a.end_latest is null
          and exists (select 1 from unnest(l.wlabels) wl where wl like '%' || lower(split_part(a.label, ',', 1)) || '%');

    Then `select count(*) from authority_records where source = 'loc' and data ? 'datesFrom';` (354,991), then the vacuum.

Built 2026-10-24 midday, finding 87 and the corrections to 83 and 84, from t629cj's run-to-run method diff and the TGN
rows. The diff (plan section 0 has the query; each run's last decision per string, since a settled string has two) read
against fk53p7: queued -> vote 20 (Bedford, Chester, Derry, Crimea, and dated names finding 83 now reads right), queued
-> single 14, the surname-only strings decided on their records (minted -> exact-name, single, vote: "Froy, Martin
1926-", "Casement, Roger 1864-1916", "Camplin, Bonnie. 1970-"), vote -> exact-name 52 (the project's own place
entities now answer at the store stage where they voted before, through the widened placement; the same entity, but a
store match gains no new equivalents where a vote did, which is the audit's business later), and four shapes to fix:

- "Bell, Trevor, B. 1930" lost its vote (vote -> minted): finding 83's regex excluded lower-case date words only, and
  "B." is "born" in capitals. Corrected: the exclusion is case-insensitive.
- "Harewood House (Leeds)" and "Harewood House (Yorkshire)" lost the project's "Harewood House (England)" (exact-name
  -> queued): finding 84's store filter refused any disagreement, and a town's name resolves to nothing, so "Leeds"
  against "England" read as elsewhere. Withdrawn: the veto in Score, which fires when the string's qualifier resolves
  to a known place, is the whole of the rule; "Lincoln (England)" is still refused Nebraska by it.
- "Statens Museum for Kunst (Copenhagen, Denmark)" lost its vote (vote -> minted): the project's "Statens museum for
  kunst (Denmark)" had its placement widened in place to "Denmark, Danmark, Europe, World", which no longer read as a
  coarser placement inside the string's "Copenhagen, Denmark", and the vetoed entity took its group of ULAN and LoC
  down with it. Corrected on finding 56's rule, ancestors widen agreement and never narrow it: the chain is kept apart
  (`Candidate.PlacementAncestors`) and consulted only when the placement as written does not agree.
- "Newbridge (Dublin)" came back by a vote onto Kildare's Newbridge and "Venice (Italy)" matched a person entity
  labelled "Venice", both because the ancestors walk resolved neither qualifier. The TGN rows: Dublin has four records
  at county level or above, the county, the region and "County Dublin", all under "Ireland, Europe, World"; "Italy" is
  TGN's nation "Italia" beside the Roman province "Italia". Finding 87: when several records of the name remain, the
  highest level wins (the nation before the province), and records that share the chain above them resolve to that
  chain (`CommonParent`, when it names a country or more); and the home-place filter of finding 71 decides only when
  it leaves something (Dublin's records, none in the United Kingdom, had emptied the list). "Palais (Augsburg,
  Germany)" minted is right (the French Palais records vetoed as elsewhere; it was wrongly a store match before).
- "National Maritime Museum (London)" lost its vote (vote -> queued; t629cj's keywords:head sample) for the same reason as
  Statens with one more twist: the project's entity is "(Great Britain)", a lone country that the placement reader leaves
  out of a label on purpose (a national body's qualifier, not a placement), and the widening path had read it in. Now a
  nation (level 5) from a label's own qualifier is left out as before, a county or a country within one is kept with
  its chain; and "Great Britain" and "United Kingdom" are one token (`Qualified.Synonyms`), since LoC's headings say
  the first where TGN's chains say the second.
- Still to read on the next run: 17 "Royal Academy of Arts (Great Britain). Winter Exhibition (nth : year : Royal
  Academy of Arts)" headings went queued -> minted, and "New York, NY", "New York (Madison Avenue)", "National Maritime
  Museum (London)", "London, New York & Bombay" went vote -> queued; the diff on the next run says whether the
  corrections above restore them.

Measured 2026-10-24 (run-20261024-100226-t629cj: findings 83 and 84, against fk53p7; prominence, finding 85, was
built while it ran and is not in it): accepted 9,386 -> 9,370 (vote 3,961 -> 3,936, single 2,110 -> 2,119,
exact-single 3,315), queued 3,603 -> 3,563, matched in store 5,958 -> 6,003, minted 11,306 -> 11,317; the second pass
reconsidered 3,869 (fk53p7: 3,896) and settled 306 (293). The path: ... -> 3,955 -> 3,603 -> 3,563. The fall in votes
(25) is unexplained from the summary; the run-to-run method diff (plan section 0, the query on two runs' decisions,
the undone run keeping its evidence) says where they went. Time by stage: store 1,054 s, the second pass 125 s. The
settled lines, read against fk53p7's:

- Finding 83 held: the nine surname-only matches ("Milton", "Gander", "Wordsworth") are gone from the settled lines.
  "Christopher, Ann, 1947-" still takes the project's entity labelled "Christopher": that entity must carry the full
  name among its labels, so it is the sculptor under a poor label, and the desk's to relabel.
- Finding 84 held and gave more than it took: "Lincoln (England)" left Nebraska and reached the project's own concept
  "Lincoln (England)" by a vote with TGN and LoC; "York (England)", "Gloucester (England)", "Chester (England)",
  "Bedford (England)", "Derry (Northern Ireland)", "Picardy (France)", "Benin (Nigeria)" and "Kildare (Ireland :
  County)", queued in fk53p7 among several TGN places each, are votes now: the widened placement made the project's
  legacy concept entity firm, and finding 35 let it break the tie.
- Wrong, three: "Münster" in the place field took "Munster (Ireland)" at 0.97: the German city's group was exact at
  1.00 with LoC's heading, but finding 35 read "tied" as any two-source group at acceptance score and the Irish group,
  holding the project's entity, won through its folded umlaut. Finding 86, built: a tie, for finding 35 and for
  prominence (85), is among groups at the top score only; "Münster" then has a rival at acceptance score and is
  queued, the desk's. "Newbridge (Dublin)" left the store stage as finding 84 meant and came back by a vote onto
  Kildare's Newbridge with TGN and LoC: the qualifier "Dublin" was not resolved to a known place (the ancestors walk
  found more than one TGN Dublin at county level or above, so no veto fell on a group placed elsewhere). "Venice
  (Italy)" matched the project's person entity labelled "Venice": finding 69's veto (a heading placed in a known place
  is not a person) needs the qualifier resolved, and "Italy" was not. Both wait on the TGN rows for "Dublin" and
  "Italy" (plan section 0); the likely fix is that several records of the name at county level or above that share a
  parent resolve to that parent.
- Noise: "L (colonnade)" in the place field minted "L, colonnade" (a fragment; parser gaps). The rest as in fk53p7.

Built 2026-10-24, finding 85, prominence (the Wikidata index's first use; doc 11 section 8.11). "Sheffield" in
keywords:sub ties between TGN's English city with LoC's heading and Alabama's, Ohio's and Massachusetts' Sheffields,
each a two-source group placed through, and nothing on an unqualified string separates them: the famous-name tie
(finding 17) that most of the keywords:sub queue is. Wikidata's items, reached through the candidates' TGN and LoC URIs
in their sameAs links (the index need not be among the manifest's sources), say which the world writes about: 122
sitelinks against 45. The rule: among two-source groups tied at acceptance score, when none holds a project entity
(finding 35 has that case) and the string carries no qualifier (a qualifier places), the group whose item has at least
20 sitelinks and at least twice the next group's is taken, decided as a vote with the reason "the prominent one: 122
sitelinks (Q42448) against 45" and the others marked "less prominent"; closer than that ("Murray" at 45, 44 and 39,
three American cities, and the publisher none of them) the groups are marked "prominence does not separate them" and
the string stays queued. Places only: for people and bodies sameness of name is weak evidence, and the desk decides.
`ProminenceMinSitelinks` (20; zero turns it off) and `ProminenceRatio` (2.0) are the options; the thresholds are the
assistant's judgement from the first queries on the index (Cornwall 126 against 46, Sheffield 122 against 45, Murray
45 against 44). Expected from fk53p7's samples: Sheffield, Parma (Italy's against Ohio's), Camden, Durham, Kildare,
Virginia and Williamsburg are settled or declined on their numbers; Cambridge (England's against Massachusetts',
both famous) is declined and stays the desk's, rightly for a rule this blunt. Cost: a sameAs lookup per URI of the
tied groups, through the GIN index, on the tie path only.

Built 2026-10-24 morning, finding 84, from the two store checks on fk53p7's settled lines. The project's "Lincoln, Neb"
is Nebraska (TGN 7013892, LoC "Lincoln (Neb.)"), and "Lincoln (England)" in keywords:head had matched it with the
reason "name 1.00": the entity's label says nothing TGN-shaped about where it is, so no placement was read and the
qualifier was never compared. "Newbridge (Dublin)" had matched "Newbridge, Co. Kildare" with the reason "qualifier not
placed (Dublin vs Kildare, Ireland)": the comparison ran, said no, and the store stage took the entity anyway, because
its qualifier filter applied only in a context whose rule reads qualified names (the place field). Now, in any context
where the string carries a qualifier: a store entity whose label gives no placement is placed through its TGN
equivalent's parent string; a label's own qualifier ("Boughton House (Northamptonshire)") is widened by the ancestors
walk (finding 56's, extracted as `AncestorsOfAsync`) so that "(England)" places it; and an entity whose placement
disagrees is not taken, while one whose placement is unknown still answers as before. The place field's "Lincoln"
is another matter: five TGN Lincolns tied and the project's own aligned entity, Nebraska's, broke the tie (finding
35), which may be right for a publisher place (the University of Nebraska Press is there) and is the desk's to say.
The corpauthor rule drops a parenthetical without reading it as a qualifier, so that context compares nothing and
"Boughton House (England)" matched as before; noted among the parser gaps, not changed.

Proposed, not built, for the owner (decision 17): a bare place name in a subject context ("Clayton" in keywords:head,
"Lincoln" in keywords:sub) that matches the project's one entity of the name takes it today whatever that entity's
place ("Clayton, Victoria, Australia"; "Lincoln, Neb"). The rule the assistant would build: the lone store entity
answers a bare place name silently only when it is placed in the project's home place (the manifest's United Kingdom:
"Cornwall (England : County)" for "Cornwall", right every time in the samples) or carries no placement at all; placed
elsewhere, the string is queued for the desk with the entity shown. The Wikidata prominence rule would then settle
some of those queued ("Lincoln" the English city at 100-odd sitelinks against Nebraska's), and the rest are the desk's.
The owner asked for examples (2026-10-24) and the rule narrowed before any count: "Dublin (Ireland)", "Saint Petersburg
(Russia)" and "Lucknow (India)" in fk53p7's settled lines were right and the plain home-place rule would send them to
the desk too, and Irish places are outside the United Kingdom but central to the project. Narrowed: the lone entity
placed elsewhere is doubted only when TGN knows a place of the name inside the home place. The count, in DataGrip:

    with m as (
      select s.context, s.raw, s.normalised, e.label
      from decisions d
      join source_strings s on s.id = d.source_string_id
      join entities e on e.id = d.entity_id
      where d.run_id = 'run-20261023-183343-fk53p7' and d.valid
        and d.method = 'exact-name' and d.evidence->>'stage' = 'store'
        and e.class = 'Place' and d.evidence->'reading'->>'qualifier' is null
        and s.context in ('keywords', 'keywords:head', 'keywords:sub')
    ), placed as (
      select *,
        label ~ '(England|Scotland|Wales|Northern Ireland|Great Britain|United Kingdom|London)' as home,
        label ~ '[(,]' as qualified,
        exists (select 1 from authority_records a where a.source = 'tgn'
                and a.normalised_labels @> array[m.normalised]
                and a.data->>'parentString' like '%United Kingdom%') as uk_namesake
      from m
    )
    select case when home then 'home' when not qualified then 'unplaced'
                when uk_namesake then 'elsewhere, UK namesake: the desk' else 'elsewhere, no UK namesake: kept' end as bucket,
           count(*), (array_agg(raw || ' -> ' || label order by raw))[1:15] as examples
    from placed group by 1 order by 1;

The same `m`, listing the bucket the narrowed rule would send to the desk, to read through: `select context, raw, label
from m where label !~ '(England|Scotland|Wales|Northern Ireland|Great Britain|United Kingdom|London)' and label ~ '[(,]'
and exists (the uk_namesake subquery) order by context, raw;`. Mostly wrong matches there means the rule is worth
building; mostly right ones means the desk is the better answer and decision 17 closes as no.

Measured 2026-10-23 evening (run-20261023-183343-fk53p7: findings 81 and 82 and the second pass, finding 51, against
z5vp74): accepted 9,139 -> 9,386 (vote 3,874 -> 3,961, single 1,936 -> 2,110, exact-single 3,329 -> 3,315), queued
3,955 -> 3,603, matched in store 5,841 -> 5,958, minted 11,318 -> 11,306. The second pass reconsidered 3,896 queued
strings and settled 293, so findings 81 and 82 took 59 (3,955 -> 3,896) and the second pass the rest. By context:
persauthorfull queued 1,303 -> 1,264, keywords 759 -> 620, publisher 798 -> 781, keywords:head 465 -> 354, place 277
-> 256, corpauthor 182 -> 175, keywords:sub 171 -> 153. The path: 4,294 -> 4,193 -> 4,109 -> 3,964 -> 3,955 -> 3,603.
Time by stage: store 1,022 s of about 1,900, the second pass 105 s. The settled lines, read:

- As designed: "Cornwall", "Norfolk", "Yorkshire", "Hampshire", "Cheshire", "Dublin", "Saint Petersburg", "Lucknow",
  "Didsbury", "West Country" in keywords:sub to the place entities the place field made later (decision 7's shape, now
  closed by the pass); "Scotney Castle", "Sudeley Castle", "Boughton House (England)", "Gawthorpe Hall?" to the project's
  qualified house entities; the dated keywords names onto the project's own undated entity of the name minted earlier in
  the run ("Hall, Harry, 1815-1882" to "Hall, Harry"; the single count's rise, 1,936 -> 2,110, is these), and the
  ULAN-LoC votes where the project's entity joined the group ("Turner, Daniel, fl 1782-1817"); the same whole heading
  in two contexts sharing one concept ("St. Andrew's Church (Freckenham, England)").
- Wrong, nine strings: "Milton, John 1608-1674", "Wordsworth, William 1770-1850", "Gander, Ryan 1976-", "Christopher,
  Ann, 1947-", "Greg, Samuel 1758-1834", "Parker, Frederick 1845-1927", "Starr, John W. 1925-", "Blackburn, John
  1932-", "Pelham, Henry 1695?-1754" accepted onto the project's surname-only legacy entities ("Milton", "Gander").
  The agent parser read "John 1608-1674" as the date segment and left "Milton" as the name: a heading with the comma
  before the dates dropped. Finding 83, built in the parser (`AgentStringParser`, step 3): a trailing date segment that
  opens with name words is the forenames and the dates, the words back on the name. The rerun reverses the nine.
- To check: "Lincoln (England)" in keywords:head matched the project's "Lincoln, Neb", as did "Lincoln" in keywords:sub
  and the place field's "Lincoln" (a vote with that entity in the group). The store stage requires the qualifier to
  agree, so either that entity's equivalents place it in England and its label is wrong, or the placement is. The
  query is in plan section 0. "Newbridge (Dublin)" matched "Newbridge, Co. Kildare" (Newbridge House is in County
  Dublin; the town is Kildare's), the same question. "Clayton" (keywords:head) to the project's one Clayton, in
  Victoria, and "Smith, William" to the project's one: the lone-entity rule for places and bare names, the desk's.
- Still queued, keywords:head: "Paintings, European", "Kirby, John", "Hughs", "Art, Primitive", "Franco", "Royal
  Collection (London)" against "(Great Britain)" (the qualifier not placed; a body's qualifier is its seat, London is
  in Great Britain: a small finding for the qualifier walk, not built), "York (England)" and "Leeds (England)" with
  three TGN places each (the inhabited place, the district, the county), "Piper, John, 1903-1992" and "Blake, Peter,
  1932-" held by the legacy twins (finding 32). keywords:sub: "Sheffield", "Parma", "Camden", "Cambridge", "Durham",
  "Virginia", "Williamsburg", "Kildare", every one the famous-name tie that waits for prominence from the Wikidata
  index (doc 11 section 8.11, plan item 3); "Trent)", a fragment with an unbalanced bracket from a split heading
  (parser gaps); "De profundis" against the Psalm, the desk's.

Measured 2026-10-23 (run-20261023-140352-z5vp74: findings 79 and 80, against eetp4h; the slow first run of them, ace2wt,
stopped and undone): accepted 9,124 -> 9,139 (vote 3,867 -> 3,874, single 1,936, exact-single 3,321 -> 3,329), queued
3,964 -> 3,955, minted 11,329 -> 11,318, matched in store 5,836 -> 5,841. By context: persauthorfull queued 1,306 ->
1,303, keywords 761 -> 759, keywords:head 469 -> 465; the rest unchanged. Small, as the two findings were. The path:
4,294 -> 4,193 -> 4,109 -> 3,964 -> 3,955. The samples, read:

- "Piper, John, 1903-1992" and "Blake, Peter, 1932-": LoC's dated record now joins ULAN's ("loc's one record of the
  name, which ulan dates", finding 79's wording for the dated lone record), and the pair is held only by the project's
  two legacy twins of each name, both aligned: finding 32, the desk's.
- "Surrealism (Literature)" (keywords:head): LCSH holds the whole heading and the bare "Surrealism" as two headings, the
  project holds both as concepts, and AAT's "Surrealist" is exact through a variant: five records at 1.00 and the string
  queued. Finding 81, built: a heading the home vocabulary holds whole beside its bare form names the whole heading's
  record; the home vocabulary's records exact only on the bare name are its other concepts, set aside, so the home join
  carries AAT's term to the whole heading; and in the store, of two of the project's entities the one labelled with the
  whole heading as written answers the heading. LCSH's qualified topics ("Realism (Art)", "Romanticism (Literature)")
  are this shape.
- "Charlton, Alan, 1948" (keywords:head): read by finding 26 as alive in 1948, "not comparable" with ULAN's 1948~-~2048
  and LoC's "1948-"; as the birth year it agrees with both, LoC's undated "Charlton, Alan" is the namesake (finding 54),
  and the two vote. "Goldsmith, Oliver, 1730?" was a birth year too. Finding 82, built: a lone year after a name is the
  birth year with its dash dropped, as the headings write it, not a year the person was alive; an uncertain or
  approximate one ("1730?") is widened by four years each way, still tight, so it meets LoC's 1728. The floruit forms
  stay working dates.
- "Paintings, European" (ULAN's department at 0.93), "Kirby, John", "Hughs", "Fine Art Society (London)", "York
  (England)", "Art, Primitive": as before. publisher: "Murray", "König", "Sutton", "Hutchinson", "Garland", "Christopher
  Cock", "Adams & Dart", "Clark Art Ltd": the bare surnames and the one-word firms, the desk's; "Department of the
  Environment" (ULAN's two); "Peter Lang" (LoC's two imprints, Bern and New York). keywords:sub: "Cornwall", "Norfolk",
  "Yorkshire", "Sheffield", "Vienna", "Parma", "Camden", "Hampshire", "Cambridge": the famous-name ties, prominence,
  waiting for the Wikidata index; "De profundis" against LCSH's "Psalms (Music)--130th Psalm" through a variant at
  0.85: the desk's.

Built 2026-10-23, finding 51 (the second pass), while the owner's run went. A string queued early in a run may be
settled by what the run does later: "Cornwall" in keywords:sub was queued before the place field's "Cornwall" made the
entity that would have answered it from the store (decision 7, 2026-10-22, closed as run order). At the end of a run
the reconciler now evaluates every string it queued once more, with the store as it stands. One that comes back
matched in store or accepted (the store stage, or the project's own aligned entity breaking a tie among places,
finding 35) is decided afresh: the queued decision is superseded, the new decision carries the run's id (so the undo
takes it too), and the queued string's placeholder, which nothing else points at (a queued placeholder never answers
another string), is removed. One still queued keeps its decision and placeholder. The indexes have not changed within
the run, so nothing else can come back different, and the pass is cheap: a store lookup per queued string. The run
report ends "second pass: n queued strings reconsidered, m settled", the settled ones counted under matched in store
or accepted, and `reconcile --verbose` prints the settled strings after the run's own lines, numbered within the pass.

Measured 2026-10-23 (run-20261023-124010-eetp4h: findings 77 and 78, against p9g534): accepted 9,017 -> 9,124 (vote
3,803 -> 3,867, single 1,907 -> 1,936, exact-single 3,307 -> 3,321), queued 4,109 -> 3,964, minted 11,295 -> 11,329,
matched in store 5,832 -> 5,836. By context: keywords queued 843 -> 761 (finding 77's county qualifiers and finding
78's bodies), place 302 -> 277 (finding 77), keywords:head 487 -> 469, publisher 811 -> 798, corpauthor 189 -> 182,
persauthorfull 1,306, keywords:sub 171. The path: 4,463 -> 4,294 -> 4,193 -> 4,109 -> 3,964: under 4,000, the band
the assistant expected the cycles to stop in. The samples, read:

- "Bolton, James, fl. 1775-1795" (keywords:head): LoC's "Bolton, James, active 1775-1795" agrees (the working dates
  of finding 26 on both sides), LoC's "active 1840-1876" conflicts and is vetoed, and ULAN's one "Bolton, James",
  1699~-1799, stands apart: finding 55 joined a lone record only when undated or dated by a window, and ULAN's end
  here is tight. Finding 79, built: a source's lone exact record whose dates do not conflict joins the record another
  source dates as the string does; a conflict would have vetoed it, and nothing else contradicts.
- "World War, 1939-1945" (keywords:head): read by the marc profile as the person "World War" with a lifespan, and AAT's
  "world wars" its one candidate; LCSH's heading of the whole string was not found by the name's keys. Finding 80,
  built: in a context that uses the parser for dated strings only, a dated heading whose whole text is a label of a
  concept, a period or an event in any index is that and not a person, and is read again without the parser; "Tuke,
  Henry Scott, 1858-1929" keeps its lifespan. Why AAT's lone concept did not take the string as finding 24's lone
  record is not shown by the printer; it no longer matters for this string.
- "Christie, Sharp and Harper" (corpauthor): ULAN's "Christie, Sharp & Harper" at 1.00 and, at 0.97, ULAN's
  "Christie, James" and "Christie's", both of which list the firm's name among their variants: ULAN's own history of
  the house, three records claiming one name. Not a matcher fault; the desk's.
- "Wheatley, John, 1892-1955": ULAN agrees; LoC's "Wheatley, John" and "Wheatley, J. (John)" (exact through finding
  78's expansion) are two LoC records of the name, two people by LoC's own rule, so neither is the lone record. The
  desk's, as finding 14 intends.
- "Canadian Art Club" against LoC's one "Canadian Art Club (Toronto, Ont.)" at 0.85: the bare string against a lone
  place-qualified heading, decision 16's shape outside the home place. The desk's by that decision.
- "Kasmin Limited (London)": LoC's "Kasmin Limited" exact, LoC's "Kasmin" at 0.95 (the suffix dropped) a rival, and
  the dealer "Kasmin, John" at 0.85; two LoC records of the body. The desk's.
- "Waltham Forest: Libraries & Arts Department" against LoC's "Waltham Forest (London, England). Libraries and Arts
  Department" at 0.85: LoC's subordinate-body form (parent, its place, a full stop, the department) against the
  archive's colon; a name-forms shape worth a rule if corpauthor's queue shows more of it. Noted, not built.
- "Messrs Royce", "Mr Abbott", "Mr Hodgson", "Mr Farebrother", "Mr Hoard": bare surnames, the one-word rule holding;
  "Nationalmuseum" (ULAN's Stockholm record and its Copenhagen one through a variant); "Musée de l'Ermitage"; "City
  Museum and Art Gallery, Portsmouth": the desk's. keywords and keywords:head otherwise as before: the famous-name
  ties, the legacy twins, "Art, Primitive", "Fine Art Society (London)", "Hughs".

The owner's two exports from the place-type query (2026-10-20, run tzm64p; `reports/tgn-namesakes-tzm64p-types.csv`
and `reports/tgn-namesakes-tzm64p.csv`, git-ignored): 843 queued place-like strings have live exact TGN namesakes.
The type combinations recur as expected (five inhabited places "Aberdeen"; county beside town "Durham", "Lancaster",
"Bedford"; nation beside hamlets "Canada", "China"; "England" beside a TGN namesake), but the largest class, 248 of
843, is strings with exactly one live exact TGN record ("Abbeyleix", "Arbroath", "Avebury", "Berkhamsted",
"Buckinghamshire", "Aldeburgh, Suffolk"), queued all the same. The reconciler explains it without more data (finding
9): LoC's qualified heading "Abbeyleix (Ireland)" scores 0.85 against the bare name, so TGN's one record is not an
exact-single (the policy wants no other group at all) and not a vote (TGN and LoC never link to each other), though
the two records are the same place: LoC's qualifier "Ireland" sits inside TGN's parent string "Abbeyleix, Laois,
Ireland, Europe, World". Design: compute `Candidate.Placement` for every candidate, not only when the string has a
qualifier, and generalise placed-through so that a candidate whose placement sits inside exactly one other-source
candidate's placement is joined to it whatever the string carries; TGN and LoC then agree and the string is a vote.
It leaves the nation shape ("Canada": LoC's heading has no qualifier) to a type preference, measured from the same
exports: among TGN namesakes of differing type, an unqualified LoC heading, which LoC gives only to a place unique in
its world, would be joined to TGN's record of the highest level (nation, state, county) rather than an inhabited
place of the name. To confirm the 248 before building, the owner runs the query in the plan, which lists the live
rivals of those strings by source and score.

Confirmed 2026-10-20 (the owner's run of that query, rivals of the 248 by source and score): loc at 0.85, 148 strings
("Abbeyleix (Ireland)", "Acton Bridge (England)"); store at 1.00, 59 ("Adelaide, South Australia" beside the project's
own bare entity "Adelaide"); store at 0.85, 41 (legacy entities labelled from LoC, "Arbroath (Scotland)"); loc at
0.90, 39 (a placed LoC heading, "Adelaide (S.A.)", on the same strings as the bare store entity); loc at 1.00, 25
("Buckinghamshire (England)", exact through a variant); lcsh at 0.85, 12 ("Black Country (England)"); and 25 of
another class, aat and lcsh at 1.00 on subdivision strings that are topics ("Cult", "Exile", "Finance") or places
("Ethiopia", "European", "Oceania") beside a TGN hamlet or nation of the name. So finding 9 reaches about 226 of the
248 (every rival that carries a placement inside TGN's parent string: LoC's and LCSH's qualified headings, and the
store's legacy entities labelled from them). The 59 bare store entities are finding 10: in a qualified reading with
a placed candidate, the project's one entity exact on the name that has no placement of its own ("Adelaide", the
only Adelaide the project has) joins the placed group and gains the authority id, as the LoC-labelled ones do. The 25
of another class are a policy question for the owner, "outvoted rivals": at a tie at acceptance score, a group two
independent sources carry would beat a rival one source carries ("Finance": LCSH's topic with AAT's term carried,
against a TGN hamlet called Finance; "Aberdeen": TGN's Scottish city with LoC's "Aberdeen (Scotland)" joined,
against four TGN namesakes no LoC heading reaches). Two two-source groups ("Cambridge" in England and in
Massachusetts) stay queued. Not built; to measure once finding 9 is in.

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

The review UI question is settled in [12-workbench.md](12-workbench.md) (2026-10-18): a separate app,
`LinkedArtNet.Workbench`, with projects and per-project members; the trainer moves into it.
