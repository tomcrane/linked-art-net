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

Open for the owner: keep the `unrec_` identifiers for headings now that they carry structure (recommended: LUX holds
them), or remint. Order of work: after step 3b (remote sources), since the LCSH index and the LUX mapping both
matter here.

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
