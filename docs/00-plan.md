# Plan: linked-art-net

Status (2026-10-26): Phases 0 to 3 delivered: linkedart.net and dates.linkedart.net run v0.1.2 (2026-10-17; the model
library, the dates parser, service, site and trainer). Phase 4 (hardening) is open. Phase 5, the alignment toolkit, is
the current work since 2026-10-17 and is not deployed: the agent-string parser (`LinkedArtNet.Agents`), the alignment
store on PostgreSQL, six authority sources loaded locally, the reconciler and the `tools/Align` pipeline, and the review
workbench, all running on the owner's machine against a local store. The reconciler was tuned run by run from 2026-10-19
to 2026-10-26 (findings 1 to 104, doc 10 section 7.6; the day-by-day record in [15-log.md](15-log.md)); the cycles
ended on 2026-10-26 and the desk is the next work. Branch `de-pmc` is the working branch and `main` is what is deployed
(README, "Branches and releases"), so `main` is behind by all of Phase 5. The brief is in `exclude/design.md`
(gitignored); this plan and the design docs in this folder are the committed record.

How this document is kept: section 0 is rewritten at the end of every working day and never appended to; what happened
in the day goes to the log (doc 15) and the findings to doc 10 section 7.6. Section 7 lists what is open, by area, and
loses an item when it is done (the log records it). Sections 1 to 6 are the plan as proposed on 2026-10-13, kept
current in their status lines.

## 0. Resume point (written 2026-11-05 evening; the one place to read first)

**On return, in order:** (1) `git push` if the evening's commits are not up (seventeen as of this writing; the last
is the one that wrote this); (2) the desk, continued: start the workbench (Commands on return, 1) and review from the
queue, and each thing the desk fails to show, do or get right becomes an item in section 7 and is built, the way
today's and the 26th's were; the desk items standing (section 7, Workbench and desk) are today's two, the twins that
share a record (a hint on the card or the search result towards "Same as" or "Accept together", saying which where it
appears) and the publisher strings without a record behind them, and the 26th's two, the identifier field on "Keep as
local authority" and no possessive key for a name in capitals; (3) the reconciler's one new item from today, two
project entities of one name at the top score (three New Yorks between the city's entity and the state's, "Christopher
Gibbs" between two persons), counted before any rule is built; (4) the Wikidata index's next uses, the crosswalk and
labels as variants (section 7, sources and indexes); then the open items of section 7 as the owner orders them.

**Where things stand.** Branch `de-pmc`, all work committed, every test project green (3,203 tests). Six authority
sources loaded: the manifest's five (ULAN, AAT, TGN, LoC names, LCSH; 14.5M records) and the Wikidata extract (5.0M
records that carry another authority's id, loaded 2026-10-23 and retyped by the subclass walk on 2026-10-24; not yet
among the manifest's sources; doc 11 section 8.11); the LoC index's 38,754 "active" headings carry working spans and
354,991 of its undated person records their Wikidata item's dates (finding 88; every staged mass write to
`authority_records` ends with `vacuum (analyze)`). The store has had two duplicate passes (292 and 89 merges) and four
hand merges; the manifest was last refreshed on 2026-10-23 (the home place). The reconciliation cycles ended on
2026-10-26 (run awn284); runs are occasional since, and a run is measured by its diff against the run before (Commands
on return, 4), never by the queue count, which is the desk's residue by design. The last full run is cpxv7k
(2026-11-05 evening: accepted 9,377, queued 4,039, minted 10,876; doc 10 7.6 "Measured 2026-11-05 (run cpxv7k)").
Today, from the two drifted decisions the owner's query found in awn284: finding 106 (a firm project entity whose
placement the string's resolved qualifier contradicts, or whose level a level word contradicts, neither matches
silently nor carries a vote alone; a name of one letter matches nothing), measured by run v6gjx5, which left three
more; finding 107 (two sources joined only through such an entity do not carry a vote), finding 105's possessive
lookalike no rival (for the single's second group, the exact-single's count, the outvoted policy), and the evidence
never cutting a group that holds a project entity, measured by cpxv7k, every one to the string and nothing else moving.
Findings 1 to 107 are in doc 10 7.6, newest first. Also today, from the owner's notes at the desk: a project entity's
name links to its page from the cards, "Currently" and the search results, the entity page shows "In the records",
and the workbench wears the site's own look (doc 12 section 3). The desk radiates through the store: the owner's
corpauthor decisions of the 26th reached their publisher twins by exact name in v6gjx5, and "Lawrence" in cpxv7k.
The Wikidata dump and the sitelinks table are on the owner's external drive (`E:\datadumps`).

**Decisions owed by the owner:** 15, *the LUX export's shape* (doc 10 7.5 item 2, before it is designed): ask the LUX
team whether a source reload replaces by URI and whether records absent from a full dump are retired; the merged-away
identifiers (`merged_into` on 381 entities and the four hand merges) are written as equivalents of the survivor so LUX
merges its copies; a reviewed Wikidata contribution export of confirmed local authorities belongs beside it (section 7,
exports and reports). 16, *"Mr Hone"* (finding 109, 2026-11-06): whether a sale catalogue's corpauthor or publisher may
be the owner whose collection was sold, as the Provenance Index names him ("Mr. Hone" on Nathaniel Hone the painter),
or is always the auctioneer; a specialist's question, the owner says, and PMC's own. Until it is answered the rule
carries a person's Index name only when his biography names one of the manifest's `trades`, and Hone's kind stays
queued, the match shown on the card. Open question 7 (section 6). Every decision taken is in doc 10 section 13 and the log.

**Commands on return** (the owner runs all of them; the assistant never runs anything against the real store; the
answered queries and one-off SQL of earlier days are in doc 10 7.6 and doc 11 section 8.11).

1. The desk: start the workbench against the real store (`LINKEDART_PG` set; Development allows the typed sign-in,
   the name `local` is an owner) and review from the queue; the assistant does not build the workbench project while
   it runs (the output is locked), so a commit that touches it waits for a stop:

       dotnet run --project src/LinkedArtNet.Workbench

   then http://localhost:5200.

2. ~~Findings 108 and 109 measured by a run~~ (run a98vpa, 2026-11-06 12:56, read in doc 10 7.6 "Measured 2026-11-06
   (run a98vpa)": 71 queued strings to single as measured, 77 votes relabelled single by the rule sitting before the
   acceptance line, put behind it the same afternoon; three strings read one by one. Run jr8hf5 at 14:05 measured the
   fix: the 77 back to vote on the same entities, the diff one row, nothing else moving; doc 10 7.6 "Measured
   2026-11-06 (run jr8hf5)". The last full run is jr8hf5.)


3. The records behind the strings, collected again after the next library export (the same catalogue, the same
   record ids, so the newer export's records replace the older's):

       dotnet run --project tools/Align -- collect-examples --file <the library export .xml> --source <a name for it> --keywords "C:\Users\TomCrane\Dropbox\digirati\PMC\linked.art\pmc_subjects_csv.sql"

4. The standing method diff after the next run, whenever it comes, against jr8hf5, the last full run (each run's last
   decision per string, since a string the second pass settled has two; an undone run keeps its evidence, though the
   undo removes the provisional entities it minted and nulls their decisions' entity, so `same_entity` reads null for
   those and true or false only where the entity survived: the desk's, and the ones made before the run undone).
   A method change on the same entity is a label, not a decision (the 77 of a98vpa):

       with a as (select distinct on (source_string_id) source_string_id, method, entity_id from decisions where run_id = 'run-20261106-140520-jr8hf5' order by source_string_id, id desc),
            b as (select distinct on (source_string_id) source_string_id, method, entity_id from decisions where run_id = '<new run id>' order by source_string_id, id desc)
       select a.method as before, b.method as after, (a.entity_id = b.entity_id) as same_entity, count(*), (array_agg(s.raw order by s.raw))[1:8] as examples
       from a join b on b.source_string_id = a.source_string_id
       join source_strings s on s.id = a.source_string_id
       where a.method <> b.method group by 1, 2, 3 order by 4 desc;

## 1. Objectives (in priority order)

1. **Model library**: a top-class Linked Art 1.0 implementation in .NET, fluent to write, able to
   generate every example on linked.art, with comprehensive tests. ([02-model-library.md](02-model-library.md))
2. **Parsers**: standalone libraries that *read* a string with no data behind them. The **TimeSpan parser** turns
   free-text dates into Linked Art TimeSpans with confidence, one-or-two-span intent, domain profiles, pluggable
   LLM fallback, deep time and BCE ([03-timespan-parser.md](03-timespan-parser.md),
   [03-timespan-prior-art.md](03-timespan-prior-art.md)). The **Agent parser** reads a name string under a profile
   (a MARC heading, an inverted personal name, a firm) into a name, lifespan, roles, nationalities and kind, and
   hands the date substring to the TimeSpan parser ([10-alignment-toolkit.md](10-alignment-toolkit.md) section 6).
   Each parser has the same four pieces: the library, a service endpoint, a playground on the examples site, and a
   corpus of ground truth with a trainer. The TimeSpan parser has all four; the Agent parser has the library and its
   tests, the rest is Phase 5 (section 7, parsers).
3. **Corpus**: a large ground-truth set of date strings, plus a trainer UI to grow it. ([06-corpus-and-testing.md](06-corpus-and-testing.md))
4. **Service**: a Kestrel API returning rich TimeSpan results for GET/POST. ([04-timespan-service.md](04-timespan-service.md))
5. **Examples site**: C# code for every site example, generated vs site JSON, parser playground. ([05-examples-site.md](05-examples-site.md))
6. **Alignment toolkit**: the consumer of every parser, in three tiers. *Read only*: the parsers, no data. *Align
   through the vocabularies' own APIs*: remote sources, no setup, slow. *Align against local indexes*: the loaded
   authority files, fast, with a store of entities, strings and decisions, a review workbench and exports. Docs 10
   to 14; doc 07 holds the earlier notes.

## 2. Phases

Phases 0 to 3 are delivered and Phase 4 is open; their step lists as struck off are in the log (doc 15, "2026-10-13 to
2026-10-19", items 1 to 30) and their designs in docs 02 to 09.

- **Phase 0, housekeeping**: done 2026-10-13 (the `src/`, `tests/`, `tools/` layout, build props, CI).
- **Phase 1, the model library to 1.0** (doc 02): done 2026-10-13. Property set generated from the 1.0 schemas and
  held by a contract test; 183 of 183 site examples implemented, 182 validated, 3 blocked by site id collisions
  (`SiteIssue`, docs/upstream-issues.md). NuGet deferred (owner, 2026-10-13).
- **Phase 2, the TimeSpan parser** (docs 03, 06): done 2026-10-13 to 2026-10-17. Grammar, corpus and trainer, profiles
  and the advisor, six lexicons, named periods and PeriodO, the LLM interpreter as a corpus-growing assistant (never at
  request time). Leftovers in section 7 (parsers).
- **Phase 3, the service and the site** (docs 04, 05, 09): done 2026-10-14 to 2026-10-16; v0.1.0 deployed 2026-10-16,
  v0.1.2 on 2026-10-17 at linkedart.net and dates.linkedart.net.
- **Phase 4, hardening and release**: open; its items are in section 7 (housekeeping and release).
- **Phase 5, the alignment toolkit** (docs 10 to 14; doc 07 holds the earlier notes). Designed 2026-10-17; the order
  of work is doc 10 section 12: (1) the agent-string parser, built 2026-10-17, its service endpoint, playground and
  corpus open (section 7, parsers); (2) the store, built 2026-10-17 (doc 11); (3) authority sources and the reconciler,
  the local half built 2026-10-18 and tuned to 2026-10-26, the remote half (3b) open; (4) the pipeline runner with PMC
  as the first instance, open (doc 14 for what a project needs); (5) the review UI, the workbench (doc 12), steps 1 and
  2 built 2026-10-18 and 2026-10-19, steps 3 to 7 open; (6) the Wellcome instance; (7) publishing, the LUX export
  (decision 15); (8) the QLever loader. The current work is the desk (section 0).

## 3. Decisions (ADR-style, short)

| # | Decision | Why |
|---|---|---|
| D1 | Drop Microsoft.Recognizers.Text; write our own tokenizer + grammar. | Wrong problem domain; caused most observed defects; heavy. Owner is not wedded to it. |
| D2 | Separate parsing (AST) from realisation (policy), with a canonical EDTF-superset form between them. | Testability; stable ground truth; LLM output can be constrained to the canonical form and realised deterministically. |
| D3 | Year numbering is ISO 8601 / XSD 1.1 astronomical (0000 = 1 BCE). Output lexical form `yyyy-MM-ddTHH:mm:ssZ`, more digits as needed, no `+`. | Linked Art is silent; this is what `date-time` implies and what LUX emits. Helpers hide the arithmetic. |
| D4 | Inclusive end instants (`23:59:59`), per linked.art/base. | Site convention and CRM requirement. |
| D5 | Default century convention `Popular` (19th = 1800-1899), bands as thirds; all overridable per profile. | Matches most museum data and prior tools; owner to confirm. |
| D6 | Circa does not widen bounds by default; it sets a flag. | Widening is a policy with no consensus; label carries the hedge; profiles can pad. |
| D7 | `_label` of every produced TimeSpan is the exact input string; Name is optional by policy. | Brief requirement. |
| D8 | Model property set generated from the 1.0 JSON schemas and enforced by a contract test. | The only reliable route to "full 1.0" and staying there. |
| D9 | Fluent list helpers via source generator; relationships via lambda builders; C# 14 extension members. | Keeps ~100 helpers in sync; preserves the owner's fluent style. |
| D10 | Vocabulary as JSON registry + generated constants. | Fixes duplicate-id errors; enables label checks; shared with the later aligner. |
| D11 | Examples become a library + test project + manifest; site bugs tracked as `SiteIssue`, never hacked around. | Repeatable 100% coverage measurement; upstream fixes. |
| D12 | Service and site are stateless containers; LLM and capture are opt-in. | Simple hosting; deterministic caching; privacy. |
| D13 | Corpus is CC0 for authored entries, imports carry their MIT/ODC-By attribution. | Publishable artefact. |
| D14 | Target net10.0 only. | LTS; owner's toolchain; no known consumers on net8. |
| D15 | Property *names* per class follow the 1.0 JSON schemas; *cardinality* and a handful of extra properties follow the site's validated examples where the two disagree. Each such case is documented in the model and reported upstream. | The schemas are the contract we can test against; the site is what consumers compare with. Extras listed in doc 02 §2 and docs/upstream-issues.md. |
| D16 | `Set`, `Type`, `Material`, `Language`, `Currency`, `MeasurementUnit` are full classes; embedded activities (`Production` etc.) derive from `EmbeddedActivity`/`ActivityBase`, not from `Activity`. | Owner's choice (2026-10-13); the schema gives embedded activities a smaller property set than `Activity`. |
| D17 | The vocabulary registry blesses only linked.art's published required/recommended/optional terms plus documented structural terms; no instances (TGN places, ULAN actors) and no dataset-specific concepts. Examples construct those inline with `Aat.Term` / `Tgn.Place` / `Ulan.Person`, as a practitioner would. | Owner (2026-10-13): examples should imply actual practice; a constant for Rembrandt or Amsterdam misleads, while qualifiers and classifications deserve re-use. |

The alignment toolkit's decisions, the owner's numbered policies from 2026-10-17 on, are in doc 10 section 13.

## 4. Repository layout

```
linked-art-net.slnx
Directory.Build.props, global.json, .editorconfig
src/LinkedArtNet/                 model, serialisation, vocabulary, LinkedArtDate
src/LinkedArtNet.Dates/           parser (AST, canonical, realiser, strategies, profiles, lexicons, named periods)
src/LinkedArtNet.Dates.Llm/       IDateInterpreter reference implementation (optional package)
src/LinkedArtNet.Agents/          the agent-string parser (profiles, honorifics and tables, Person/Group builder)
src/LinkedArtNet.Examples/        one method per site example
src/LinkedArtNet.Dates.Service/   Kestrel API (timespan, agent, profiles, advise)
src/LinkedArtNet.ExamplesSite/    Razor Pages site + playgrounds + trainer
src/LinkedArtNet.Alignment/       the alignment store (PostgreSQL), loaders, the reconciler, review and curation services
src/LinkedArtNet.Workbench/       the review workbench (Razor Pages; sign-in, projects, desk, entities, variants, export)
tests/*.Tests/                    per project (eight; the Alignment and Workbench tests use Testcontainers PostgreSQL)
tools/SchemaInspector, ExampleManifest, ExampleSourceExtractor, VocabGen, FluentGen, Corpus, Align
corpus/                           ground truth (dates and agents)
vocabulary/                       aat.json, tgn.json, ulan.json
examples/manifest.json, cached/   site JSON snapshots
profiles/, periods/               parser profiles; PeriodO and the named-period tables
docs/                             these documents
```

## 5. Risks

- **Site drift**: linked.art examples change; cached JSON refresh is a scheduled job and diffs are reviewed, not
  auto-accepted.
- **Grammar sprawl**: the idiom space is large; the corpus-driven loop and the salvage/interpreter fallbacks keep the
  grammar focused on high-frequency forms.
- **Policy disagreement**: bands and conventions are subjective; profiles make disagreement explicit and testable
  rather than argued in code.
- **Imported corpus quality**: yearrange has known wrong expectations; imports stay `proposed` until reviewed.
- **Rule sprawl in the reconciler**: 104 findings in eight days; a new rule now breaks old decisions more often than
  it mends new ones (finding 98 took 48 right places with one wrong one, 2026-10-26). The stopping criterion and the
  run-to-run diff are the guard; new rules come from the desk's decisions, not from samples.
- **The desk's cost**: the queue of some 4,000 is a person's work; the desk must make each decision cheap, or the
  residue stays minted and provisional.

## 6. Open questions for the owner

Owner's initial responses recorded 2026-10-13; items marked *open* are still to be decided.

1. Century convention default: `Popular` (1800-1899) as proposed, or `Strict` (1801-1900)?
   **Response:** must be configurable (it is, per profile). Default stays `Popular` until told otherwise.
2. Band defaults: thirds for centuries (0-33/34-66/67-99) and 0-3/3-6/7-9 for decades?
   **Response:** agreed.
3. Should `before X` / `after X` emit only one bound (proposed) or also clamp with a plausible other
   bound (e.g. `OpenEndPolicy.ClampToYear`)? **Response:** one bound. (`ClampToYear` remains an option.)
4. Label policy default: `_label` only (proposed) or also an `identified_by` Name? **Settled in practice:** the
   default is `_label` only, with `labelPolicy=labelAndName|name` per request and per profile.
5. Hosting target and domain names for the service and site. **Resolved 2026-10-15/16:** linkedart.net,
   dates.linkedart.net, a Mythic Beasts VPS; see [09-hosting.md](09-hosting.md).
6. NuGet package ids: `LinkedArtNet` and `LinkedArtNet.Dates` as proposed? **Response:** yes.
7. May PMC date strings (from the local Postgres) be included in the public corpus? *Open (owner to check).*
8. Keep the class name `LinkedArtTimeSpan` (avoids `System.TimeSpan` clash) or alias to `TimeSpan`
   inside the namespace? **Response:** keep `LinkedArtTimeSpan`.

Only question 7 is open.

## 7. Open items, by area

Each item carries the date it was seen and where its evidence is. An item leaves this list when it is built or closed,
and the log (doc 15) records that. The order within an area is rough value.

**Workbench and desk** (doc 12 sections 5 to 8 and 11):

- ~~One action per group~~ (built 2026-11-06, the owner at the next Chris Beetles string: the group's tick and action in
  its header, "Accept these as one" or "Same as X", "Always, in this field" beside it; the rows of a group of several
  carry only "just this one"; the words say "group"; doc 12 section 5, doc 13).
- ~~Evidence overtaken by a merge~~ (built 2026-11-06: a line above the candidates when an entity in the evidence has
  merged since, "now merged into X" on its row, "the same entity as a row above" on a second row for one entity;
  doc 12 section 5, doc 13).
- ~~The desk finishes a name before it leaves it~~ (built 2026-11-06, the owner after the first merge: "Also waiting
  under this name" on every item, the next item drawn from those first with the banner saying so, the entity page's
  "Waiting at the desk under this name"; `QueuedByKeysAsync` in the store; doc 12 section 5, doc 13).
- ~~A group that holds two of the project's own entities~~ (built 2026-11-06, the owner at "Chris Beetles Ltd": every
  card of the project's own says its class and status, what put it in its group and what it stands for, with the
  records behind it; a group holding two firm entities of ours says the store disagrees with itself and offers the
  merge; "Accept the ticked together" with two or more of ours shows the plan and asks; doc 12 section 5, doc 13).
- Twins that share a record (2026-11-05: "Lawrence" in corpauthor on the desk and "Lawrence" in publisher as a search result, two placeholders, the same three Crewkerne sale catalogues behind both, the name in both fields of each record). When another placeholder of the string's name has a record in common with the string's own, the desk says so on its card or search result ("in the same records as this string"), as a hint towards "Same as" or "Accept together"; the reconciler keeps twins apart on purpose (finding 104), so the hint is the desk's, not a rule. The ticks are on the candidate cards only; a twin met among the search results has "Same as this" and nothing to tick, and the hint must say which action it points at where it appears.
- The publisher strings without a record behind them (2026-11-05: the entity page of a placeholder "Stanley", one string, publisher, no "In the records"; the collection covered 10,608 of the field's 11,141 strings, this one not among them). List them beside what the export's publisher element holds for the same records and find the cleaning the 2024 process applied, as the cleaned-keywords table closed the keywords gap; a `collect-examples` report of the export values that matched no string would do the listing.
- ~~Help for the reviewer who asks what to do~~ (built 2026-10-26 evening, the owner's ask at the desk: a fold at the
  top of the desk, "What am I supposed to do here?", a Help link in the bar, and `/help` rendering doc 13 from the
  embedded committed text; the search under "Something else" pre-filled with the name as read).
- ~~The possessive firm name as a candidate~~ (finding 105, built 2026-10-26 evening, doc 10 7.6 "Built 2026-10-26
  evening, finding 105": "Dowell's Ltd" offered for "Mr Dowell" at 0.90, shown and never carried alone; measured by
  the next run, and seen at the desk once the 500 of run utt9xm are re-decided, Commands on return item 4).
- ~~"Look again" at the desk~~ (built 2026-10-26 evening: a link beside the decision date re-evaluates the string
  under today's rules and shows the fresh evidence with what the outcome would be, deciding nothing; the actions act
  on what is shown).
- The possessive key of an acronym (finding 105's follow-up, run evhvuw): "ICA" went from 82 to 102 groups through
  the key "icas"; a name written in capitals gets no possessive key. One line and a test line, with the next library
  change.
- ~~ULAN's "1200~-~2080" on the desk~~ (2026-10-26 evening, the owner: confusing on every undated owner. It is
  ULAN's estimate for "unknown", a window the reconciler keeps to rule matches out; `Lifespan.Display` now shows
  nothing for a window alone, a dated side alone when the other is a window ("1952-", "-1748"), and the source's own
  text when both sides are dated or the dates are working dates; the desk, the search results, the entity and
  variants pages use it).
- ~~The banner's link to the entity just decided, and an index search on the entity page~~ (built 2026-10-26
  evening, the owner's ask: after an action the banner on the next item names the entity with a link, and the entity
  page searches the local indexes with "Add as identifier" on each result, so a Wikidata record seen under the desk's
  "Something else" can be added before going on).
- ~~"Accept together"~~ (built 2026-10-26 evening, from the owner's FYI at "Christopher Cock": a "together" tick on
  each card and one action that makes one entity with every ticked identifier, one of the project's own entities
  allowed among them as the base). Behind it, two reconciler items: LoC's courtesy heading "Surname, Mr. (Forename)" read as "Surname,
  Forename" by the matcher (it scored 0.93 for the "Mr."; size S), and, to measure before building, a join for an
  undated string whose two sources hold one exact record each with compatible dates (LoC's working span inside ULAN's
  window), the shape of finding 79 without the string's own dates; namesakes are the risk.
- An identifier on "Keep as local authority" (the same desk item): the NLS archive is the one authority for
  Dowell's Ltd (https://manuscripts.nls.uk/agents/corporate_entities/1476), and the entity page already takes a URI
  as an identifier (since 2026-10-26 evening it takes an ArchivesSpace agent's public URL on any host, by the shape
  of the path, source "archivesspace"; before that the namespace guard refused NLS's); the keep-local form wants an
  optional "Known elsewhere (URL)" so the reviewer records it in one step. Size S. Behind it, the owner's thought:
  NLS runs ArchivesSpace, and an ArchivesSpace instance's agents are a source like any other (a public JSON API per
  agent, `/agents/corporate_entities/{id}`). The shape is fixed and the hosts vary, so the proper form is a list of
  instances in the project's manifest, each with a name and its host, so that NLS's agents read as "nls" and an
  unknown host is a question rather than a match; a remote source for the residue (section 7, reconciler, 3b) is the
  shape for the lookup. Not before the desk has shown how often an archive's agent is the answer.
- ~~The record behind the string, at the desk~~ (built and collected 2026-10-26 evening, doc 12 section 5 "The
  records behind the string": `string_examples`, schema version 8; `tools/Align collect-examples` with the 2024
  process's cleaned keywords; the desk's "In the records" cards; every context at or near full coverage, Commands on
  return item 5). A note on the source column: the March and October exports are the same catalogue with the same
  record ids, so collecting both did not put them side by side; the later collection overwrote the earlier's rows
  for shared records, and the source names which export a record was last seen in. One export at a time is the
  intended use; the thousand records only the October export has keep its name. Whether the cards show what the
  desk needs is the first desk session's reading.
- ~~The 88 strings queued on 2026-10-18 and never re-decided~~ (done 2026-10-26 evening: run-20261018-152324-k7sdqb
  undone, its 500 decisions and 221 unreferenced provisional entities; run utt9xm re-decided the 500 under today's
  rules in nine seconds: matched in store 283, accepted 41, queued 61, minted 107, overridden 7; the desk's list
  4,093). "Messrs Langford" is now exact-name on the project's group "Langford" (finding 37's courtesy prefix), to
  check on its entity page. The jobs step still wants a "re-decide strings decided before run X" operation. Seen in
  the 500: (a) stale labels on provisional entities from the first runs' readings, kept because another context's
  string points at them ("St Mary's Church" matched the project's "Mary's Church", "St Barbe Museum & Art Gallery" its
  "Barbe Museum & Art Gallery"; today's parser keeps "St" on a body under every profile): a housekeeping job to
  relabel a provisional entity from its strings' current reading, or the desk; (b) legacy entities typed Person that
  are plainly bodies ("Chiswick Auctions", "Castle Howard", "Burlington Magazine", "Warburg and Courtauld
  Institutes") answering corpauthor strings by exact name, with the legacy class item below: a retype job for
  entities whose label is plainly a body; (c) a corpauthor string that is only "(Firm)", queued on four LoC
  lookalikes, for a skip rule or the desk; (d) an exact ULAN record queued beside an LCSH lookalike at 0.85 ("Royal
  Pavilion (Brighton, England)", "Huis ten Bosch (Hague, Netherlands)"), which finding 48's lookalike rule may not
  reach in corpauthor; the desk first, a finding if it recurs.
- The first desk session (section 0, Commands on return, item 1) and what it shows: each shortfall becomes a desk
  item here and is built. Doc 13, the user guide (2026-10-18), is revised after it: it predates placeholders, the
  second pass and the evidence shape as it now is.
- Step 3, jobs: runs, loads and indexes pages (`authority_sources` is the indexes table), progress, undo, a schedule,
  the counters list; the shadow comparison table; the audit as a review list; the per-context redo as a job
  (2026-10-19, 2026-10-20).
- Step 4, projects and the pipeline runner, as extended by doc 14 section 4: sources and uploaded dumps, the profile
  job, contexts namespaced by source, role routing, mapping tables as overrides, the brief's tables imported as
  credited overrides (2026-10-19).
- Steps 5 to 7: reports outward (the audit's findings about the authorities themselves, for Getty and LoC), the
  trainer moving in (date and agent items on the desk), pictures (coverage by context run over run, the equivalents
  graph, places on a map).
- Compounds: export as coordinated concepts (doc 10 7.5 item 2), the entity page listing a heading's parts and the
  desk showing a part's parents (7.5 item 4) (2026-10-20).
- Keying workbench users by GitHub id (`github:<id>`, the local user `local:<name>`), with the first user besides the
  owner (2026-10-21).
- Legacy data for the desk: entities whose class disagrees with their label ("Crimean War, 1853-1856", the two World
  Wars, "Americans", "Archaeology" imported as Place); legacy duplicates among places (two aligned "Washington"
  entities, two "Sheffield" entities, place/c4c4mbm7 with VIAF 133706141 and place/djtugwgt with Wikidata Q42448 and
  VIAF 124322655; two "Evanston" entities seen 2026-10-26), for merge-duplicates or the desk, or the crosswalk;
  decision 17's three ("Clayton" to Victoria, "Lincoln" to Nebraska, "Stratford" to Virginia, each the project's one
  entity of the name) (2026-10-24).

**Reconciler** (doc 10 sections 7 and 7.6):

- Place drift between runs (2026-10-20, seen again 2026-10-26): the same strings move between runs as the order makes
  entities firm or not ("Stratford" and "Stratford, Va.", the two Jamestowns in awn284); undo and redo are not
  idempotent on the store side. Look at it with the store-stage work.
- ~~Findings 108 and 109~~ (built 2026-11-06, doc 10 7.6 "Built 2026-11-06, findings 108 and 109": the heading as written
  carries a tie in the heading fields, a name with an honorific carries a tie when one tied record bears it whole as a
  name and is of the trade, the manifest's `trades`; the natural-order form and the placeholder ties left out;
  measured by run a98vpa, doc 10 7.6 "Measured 2026-11-06 (run a98vpa)": 71 queued strings to single, and 77 votes
  relabelled single because the rule's tie was any live group at the top score, a queued namesake's placeholder among
  them, which the acceptance line votes past; the rule stands aside wherever the line would accept, built the same
  afternoon, and run jr8hf5 showed the 77 back to vote on the same entities, nothing else moving).
- ~~The second pass's silent exact-name onto an entity the first pass made from a qualified heading~~ (built 2026-11-06
  as finding 110, doc 10 7.6 "Built 2026-11-06, finding 110": an entity with records answers a queued string silently
  only when the string's own index stage puts one of its records at the top; Finding110Tests; the next run to show
  seven strings from exact-name to queued and nothing else). The measure that shaped it, kept: whose bare label the
  string bears while the first pass had left it tied at 1.00 among others ("Apollo" bare, tied between ULAN's Apollo and
  LoC's "Apollo, 1980-", taken in the second pass by the "Apollo (Deity)" entity finding 108 made minutes earlier, a
  record the first pass scored 0.85; run a98vpa). The standing policy, not the rule, and a record that lost in the
  first pass should not answer silently in the second. Count the second-pass exact-name decisions onto an entity whose
  label is not the string before any rule:

      with q as (select distinct source_string_id from decisions where run_id = 'run-20261106-125611-a98vpa' and method = 'queued'),
           x as (select d.source_string_id, d.entity_id from decisions d join q using (source_string_id) where d.run_id = 'run-20261106-125611-a98vpa' and d.method = 'exact-name' and d.valid)
      select count(*), count(*) filter (where e.status = 'provisional') as onto_provisional, (array_agg(s.raw || ' -> ' || coalesce(e.label, '?') order by s.raw))[1:12] as examples
      from x join source_strings s on s.id = x.source_string_id join entities e on e.id = x.entity_id
      where lower(coalesce(e.label, '')) <> lower(s.raw);

  Counted on a98vpa (the owner, 2026-11-06): 87, three onto provisional entities, and the examples mostly the good
  radiating of a qualified entity's bare name ("Abbotsford" to Abbotsford (Roxburghshire), "Chester" to Chester
  (England), "Boughton House" to the Northamptonshire one, "[Farebrother]" to the auctioneer), with Apollo's shape
  and one class crossing ("Cawdor Castle (Tourism) Ltd" to the castle) among them. The sharper count, after the next
  run: of these, the ones whose first pass had a live group at the top score the entity's own records were not in
  (Apollo's: the deity scored 0.85 under two at 1.00), which is the shape to rule on; the rest stand:

      with q as (select distinct on (source_string_id) source_string_id, evidence from decisions where run_id = '<run id>' and method = 'queued' order by source_string_id, id),
           x as (select d.source_string_id, d.entity_id from decisions d join q using (source_string_id) where d.run_id = '<run id>' and d.method = 'exact-name' and d.valid),
           g as (select x.source_string_id, x.entity_id,
                   max((grp->>'score')::numeric) filter (where coalesce(grp->>'vetoed', 'false') <> 'true') as top,
                   max((grp->>'score')::numeric) filter (where coalesce(grp->>'vetoed', 'false') <> 'true'
                     and exists (select 1 from jsonb_array_elements(grp->'members') m join entity_equivalents eq on eq.uri = m->>'uri' where eq.entity_id = x.entity_id)) as held
                 from x join q using (source_string_id), jsonb_array_elements(q.evidence->'groups') grp group by 1, 2)
      select count(*) filter (where held is null) as record_not_a_candidate, count(*) filter (where held < top) as record_below_top, count(*) filter (where held >= top) as record_at_top,
             (array_agg(s.raw || ' -> ' || coalesce(e.label, '?') order by s.raw) filter (where held is null or held < top))[1:12] as examples
      from g join source_strings s on s.id = g.source_string_id join entities e on e.id = g.entity_id;

  Counted on jr8hf5 (the owner, 2026-11-06): 73 at the top, 11 onto an entity with no records (provisional lookalikes,
  the standing policy, no rule), 7 below the top, read by name: four right ("Boughton House", "Sudeley Castle",
  "Western Isles" to the project's place of the name, its heading at 0.85 for the qualifier the string lacks; "Messrs
  Ellis & Smith" to Ellis & Smith) and three wrong ("Apollo" in publisher to the deity under ULAN's body at 1.00;
  "City Art Gallery, Leeds" to a City Art Gallery of no city; "Sarah Roberts" in natural order to a dated "Roberts,
  Sarah"). No score separates the four from the three (Apollo and the three places all at 0.85), so the rule proposed
  is the plain one: in the second pass an entity with records behind it answers a queued string silently only when
  the string's own index stage puts one of its records at the top score; otherwise the string stays queued with the
  entity shown on its first card. Seven strings, four of them a click each at the desk, three silent errors stopped.
  The owner's word given the same afternoon, and built.
- The placement join missing LoC's English form against TGN's vernacular label, to measure (seen in the forty: "Crete
  (Greece)" tied with TGN's Kríti, "Picardy (France)" with Picardie, "Cumberland (England)" with TGN's Cumberland,
  "Cambridge (Mass.)" with TGN's Cambridge in Massachusetts, each the same place in two groups, so that finding 108
  would take LoC's record alone and lose TGN's id). Count the queued place-field and keywords strings whose top tie
  holds a LoC heading and a TGN record whose chain contains the heading's parenthetical, before any change to the join.
- Two project entities of one name tie (v6gjx5: "New York" bare, "New York, NY", "New York (Madison Avenue)" between
  the city's entity and the state's the run made; "Christopher Gibbs" between two persons): for a place, a bare name or
  a qualifier that is no level word means the inhabited place, LoC's own convention; persons stay the desk's. And the
  entities of different classes (a98vpa: "Colchester (England)" between the project's place Colchester and a body of
  the name the first pass made from a publisher string, three groups at 1.00, finding 108 declining over project
  entities), where the heading's own class says which. Count the strings queued between two project entities at the
  top score before building it; the different-classes part of that count:

      select count(*), (array_agg(s.raw order by s.raw))[1:12] as examples
      from decisions d join source_strings s on s.id = d.source_string_id
      where d.run_id = 'run-20261106-125611-a98vpa' and d.method = 'queued' and d.valid
        and (select count(distinct split_part(m->>'uri', '/', 4))
             from jsonb_array_elements(d.evidence->'groups') g, jsonb_array_elements(g->'members') m
             where coalesce(g->>'vetoed', 'false') <> 'true' and m->>'stage' = 'store'
               and m->>'uri' like 'https://data.paul-mellon-centre.ac.uk/%' and m->>'uri' not like '%/unrec_%') >= 2;

  Counted on a98vpa (the owner, 2026-11-06): 35, all of them place names ("Birmingham (England)", "Birmingham (West
  Midlands)", "Cambridge" twice, "Colchester (England)", "Compton (Surrey)", "Coventry", "Derby", "Durham (County)",
  "Durham (England)"): the project's place of the name beside a body of the name the first pass made, and for the
  qualified ones the heading says the place. Before the rule: read two on the desk after the next run ("Colchester
  (England)" and "Birmingham (England)"; the cards say which entity holds what and why the store stage declined), since
  the shape of the rule depends on whether the place entity is aligned or a bare provisional, and on what made the
  bodies. First look (the owner, 2026-11-06, after jr8hf5): the two entity pages are the placeholders of the queued
  keywords:head strings, both classed Type because keywords:head reads a Type first (a small wrong of its own, below);
  and a "Birmingham (England)" in corpauthor is a jurisdiction used as a body ("Birmingham (England). City Council"),
  read as a person or group with ULAN's owner named Birmingham at the top. The keywords:head review pages, with the
  store cards, still to read; and where the corpauthor string came from, since the collector takes each corpauthor
  element whole:

      select s.context, s.raw, s.occurrences from source_strings s where s.dataset = 'pmc' and s.raw like 'Birmingham (England)%' order by 1, 2;

- A queued string's placeholder takes the context's first class (keywords:head: a Type), and a qualified place string's
  placeholder ("Colchester (England)", "Birmingham (England)") is a concept on its entity page until the desk decides it.
  The placeholder's class should follow the reading where the reading is plain: a qualifier that places it, a place.
  Count the queued strings with a placing qualifier whose placeholder is a concept, before changing MintAsync.
- The remote sources (3b), LUX self-echo first (doc 10 section 8): also what a place shadow needs, since the 2024
  places carry VIAF, Wikidata and LUX ids and never TGN (2026-10-20).
- The 2024 shadow again: a shadow sample after the stricter date agreement, and a human sample through the desk
  (2026-10-19); the shadow's eight 2024 disagreements are in the log (2026-10-19).
- Outvoted rivals for people and bodies on name alone (built for places, the owner's decision 2026-10-20): only
  after a shadow against the 2024 alignment says what it would do to them (2026-10-24).
- Finding 75's veto could still fall on a record dated only by finding 88 when a sibling of the same source agrees on
  its heading's own dates ("Charlton, Alan, 1948", Clatworthy, Richards in bp8unt's diff; 2026-10-24 night; noted, not
  built).
- "Colours (Great Britain)" and "Humber (England)", queued since finding 103 (2026-10-26): whether a nation qualifier
  on an idea is a placement or a national body's qualifier is the desk's reading first.
- The `decisions` printer shows three groups of three members; order tied groups with the context's home source
  first, or print the home record always (2026-10-20).
- Hyphen and space in keys (finding 8, 2026-10-20): `StringKey.Normalise` with hyphens as spaces and a full reload of
  every source; waits for the next full reload.
- The placeholder consolidation's fuller form (every ProvisionalQueued candidate out of the post-passes, twins'
  included) was declined on 2026-10-26 for the minimal form (finding 104); recorded in doc 10 7.6 in case the twins'
  placeholders ever need the same.

**Store** (doc 11):

- Store-stage speed: 1,012 s of a 33-minute run on 2026-10-26 (738 s of 19 on 2026-10-19); plan the entity side on the
  real store as the authority side was.
- Npgsql's infinity conversion off (year 1 became -infinity; LoC n82239358) (2026-10-19).
- A run that dies marked interrupted at the next start, not left as "running or interrupted" (2026-10-19).
- Per-context redo (`redo --context place [--method minted]`): invalidate the matching valid decisions, remove the
  provisional entities only they held, reconcile those strings again; a job on the runs page. Constraint (2026-10-20):
  equivalents carry the run id, not the decision id, so a redo by context cannot take back the ids other contexts'
  decisions added in the same run; either equivalents get a decision id (schema change) or the redo is measured with
  the store hidden, as the shadow is.

**Sources and indexes** (doc 11 section 8):

- The Wikidata index's next uses (doc 11 8.11; the extract is not yet among the manifest's sources): (c) the crosswalk,
  two candidates from different sources both linked from one Wikidata item are one entity, joined as a VIAF cluster
  would be (18,220 items carry TGN and LoC, 67,069 ULAN and LoC); (d) Wikidata labels count as variants only, never as
  a heading match (London's record carries "Augusta", "Lon." and "LDN"). (a) prominence is built (finding 85) and (b)
  dates for LoC's undated records was done as a write into the LoC index (finding 88, 2026-10-24); the LoC loader
  should learn finding 88's step so a reload keeps the dates.
- The wider Wikidata, for the entities no authority has (the owner's question, 2026-10-23): the loaded extract keeps
  only items carrying another authority's id; the name-only item is left out. In order: (1) measure first, fifty
  minted and fifty queued persauthorfull names asked of `wbsearchentities`, counting items with compatible dates; (2) a
  remote source for the residue only, rate-limited and cached into `authority_records` as `wikidata`, run on demand
  (`reconcile --remote wikidata`), a name-only item accepted for a person only with agreeing dates and for a place as a
  prominence tie-break; (3) a Wikidata search on the desk. Widening the extract to every human (eleven million) is the
  alternative and not first.
- Audit: entities against their LoC records as well as ULAN; ULAN's person-typed corporate bodies (1,552) and stale
  LUX ids to report (reports outward, doc 12 step 5) (2026-10-19). ULAN's "Knight, Laura" lists "Orovida" among its
  labels with Laura Knight's dates (Orovida Pissarro is 500008117): a candidate for the same report (2026-10-26).

**Parsers** (doc 10 section 6; docs 03 and 06):

- The Agent parser as a first-class output (the owner's thought 2026-10-21, the go 2026-10-22; objectives 2 and 6),
  size S each: (a) a service endpoint beside the dates one, taking a string, a profile name and an optional class hint
  in the manifest's three strengths (none; an ordered list, expect the first and allow the rest; one class only),
  returning the parsed agent (name, lifespan with the dates parser's result nested, roles, nationalities, kind,
  confidence) in the shape the reconciler reads, and saying when the parser's kind contradicts a hard hint; (b) a
  playground page on the examples site beside the dates playground, with the profiles and PMC-shaped examples; (c) a
  corpus folder for agent strings in the dates corpus's shape, each entry nesting a dates expectation for its date
  substring (doc 10 section 13, decision 5), seeded from the PMC strings the reconciler has read, and the trainer page
  extended to it. Not started.
- Agent parser gaps from the runs: "The Hunterian" and "Hunting Art prizes" read as Person; "Mainz/Rhein", a slash
  variant (2026-10-20); "S. W. [(Simon Wilson)] (editor)" read as "S. W.", a bracketed parenthetical of capitalised
  words after a name of initials being the fuller form; "Trent)", a fragment with an unbalanced bracket from a split
  heading; "Royal Academy of Music (London, England : 1719-1728);" with a trailing semicolon; "Royal Collection
  (London)" against LoC's "(Great Britain)", a body's qualifier being its seat (the qualifier walk could place a city
  in its country) (2026-10-23); the corpauthor rule drops a parenthetical ("Boughton House (England)") without reading
  it as a qualifier, so the store stage compares no placement there (2026-10-24); "L (colonnade)" in the place field, a
  fragment (2026-10-24); the hyphenated "Country-City" form ("Portugal-Lisbon", "England-Coventry"), which a qualified
  context could read again with the first part as the qualifier when no index has the whole (2026-10-20); "Vt."
  resolving to the Holy See in the ancestors walk, harmless since the correction of finding 98 (2026-10-26).
- Dates parser leftovers (Phase 2): `interpret` over the server's capture file once the API key is in the server's
  `.env`; benchmarks and the perf doc; `during: Period` with PeriodO URIs on TimeSpans; PeriodO narrowing (place,
  authority) as profile settings; multilingual coverage; the four-point (`EmitInnerBounds`) polish.

**Exports and reports** (doc 10 sections 7.5 and 9; doc 12 step 5):

- Decision 15, the LUX export's shape (section 0).
- A reviewed Wikidata contribution export of confirmed local authorities: a QuickStatements batch of label, dates,
  roles, nationality and the archive's URI as the reference, for a person to review and submit; confirmed entities
  only (2026-10-23).
- Reports outward to Getty and LoC: ULAN's person-typed bodies, the Orovida label, stale LUX ids (above).

**Housekeeping and release** (Phase 4):

- ~~`manifest --use-default` says what it replaced~~ (built 2026-11-06: "replacing the trades (none against auction,
  ...)" before the save, "already equals the PMC library rules; saved again unchanged" when nothing drifted, "no saved
  manifest" when the project had none).
- 1.0 releases of both packages (decoupled; NuGet publishing deferred, owner 2026-10-13); submit the parser and service
  to linked.art/software.
- Weekly scheduled jobs (site JSON refresh, Getty label check); no workflow has a `schedule` trigger yet.
- The backup scripts (nightly snapshot and the off-box pull, doc 09 section 8) exist and need installing on the server
  by the owner.
- The axe accessibility lint; renaming `de-pmc` to `main` as a deliberate later sweep (19 references across CI,
  compose, the trainer's base branch, docs and site links).
- The owner's: filing upstream issues (docs/upstream-issues.md), open question 7 (PMC strings in the public corpus),
  NuGet when ready.
