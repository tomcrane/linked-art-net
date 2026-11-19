# Plan: linked-art-net

## The short version (written 2026-11-15 at the owner's request; kept current with section 0)

**What it is for.** A .NET toolkit for producing Linked Art from catalogue data, in six objectives (section 1): the
model library; parsers that read a string with no data behind them (dates, agent names); a ground-truth corpus of
date strings with a trainer; a web service for the parsers; an examples site with every linked.art example in C#; and
an alignment toolkit that matches the names in a catalogue to authorities, with a store, a review workbench and
exports.

**What is built.**

- The model library: done, 183 of 183 site examples implemented, 182 validated.
- The date parser, its corpus, trainer and service: done and live at linkedart.net and dates.linkedart.net (v0.1.2,
  2026-10-17).
- The agent-name parser: the library and its tests, and on the working branch, not deployed, a service endpoint
  (`GET /v1/agent`), a playground page on the examples site and a corpus folder (`corpus/agents`); how far each goes
  has not been checked since they were written. The owner, 2026-11-15: "At some stage we should go back and do the
  agent parser work, including a service for that as well" (section 7, Parsers).
- The alignment store on PostgreSQL; TGN, ULAN, AAT, LoC names, LCSH and a Wikidata extract loaded locally; the
  reconciler, tuned over 124 findings (doc 10 section 7.6), which decides PMC's 45,910 strings in about 45 minutes;
  the workbench (the review desk, entities, variants, the merge tray, maps, sign-in and projects). All of it runs on
  the owner's machine only: `main` is what is deployed and is behind by the whole alignment toolkit; `de-pmc` is the
  working branch.

**Designed or planned, not built.**

- Records in the store: decisions for one record, the cataloguers' list, collecting a corrected source again (doc 12
  section 14, agreed 2026-11-15). Its first step is built (every record and occurrence collected, the desk showing
  all of a string's records in groups); the findings list, the collect's matching and record decisions are not.
- The pipeline runner: collecting strings from a fresh export as a routine step, PMC first.
- The write stage: aligned records turned into Linked Art, and the export to LUX; before it, publication as a recorded
  act and the rule that nothing published is ever deleted (section 5, the last risk).
- The workbench's jobs, runs and indexes pages; the trainer moving into the workbench; "Ask for help" at the desk.
- Aligning through the vocabularies' own APIs, for a user with no local indexes.
- Wellcome as the second project, the test that nothing is PMC-shaped; a QLever loader.
- Phase 4 hardening; NuGet publishing deferred by the owner.

**Where it is going, as the owner has said it.**

- PMC's library, archive and photographic archive records published as Linked Art and exported to LUX, on a schedule
  that repeats (export, reconcile, write, import, again next quarter).
- The project's entity store as the local authority that librarians and archivists correct their MARC and CALM
  records against (doc 12 section 13).
- A tool for other institutions: projects, members, a manifest per project; not a PMC tool.
- Two uses of equal weight: alignment for transformation, and showing cataloguers what their sources could say better
  (2026-11-15).
- Seeing and understanding the work done well enough that the software "could prove popular" (2026-11-15).
- Beyond this repository (the owner, 2026-11-15): "I have other goals but I suspect they fit outside of this project;
  they start from the linked art outputs of this work, so they'll be in another repo eventually." So this project's
  end is good Linked Art out, and what is built on it is elsewhere.

**Not stated anywhere yet** (the assistant's list, for the owner when it matters): who hosts the workbench for another
institution, or whether each runs its own; whether it is to be a product, a service or open source only, and when;
whether PMC serves its own Linked Art publicly after LUX.

The brief is in `exclude/design.md` (gitignored); this plan and the design docs in this folder are the committed
record. The day-by-day record is [15-log.md](15-log.md).

How this document is kept: section 0 is rewritten at the end of every working day and never appended to; what happened
in the day goes to the log (doc 15) and the findings to doc 10 section 7.6. Section 7 lists what is open, by area, and
loses an item when it is done (the log records it). Sections 1 to 6 are the plan as proposed on 2026-10-13, kept
current in their status lines.

## 0. Resume point (written 2026-11-19 evening; the one place to read first)

**State.** The last full run is ykb9jd (`run-20261115-123145-ykb9jd`); no reconcile has run since, and nothing
built on 2026-11-19 wants one: the day's additions to the card are looked up when the card opens. Schema version
12 on the real store. 3,764 strings queued. The way of working is the assistant driving the workbench as a user,
signed in as `claude`, the owner reviewing after; on 2026-11-19 it widened, at the owner's word, from card by
card to a survey of the whole queue and two trials of help on a set. The day's commits are local on `de-pmc`, the
full test loop green at day end; `git push` is the owner's word.

**What 2026-11-19 found** (section 7 has each in full, under "The survey of the queue" and the trials):

- The queue waits on judgement with the record in view, not on a missing rule: 37% are level namesakes none of
  them the project's, 29% one candidate from one source, 19% namesakes with a project entity among them. The 2024
  import accounts for about 5% of it.
- Trial 1, 49 publisher strings (is the body below the people the publisher?): the assistant's firm verdicts held
  35 of 35 against the owner's marks, its "probably" 3 of 6; each miss was a heading judged by its label and not
  by its record. The body offered was the publisher in 18 of 49.
- Trial 2, 100 publisher strings with a longer form already decided (is the string that entity?): same 62,
  probably 13, not 19; the owner agreed with all of the first 49 marked and stopped there
  (`reports/trial2-longer-forms-2026-11-19.md`, blocks 50 to 100 unmarked).
- The right body is usually in the index and not offered (ten of fourteen looked for), because a one-word string
  scores low against the fuller name. And project entities are themselves misaligned to namesakes: a count finds
  93 bodies whose heading's place agrees with none of their records' places, Country Life (88 records) on an
  academy in North Carolina among them.
- How the owner checks a verdict: the string searched for, its card, the records, then the web for the title, or
  the title with the publisher. "Very often a bit of sleuthing will reveal a confident answer."

**Built on 2026-11-19, for the owner to look at** (seen by the assistant in the browser except where said):

- The desk is "String review"; a card says why it waits for a person; "Fetch what each heading was made from".
- A search box for the project in the header ("/" reaches it, not tried): http://localhost:5200/p/pmc/search?q=Blackwell
- A search or small action that reloads its own page keeps the reader's place (seen at the desk's search only).
- On a card: beside each record, the title looked up at Library Hub and on the web, alone or with the string
  (the links not followed: Library Hub's address is a guess to be tried); "Longer forms of this name, already
  decided in this project", with "Same as"; for a publisher, "Fuller names in the indexes", with its caveat; on a
  candidate, "Already in this project: N records ... stand on this". Cards to see them on: "Blackwell"
  http://localhost:5200/p/pmc/review?id=5814, "Yale" http://localhost:5200/p/pmc/review?id=8770.
- The entity page's "N waiting at the desk" opens the desk (not tried in the browser).
- The assistant's decisions as `claude` on 2026-11-19: "Heaton, Bradford" kept local, "Desnoyers, Rosika"
  accepted, "Pisa" as the city, "Robbins, David (editor)" as LoC's "Robbins, David, 1937-"; and from 2026-11-17,
  three on its own knowledge: "Royston" to Hertfordshire, "Plaistow" kept as Newham's, "Kingston" to TGN 1016870.
- Left by hand from 2026-11-15, if not done since: "Cardiff" (`place/t7mbc5a4`), "Belfast" (`place/scrhnxf9`),
  "Raleigh" (`place/mcgyqvqf`), the Ontario "Peterborough" (`place/bk5w54aq`).

**Next, in order.** (1) The owner's word on two things put to him: the check "a body aligned to a namesake
elsewhere" (the 93; the entities to mend before "Same as" is pressed onto them: Penguin Books, Jonathan Cape,
John Donald, Pilgrim Press, Country Life); whether the publishers' table should name the body as well as the
place. (Ruled the same evening: predecessor and successor firms are aligned each to its own record, the
succession kept as data; section 7 has the ruling and what follows from it.) (2) The rest of trial 2's marks, if the
owner wants them counted. (3) The design of help on a set, to put to the owner: the set drawn from evidence the
card shows (longer forms, fuller names), the heading's own record read, the title asked of a library catalogue by
ISBN, a verdict with what it cites, a third outcome "the record wants fixing" that feeds the cataloguers' list,
and a "suggested" method confirmed by a person. (4) Decisions by record for a string that is two bodies ("Viking"
in London and New York, "Musée des Beaux-Arts" in three towns): doc 12 section 14 step 3. (5) The desk's place
queue where it was left: http://localhost:5200/p/pmc/review?context=place, at "Winchester" (string 42105). (6) The
cataloguers' list (doc 12 section 14 step 2), for which the trials gathered examples. (7) Housekeeping: section 7
pruned of built items and long since due; doc 12 a section for the Checks page, the Decide screen and the day's
additions to the card; doc 16's scenarios and pictures, stale after the day's changes to the card.

**How the driving goes** (section 7 has it in full): the workbench started by the assistant
(`dotnet run --project src/LinkedArtNet.Workbench`); sign in as `claude` at http://localhost:5200/signin (the owner
uses another browser or a private window to be `local`, the two sharing one sign-in in Chrome); the assistant
presses and the owner reviews after (the project page's Recent changes, and the entities' notes); each kind of
card reported at first and less as kinds repeat; small things mended as they are met, rules and design put to the
owner, and anything counted before it is made a rule (the one rule added uncounted that day listed fifteen right
places as wrong and came out within the hour). Tests while iterating: the alignment and workbench projects per
commit, the full loop before a push and at day end.

**Decisions owed by the owner:** 15, *the LUX export's shape* (doc 10 7.5 item 2, before it is designed): ask the LUX
team whether a source reload replaces by URI, whether records absent from a full dump are retired, and how LUX takes
an equivalent we withdraw; the merged-away identifiers (`merged_into` on 381 entities and the hand merges) are written
as equivalents of the survivor so LUX merges its copies; the dead LUX identifiers (above) belong to the same
conversation; a reviewed Wikidata contribution export of confirmed local authorities belongs beside it (section 7,
Exports and reports). 16, *"Mr Hone"* (finding 109, 2026-11-06): whether a sale catalogue's corpauthor or publisher
may be the owner whose collection was sold, as the Provenance Index names him, or is always the auctioneer; a
specialist's question, the owner says, and PMC's own. Until it is answered the rule carries a person's Index name only
when his biography names one of the manifest's `trades`, and Hone's kind stays queued, the match shown on the card.
Open question 7 (section 6). Every decision taken is in doc 10 section 13 and the log.

**Commands on return** (the assistant runs them too, from 2026-11-08: read-only SQL through `tools/pg.sh` and the
Align tool's commands, saying what it does before it does it; writes to the store only through the Align commands,
never SQL; from 2026-11-15 the assistant starts and stops the workbench itself, in its own session, saying so each
time, and looks at its pages in the browser (the owner's leave, so that work goes on while the owner is away;
deciding strings, merging and the store's content stay the owner's); it stops the workbench before workbench tests,
a commit that touches code, a reconcile or an undo, builds neither the workbench nor `tools/Align` while either
runs, and does not stop a workbench the owner started without saying. The answered queries and one-off SQL of earlier days are in doc 10 7.6 and doc 11
section 8.11; after a schema change run `init` first; `reports/` is git-ignored).

1. The desk: start the workbench against the real store (`LINKEDART_PG` set; Development allows the typed sign-in,
   the name `local` is an owner):

       dotnet run --project src/LinkedArtNet.Workbench

   then http://localhost:5200. **The desk session script** (written 2026-11-07 for the owner, who asked what to look
   for; the open desk items of section 7 are faults already found and are the assistant's to build, so the session
   is: decide strings, and notice). What to notice on any card, and paste when seen: the wrong thing at the top, or
   the right thing greyed; a candidate missing that the records plainly name; a button that says something ambiguous
   or does something other than it says; a detour (two actions where one would do, a search for what should have
   been offered); text that only makes sense if you know the code. In order:

   - The holder pairs (two project entities in one group): open each by id from the query below. Right: the card's
     head says two of the project's entities hold the group's records and offers the merge; pressing it opens a
     confirm step in view, naming the survivor, the losers, the identifiers added, and the class if they differ;
     after it, the string and the name's other strings are off the queue. Wrong: a pair offered that are two
     different things (the two Grosvenor Galleries, 1877-1890 and the later dealer) with nothing on the card to tell
     them apart; a merge done with no confirm step; the siblings left in the queue.

         select s.id, s.raw, s.context from decisions d join source_strings s on s.id = d.source_string_id
         where d.run_id = 'run-20261114-192034-wzkhhb' and d.method = 'queued'
           and (select count(*) from jsonb_array_elements(d.evidence->'groups'->0->'members') m where m->>'source' = 'store') >= 2
         order by s.raw;

   - Place names among namesakes, with the map ("Cambridge" in place, id 65756; "Newport", 65782; "San Francisco,
     CA", 53131): each candidate lettered on the map and on its card, the two lighting each other, a hollow marker
     when the coordinates are borrowed, and the unplaced named under it.
   - Ten strings each from publisher, corpauthor, keywords:sub and place (the context dropdown), deciding them as a
     reviewer would. A string you cannot decide from what the card shows is the paste that matters most: say what you
     would have needed.
   - The things built on the 13th and 14th, each once if not yet seen: a card from the minted list, searched for
     you; a decided card opened by its link (the green notice); a placeholder's page (its notice and the link to its
     string); "Keep as local authority" with a kind chosen and a Wikidata page address pasted as it stands; an
     entity's identifiers in their sets with "Remove all"; the scenarios at /help/scenarios.

2. No reconcile is pending. wzkhhb (2026-11-14 19:20) is the last full run: findings 120 to 123 measured, queued 3,722. The
   next reconcile comes with the next rule: the last full run taken back, `reconcile`, then command 4 against it. The next reconcile comes with the next rule, as always: the last
   full run taken back, `reconcile`, then command 4 against the run before, the assistant running it with the
   workbench stopped:

       dotnet run --project tools/Align -- undo-run --run run-20261114-192034-wzkhhb
       dotnet run --project tools/Align -- reconcile

3. The records behind the strings, collected again after the next library export (the same catalogue, the same
   record ids, so the newer export's records replace the older's); the publisher strings without a record, and the
   cataloguers' report ("Art, Baroque--Italy--ome", the collector and jurisdiction headings cut at a full stop), wait
   on it:

       dotnet run --project tools/Align -- collect-records --file "C:\pmc\2024-03-18\2024-03-11_library\2024-03-11_library.xml" --system library --collect 2024-03-11 --keywords "C:\Users\TomCrane\Dropbox\digirati\PMC\linked.art\pmc_subjects_csv.sql"

4. The standing method diff after the next run, against wzkhhb, the last full run (each run's last decision per
   string, since a string the second pass settled has two; an undone run keeps its evidence, though the undo removes
   the provisional entities it minted and nulls their decisions' entity, so `same_entity` reads null for those and
   true or false only where the entity survived). A method change on the same entity is a label, not a decision:

       with a as (select distinct on (source_string_id) source_string_id, method, entity_id from decisions where run_id = 'run-20261114-192034-wzkhhb' order by source_string_id, id desc),
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
- **One-way streets in the lifecycle** (the owner's question, 2026-11-07; LUX's import of the earlier export before it
  was ready the case in point): the schedule a project settles into (source export, reconcile, write, import into LUX,
  again next quarter) is not yet a path in this code (collecting strings from a fresh export and the write stage are
  designed, not built), and the faults that only show in real use live there. Named so far: a published URI the store
  can still delete (undo-run and cleanup-orphans delete a provisional entity nothing points at; the desk merges a
  placeholder into the entity it accepts, which keeps it resolvable, but only a bare provisional one); an equivalent
  withdrawn after publication that reaches the consumer as an absence, not a retraction, when a consumer that merged on
  it needs to unmerge; a merge that is permanent and a split that does not exist; a string's identity (dataset, field,
  normalised text) that cannot say "in this one record it means someone else"; a spelling corrected in the catalogue
  that arrives as a new string beside the old, which nothing retires; an authority dump reloaded with records merged or
  retired under our equivalents. The guard is publication as a recorded act and the rule that nothing published is
  ever deleted (section 7, Exports and reports), settled before the write stage is designed.

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

- ~~The second pass's reasons on a string it leaves queued~~ (built 2026-11-07: the last evaluation of each string still queued at the end of the second pass is written as its queued decision, the placeholder unchanged, with `secondPass` in the evidence; DeskEvidenceTests) (finding 110's "its records are not at the top here") are not
  persisted: the first pass's queued decision stands and the second writes none, so the desk shows the first pass's
  evidence and the reason reaches it only through "Look again". The second pass should save its evidence on the
  string it leaves queued (a new queued decision, or the standing one's evidence replaced), so the desk reads why.
- ~~An entity's class is the field's, not the record's~~ (built 2026-11-07, the owner's word, at "Ackermann and Son": a
  confirmed Person holding ULAN's corporate body, made that day by a persauthorfull cluster, the owner unwilling to
  merge a Person with a Group. The variants view's cluster now takes the record's class when an identifier given with
  it has one the field allows, the field's first otherwise (the desk's accept already took the record's class through
  the reconciler); the entity page has a class control, the change noted on the entity, and on a placeholder it says
  to merge into the entity of the right class, or decide the waiting string there, rather than change the class (the
  owner's question at the "Ackermann" placeholder); a merge whose two entities differ in class offers the class, on the
  entity page's merge row and in the desk's confirm step, the change noted (`CurationService.SetClassAsync`,
  `MergeAsync(cls)`, `AcceptTogetherAsync(cls)`, `UpdateEntityAsync` writing the class; CurationServiceTests). The
  identifier keeps its minted path. Left: a count of entities whose class disagrees with the type of the records they
  hold, the legacy side, on the legacy-classes item below.)
- ~~A queued string settled by a merge keeps saying queued~~ (built 2026-11-07, the owner's word: the firm's entity page
  after the owner merged the "Ackermann" placeholder into Arthur Ackermann & Son showed "Ackermann" in corpauthor and
  publisher decided "queued" and "Ackermann's" "minted", since a merge re-points the loser's valid decisions and leaves
  their method, so the desk would ask again. Now a person's merge, on the entity page or at the desk's confirm step,
  first decides each of the loser's queued and minted strings onto the survivor, method `human`, evidence
  "same-as-by-merge" with the earlier method, so they leave the queue and undo puts the earlier decision back
  (`MergeSettlement`; the string being decided at the desk gets its own decision after). The ones merged before this,
  the Ackermanns and any since the 5th, are listed by `settle-merged` and decided by `settle-merged --apply`; the
  count the owner pasted was the finding 111 count again, so the tool's listing is the count. CurationServiceTests.)
- ~~The confirm step below the fold~~ (built 2026-11-07, the owner at "Harewood House (Yorkshire)": after the redirect
  to the confirm step the page opened at the top and the step sat under the records and the candidates, "if I didn't
  know it was waiting there below the fold, I'd be lost". The redirect now carries the step's fragment, so the page
  opens on it, and a line at the top says a confirm is waiting, with a link to it.)
- ~~A vetoed card keeps a quiet accept~~ (built 2026-11-07: a greyed row carries "accept anyway", Accept or Same as by the row, recorded as human) (the owner, 2026-11-07, at "Hall, Harry, 1815-1882": ULAN's and LoC's Harry Hall
  of 1813-1882, the painter the string names, greyed at 0.00 for the two years' difference on the birth, with no button,
  while two undated LoC Harry Halls stood above at 1.00 and 0.85; the way round is "Something else" and a search). The
  person outranks a veto: a greyed group keeps a quiet "Accept anyway" (and "Same as" when a project entity holds it),
  recorded as human with the veto's reason in the evidence, so the catalogue's two years do not cost a detour.
- ~~A project entity on a card named with its placement~~ (built 2026-11-07: `EntityFacts.Placement` from the LoC heading's parenthetical or the TGN parents, shown on the row, in the group's button, the merge notice and the confirm step; DeskEvidenceTests) (the owner, 2026-11-07, at "Birmingham" in keywords:sub, string
  79377: two groups at 1.00, each holding a project entity labelled "Birmingham", Alabama's above England's, and both
  buttons reading "Same as Birmingham"). When a namesake project entity is on the page, the button and the row name the
  entity with its placement, read from the LoC heading it holds ("Birmingham (Ala.)") or its TGN record's parent:
  "Same as Birmingham (Alabama)", "Same as Birmingham (England)".
- ~~The card's holders, live~~ (built 2026-11-07: `ReviewItem.Holders`, the project entity holding a card's record now and not on the card, shown under the row with "Same as"; DeskEvidenceTests) (2026-11-07, the owner's question after adding LoC's and Wikidata's Arthur Ackermann & Son
  to the firm's entity by hand: "does that take effect in the desk?"). It does in the next reconcile, in "Look again",
  and in the searches; not on the stored card of a string already queued, whose project entities are the members the
  run wrote. As the desk already looks up what a card's entities have become since (a merge shows under the survivor),
  it should look up which project entity now holds each card's records and say so, with "Same as" offered, so a hand-
  added identifier reaches the queue without "Look again".
- ~~The way from a placeholder's entity page to its own desk page~~ (built 2026-11-07: the decided column links a queued string to its desk page and the title's signal counts the entity's own queued strings) (the owner, 2026-11-06, at "Colchester (England)"): the
  entity page's "Waiting at the desk" list is the other strings of the name, and leaves out the strings that resolve
  here, which sit under "Strings that resolve here" with "queued" as plain text. A placeholder whose only string is its
  own shows no way to the desk. The decided column should link a queued string to its desk page, and the title's
  signal should count it. Small; the entity page and its test.
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
- ~~Skip to the back of the desk~~ (built 2026-11-07, the owner's idea: a skipped string came back to the front next
  session. `source_strings.skipped_at` (schema version 9), set by Skip; the desk's order is the unskipped by id, then
  the skipped in the order they were skipped, and "after" follows that order (`NextForDeskAsync`); Unskip on a skipped
  string puts it back; ReviewServiceTests.)
- ~~From the sources, on demand~~ (built 2026-11-08: `Authorities/SourcePull` with a fetcher for Wikidata's entity JSON,
  then the English Wikipedia summary for its sitelink and Commons' licence and author for its image, P18 first and the
  Wikipedia page image when there is none; ULAN through Getty's SPARQL endpoint (biography, dates, nationality, note;
  the vocabulary's own JSON endpoint failed with a certificate error on their side that day); LoC's JSON-LD (variants,
  "found in" sources, same-as, the real-world object's dates); LUX's Linked Art record (names, statements, dates,
  equivalents, a polygon's centre, a Commons representation), marked a self-echo when its `equivalent` carries one of
  our identifiers. Cached under `pulled` in the record's `data` by `PutPulledAsync`, a row made when the cache has
  none; nothing fetched on page load or in a run; one identifying User-Agent, a 20-second timeout, no retry. The
  entity page's "From the sources" panel, one card per identifier, "Pull in" for all or one, each fact the source's
  own and dated, the self-echo a notice, the first picture at the top of the page with its attribution and never
  without it; the desk's "show pictures" link puts the cached thumbnails and descriptions on the cards. SourcePullTests
  against canned copies of the live answers for John Robert Cozens and LUX's "King's Gallery"; ReviewDeskTests the
  panel from the cache. Left for later: LUX's works that cite the entity as a count, a pull from the desk itself, a
  budget across a session.) Seen at once from it: a LUX identifier the project holds answers 404
  ("Peabody"'s concept neighbour, `concept/a277azwt`, 2026-11-08), the re-mint doc 10 7.5 found in October; the
  pull says the status now. Sampled the same evening (the owner's question whether the 404 went with the heading
  being a compound, "Violence--Press coverage"): of the 17,454 LUX identifiers the import brought, a sample of 15
  persons all resolved, 9 of 10 groups and 9 of 10 places, but only 5 of 15 concepts; among 24 concepts read with
  their labels, 9 of 12 compound headings were gone against 3 of 12 simple ones ("Dwellings", "Manners and
  customs", "Romanticism"). So the re-mint fell on the subject concepts above all and on the compounds hardest, as
  doc 10 7.5 saw for the "Art, Modern--19th century" family, though not only on them. An item of its own: count the
  project's LUX identifiers that no longer resolve (a HEAD
  pass with the polite client, outside any run) and decide whether they are withdrawn as equivalents or re-found by
  the PMC identifier LUX still carries in the records it kept; decision 15's shape. The design as agreed (the owner, 2026-11-08: the project's entity store is becoming the local authority
  librarians and archivists correct MARC and CALM records against, so an entity page should pull in what the aligned
  sources say, Wikidata and through it Wikipedia above all; "on-demand rather than on page-load, perhaps, or combined
  with heavy caching"). A "Pull in" action on the entity page, per identifier or once for all, fetches the live record
  for each source with a fetcher (Wikidata's entity JSON: description, aliases, image, the English Wikipedia sitelink;
  then the Wikipedia summary for that sitelink, extract and thumbnail; ULAN's biography and nationality; LoC's notes
  and sources; LUX's record, with its names, dates, equivalents and the works that cite it, for PMC and any project
  LUX holds) and writes it into the record's cached row in `authority_records` under its own key with the time; the
  page then shows a "From the sources" panel from the cache, each fact attributed and dated, with a refresh link.
  LUX's record is marked when it is a self-echo (doc 10 7.4: our own earlier export under PMC's id, or attributed to
  the PMC source), so what it says is read as our old data reflected back and not as a second witness (the owner,
  2026-11-08: "beware of our data-pollution problem"). Nothing is fetched on page load or inside a run; a polite client
  with a budget; the same fetchers become step 3b's remote candidates. Doc 12 section 13 has the comparison with the
  earlier Authority Server that never fetched them. Pictures too (the owner, 2026-11-08): Wikidata's image (P18) and
  the Wikipedia page image, kept as the Commons file name and its licence and author in the cache, never the bytes;
  shown on the entity page in the panel as one small thumbnail with its attribution and a link to the file, loaded
  from Commons' thumbnail service only when the page is viewed after a pull. The UI effect to weigh: the desk stays
  text by default, since pictures slow it and most cards have none; a "show pictures" toggle on the desk puts a
  thumbnail on the cards whose records have one, which is the one place a face separates two John Smiths. Commons
  licences require the attribution to be shown, so the thumbnail is never shown without it.
- ~~A map~~ (built 2026-11-08: `Authorities/Coordinates` reads TGN's lat/long from the record's data or a pull's under
  `pulled`; `EntityFacts.Coordinates` for the desk's project entities; the entity page shows a map at the top when any
  of its records places it, and the desk one map above the cards when any candidate is placed, a marker per candidate
  labelled with its record's label and TGN parents or the entity's name and placement, the best group's markers
  emphasised; a record with no coordinates stands at its TGN parent as a hollow marker said to be approximate, and a
  place with none anywhere is named under the map (the owner, 2026-11-08: "Windsor Castle" showed only its
  Pennsylvanian namesake, TGN giving the English castle no coordinates; `Coordinates.ResolveAsync`, `Placed`,
  `ReviewItem.Placed`); `Pages/Shared/_Map.cshtml`, Leaflet 1.9.4 from unpkg with its integrity hash over OpenStreetMap tiles,
  their attribution shown; CoordinatesTests, DeskEvidenceTests, ReviewDeskTests. Left for later: the entities list
  filtered to places on one map, and the merge tray's map when the tray exists.) The design (the owner, 2026-11-08): an entity that is a place shows where it is on a map on its page, and wherever the
  UI puts namesake places side by side (the desk's cards for "St Ives", "Boston", "Birmingham"; the merge tray; the
  entities list filtered to places) the candidates are shown together on one map, since seeing the St Iveses apart
  is how a person picks the right one. Coordinates from TGN's record (it carries them) and Wikidata's P625, cached on
  the record; a small static-tile map (Leaflet over OpenStreetMap tiles, attribution kept), one marker per candidate,
  the chosen one emphasised; no map when nothing has coordinates. Doc 12 section 13.
- ~~Notes as the audit log, History on the entity page, Recent changes on the home page~~ (built 2026-11-08, doc 12
  section 13, item 1: every hand change writes a note with before and after, label, class, an identifier added or
  removed, a confirm with the status it came from, a cluster as one note with its identifiers, a merge on both sides
  with the strings and identifiers that moved, a reopen with the strings; the entity page's "History and notes" lists
  them with the human decisions on its strings, newest first; the project home's "Recent changes" lists the latest
  notes and human decisions across the project, who, what, when, a link; `RecentNotesAsync`,
  `RecentHumanDecisionsAsync`, `CurationService.MergeNotesAsync`; CurationServiceTests).
- ~~A tray for merging across pages~~ (built 2026-11-08, doc 12 section 13, item 2: "Add to tray" on an entity's page, "Tray"
  on the entities list's rows and beside the desk's project entities; the tray a bar at the foot of the window, held
  per project in the browser (`wwwroot/tray.js`, localStorage), with the entities named, each removable, "Merge
  these" and "Clear"; the merge page `/p/{dataset}/merge?ids=` lists them side by side with strings, queued, status
  and identifiers, suggests the survivor (most strings, then the firmer status, then more identifiers), takes the
  label and, when the classes differ, the class, and merges the rest into it in one post, each merge written on both
  histories; the redirect's `#tray-cleared` empties the tray. The desk's confirm step is not repeated here: the page
  itself is the confirm, everything shown before the one button. ReviewDeskTests. Left for later: the published id as
  the suggested survivor once publication is a recorded act, doc 12 section 13, item 4.)
- ~~An identifier in use says so~~ (built 2026-11-08, doc 12 section 13, item 3: "Add identifier" on an entity page, a
  cluster's identifiers and the keep-local URL refuse a URI another entity of the project holds, naming the holder
  (`IdentifierInUseException`, `CurationService.HolderOfAsync`); the entity page then lists the holder alone under
  Merge with a line saying so, so the merge is one click; CurationServiceTests, ReviewServiceTests, ReviewDeskTests).
- A part's parents on the desk, and "not a compound" (the owner, 2026-11-07, at "New" in keywords:head, string 79768:
  the record's keyword "New-York Historical Society (New York, New York, USA)", the society's own hyphenated spelling,
  split at the hyphen into the head "New" and a sub, with ULAN's unknown-artist "New" at 1.00 and nothing on the card
  to say where the string came from). Two parts: the desk shows a keywords:head or keywords:sub string's parent
  headings, each a link to the heading's entity page (the other half of the compounds item, 7.5 item 4); and an action
  on a part or a heading, "not a compound", that reads the heading whole as one string (its parts detached from it,
  the heading re-collected in the keywords context and decided by the ladder, where LoC's corporate name takes it).
  Count first, the one-word heads, where mis-splits hide:

      select h.raw, count(*) as headings, (array_agg(p.raw order by p.raw))[1:3] as examples
      from source_strings h join source_string_parts sp on sp.part_id = h.id join source_strings p on p.id = sp.parent_id
      where h.dataset = 'pmc' and h.context = 'keywords:head' and h.raw not like '% %'
      group by h.raw order by 2 desc, 1;

  The count came back the same day (doc 10 7.6 "Read 2026-11-07, finding 112 shaped"): the export writes one hyphen
  for the separator and for a hyphen, the 2024 cleaning doubled every one, and the legacy import brought the doubled
  forms in; some fifty one-word heads through "H" are broken words ("Wood--carving", "Still--life", "Self--control")
  or broken names ("Pre--Raphaelites", "Anglo--Saxons", "New--York Historical Society", "Kit--Cat Club", the
  double-barrelled surnames). Finding 112, shaped: at the split, a "--" followed by a lower-case letter is a hyphen,
  and a one-word head whose join with the next part is a heading or name the indexes hold whole is a hyphenated name;
  a repair pass (`resplit-headings`) re-splits the stored headings under the rule, detaching the old parts and
  collecting the new; "not a compound" at the desk is the pass for one heading. The dry run is built (doc 10 7.6
  "Built 2026-11-07, finding 112's dry run"): the lower-case rule was finding 4's already and the store's parts are
  stale from before it; the splitter gained the wider prefixes and the linking words of a hyphenated name; rule (b)
  is in the pass. The owner read two listings (the second: 167, of which 33 by the splitter, 14 by the indexes, 119
  leftover rows of re-splits after finding 4, one stale; nothing wrong) and gave the word: `--apply` and rule (b) in
  the reconciler's reading are built (doc 10 7.6 "Built 2026-11-07, finding 112's repair and rule (b)"). Next: the
  owner ran `--apply` at 17:59 (run jsf3ur, doc 10 7.6 "Measured 2026-11-07 (run jsf3ur)": 167 split again, 20 read
  whole, 22 parts reconciled) and `cleanup-parts --apply` after schema version 10 (168 fragments deleted, 119
  placeholders removed); the run's decisions read (the society, the museum and the Wolds onto the project's own
  entities, Sens-sur-Yonne voted, the Franco-British Exhibition's two spellings queued as a pair). Done, except: the
  desk's view of a part's parents and "not a compound" as the pass for one heading, below; and "Art, Baroque--Italy--ome"
  (the "R" of "Rome" missing in the catalogue) for the cataloguers' report (Exports and reports).
- ~~Records of another class in a place's group~~ (built and measured 2026-11-13, finding 118, run q9zpqk; open only: "Northampton", the owner's to mend by hand) (2026-11-13, met writing the first scenario of doc 16: "Coventry (England)", string 82037). The first group, "6 records agree they are one thing", holds TGN's Coventry, LoC's "Coventry (England)", the project's two Coventrys, and two LoC groups, "Catholic Church. Diocese of Coventry and Lichfield (England)" (n88126694) and "Church of England. Diocese of Coventry" (nb2007002969), each "name 0.85; qualifier places it (England); placed through tgn Coventry, the inhabited place among 2". The holder pair's merge would add both dioceses' identifiers to the city (the confirm step lists them among "3 identifiers added"), and "Same as" on the group would do the same after a tray merge. Counted and built the same day as finding 118 (doc 10 7.6 "Built 2026-11-13, finding 118": 35 strings, 28 accepted by vote, the dioceses' identifiers written onto some twenty place entities by run v7aq7v; a record of another class joins only when its own name is the string's and its heading has no type word, so Balmoral Castle and the abbeys stay). Measured by run q9zpqk the same day (the method diff empty, the dioceses' identifiers gone from the place entities, the buildings' kept), doc 16's Coventry paragraph rewritten; "Northampton", confirmed by hand with a diocese and an archdeaconry among its identifiers, is the owner's to mend on its entity page.
- "Osterley Park" the publisher and "Osterley Park (Middlesex)" the subject (2026-11-14, read from run vd2cg3, and corrected the same day when the owner asked "what two?": the assistant had called the first "the import's entity, classed a person"; it is a placeholder the run mints for the string "Osterley Park" in the publisher field, of the field's class, vetoed beside the house because "a string placed in a known place is not a person"; the second is the entity the run aligns to TGN's Osterley Park for the subject heading). Two things follow. A placeholder is a run's: a class changed by hand on it, or a merge of it, is lost when the run is next taken back, so the way to join the two is to decide the publisher string at the desk ("Same as" the aligned entity), which a run keeps; the entity page says so on a placeholder since the same day (a notice at its head, with links to its strings at the desk; the edit forms left in place). And whether a house that publishes is the place or a body of its name is the owner's judgement (the University of Oxford was merged, group and place). Worth a count: publisher and corpauthor placeholders whose name is an aligned place entity's.
- An entity's label when a string is accepted with its post town or county in it (the nearby-town case built 2026-11-14 with finding 123: such an entity is labelled from the record and its first two parents; the wider question stands, 276 of the 955 place entities run vd2cg3 made carry a comma, most of them "Almeley, Herefordshire", the string as a reader would write it, and the owner's taste to say) (2026-11-14, the owner at "Donnington", string 27535: the candidate "now held by the project's entity Donington, Spalding"). A string accepted onto a record with no project entity yet mints one labelled with the string as written, "Donington, Spalding", "Crowmarsh Gifford, Nr. Wallingford", "Padiham, Nr Burnley", "Paulton, near Bristol". The label to publish is the place's, with a placement a reader can use: the record's own label and its first parents ("Donington (Lincolnshire, England)"), the string kept as a variant. Count first: the entities a run mints whose label carries a comma or a bracket that the record's label does not.
- ~~A record matched on a variant spelling listed ahead of the records that bear the name~~ (counted and built 2026-11-14 as finding 122, an order among equals only, doc 10 7.6; the count showed a rule that demoted variants would be wrong, "Rome" being TGN's Roma. Left for the desk and "Ask for help": which Donnington a saleroom's place of publication is) (2026-11-14, the same card: "Donnington" lists TGN's Donington, Lincolnshire, first at 1.00 through its variant "Donnington", ahead of TGN's two Donningtons, in Shropshire and in Berkshire; the place of publication is most likely Donnington Priory at Newbury, the saleroom). `ByVariant` exists for LoC (finding 74); TGN's variants score as the name. A record that bears the name as its heading should stand ahead of one that only lists it. Count first: queued strings whose first group matched only on a variant while a later group bears the name.
- ~~Two headings of one source in one group~~ (counted and built 2026-11-14 as finding 121, doc 10 7.6: 57 first groups with a typed heading beside the untyped, 42 accepted by vote; one record of a source stays with a holder, the others set aside. Measured by run kg6v85 the same day. Left: the handful of typed headings a run still writes where the untyped one was not a candidate or tied, "Mainz (... : Landgerichtsbezirk)", "Iona (Scotland : Parish)", "Antrim (N.H. : Town)", "Cornwall (England : Duchy)". The scenario for doc 16 has its four pictures, `docs/img/scenarios/two-records-one-source-*.png`, taken before the rule, and is to be written) (2026-11-14, the owner at "Poughkeepsie, New York", string 66289: "why are there different LoC ids?"). The group holds LoC's "Poughkeepsie (N.Y.)" (n81090425, "City of Poughkeepsie") and LoC's "Poughkeepsie (N.Y. : Town)" (n89123175), both placed through TGN's one Poughkeepsie, and the project's two entities each hold one of them, so the desk offers a holder pair. But the Library of Congress keeps two records because they are two things, the city and the town around it, and a merge gives the survivor both. A source that holds two records of a name, neither naming the other, is saying they differ: the typed heading ("N.Y. : Town") should not join the placed record the untyped one joins, or the group should say "LoC holds these apart" and the merge not offer both identifiers. Finding 118 barred a type word only across classes; these are both places. Count first: the groups of the last full run with two or more records of one source that are not each other's sameAs, and of those the ones with a type word. A scenario for doc 16 ("the authorities hold two, the project means one").
- ~~A place written with a nearby town after the comma~~ (built 2026-11-14 as finding 120, doc 10 7.6, `Reconciler.ScoreWithNearbyAsync`; measured by run kg6v85 the same day after one mending, 41 strings placed, all rightly. Left from the count: the regions the rules should place and do not, "Enfield, Middlesex", "Swindon, Wilts", "Ulverston, Lancashire"; and the strings whose right place is not among the candidates fetched, "Wilton, Salisbury", "Botley, Oxford", "Heaton, Bradford") (2026-11-14, the owner at "Bottrop, Essen", string 20654, place of publication of "The Boydell Shakespeare Gallery" (1996), publisher "Pomp": "why is Bottrop in the place field at all?"). The reconciler reads what follows the comma as a qualifier, a region that contains the place, and TGN's Bottrop is "not placed (Essen vs North Rhine-Westphalia, Germany, Europe, World)", so the string queues beside two Bottrops. Of the 186 place strings queued in run rhf5rg, 79 carry "qualifier not placed", and many are of this kind, the English postal habit of a village and its post town: "Firle, Lewes", "Chatsworth, Bakewell", "Botley, Oxford", "Dedham, Colchester", "Culham, Abingdon", "East Bergholt, Colchester", "Goodwood, Chichester", "Heaton, Bradford". Others in the same 79 are two places of publication with a comma for the manifest's semicolon ("Kingston, Montreal", "London, New York & Bombay"), an address ("London (59 Gordon Sq., W.C.1)", "Glasgow, Central Hotel"), or the desk's new nine (finding 119). A rule to count first: when the qualifier is itself the name of a TGN place whose coordinates lie within some twenty kilometres of a candidate of the name, the qualifier places that candidate ("near Essen"), as a containing region would; TGN carries coordinates for both. The count: of the 79, how many have one candidate so placed and no other. A scenario for doc 16 either way ("a village and its post town"). Counted 2026-11-14 against rhf5rg (`reports/q-near.txt`; "Bottrop, Essen" itself decided by the owner meanwhile and not in it): 71 of the 79 are a name, a comma and a qualifier (the other eight carry a street address in brackets). In 25 the qualifier names a TGN place and exactly one of the card's candidates lies within twenty kilometres of it, and all 25 are the right place: "Barnsley, Cirencester" (6.9 km, among six Barnsleys), "Chatsworth, Bakewell" (Chatsworth House, 4.8), "Dedham, Colchester" (9.4), "Egerton, Ashford" (11.8), "Goodwood, Chichester" (6.6), "St Ives, Huntingdon" (8.8, both spellings), "Stowe, Buckingham" (2.9), "Wheelock, Sandbach", "Culham, Abingdon", "Thatcham, Newbury", "West Dean, Chichester", "Chillingham, Alnwick" the farthest at 18.1. One has two candidates near ("Beaulieu, Brockenhurst": the village and the river). In 33 the qualifier names a TGN place and no candidate on the card is near: a county, state or country that the present rules should have placed and did not ("Enfield, Middlesex", "Ulverston, Lancashire", "Swindon, Wilts", "Victoria, Australia", "Murcia, Espana"), finding 119's nine, and strings whose right place is not among the card's candidates at all ("Wilton, Salisbury", "Botley, Oxford", "Heaton, Bradford", "Firle, Lewes" with no TGN candidate of the name), which is a second question, the candidates fetched, not the qualifier. In 12 the qualifier is no TGN name: abbreviations ("Cambs.", "Eng.", "N.S.W.", "Co. Down"), several places ("London, New York & Bombay", "Rutherford, Madison, Teaneck"), a hotel. So the rule as stated settles 25 of the 186 queued places, none wrongly on this evidence; twenty kilometres is the right side of the one near miss ("Tisbury, Salisbury", 21.0).
- An imported entity whose label and identifiers are of different things (2026-11-14, the owner on the page of `group/hwh4kxkg` after the holder pair "Musée des beaux-arts de Tours (Tours, France)": "it has Palais des Beaux-Arts de Lille too"). Not the day's merge: the import of 2026-10-18 brought an entity labelled "Musée des beaux-arts de Tours" that stood for the string "Palais des Beaux-Arts de Lille" and held Lille's Wikidata item (Q2628596, "art museum in Lille, France") and VIAF number; the pass through Wikidata on 2026-11-06 added LoC's "Lille (France). Musée des beaux-arts" on the strength of it. Beside it stood the true Tours entity (`group/skrtu92g`, LoC's "Musée des beaux-arts de Tours"), the desk offered the two of one label as a holder pair, and the owner merged them. Mended by hand: the Lille set removed from the survivor, the Lille string reopened and decided at the desk. Counted 2026-11-14 (`reports/q-mislabel.txt`, a first cut: the entity's label shares no word of four letters with any label of a record it holds): 14 entities where no held record agrees and 30 where some do not. About half are translations and transliterations and right ("Cappella Sistina" and Sistine Chapel, "Hofstad" and The Hague, "Mouseio Akropolēs" and Acropolis Museum); the rest are the import's wrong alignments, for the owner's desk: "Alice Austen" holding Anne Seymour Damer, "Mark Bradford" holding Richard Avedon, "Sybil Andrews" holding Cyril Power, "Rodrigo Moynihan" holding Elinor Bellingham-Smith, "Farington" holding Warrington, "Newport" holding Gwent, "Casa Garriga Nogués" holding Fundación Mapfre; and with one stray identifier, "Cathédrale de Chartres" (Wikidata's church at Louviers), "Peter Coxe, Burrell, and Foster" (30 St Mary Axe), "Treviso (Italy : Province)" (Frankfurt), "Emerald" (Seattle, three records), "Rossini, Pietro", "John Hardman & Co." (Harman and Co.), "Glyptothek München" (Staatliche Antikensammlungen), "Appledore" (the River Torridge), "Walthamstow" (Waltham Forest). To build: the audit as a page of the project (the list, each row the entity and what its records are, with the entity's page a click away), since the word test is good enough to find them and too crude to act on; and the holder pair's box saying so when the two entities' identifiers describe different things (built 2026-11-14: "Look before merging", naming the different records of one curated source each holds).
- A body that is part of another (2026-11-14, the owner at "Improvements and Town Planning Committee", string 51, corporate author of the 1944 report on the post-war reconstruction of the City of London, kept as a local authority; the record's corporate authors are that and "Corporation of London", the cataloguer's two values for one body and its part). The store has no way to say that one entity is part of another: the label carries it for a reader ("Corporation of London. Improvements and Town Planning Committee", as authority files write a subordinate body) and nothing links the two entities. To design: a "part of" between entities, set by hand on the entity page and offered by the desk when a record's corporate authors are a body and what reads as its committee, department or branch; what Linked Art says of it (a Group's `member_of`) and how the export writes it; and LoC's subordinate headings ("Westerly (R.I.). Town Planning Committee"), whose first element is the parent, as a source of the same link for aligned bodies.
- Taking a merge back (2026-11-15, the owner's merge at "Peter Nahum at the Leicester Galleries", string 112402: the identifiers of the Leicester Galleries of 1902-1977 removed from one entity (ULAN's 500301853, LoC's n2009001998, a LUX record) and the two merged, so that one entity now stands for "Leicester Galleries" and "Ernest Brown & Phillips Ltd" as well as the dealer of 1984 onward, which LoC holds apart as "Leicester Galleries (1984- )"). A merge is permanent, and the way back is by hand: reopen the strings that belong to the other body, decide them at the desk, add the identifiers again; the merged-away entity's URI still leads to the survivor. To build: "take this merge back" on the survivor's page, from what the merge recorded (the loser's strings and identifiers are in its note today; a merge should keep them as data), restoring the loser with its URI, strings and identifiers, as a noted change. The scenario "I merged what should not have been merged" of doc 16 waits on it.
- One spelling, more than one record, perhaps more than one thing (2026-11-15, the owner at "Plymouth", string 61711, a place of publication seen in three records: a Plymouth City Museum pamphlet, an 1860 notice "extracted from the Plymouth newspapers", and "Modern antiques" (c2012), "Lewisburg [Pa.] ; Plymouth", Bucknell University Press with Rowman & Littlefield: "the first and third are almost certainly Plymouth UK but the second one is uncertain - what to do about that?"). A string is decided once, for every record it occurs in; the store has no decision for a string in one record. Here the second is the same Plymouth (the assistant's own knowledge, to be checked: Rowman & Littlefield's British office was at Plymouth in Devon), so nothing is lost, but the case is real: a bare "Cambridge" on a Harvard book and on a Cambridge University Press one is one string. **The owner's ruling the same day: this is required, not an edge.** "This is likely to happen a lot and the system needs to deal with it. The record context will be used when the data is transformed into linked art (which is the point of all the reconciliation). A record-specific context is required for this, but also as a mechanism for recording what could be clarified/improved in the source data. The tool is useful both ways - for alignment to enable linked art transformation, and for finding inconsistencies in sources that the cataloguer can go back and fix for the next run. (so the whole process has to survive corrections and improvements in source data, including those that were made as a result of people using the tool!)" So, to design, and before any write stage (it belongs with the lifecycle design, section 5's risk and the Exports and reports items):
  - *The occurrence is a thing in the store.* A string in a field of a record, by the record's own identifier: what the transformation to Linked Art reads (this record's publisher is that entity), and what a decision may be scoped to. The examples collected today (`string_examples`, three records a string at most) are a sample of it, not it.
  - *A decision at two levels.* The string's decision is the default for all its occurrences; an occurrence may carry its own, which wins for that record. The desk decides the string as now and, from the records shown, can say "in this record it is something else".
  - *A finding for the cataloguer, as a record of its own.* What the reviewer saw that the source could say better: an ambiguous bare name ("Plymouth" for the Massachusetts one), two places joined by a comma, a truncated heading, a wrong class, a publisher supplied in brackets, a spelling slip ("Gainsville, Ga."). Each names the record and field, what is there, what is suggested and why, and whether the project has decided it meanwhile. The cataloguers' report (Exports and reports) is these, by record. The owner, the same morning: "each project could have a list of 'things that could be fixed in sources' that is persistent and acts as a task list for cataloguers". So it is a list of the project, not a report run now and then: a page in the workbench (open, sent, mended in the source, declined, each with who and when), added to from the desk and the entity page in one step where the thing is seen, and by the audits (the mislabels, the bracketed publishers, the comma-joined places) in bulk; exported for the cataloguers in a form they can work down; and closed by the next collect when the export shows the record changed, not by hand. Each entry carries the source's own identifier for the record (the export's record id, "D2805", "0904788113"), and where the project has one, a URL template in its manifest that the identifier fits into (the owner, 2026-11-15: "if there is a URL template that it can fit the identifier into it could allow users to click straight into the source system from the list. This would be a project-specific setting and may not always be possible"); the same link then serves wherever a record is named, on the desk's "In the records" and the entity page.
  - *Surviving the source's own correction.* The next export is collected again: a string that is gone because the cataloguer mended it retires its decision without losing the history; an occurrence-scoped decision whose record now says something plain is satisfied and closed, and says so; a correction that makes a string the desk already decided appear in a new record inherits the decision. Entities keep their identifiers across all of it. The measure is a round trip: decide, report, correct the export, collect again, and nothing decided by hand is asked twice.
  - *How the desk shows it.* All of a string's records, not three, grouped where their other fields point different ways (the publisher, the other places, the class mark), so that a split is seen and not chanced upon.
  - *What a record's identifier is, looked at 2026-11-15 with the owner* (the library export `2024-03-11_library.xml`, 63,392 records, profiled by the assistant; the old pipeline's output in `C:\pmc\full_output\library\work`). The Heritage export's `record ID` is its `stdno`, the standard number, in every record, and it is unique in the export. For 18,613 records it is a valid ISBN-10 (and 22 a valid ISBN-13); 98 ten-character and 19 thirteen-digit ones fail the checksum; about 200 eight- or nine-character ones are ISSNs and the like (journals); the rest are numbers the system or the library gave, by a letter that goes with the kind of material: D 17,529 (text and exhibition catalogues without an ISBN), E 9,904 (image files, the photographic archive's folders), S, ZB, T, ZA, R, V, X, U (auction catalogues), A 541 (information files, manuscripts), P and Y (journals), W 53 (web sites). The old code published whichever it was as a "System-Assigned Number" and made the work's URI from it. So: the identifier is unique within one export of one source system and is not promised to last, since an ISBN that is corrected (the 117 that fail are candidates) changes the record's identifier; the owner: "I think we have to assume this is the norm." The design therefore keys an occurrence on the source system and the record's identifier, and at each collect matches records whose identifier is gone to new ones by what else they say (title, year, the other fields), carrying their record-level decisions and findings across, and showing a person the ones it could not match. The owner also: a project has several source systems (PMC's library, archive and photographic archive; another's museum catalogue), each with its own identifiers and its own URL template, so a record is named by source system and identifier together, and the manifest describes each system (name, what its identifier is, the template). Today's `source` on an example ("library-2024-03") runs the system and the export's date together; they part.
  - *Who works the list* (the owner, 2026-11-15): not known and it will vary; two activities, the reviewer's and the cataloguer's, to be supported apart (an export a cataloguer works down who never signs in) and together ("in a small institution it may be the same person who has the workbench open AND the catalogue management tool open at the same time, so should be nice and usable for them too").
  - *Rules may decide for a record* (the owner, 2026-11-15): "the rules should, if we are confident that they will work. As long as someone can check what's happened." With it: "any UI ideas you have about visualising/understanding the work are important, I feel this is an unsolved problem so if this software can do it well it could prove popular. As ever, we need to ensure that the particulars of PMC don't dictate the design entirely, although it's a good start."
  - *A count first, as ever:* bare place names with more than one record whose other places or publishers point to different countries.
  Until it is built: decide for the majority, and note the exception on the string.
- A heading placed in a village the gazetteer's chain does not name (2026-11-14, the owner at "Paxton House (Berwick upon Tweed)", string 36804: LoC's and the project's "Paxton House (Paxton, Scottish Borders, Scotland)" in one group, TGN's Paxton House, "Scottish Borders, Scotland, United Kingdom", in another, both at 1.00, and the desk left to tick them together). The placed-through join wants every word of the heading's placement in the holder's chain, and "Paxton" is not in TGN's; the same record is carried by finding 120 only because its placement names the county. A heading whose placement holds the chain's first segment and whose own name is the string's could join the holder whatever village it adds. Counted 2026-11-14 (`reports/q-village.txt`): 60 strings have a heading of the TGN record's name and county in another group, 28 of them queued, but most are a city beside a county, district or council area of its name (Aberdeen, Derby and Derbyshire, Leicester and Leicestershire, Durham), where two TGN records share the county word and the heading belongs to one of them; the houses and castles (Paxton House, Stirling Castle, Edinburgh Castle, Kenilworth Castle, Penrhyn Castle) are the clean cases. A rule would need to be for records that are not inhabited places or regions, or for a string with one TGN record of the name: to design, not built.
- A place no loaded source holds (2026-11-14, the owner at "Brockley", string 52621, the place of publication of "St Peter's, Brockley : a short history from 1866 to 1893": "none of the candidates are correct. It's the London suburb"). TGN has three Brockleys (Suffolk, North Somerset, New South Wales) and not the district of Lewisham; LoC has no heading of the name; the Wikidata extract's one Brockley is the Suffolk village. The desk's answer today is a local authority of class place, with Wikidata's page for the district pasted as "known elsewhere" if the reviewer finds it. Two things to look at: what the Wikidata extract leaves out and why (a London district with its own article is not in it), since a fuller extract or a live lookup from the desk's search would have offered it; and the same place waiting twice, the placeholder "Brockley" for the place of publication and "Brockley (London)" for a subject subdivision, which the desk should offer to each other.
- ~~A bare place name among namesakes, one of them at home~~ (built 2026-11-14 as finding 123, doc 10 7.6, and tightened after its first run: the home group must bear the name and be the only live group at home, and no group abroad may have more than one and a half times its sitelinks; measured by run wzkhhb, five strings, all right. Left to the desk by the guard and right all the same: "Tain", "Dudley", "Monmouth", "Plymouth", "Otley") (2026-11-14, the owner at "Reading", string 61081, the place of publication of three books from Research Publications and Spire Books: "pretty sure this should be Reading in Berkshire but that isn't the top hit"). Eight groups tie at 1.00 with two sources each, Kansas first, and "prominence does not separate them (91, 63, 39, 33, 33, 0, 0, 0 sitelinks)". The home place (finding 71) is used to resolve a qualifier, never to choose among bare namesakes. Counted against vd2cg3 (`reports/q-home.txt`): of the queued strings without a qualifier in the place and subject fields whose first groups tie, 29 have exactly one tied group in the home country, 39 several, 76 none. The 29 are mostly right for a British art library (Andover, Carlisle, Glasgow, Hastings, Manchester, Reading, Sheffield, Winchester, Woodstock, Strawberry Hill, Enfield, Sandy) and at least one is wrong: "Washington" would go to Washington in Tyne and Wear; "Hamilton", "Somerset", "Warwick" and "Charles" (a subject, not a place) are doubtful. So the rule to build is a presumption with a guard: among tied groups the one at home stands, unless a group abroad is far more prominent (the sitelinks the tie already reads; a factor to be set from these 29). Until then the desk could at least list the group at home first among equals, as finding 122 does for the bearer of the name.
- What the cataloguer says a person is, read against what a record says (2026-11-14, the owner at "Hamilton, Thomas [(Bookseller)] (publisher)", string 9055, one of nineteen names on the imprint of "The new pocket-dictionary of the Italian and English languages" (1819), "Printed for J. Mawman, J. Cuthell ... T. Hamilton, R. Fenner": "why is a Scottish architect the publisher?"). The first group at 1.00 is ULAN's and LoC's Thomas Hamilton, 1784-1858, "Scottish architect"; eleven more Thomas Hamiltons follow, none said to be a bookseller. The string carries its own answer in the cataloguer's bracket, "(Bookseller)", and the rules read it as nothing: a record whose description names another calling (architect, inventor) is not agreed with by it, and one that names the same would be. The manifest's `Trades` (finding 109) is the nearest thing. Count first: the strings carrying "[(...)]", what the brackets hold, and how many first groups carry a description that names another calling. Also the card's case for "Ask for help": the imprint, the date and the bracket together say a London bookseller, and the answer is a local authority.
- "Ask for help" at the desk: an LLM's advice on the card in front of the reviewer, and its hand if wanted (the owner, 2026-11-14, after following doc 16's University of Oxford and meeting "Bottrop", a place unknown to him; noted for later, the scenarios first). The owner's brief: a button on the desk that sends the review in a clear, efficient prompt, as cheap to run as it can be without losing usefulness or accuracy; the answer tells the reviewer what to do so that they can do it themselves, and can offer to do it for them, which means an instruction format the desk can execute, including work beyond the card (the tray, an entity's identifiers); a brief exchange, not one shot; and a way of learning the workbench, since each answer names the steps. The assistant's design notes, agreed in outline and not built:
  - *When.* After more scenarios are written: doc 16 and doc 13 are the prompt's stable part (what each action does, when the usual answer is wrong), and the actions it may propose are the desk's own, which are still moving.
  - *Shape.* One Messages API call per ask, through the official C# SDK, from the workbench's server; the key from the environment only (`ANTHROPIC_API_KEY`, the standing rule). The system prompt (the guide's actions, the scenarios, the project's fields and their labels, the answer's schema) is frozen and cached; the card is the user message, rendered as compact text from what `ReviewItem` already holds (the string, its field, the records, each group and member with its reasons, each project entity with what it stands for and what its identifiers are). A follow-up question appends to the same conversation. Catalogue strings and pulled source text are data in the prompt, never instructions.
  - *The answer.* Structured output: a recommendation in a sentence, the reasons tied to what is on the card, what the model knows beyond the card said as such ("from general knowledge: Bottrop is a city in North Rhine-Westphalia"), its confidence, anything it would want checked, and `steps`, a list from a closed vocabulary that mirrors the desk and the entity page (accept a group, same as, just this one, accept the ticked together, merge entities with a survivor and class, remove an identifier or a set, keep as local authority with a label, add a note, skip, look again). Each step names its target by an identifier that is on the card; the desk validates every step against the card before offering it, so nothing invented can be executed.
  - *Doing it.* "Do this for me" runs the steps through `ReviewService` and `CurationService`, the same code as a click, one at a time, shown as they go; a merge still asks, since it is permanent. Every change so made is noted as the reviewer's with the model's help (the audit log says which model and keeps the recommendation), and the decision's method stays human.
  - *Cost.* Counted per ask from `usage`, shown on the desk and totalled per project, with a monthly ceiling in the manifest. Estimate, to be measured: a card is some 3,000 to 6,000 tokens in, the cached part as much again, the answer a few hundred out; at 2026-11 prices that is a fraction of a cent on Claude Haiku 5.5, one to two cents on Claude Sonnet 5.5, two to four on Claude Opus 5.5. The model is a project setting, the owner's choice.
  - *Knowing it is right.* The store already holds the test: the strings a person decided. Replay their cards as they stood, ask, and compare the recommendation with what the person did; that says which model is good enough, and what each prompt change costs or gains, before anyone relies on it. A small eval set of the scenarios' own examples first.
  - *A teaching mode* (the owner, 2026-11-14, after a day of cards that needed explaining: the placeholder edited by hand, the publisher card with "nothing to do", the two Donningtons, Poughkeepsie's city and town: "these experiences as a user reinforce for me the need for an AI-assisted teaching mode that would guide the user through decisions until they were confident in the use of the tool"). The same call with a different manner: for a reviewer who is learning, the answer walks the card in order (what the string is and where it was seen, what each candidate is and why it is there, what the software could not decide and why, what each button would do here), asks the reviewer what they would do before saying what it would, and names the scenario of doc 16 the card is an instance of. It is a setting of the reviewer's, on by default for a new one and turned down as they go (full walk, a hint, off), and the questions asked today are its first test set: each is a card, a confusion and the explanation that settled it.
  - *Cards kept as its test cases* (each a card, what the reviewer did or asked, and what knowing the subject would have said): "Hamilton, Thomas [(Bookseller)]" (9055: a London bookseller on an 1819 imprint, not the Scottish architect at 1.00); "Plymouth" (61711: "Lewisburg [Pa.] ; Plymouth" is a British office, the same Plymouth); "Osterley Park" as a publisher (9036: a house issuing its own guide, the cataloguer's bracket); "Donnington" (27535: the saleroom at Donnington Priory, Berkshire); and "Peter Nahum at the Leicester Galleries" (112402, the owner, 2026-11-15: "this is where domain knowledge would have led me down a different path. A place where an AI assistant would have helped": the gallery of 1902-1977, Ernest Brown & Phillips, and the dealer who took the name in 1984 are two bodies, the records on the card said so by their dates, and the reviewer removed the earlier one's identifiers and merged). The last is the case for the assistant speaking before a merge, unasked, when the two entities' records carry different dates or descriptions.
  - *What is already there* (the owner's reminder, 2026-11-14). The trainer has asked Claude for a proposed reading of a date since 2026-10-17 (doc 08 "Asking a language model"; `LinkedArtNet.Dates.Llm.ClaudeDateInterpreter`, the official C# SDK `Anthropic` 12.58.0; hosted by the examples site's `InterpreterHost`). Its pattern is the one to follow and partly to share: the key from `Interpreter:ApiKey` or `ANTHROPIC_API_KEY` (already in `deploy/linkedart/.env.example` and the compose file), no controls shown without a key, the model and effort as settings (`claude-opus-5-5`, effort `low`), the system prompt a committed document embedded in the assembly and marked for prompt caching, structured output, a refusal handled, usage counted per call (input, output, cache read and write), and the rule that the model proposes and a person confirms. New for the desk: the steps a person can have executed, the follow-up exchange, the spend shown and capped, and the workbench's own configuration and compose entry. Doc 08 says the trainer is "the project's only use of an LLM"; that sentence changes when this is built.
  - *The owner's answers, 2026-11-14.* The monthly allowance is API credit, and the owner can make a key (or may have one); it goes in the environment as `ANTHROPIC_API_KEY`, never in the repository or the chat. Catalogue records may be sent to the API. Web search is not on by default: when the model needs it, it says so and asks the reviewer, who may allow it once or for the rest of the session; the choice is the reviewer's and is kept for the session only.
- The project's home page timed out (2026-11-14, the owner signing in from a second Chrome profile after a restart: Npgsql's thirty seconds, in `CountsAsync`). The count of authority records scanned the whole table, 19.4 million rows and 1.49 million pages read cold, 23 seconds warm-ish; mended the same morning by summing `authority_sources.records`, which the loaders keep (the whole query now a second). `authority_records` had last been vacuumed on 2026-10-18 and carried 2.26 million dead rows: the Align tool has a `vacuum` command from the same morning (`PostgresAlignmentStore.VacuumAsync`: the authority records, decisions, strings, entities and equivalents, each vacuumed and analysed; no row changed), run then (151 seconds; 2,263,245 dead rows in the authority records, 378,179 in decisions; a full count after it 9 seconds). Left: the per-source totals the home page now sums come to 19,467,495 and the table holds 19,419,848, some 47,600 fewer, so one source's total is stale and is to be found and refreshed; any other page that counts or scans that table whole is to be looked for, and `vacuum` belongs after a run is taken back and after a day of pulls.
- ~~A field's name in plain words~~ (built 2026-11-13: `Label` and `About` on a context in the manifest, `ProjectManifest.FieldName` and `FieldAbout`; the PMC labels and sentences are the assistant's reading of the export's field names and of the records, the cataloguers' to correct; the desk's head, its rows and placeholders, the entity page's panel, records, waiting list and strings table say the label, the field's own name on hover; a label is not a reading, and `Differences` says "named or described differently"; the saved manifest refreshed with `manifest --use-default`. The context dropdown and the lists keep the fields' own names) (2026-11-13, with the panel at the head of the entity page, "This entity stands for 1 string ... in the field place": `place` is the place of publication, `corpauthor` a corporate author, `keywords:sub` a subdivision of a subject heading, and the page can only print the context's own name). The manifest gives each context a label and a sentence ("place of publication"), and the desk, the entity page and the lists use it wherever a context is named to a reviewer. The same panel on the desk's project-entity rows, which say "stands for 1 string in place", follows from it.
- ~~What an identifier is, on the entity page, and a VIAF or LUX identifier said through the record that cites it~~ (built 2026-11-13: `IdentifierSets`, the entity's identifiers gathered into sets that cite one another through `sameAs`, the Wikidata extract's `ids` and a pull's "same as" facts, or cite a common third; the entity page boxes each set under what it is with "Remove all N", each row says what its record is (`RecordSummary.Says`), a VIAF number "the viaf identifier that wikidata's Q3403624 gives (...)", and an identifier nothing describes says so and points at the pull; on the desk a held identifier without a record says "given by wikidata's Q...". Not built: a lookup of the whole extract for a VIAF number no held record gives, which wants an index on `data->'ids'`; a LUX identifier joins a set only once pulled) (2026-11-13, the owner at the bare "Dagenham", `place/hvj4jgry`, meaning to remove the constituency's identifiers there: "it's hard to tell what the VIAF one actually is when you go to it on VIAF.org"). The entity page's identifier table shows a record's label and dates only, so the two Wikidata rows of the Dagenhams both read "Dagenham"; it should say what the desk now says (`RecordSummary.Says`). VIAF and LUX identifiers have no record in the store, but the Wikidata extract's records carry `ids` (`"viaf": "143706493"` on Q3403624): a VIAF identifier an entity holds can be described as "the VIAF number Wikidata's Q3403624 gives (Dagenham: Parliamentary constituency ...)", first from the entity's own Wikidata identifiers, then by a lookup of the extract; and identifiers that came as one set (a Wikidata item, its VIAF, the LUX record of the same import) could be offered for removal together. Desk and entity page both.
- ~~What a held identifier is, said where a merge is confirmed~~ (the rule question built the same evening as finding 119, doc 10 7.6: the count, 10 of 63, and the bare entity aligned elsewhere not joined; measured by the next reconcile) (built 2026-11-13: on the desk every held identifier, in the confirm step, the holder-pair notice and a project entity's row, is a link to its record followed by what the record is when the store holds it, `RecordSummary.Says`: the label and the Wikidata description, a pull's, or TGN's first two parents, "wikidata's Q3403624 (Dagenham: Parliamentary constituency in the United Kingdom, 1945-2010)"; a row lists the entity's other identifiers after those of the group; VIAF and LUX identifiers, with no record in the store, are links only. Open: the rule question at the end, with its count) (2026-11-13, the owner at "Dagenham (London, England)", string 119335: "how does this come about where the two Dagenhams have different VIAF identifiers?"). The import had two entities: "Dagenham (London, England)" holding Wikidata's Q1075299, "Town in east London, England", and a bare "Dagenham" holding Q3403624, "Parliamentary constituency in the United Kingdom, 1945-2010", the earlier pipeline's alignment of a place of publication. The bare one joined the placed group as "the project's one Dagenham" and the desk offered the pair. The confirm step and the entity rows list identifiers as numbers only; the Wikidata extract's description (in `authority_records.data`) and each held record's label, shown beside the identifier, would have said the two differ before the merge. Also to consider: a project entity whose Wikidata record's `instanceOf` is not the kind the group's records are (a constituency against an inhabited place) should not join as "the project's one". Count first: the bare entities joined by that rule whose held Wikidata description differs in kind from the group's.
- The scenarios served (built 2026-11-13: /help/scenarios renders doc 16 from the assembly, its pictures at /help/img/scenarios/, the two documents' links by file name turned into the pages', a line at the head of each to the other, and the holder pair's box on the desk links "How to decide this, with an example" to #holder-pair). The document itself, below, stays open.
- The scenarios document (doc 16, begun 2026-11-13 at the owner's asking: scenario, example, solution, with pictures): one scenario written, the holder pair; a scenario is added when one is met at the desk, the pictures taken from the running workbench at the time. The list to write is in doc 16 itself ("Scenarios to write", agreed 2026-11-13: sixteen at the desk, six cleaning an entity, three putting something right), and so is the table of when each scenario's pictures were last checked: after any change to the desk's or the entity page's markup, the scenarios that show the changed part are retaken (the owner, 2026-11-13: "we'll also need to go back and refresh them as tweaks are made to the UI"); the holder pair's are stale since the same day and to retake first. The workbench's Help renders doc 13 only (embedded at build); serving doc 16 and its pictures beside it, linked from the guide and from the card where the scenario is recognised, is part of this item.
- Twins that share a record (2026-11-05: "Lawrence" in corpauthor on the desk and "Lawrence" in publisher as a search result, two placeholders, the same three Crewkerne sale catalogues behind both, the name in both fields of each record). When another placeholder of the string's name has a record in common with the string's own, the desk says so on its card or search result ("in the same records as this string"), as a hint towards "Same as" or "Accept together"; the reconciler keeps twins apart on purpose (finding 104), so the hint is the desk's, not a rule. The ticks are on the candidate cards only; a twin met among the search results has "Same as this" and nothing to tick, and the hint must say which action it points at where it appears.
- ~~The publisher strings without a record behind them~~ (closed 2026-11-14: collected again from `C:\pmc\2024-03-18\2024-03-11_library\2024-03-11_library.xml` as source `library-2024-03`, 63,392 records, 122,384 examples; publisher 11,141 of 11,141 strings now have a record, from 10,612. Left without one: keywords:head 73, corpauthor 25, place 22, keywords:sub 12, to look at): the cause found 2026-11-14 (the owner at the publisher string "Osterley Park", "seen 1 time as publisher" and no record on the card): the export writes a publisher the cataloguer supplied in square brackets, "[Osterley Park]" on record D2805, "A guide to Osterley Park, Isleworth" ([1939]), and the 2024 strings have none; 487 of the 529 publisher strings without a record are explained so (the other 42 to look at after). `HeritageExampleCollector` offers both forms from that day, as it did for place. The export's path is the owner's, given that day; command 3 of section 0 names it. (2026-11-05: the entity page of a placeholder "Stanley", one string, publisher, no "In the records"; the collection covered 10,608 of the field's 11,141 strings, this one not among them). List them beside what the export's publisher element holds for the same records and find the cleaning the 2024 process applied, as the cleaned-keywords table closed the keywords gap; a `collect-examples` report of the export values that matched no string would do the listing.
- ~~Help for the reviewer who asks what to do~~ (built 2026-10-26 evening, the owner's ask at the desk: a fold at the
  top of the desk, "What am I supposed to do here?", a Help link in the bar, and `/help` rendering doc 13 from the
  embedded committed text; the search under "Something else" pre-filled with the name as read).
- ~~The possessive firm name as a candidate~~ (finding 105, built 2026-10-26 evening, doc 10 7.6 "Built 2026-10-26
  evening, finding 105": "Dowell's Ltd" offered for "Mr Dowell" at 0.90, shown and never carried alone; measured by
  the next run, and seen at the desk once the 500 of run utt9xm are re-decided, Commands on return item 4).
- ~~"Look again" at the desk~~ (built 2026-10-26 evening: a link beside the decision date re-evaluates the string
  under today's rules and shows the fresh evidence with what the outcome would be, deciding nothing; the actions act
  on what is shown).
- ~~The possessive key of an acronym~~ (built 2026-11-07: a name in capitals has no possessive key; PossessiveTests) (finding 105's follow-up, run evhvuw): "ICA" went from 82 to 102 groups through
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
- ~~An identifier on "Keep as local authority"~~ (built 2026-11-07: "Known elsewhere (URL, optional)" on the keep-local form, kept as the entity's identifier under the namespace guard; ReviewServiceTests. The manifest list of ArchivesSpace instances stays open below) (the same desk item): the NLS archive is the one authority for
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
  desk showing a part's parents (7.5 item 4) (2026-10-20). The owner, 2026-11-07, in the entities list at "Ackermann &
  Son, Arthur (London)--History", a provisional Type beside the firm: "should we not recognise this as a compound
  heading?" It is one (method `composite`; the entity is the heading's own concept, its head and subdivision strings
  of their own), but nothing on the list or the page said so. Built the same day, the owner's word: the list tags such
  an entity "compound" (`CompositeEntityIdsAsync`, one query per page) and the page lists its parts in order, each
  with its decision and the entity it resolves to, a part still queued linking to its desk page, under a line saying
  the whole is coordinated from them and holds no identifier itself (`EntityView.Parts`; CurationServiceTests). Still
  open here: the export as coordinated concepts and the desk showing a part's parents.
- Keying workbench users by GitHub id (`github:<id>`, the local user `local:<name>`), with the first user besides the
  owner (2026-10-21).
- Legacy data for the desk: entities whose class disagrees with their label ("Crimean War, 1853-1856", the two World
  Wars, "Americans", "Archaeology" imported as Place); legacy duplicates among places (two aligned "Washington"
  entities, two "Sheffield" entities, place/c4c4mbm7 with VIAF 133706141 and place/djtugwgt with Wikidata Q42448 and
  VIAF 124322655; two "Evanston" entities seen 2026-10-26), for merge-duplicates or the desk, or the crosswalk;
  decision 17's three ("Clayton" to Victoria, "Lincoln" to Nebraska, "Stratford" to Virginia, each the project's one
  entity of the name) (2026-10-24).

**Reconciler** (doc 10 sections 7 and 7.6):

- A store match on a historical name the entity gathered (seen 2026-11-15 in run x9ptwy, for the owner to rule on): the
  owner decided "Plymouth" (61711) onto TGN 7011301 with LoC n80097355, the entity `place/adjrx32x` took TGN's other
  names as labels ("Sutton", "Sudtone"), and the next reconcile matched the bare place of publication "Sutton" (61352) to
  Plymouth by exact-name, where it had been queued. Matching on gathered labels is long standing and mostly right
  (about 200 exact-name matches in that run are on a label unlike the entity's own: "India" on "Bhārat", "Calcutta" on
  "Kolkata", inverted personal names), so nothing is changed without a ruling. Choices to put: a place's historical or
  former names (TGN marks them) are not gathered as labels; or a label that is another place's common name does not
  carry an exact-name match on its own; or leave it and decide "Sutton" by hand. Count first. The owner, the same day:
  "I guess 1) depends on the nature of the material being catalogued." So it is a setting of the project (its
  manifest), not a rule of the library: whether a place's former names are names a string may be matched on. A
  library of early printed books wants them; a list of modern imprints does not. To design with the manifest.
  The owner at the entity page, the same evening: the page lists "Five centuries of artists in Sutton ... Sutton,
  London" under Plymouth, "which would lead me to remove 'Sutton'". So there is a second, nearer remedy, for one
  entity: a name under ALSO can be set aside there ("not a name these strings may be matched on"), kept and shown as
  set aside so that a pull from the source does not bring it back, the store match ignoring it, and the strings that
  resolved by that name reopened. *Built 2026-11-15 (the owner: "yes, build it"):* schema version 12 (`entities.set_aside_labels`), `CurationService.SetNameAsideAsync`, "set aside" beside each name under ALSO on the entity page and "restore" beside a name set aside; an update, a pull or a merge does not put the name back, and strings a rule had matched by it are reopened. Before that: the entity page had no control on a name. Today's way round: tick
  "Sutton" under "Strings that resolve here", reopen it, and decide it at the desk; a person's decision is not redone
  by a run. To agree with the owner before building.
- What a record's other fields say about a string (the owner at "Cambridge", 65756, 2026-11-15: "another piece of
  configurable knowledge that would take the hint offered by the publisher being Cambridge University Press ... The
  app having a set of common background data would help here"). Counted the same day: of the 525 records with the
  bare place "Cambridge", four are American (Harvard University Press 2, Harvard Art Museums 1, The MIT Press 1) and
  276 are Cambridge University Press, so one decision for the string is wrong for four records and a decision by
  publisher is right for all of them. Three places the knowledge can come from, to choose among with the owner: (a)
  the store itself, once publishers are aligned: the publisher's entity, and where its source says it is (Wikidata's
  headquarters, LoC's heading), so nothing is kept by hand; (b) the project's own records: the same publisher under a
  place that is qualified elsewhere ("Cambridge [England]", "Cambridge, Mass.") says where that publisher is,
  counted, with no outside knowledge; (c) a table in the manifest or shipped with the application (publisher to
  place), which must state its provenance. And where it acts: as evidence on the card ("276 of these records are
  Cambridge University Press, which is in Cambridge, England"), as the default of a decision for a group of records
  (doc 12 14.3: a decision for the records where the publisher is this one, between the string and the single
  record), and only then as a rule (doc 12 14.8 step 6).
  *Built 2026-11-15 as a hint at the desk, nothing decided by it (the owner: "do 1 and 2 and 3 but just with a
  skeleton model for 3 ... maybe adding to it via PRs in GitHub"):* a place grouped by its publisher shows, on each
  publisher's group, where that publisher is (`ReviewService.PublisherHintsAsync`, `FieldHint`): (c) from
  `Tables/publisher-places.json` (`PublisherPlaces`), a skeleton of seven publishers written by the assistant from
  general knowledge, the TGN identifiers checked against the local index, the provenance and how to add by pull
  request in the file's notes; (b) from the project's records, the same publisher's imprints that write the place
  with a qualifier (`FieldValuesWhereAsync`); (a) the project's entity for the publisher, named and linked. Still to
  do: (a) says nothing of place, because the Wikidata index keeps no headquarters (P159) or country for a body: the
  loader is to keep them at the next load of the dump, or a pull is to bring them; an entry of the table carries one
  place, so a publisher of two cities (Yale University Press) is absent; the hint is not yet the default of a
  decision for a group of records (section 14 step 3) nor evidence to the reconciler; only the publisher of a place
  is hinted, the general form (any field about any other) waits for a second case.
- The audit of the old import's Wikidata links for bare place names (the owner, 2026-11-15: "YES", build the list;
  unsure whether its own page or the first source of the cataloguers' list). The assistant's recommendation, put to
  the owner: its own page. The cataloguers' list (doc 12 14.4) is of things the source records could say better,
  for cataloguers to mend in their system; these links are faults in the project's own entities, mended at the
  workbench by whoever aligns, and a problem in an external authority is a third list again. So: a page of checks
  on the store's entities, each check a named query giving entities with the reason and the strings and records
  behind them, the first check "a place aligned abroad of the home place by the old import, with a namesake at
  home" (`reports/q-import-us.sql`), each row leading to the entity page where the identifier is removed and the
  right one added. *Built 2026-11-15 (the owner: "go ahead and build the next logical thing"):* the page Checks (`/p/<project>/checks`, in the navigation), `CurationService.PlacesAlignedAwayAsync`, `ImportedPlacesWithHomeNamesakesAsync`; counted first (`reports/q-check1.txt`: 509 such entities, 277 with a namesake at home, 77 listed before the test on words of the chain); "likely wrong" where the place at home is at least as written about, "to look at" otherwise; a confirmed entity leaves the list. "Mend..." on a row, built the same day at the owner's word after "Cardiff" and "Belfast" were mended by hand (`MendPlanAsync`, `MendAwayAsync`: a confirm screen, the identifiers to remove ticked, the places at home to choose among, merged into the project's entity that holds the place or the identifier added); the query made to use TGN's label index (11 seconds to 0.27); the merge search's results link to their pages and say where and how many strings. The check reads the strings from the same evening (the owner at "Birmingham", whose one string is "Birmingham, Ala."): an entity whose every string bears a qualifier the item bears is not listed, a string that names the place at home is the reason a row is likely, and every row shows its strings (100 rows became 79: 16 likely, 49 to look at, 14 that cannot be told). A label for such an entity, the owner asking what form American places should take: the project's place labels are mostly LoC's headings ("Gloucester (Mass.)", 1,061 with a parenthesis against 580 with a comma), so "Birmingham (Ala.)" matches what is there; whether published labels should be spelled out ("Birmingham, Alabama") is a ruling for the whole set, not one entity, and a suggested label could be part of this check. The owner, working down the list the same evening: "we'll want an AI assistant here too" (as "Ask for help" at the desk: for a row, read the entity's strings and records against the item and the place at home and say which it is and why, for the person to accept; not built, to design with the desk's). And what the first evening's use showed: "Berwick" right by TGN and LoC and wrong by the import's three identifiers (a hamlet in Kent), "Biggleswade" right and listed through a fault since mended, "Bromley" wrong though its item is not in the index: so the rows that cannot be told want their items pulled (a pull of every such item at once is not built; one at a time on the entity page works, and the check reads a pulled description). "Raleigh" (the owner, the same evening): wrong as listed (a neighbourhood of Memphis), and the place meant is Raleigh, North Carolina, not the Raleigh in Devon that Mend offers: Mend knows only the places of the name at home. It is to offer as well the project's other entities of the same name, each with where it is (the project has a "Raleigh" on North Carolina already, to merge into), and a box for any other place's identifier; not built, mended by hand meanwhile. Still to do: an entity whose identifiers were all removed still says "aligned" (to rule: back to provisional?); a place at home with no Wikidata item found through its TGN id has no count, so most of the list cannot be ranked; only imported Wikidata links of places are checked; the cataloguers' list (doc 12 section 14 step 2) is separate and not begun.
- The assistant on the checks page, a worked example to build from (the owner, 2026-11-15: "I would ask AI about
  'Wimborne Minster'"; the assistant answered by hand from the store, and the owner agreed the example and the second
  check below go here). The entity `place/wxc544fj` stands for "Wimborne" and "Wimborne Minster", places of
  publication in four records (Dovecote Press three, Wimborne Bookshop one; the books are about Longleat, Sudeley
  Castle, St George's Chapel and Sven Berlin, so the subjects say nothing and the publisher settles it); the import
  aligned it to Wikidata Q2632746, described only as "church"; the place meant is the town, TGN 7011278 (Dorset),
  Wikidata Q535233. The shape of an answer, to keep: a verdict; how sure; the evidence from the records (which field
  told); what each identifier on the entity is; the ticks and the choice proposed for Mend, for the person to accept
  or change; and what was not checked (here VIAF and LUX, and LoC's unqualified heading). The assistant reads the
  store and the pulled records only, writes nothing, and its answer is kept as a note when accepted. To design with
  "Ask for help" at the desk, the same kind of help.
- What an evening on the checks list showed (the owner, 2026-11-15: "the early checks usually ended up in picking a
  new one, but the recent ones were all relabels"). The list's order is a rank of wrongness: the likely first, then
  the least written-about item first, so the obscure namesakes abroad (wrong, mended) came early and the famous
  cities abroad (right, wanting only a label that says which) came late. To do with it: the Decide screen leading
  with the likelier outcome (keep and relabel where the item is widely written about and the strings' qualifiers
  agree, Mend where it is obscure), the person still choosing; and the evening's decisions as a labelled set for the
  assistant (which rows were mended, moved, relabelled or confirmed, against what the check knew of each: the notes
  on the entities have them), to be counted before any default is set and to test the assistant's suggestions
  against. Not counted yet. Built the same evening, in order: Mend, the strings read, the records on the screen,
  places not at home, a search of the project's places, moving some strings, relabel and confirm, and one "Decide..."
  a row with every outcome on the screen that shows the records.
- The assistant driving the workbench as a user (the owner, 2026-11-17: "you drive the browser ... wear the hat of
  a user", "press and I'll review after"), signed in as `claude`, a reviewer the owner added on the Members page, so
  that the history tells its decisions from the owner's. What it is for: usability, and issues the owner has grown
  used to. It reports each kind of item at first and less as kinds repeat, keeps a list of what confused it, mends
  the small things as it goes and brings rules and design to the owner. To know: the owner's Chrome and the
  assistant's share one sign-in cookie, so signing in as one name in either makes both that name; the owner uses
  another browser or a private window to be `local` meanwhile. The assistant does not sign in as the owner.
- The label of a place, the owner's ruling (2026-11-17): "On the whole, UK places don't need qualifiers. But
  sometimes they do! For example, Boston would usually be taken as Boston, Mass rather than Boston in Lincolnshire.
  And even more, 'California' would usually not be taken to mean the area of Ipswich." So the bare name is for the
  place the name is taken for, at home or abroad, and a qualifier for any other of the name. As built the same day
  on the Decide screen: the name as it is suggested unless the local index knows a more written-about Wikidata
  place of exactly that name that is another place (`BestKnownNamesakeAsync`, `MendPlan.Rival`), in which case
  LoC's heading is suggested where the entity holds one; the person picks among the name as it is, LoC's heading
  and another. Still a suggestion on one screen: not yet a check of its own ("a bare label that would be taken for
  somewhere else"), which would find the Bostons and Californias across the whole store.
- A project begun from the catalogue alone (the owner, 2026-11-17, "much later": "a project where there is no
  prior PMC data, starting from scratch with the catalogue data. Where my previous entity minting hasn't muddied the
  waters and made the task harder"). The same export collected into a second dataset with no import, reconciled from
  nothing, and worked at the desk: to see what the toolkit does on its own, how much of the present work is the old
  import's (the whole checks list of 2026-11-15 to 17 is: craters on Mars for Ely and Maidstone, a railway station
  for Kingston, Stafford in Ohio), and what a new project's first days are like. Not begun; needs nothing built
  that is not there, only a new project and a run.
- What the assistant's pass down the last 21 rows of the checks showed (2026-11-17, as a user; the fixes of the day
  are in the commits): of 21, 9 kept (6 of them given their TGN identifier), 9 mended to another place (the import's
  item was a namesake at home in 4, abroad in 3, one of those a railway station, a crater on Mars in 2), 1 merged into the project's other entity
  of the place, 2 left with a note (a town's entity standing also for a building or a house of the name: the
  London Charterhouse, Wallington in Northumberland). Still to do from it: an entity that wants two different
  things done (the owner's concern the same day: "when there are several things that need to be done ... making
  sure the user doesn't lose track of the simple task") had only whole-entity forms and a note. *Built the same day at the owner's word:* the Decide screen lists each
  string with its records and a choice, it stays or it goes to the other place, and one button applies it all
  (`CurationService.ApplyAsync`), returning the person to the same entity while the check still lists it; what the
  entity is aligned to is drawn as chains of places broadest first, one above the other, the part that differs
  bold; a distance in words from the import's item; a map folded away, shown only where the two are 50 km or more
  apart. Still one "other place" a pass: an entity whose strings are three things (Charterhouse: a village, a
  building, perhaps a school) takes two passes. The same drawing of chains is wanted at the desk and for compound
  subject headings, not built. Two project entities for
  one place ("Harmondsworth" and "Harmondsworth (London, England)", "Ely" twice) are not found by any check. The
  owner's question on pictures (a map, or containment drawn): the assistant's view is in the day's log.
- A house or a building under a town's entity (the owner, 2026-11-17, at "Wallington": "I would leave this one as I
  think the records are all about the house in Northumberland, so it needs more work elsewhere"). The subject
  heading "Wallington (Northumberland)", eleven records, is the National Trust's house, and stands on the place
  entity "Wallington", aligned by the import to the town in the London Borough of Sutton; "Charterhouse (Smithfield,
  London, England : Building)" stands the same way on a village in Somerset. Both are left on the checks list with
  notes. The work elsewhere: what such a heading is (a built work, not a place; LoC's own qualifier says
  "Building"), which class the keywords field should give it, whether the project has or wants entities for houses,
  and where its identifier comes from (TGN has neither). To take up with the reading of subject headings, not at
  the checks.
- The desk as a user, first cards (the assistant driving, 2026-11-19; mended that day: an action in a field's queue
  dropped the field and went on in all fields; the search found by whole names only, so the "Plymouth Devon" the
  desk's own notice advises found nothing; a search for a place opened with people of the name; the publisher hint
  cited the card's own record). Seen and not mended, for the owner:
  - "Athens" (32959), 13 records: three of the University of Georgia Press, two of Ohio University Press, the rest
    Greek publishers. The card shows it plainly, the publishers grouped and hinted ("in Athens, Ga.", "in Athens
    [Ohio]"), and offers only one decision for the string. The plainest case yet for doc 12 section 14 step 3: a
    decision for a publisher's records, pressed on the group. Skipped.
  - "Robbins, David (editor)" (28574): ten people of the name at 1.00, each a name and a year of birth, nothing
    to say which edited a catalogue of the Independent Group. LoC's own record cites the works a heading was
    made from; the card should show them, or the one that names this record's title. Skipped.
  - "Desnoyers, Rosika (author)" (28565): one candidate, LoC's, exact, at 1.00, and nothing on the card says why
    it waited for a person. Accepted. A queued card should say why it is queued.
  - "Heaton, Bradford" (28561): the first candidate, lit and with the Accept button, was LoC's Heaton in North
    Dakota at 0.85, its own reason saying the qualifier is not placed; four more American Heatons at 0.00 took a
    screen. A candidate the string's qualifier speaks against should not lead, and those ruled out should fold.
    TGN has no Heaton in Bradford; kept as a local authority. Nothing told the assistant that the indexes had been
    searched and have none, as against the search failing.
  - The card for "Athens" is some six thousand words: twelve groups, each member repeating one sentence of reason
    ("prominence stands aside: ...") thirty times. Said once for the card, it would be a third the length.
  - Built the same day at the owner's word: the title "String review" with a line saying what a string is; why a
    card waits for a person; a shared reason said once; what each heading was made from, fetched on the card; a
    contradicted candidate not leading; a search that matches nothing showing all there is of the name.
- A survey of the queue before more is built card by card (the owner, 2026-11-19: "perhaps it's only by seeing a lot
  of review strings, which you can do a lot faster than I can, that you can begin to see large-scale patterns and
  features the platform should have, rather than behaviour that is an ad hoc reaction to each new card seen"). The
  assistant's proposal, the next thing to do: every queued string (3,764 that day) sorted into kinds by what its
  evidence is like, counted, by field: level namesakes with nothing to tell them apart; one candidate from one
  source; a best candidate under the threshold; a qualifier the candidates do not bear; a string whose records are
  of several places; nothing found; and whatever else turns up. Done first from the store (read-only queries over
  the stored evidence and the records), then a sample of each kind opened at the desk as a user, a dozen or so,
  to say what a person needs there. The outcome a table: kind, count, what decides it, what the platform lacks. It
  is the count that comes before a rule, made for the whole queue, and it says which of the things below is the
  larger lever.
- The survey, step 1, counted 2026-11-19 from the stored evidence of run ykb9jd (`reports/q-survey1.sql`, one row a
  queued string; `reports/q-survey1.txt`; no reconcile was needed, the owner having asked). 3,764 queued strings:

  | kind | strings | share |
  |---|---|---|
  | C2 level namesakes, none of them the project's | 1,399 | 37.2% |
  | D one candidate at the threshold, from one source | 1,092 | 29.0% |
  | C1 level namesakes, a project entity among them | 729 | 19.4% |
  | F best candidate under the threshold | 345 | 9.2% |
  | B every candidate ruled out | 150 | 4.0% |
  | E one candidate at the threshold, two or more sources | 36 | 1.0% |
  | A nothing found | 13 | 0.3% |

  By field: personal authors 1,281 (886 of them level namesakes, 302 with thirteen or more of the name; 355 one
  candidate), subject headings 937 and their heads 368, publishers 752, corporate authors 211, places 145, subject
  subdivisions 70. By weight: 2,606 strings are in one record, 957 in two to five, 156 in six to twenty, 40 in more
  than twenty ("Manchester" 447 records, "s.n." 375, "Phaidon" 224, "Penguin" 202, "Pre" 193, "UMI" 179). What
  the count says, each to be looked at in step 2 before anything is built:
  - More than half the queue (56.6%) is namesakes, and most of those are people. What tells people of one name
    apart is the lever, far ahead of anything else: the works a heading was made from (built 2026-11-19, on asking,
    a card at a time) wants counting as evidence for a rule.
  - Nearly a third (29%) is one candidate at the threshold waiting only for a second opinion ("Phaidon", "Rizzoli",
    "Viking", "Blackwell"). A card each is the wrong tool: a list to read down and confirm in bulk, or a second
    witness found for them, would clear a thousand.
  - Decisions for a publisher's records (doc 12 section 14 step 3) touch few strings: of 145 queued places, 50 are
    in two or more records and 37 of those under two or more publishers. They are heavy ones ("Manchester" 447
    records, "Washington" 55, "Gloucester" 55, "Durham" 35), so step 3 matters by records and not by cards; it is
    not the first lever.
  - The queue is in no order of weight. The 40 strings in more than twenty records are worth more than the 2,606
    in one; the desk should offer the heaviest first.
  - Things that are not names at all sit among the heaviest: "s.n." (no publisher named, 375 records, 22 namesakes
    offered), "l", "s", "B", "C?". A decision "this is not a name" is wanted, and a list of cataloguing
    conventions that are never entities.
  - "Pre" as the head of a subject heading, 193 records: a compound split at the hyphen of "Pre-Raphaelites". A
    fault in the splitting, to find and count.
  - Under the threshold (345) is mostly subject headings: a house with its county ("Arundel Castle (Sussex)",
    "Powis Castle (Montgomeryshire)", 94 of 97 qualified ones saying "qualifier not placed") and a heading with a
    form subdivision left on ("Munnings, Alfred James, Sir, 1878-1959-Exhibitions"). The first is the house or
    building as a subject, met at the checks; the second a parsing matter.
  - Every candidate ruled out (150) is all subject headings, people with dates ("Pugin, Augustus Charles,
    1769-1832"): ruled out, it seems, by dates that differ from the authority's. To read in step 2.
  Step 2, 2026-11-19: the evidence of sixty sample strings read, about ten of each kind (`reports/q-survey2.sql`,
  `.txt`), by query and not card by card, with the cards met at the desk before it. It overturned two of step 1's
  readings and found the patterns below. None is counted across the queue yet; each wants its count before a rule.
  - "One candidate from one source" is not safe to confirm in bulk. Of the publishers sampled about half are
    wrong, and in one way: a firm known by a surname matched to a person of that surname ("Sansom" to Francis
    Sansom, active 1784-1799, for a book of 1995; "Blackwell" to Lewis Blackwell, died 1810; "Rizzoli" to Angelo
    Rizzoli; among the namesakes "Murray" and "Faber" to three artists each, "Penguin" led by a John Bingham,
    "UMI" by a person called Umi). Two general tests would catch them: the kind of thing a field holds (a
    publisher is a body; a person matched on a bare surname is not a candidate there), and the record's date
    against a life (nobody active to 1799 published in 1995; "Smith, W N", author of a catalogue of 1990, offered
    a W. N. Smith born 1868). The records' dates have been in the store since 2026-11-15 and no rule reads them.
  - "Every candidate ruled out" (150) is one cause: the string's dates and the authority's differ by a few years
    ("Pugin, Augustus Charles, 1769-1832" against 1762-1832; "Shepherd, George Sidney, 1784-1858" against
    1784-1861; "Barret, George I, 1732-1784" against 1728-1784), each the source's one record of the name. The
    same person, dated differently by scholars. A tolerance, where one end agrees and the name is unique, would
    settle most; "op 1785" (flourished) read as a birth is a parsing fault among them.
  - Under the threshold (345): the right record, marked down for a qualifier written another way ("Arundel Castle
    (Sussex)" for LCSH's "Arundel Castle (Arundel, England)"; "Royal Collection (London)" for "(Great Britain)";
    "London-Intellectual life" for "London (England)--Intellectual life"; "Belfast Museum and Art Gallery" for
    its later name, the Ulster Museum); a form subdivision left on a dated name ("...1833-1898-Exhibitions", the
    hyphen of the dates defeating the split); and wrong ones of the surname kind ("Garland" to a destroyer,
    "Down, Canon" to a rap musician).
  - Namesakes: for firms, as above; for people, the works behind each heading, and the string's own dates when
    they are in brackets ("Munro, George [1805 or 1806-1883]", offered Munros born 1934 and 1944: the brackets
    are not read). "United Kingdom" waits between TGN's United Kingdom and LoC's Great Britain, one thing under
    two names in two groups. "Ophelia", a subject, is offered three places called Ophelia.
  - How much is the 2024 import's doing (the owner's question): of 3,764 queued strings, 197 (5.2%) have an entity
    the import brought among their candidates, and 32 wait expressly because the project's own entity stood in
    the way of prominence. By field it is places that it touches: 50 of 145 queued places (34%), against 11 of
    1,281 personal authors. So the queue would be much the same from scratch: namesakes, surname matches and
    dates are the catalogue's and the authorities'. What the import cost is elsewhere, and unseen at the desk:
    strings matched by name onto an entity it had aligned wrongly (the checks page found a hundred such places,
    and only places with a namesake at home have been checked). 738 queued strings have one of this toolkit's own
    placeholders (`unrec_`, the 2024 process's identifiers for what it did not reconcile, kept so that LUX's
    links hold) among the candidates: those carry nothing and mislead nobody.
  - Where AI help belongs (the owner's question: "this might not always be on a per-string basis, sometimes on
    sets of strings"). The assistant's reading of the sixty was itself the trial: with the string, its field, a
    record's title, date and publisher, and the candidates' labels and dates, it could say right, wrong or unsure
    for nearly all in a line each. So: (1) on a set, the single-candidate strings (1,092) and those under the
    threshold (345): a verdict and a reason for each, the person reading down a list sorted by verdict and
    confirming in bulk, not a card each; (2) on a set, namesake people (886): the citations fetched and the
    heading chosen whose works are the record's, with the reason; (3) over the whole queue, what this survey is:
    kinds, counts and proposed rules, run again after each reconcile; (4) on the source, drafting the
    cataloguers' list ("s.n.", a church placed in West Sussex in one record and East in another); (5) per item
    only where judgement is singular, as at the checks ("Wimborne Minster"). In every case a proposal with its
    reason, kept, which a person accepts; a decision method of its own ("suggested") would let proposals wait
    and be measured against what people then decide.
- The survey, step 3: the two tests counted, 2026-11-19 (`reports/q-survey3.sql`, `q-survey3b.sql`, and their
  `.txt`). Neither settles much on its own; both thin the cards. The assistant had expected more of each.
  - *The kind of thing a field holds.* Of 749 queued publisher strings with a live candidate, 320 have only people
    level at the top, 71 people and bodies level, 313 only bodies; of 208 corporate authors, 43, 20 and 101. In
    139 of the 320 the string is one word. Of the 320, 49 have exactly one body waiting just below the people
    ("Rizzoli" and Rizzoli editore, "Blackwell" and B.H. Blackwell Ltd., "Deutsch" and André Deutsch (Firm), "De
    Gruyter"; but also "Yale" and W. & M. Yale (Firm), "Mr Sharpe" and M.E. Sharpe, Inc.), 22 several, and 249 no
    body at all: a person who published, or a firm the indexes lack. So putting bodies first in a body's field
    would right the order of some 120 cards and decide none safely. Among strings already decided, those standing
    on an entity of class Person in these fields are mostly the project's own entities by name (publisher 345,
    corporate author 154), many of them firms and houses classed as people ("Hurcombs", 43 records; "Bearne's";
    "Sedelmeyer"): an entity's class to mend, not an alignment. One caught outright: the publisher "Boethius",
    accepted by vote as the philosopher (469-525).
  - *The record's date against a life.* Queued, with dated records: of 1,245 personal authors, 383 have a
    candidate at the top born after every record or dead a century before the first; striking those leaves
    exactly one in 20, none in 14, several in 349. Publishers (dead thirty years before): 170 of 723, one left in
    37, none in 40. Corporate authors 31 of 204, one left in 3. So it shortens some 580 cards and decides about
    60. Among strings already decided it finds one born after its records and 27 authors dead thirty years before
    theirs (which reprints make ordinary); but only some 300 decided strings stand on an entity that carries
    dates at all, the authority's dates not being the entity's, so that count is of a small part.
  - What follows: the queue is not waiting on a missing rule. It is waiting on judgement with the record in view,
    of the kind the assistant made over the sixty samples, which is the case for help on sets (above). The two
    tests are worth having as evidence on the card and for that help to use (bodies first in a body's field; a
    candidate the record's date speaks against marked and folded), not as acceptances. Not built.
- A trial of help on a set, 2026-11-19 (the owner's yes; `reports/trial-publishers-2026-11-19.md`, for the owner
  to mark; nothing written to the store). The 49 queued publisher strings with only people at the top and one body
  just below, each given a verdict and a line of reason by the assistant from the string, three records' place and
  date, and the body's label, on its own knowledge of publishers. Right 15, probably right 6, unsure 8, wrong 20:
  the body is the publisher in about 43% and plainly something else in 41%. So "take the body" as a rule would be
  wrong four times in ten, and the verdicts do tell them apart, by the record's place and date against what the
  body is ("Matador" at Kibworth Beauchamp against an American television company; "Mr Guibert", 1766, against a
  modern Paris firm). Three kinds among the wrong: an auctioneer written "Mr ..."; an imprint known by one word
  whose firm has a fuller name ("Pindar", "Arno", "Oryx", "Harper", "Yale"), which LoC very likely has under that
  name ("Arno Press"): a search with "Press", "Books" or "Publishing" added is a thing to count; and a heading
  matched on a word or on initials ("HENI", "Weiss"). The owner's marks (2026-11-19, in the file; 48 of 49 marked, "Bayford" left blank):
  - The firm verdicts held: "right" 15 of 15, "wrong" 20 of 20. "Probably right" held 3 of 6: "White Rabbit" is
    wrong (the heading is a motion picture studio), "Granada" (a television company's heading) and "Berenice" (LoC's
    record suggests the Spanish publisher, the record is Milan) are doubted. Each of the three was caught by
    opening the heading's LoC record, which the assistant had not read: it judged by the label. Of the 8
    "unsure", the owner settled 6 as not the body offered ("Rizzoli", "Blackwell", "Canale", "Anaya", "Cunningham",
    "Lenz"); "Legenda" is the MHRA imprint, the heading not settled. So the body is the publisher in 18 of 49.
  - What a verdict must read, then: the heading's own record (what kind of body, where, when), not its label.
  - What the owner did to check: the web for the place with the string ("Kibworth Beauchamp Matador", "glasgow
    drew publisher", "rizzoli new york"); a scan of the book itself, its title page naming the publisher ("Grund").
  - The project's own neighbours are not offered: "Blackwell" is the project's Basil Blackwell (group/zxx9fbtr);
    "Jupiter Books" is a queued string and not offered under "Jupiter".
  - The right body is not among the candidates at all: "Pindar", "Artemis" (Artemis Fine Arts), "Drew" (a search
    for "richard drew" under "Something else" does not find the publisher: to look into, the index or the search).
  - "Yale" given W. & M. Yale: "a bad mistake for this bench! Especially in a PMC context". Yale is not in the
    publishers' table (left out on purpose when its place was the question); for the owner's word, the table to
    name the body as well as the place.
  - "Benedikt Taschen" and "Taschen (Firm)": one thing, taken together, the firm the survivor?
  - The trial's set and the desk disagree on order: "ARTEMIS (Organization)" is fifth at the desk, not just below
    the people; "Mendez" and "Granada" have the body second at 0.97. The query's "just below" is not the desk's.
  Two counts after the marks (2026-11-19, read-only; `reports/q-neighbours1.sql`):
  - Is the right body in the index? Of fourteen right bodies looked for by their fuller names, ten are there and
    were not offered (Arno Press, Artemis Fine Arts (Firm), Freedom Press (London, England), Harper & Row, Mead
    Gallery, Oryx Press, Rizzoli International Publications, Troubador Publishing, Weiss Gallery (London, England),
    Yale University Press); four are not (Richard Drew Publishing, Pindar Press, Kala Press, HENI: so "richard
    drew" finding nothing is the index, not the search). The one-word string scores low against the fuller name.
    BUILT 2026-11-19 as a hint on the card and not in the rules (the owner: yes, "though with enough caveats that the user doesn't lean on it too heavily - they still have to be sure"; FullerNamesAsync; unscored, apart from the candidates, the caveat said; seen on "Yale"; to put into the rules' own candidates is a later question): in a publisher's field, candidates sought also under the string with "Press",
    "Books", "Publishing", "Publications", "Gallery", "& Co" after it, the record's place to tell among them.
  - Longer forms already decided in the project, same field: 173 of the 749 queued publisher strings have one,
    100 of them onto a single entity ("Arno" beside "Arno Press", "Cape" beside "Jonathan Cape"); other fields few
    (keywords 40, corporate author 34, place 12). Not a rule: "Mead" beside "Mead Art Museum" is another thing.
    BUILT as a hint on the card, with "Same as" for a person to press (LongerFormsAsync, the desk).
  The owner on the two hints (2026-11-19): "These are also good candidates for AI assist, which could group them
  into a set for consideration." So the set for help is defined by evidence the workbench already holds, not by
  a query of the assistant's own: the queued strings that have a longer form decided onto one entity (100
  publishers), and those with a fuller name in the indexes. For each the assistant would read the string's
  records and the body's own record (the trial's lesson), look the title up outside, and give a verdict with what
  it cites; the person goes down the set, most records first, and presses or corrects. Not built; the design to
  put to the owner with the "suggested" method.
  A second trial on that set, 2026-11-19 (the owner: "yes OK"; `reports/trial2-longer-forms-2026-11-19.md`, for
  the owner to mark; `reports/q-trial2.sql`; nothing written to the store). The 100 queued publisher strings with
  a longer form decided onto one entity; the question, is the string that entity. The assistant's verdicts: same
  62 (527 records), probably same 13 (69), not the same 19 (67), split by record 1, unsure 5; of 671 records. The
  basis is said on each: the place and years of the string's records against those of the longer form's, which
  the store holds, and the assistant's own knowledge where used; no web search, LoC's records not opened. Found
  on the way, six entities whose own alignment looks wrong or doubtful, the string being right for the project's
  body: Penguin Books on LoC's New Zealand firm (202 + 16 records), Jonathan Cape on "Jonathan Cape & Harrison
  Smith" of New York, John Donald on the "John Donald Robb Archive of Southwestern Music", Pilgrim Press of
  Derby on LoC's Auckland one, Olympia Press on a Frankfurt heading. So a check for the Checks page: a body
  whose records' places disagree with its heading's place. And a kind for a ruling: predecessor and successor
  firms ("Alfa" and Nuova Alfa, "Trubner" and Kegan Paul, Trench, Trubner, "Vista" and Studio Vista).
  RULED by the owner, 2026-11-19: "Predecessor and successor firms should try to align with how they are recorded
  in LoC or Wikidata. It's more precise if they can be aligned to their contemporary publisher. And older catalogue
  records are going to name that publisher not the modern successor." The cost, that faceting, discovery and
  search do better with them grouped under the successor, is met in the data and not in the alignment: "if the
  fact that Publisher P2 is a successor of P1 is also part of the linked art data set we eventually produce (the
  serialised group), then downstream discovery/search layers can use that information." What follows, not built:
  - a string is aligned to the body of its records' own time; where LoC or Wikidata keeps the earlier and the
    later body as two records, the project keeps two entities. So in the second trial "Alfa" (1962), "Trubner"
    (1883) and "Vista" (1962) are NOT the entity offered, and each wants the earlier body's own record;
  - entities already holding both ("Rupert Hart-Davis" with "Hart-Davis MacGibbon" on one entity) are to be looked
    at: a count of entities whose strings' records span a known change of name;
  - the relation itself is data the project must hold and write out: one entity the successor of another, taken
    from the sources where they say it (LoC's earlier and later established forms; Wikidata's "replaces" and
    "replaced by": whether the loaders keep them is to be checked) and settable by hand. How Linked Art says it in
    a Group's record is to be found in the model and the upstream issues before anything is designed
    (feedback: check upstream first);
  - to settle with the owner when it is designed: LoC also makes a new heading for a body that only changed its
    name (Graphische Sammlung Albertina and Albertina; Mostyn Art Gallery and MOSTYN). Read strictly the ruling
    splits those too; they were marked "probably same" in the trial;
  - at the desk: a candidate's dates and its earlier and later forms shown, and the record's date against them,
    so that the contemporary body is the one a person is led to.
  - the owner, a moment later, of the layers that can use the succession: "...and so could the workbench, in
    fact". So the workbench reads it as well as writing it out: an entity's page naming what it succeeded and
    what succeeded it, with their strings and records a step away; the entities list, the project's search and the
    card's hints able to gather a line of firms as one ("Trubner" finding Kegan Paul, Trench, Trubner and saying
    which is which by date); the check for a string aligned to the wrong one of a line.
  - RULED the same evening, on a body that only changed its name (Albertina, MOSTYN): the same rule. The owner:
    "I think that last question is the same no? Try to follow external alignment." Two headings outside, two
    entities here, the one the successor of the other. So "Albertina" (2022) and "Mostyn" (2018) in the second
    trial are for the later heading, not the entity offered.
  - the owner: "the successor chain mirrors the geographical hierarchy of places, ... kinda". A place has a
    chain of what it lies in, a body a line of what it came from; both are relations between entities that tell
    namesakes and near-names apart, and the workbench already draws the first (the paths on the Decide screen,
    broadest first). The second to be drawn the same way, earliest first, the record's date marked on it.
  - a wrinkle to settle when designed (the assistant's): the sources do not always split alike. LoC makes a
    heading for each name; Wikidata often keeps one item through a change of name. Then one Wikidata item belongs
    to two of the project's entities, which the store refuses today (an identifier in use). To count before
    choosing: how many bodies have several LoC headings and one Wikidata item.
  The owner (2026-11-19): "This is also gathering evidence for what would go on our page of source fixes -
  cataloguer makes a note a record needs fixing" (doc 12 section 14, the cataloguers' list, step 2). What the two
  trials turned up for it, seen in the exports and not yet listed anywhere: places misspelt in a record
  ("Harmonsworth", "Conwyn" for Conwy, "[London}"); a publisher written short where the record's own book
  gives the full name ("Yale", "Arno", "Cape"); a bare name that is a different body in each record
  ("Musée des Beaux-Arts" at Rouen, Nantes and Calais); a record that looks out of place among its string's
  others ("Ikon" at Bristol, "Allen & Unwin" in 1892). So a verdict wants a third outcome beside same and not
  the same: "the record wants fixing", which goes to that list with the record's number and what was seen.
  The owner's marks on the second trial, the first 49 (2026-11-19, half way): every verdict agreed with, 47
  outright, "Buckingham Palace" unsure with the assistant ("a shifting named entity"), "Humphrey Milford" a
  question (two publishers on one book? no: Milford was Publisher to the University, the head of OUP in London,
  1913-45, the assistant's own knowledge). From the notes, not built:
  - a candidate or hint to say how many OTHER records of the project stand on it ("Cape": "One hint might be how
    many _other_ records the candidate is in");
  - "Penguin": the owner would take Wikidata's Penguin Books from the fuller names, not the project's entity on
    the New Zealand heading: the hint that is "Same as" a misaligned entity leads astray, so that entity first;
  - "Viking": "if we decided here would we have to go and re-edit records to split them out?" A decision on a
    string is for all its records; one house in London and another in New York wants decisions by record
    (doc 12 section 14 step 3);
  - "Allen & Unwin", 1892: record D1925, "The letters of Philip Dormer Stanhope Earl of Chesterfield", London:
    the date the likelier mistake (the owner: "a typo for 1982?"): one for the cataloguers' list;
  - the fuller names are noisy with "& Co": "Murray" is offered F.J. Murray Co., C.W. Murray and Co., B.C.
    Murray & Co.; DROPPED 2026-11-19.
  - the owner, the same day: "Very often a bit of sleuthing will reveal a confident answer. Sometimes a simple
    search, sometimes more detailed, cross checking, gathering evidence."
  A trial of the sleuthing, 2026-11-19 (the assistant, one plain web search each for eight hard strings, by title,
  author, place and the string). Four came back with something to cite, four with nothing:
  - "Butler" (Morpeth, "Holy Island"): Butler Publishing, the listing's ISBN 0946928134 the record's own
    (https://biblio.co.nz/book/holy-island-graham-frank/d/746440756);
  - "Bayford" (Dorking): Bayford Books, ISBN 0907287034 the record's own
    (https://biblio.co.uk/book/small-books-history-embroidery-gertrude-jekyll/d/1257518572);
  - "Canale" (William Roberts, London, 1957-64): an imprint of the artist's own pamphlets, "A Canale Production",
    as the owner guessed (https://libcat.library.tamu.edu/Record/in00004690867);
  - "Olympia" (Keith Vaughan, 2002): the Fine Art and Antiques Fair at Olympia, 26 February to 3 March 2002
    (https://www.theartnewspaper.com/2002/02/01/the-status-of-british-neo-romantic-artist-keith-vaughan-is-rising-and-so-are-the-prices-of-his-art);
  - nothing found for "Columbus" (1987 reissue), "Lenz", "Cunningham" (Alva, 1951), "Echo" (Worcester, 1929).
  What it shows: the record's ISBN is the best key there is, a listing that carries it is the same book; a web
  search does not find small presses and old printers, where a library catalogue searched by ISBN or title would;
  so the help wants a catalogue it can ask (Library Hub, the British Library, WorldCat: which may be asked by a
  program, and on what terms, to be found out and credited) before it wants a web search.
- A count for a new check, 2026-11-19 (read-only; `reports/q-check-bodyplace.sql` and `.txt`): bodies whose LoC
  heading carries a place in brackets, set against where the project's records of that body were published. Of 334
  such bodies with placed records, 93 have no record place that agrees with the heading's place, 434 records on
  them. Many are plainly a namesake taken: Country Life on "Country Life Academy (Star, N.C.)" (88 records, London),
  Unicorn Press on "(Bryan, Tex.)" (36), David & Charles on "(North Pomfret, Vt.)" (29, Newton Abbot), Pilgrim
  Press on "(Auckland, N.Z.)" (23, Derby), Duckworth on "Duckworth & Co. (Manchester, England)" (21, London),
  History Press on "(Charleston, S.C.)" (9, Stroud), Black Dog on "(Clinton, Wash.)", Tate Gallery on "(Sandpoint,
  Idaho)". Some are right and only spelt otherwise ("Mauritshuis (Hague, Netherlands)" at Den Haag, "Alte
  Pinakothek (Munich, Germany)" at München), and Skira of New York against Milan is a firm in two cities. So: a
  check for the Checks page, "a body aligned to a namesake elsewhere", listing likely wrong where the heading's
  place is abroad and every record at home; the names of cities in two languages wanted first. Not built; for the
  owner's word. It does not catch a wrong heading with no place in it (Penguin on "(Firm : N.Z.)" was caught only
  because N.Z. is there; Jonathan Cape on "Jonathan Cape & Harrison Smith" is not: the right one, LoC nr2002017924,
  is a candidate on the "Cape" card, one digit from the wrong one the entity carries).
  How the owner checks a verdict (2026-11-19, "a common pattern", while marking the file): takes the string from the
  document, searches the entities for it, follows the link to the desk, looks at the records the string is in, and
  then searches the web. Four steps a line. So a list of verdicts, when it is built, wants on each row: a link
  straight to the string at the desk (the file gave no number or link); the string's records there on the row, all
  of them reachable, not three; and what was looked up outside with where it was found, so that the person checks
  a citation and does not repeat the search. The assistant's verdicts in the trial had no lookup behind them.
  And what the owner searches the web for is often the book's title, to find who published it (2026-11-19, "just
  FYI"): the record, not the string, is the way in to a publisher. So, not built and for the owner's word: beside
  each record on a publisher's card a link that searches for its title outside (a library union catalogue or the
  web: which, to be chosen with the owner, and credited); and for help on a set, the lookup to make is the title
  in a catalogue that gives the publisher's full name and place, which is evidence a verdict can cite.
- Evidence the desk now shows that no rule reads yet, each to count in that survey before anything is made of it:
  LoC's citations (a heading "made from" a work whose title is the record's: at "Robbins, David (editor)", ten of
  the name at 1.00 and one made from "The Independent Group, 1990", fetched on the card and accepted by the
  assistant on 2026-11-19); the publisher's place for a place of publication (Athens); the inhabited place against
  the province of its name in a field of imprints (Pisa).
- Decisions for a publisher's records at the desk, doc 12 section 14 step 3 (the owner, 2026-11-19: "do you think
  we should do section 14 step 3 now?"). The assistant's answer: it is the right next large thing, ahead of step 2,
  and after the survey above, which will say how many strings want it and by which field they split; a session of
  its own. Its first slice: on a group of the desk's "In the records" (the records where the publisher is this
  one), "decide for these records", stored as record decisions, the string's own decision left for the rest.
- At the desk, not built: candidates that were ruled out still take a card each (four American Heatons under
  "Heaton, Bradford"); they want folding into one line, "4 ruled out", opened on asking. The candidate a LoC
  citation speaks for should stand first on the card.
- A search box for the project, in the header (the owner, 2026-11-19, while checking the trial's verdicts: "I'm
  frequently reaching for a top-level search box, rather than a click first into entities. But a top-level (or
  project) search box might be expected to give other results in other contexts"). The owner's way round at
  present: the string, searched for among the entities, then the link to the desk. The assistant's proposal, put to
  the owner: one box inside the project's part of the navigation, saying what it searches, and one page of results
  in sections with their counts, so that it need not guess the kind meant: strings as the records write them (the
  field, how many records, how it stands, a link to its card: the step now missing), entities, records by title or
  number. Not the authority indexes, which are searched where a string is being decided; no box outside a
  project. "/" to reach it; a query that is exactly one string and nothing else goes to its card. Not built.
- A second check for the page: a place entity aligned to an item that is not a place (Wimborne Minster's "church":
  the same name, another kind of thing, not a namesake abroad, so the first check lists it only by chance). From
  Wikidata's instance-of where the index has it (the `instanceOf` of the item against the classes of places), else
  the description's first noun. Count first. Not built.
- The project's "Glasgow" (`place/k5nzg3aj`) is aligned to Glasgow, Kentucky (Wikidata Q1762833 from the old import,
  LoC n82030840 through it), as its "Madison" is to Madison, New Jersey. The place of publication "Glasgow" (65770,
  imported) and the keywords "Glasgow" (76007, and 46908 in runs where nothing else contests it) stand on it. With
  the other "aligned elsewhere" places of finding 119 this wants one look over the old import's Wikidata alignments
  of bare place names. Counted 2026-11-15 (`reports/q-import-us.txt`, the query `q-import-us.sql` beside it): of the
  956 imported places with a Wikidata item, 166 have a bare name and an item described as in the United States,
  Canada, Australia, New Zealand, South Africa or Jamaica. Most are right (Pittsburgh, Baltimore, New Haven, Ann
  Arbor, the university-press towns); about forty read wrong on their face, a British name on an obscure namesake
  (Brisbane a ghost town in North Dakota, Cardiff a hamlet in New York, Croydon in Utah, Farnham in Virginia, Perth
  in Tasmania, Belfast in South Africa, Harrogate, Woolwich in Maine, Dunstable, Chelmsford and Truro in
  Massachusetts, Buxton in North Dakota, Durham in New Hampshire, Atlanta in Texas, Raleigh a suburb of Memphis);
  and some cannot be told without the record (Cambridge, Massachusetts; Durham, North Carolina; Athens, Ohio). The
  owner asked whether importing the Wikidata dump again would mend it: no, the dump's items are right about what
  they are (Q1762833 is Glasgow, Kentucky); what is wrong is the link from the project's entity to the item, which
  came in with the old database's alignments (method `imported`, 2026-10-18; the assistant's inference from the
  method and date, not checked against the old database). To mend: an audit list at the workbench of imported places
  whose item is abroad and has a namesake at home with more sitelinks, each with its records' publishers beside it,
  and one action to move the entity to the namesake; the entries are also findings for the cataloguers' list where
  the old system's link is still live in a source.
- Stored evidence keeps only so many vetoed candidates, so a rule that vetoes cannot be counted by searching the
  reasons for its words (finding 124's first run showed one string by that search and seven by comparison). The
  query that does count it, candidates that stood in the last run and do not stand in this, is
  `q-diff124c.sql` in the 2026-11-15 scratchpad; make it an Align command (`diff-runs --stood`) when next needed.

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
  only when the string's own index stage puts one of its records at the top; Finding110Tests; measured by run krbnw6,
  doc 10 7.6 "Measured 2026-11-06 (run krbnw6)": five to queued by the rule, fourteen to vote on the same entity by
  the ladder's own rules with evidence, "St Ives (Cornwall)" to read). The measure that shaped it, kept: whose bare label the
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
- ~~US state abbreviations in a qualifier~~ (built 2026-11-08 evening as finding 117, doc 10 7.6 "Built 2026-11-08 (evening),
  finding 117"; measured by run v7aq7v the same evening, 33 strings settled or relabelled, nothing worse) ("Hamden, CT", "San Marino, CA.", "Durham, NC" against LoC's "(Conn.)", TGN's
  "Connecticut"): the qualifier reading knows neither the postal nor LoC's abbreviations, so a string placed by its
  state is not placed (seen in jnagjy's diff, 2026-11-08, when finding 113 placed the legacy entity through its
  heading and the string's "CT" then disagreed with "Conn."). A small table of the fifty states' postal and LoC forms,
  from general knowledge, credited as such; count first the place strings whose qualifier is a two-letter capital.
  Counted 2026-11-08 (the owner at "Hamden, CT", whose LoC card had no marker on the map while TGN's Hamdens had):
  240 place strings end in a short qualifier of five letters or fewer; the states lead ("Mass." 13, "Calif." 11,
  "NY" 9, "MA" 8, "Conn." 7, "Va." 6, "NJ" 5, "Conn" 4, "CA" 4, "Ca" 3, "Pa." 3, "Ga." 3, "Fla." 3, "Ill." and "Ill" 6,
  "IN" 3, "Ohio" 4), English counties beside them ("Kent" 10, "Essex" 9, "Cambs." 3, "Herts." 3), "Vic." and "Ont."
  for Australia and Canada. The same table would let a LoC-placed candidate on the desk borrow the coordinates of the
  TGN candidate whose parents name the state, so the chosen "Hamden (Conn.)" stands on the map and not only TGN's
  seven Hamdens.
- The placement join missing LoC's English form against TGN's vernacular label, to measure (seen in the forty: "Crete
  (Greece)" tied with TGN's Kríti, "Picardy (France)" with Picardie, "Cumberland (England)" with TGN's Cumberland,
  "Cambridge (Mass.)" with TGN's Cambridge in Massachusetts, each the same place in two groups, so that finding 108
  would take LoC's record alone and lose TGN's id). Count the queued place-field and keywords strings whose top tie
  holds a LoC heading and a TGN record whose chain contains the heading's parenthetical, before any change to the join.
  Its supposed sibling of 2026-11-07, the St/Saint spelling behind "St Ives (Cornwall)", was read directly on the 8th and
  is not a key gap at all (LoC and TGN carry every form); the fault is finding 113, an entity placed through its
  records when its label has no qualifier (doc 10 7.6 "Read 2026-11-08, finding 113 shaped"; three strings), built
  2026-11-08 on the word (Finding113Tests), measured by the reconcile after.
- ~~A subdivision placed by its sibling parts~~ (built 2026-11-08 as finding 114, doc 10 7.6 "Built 2026-11-08, findings 113 to 116"; measured by the reconcile after) (2026-11-07, "Birmingham" in keywords:sub, string 79377: its parent
  headings read "Architecture--England--Birmingham" and "Art, Modern--19th century--England--Birmingham", the sibling
  part "England" beside it, and the desk showed Alabama's group first). A place subdivision whose parent heading has
  another place part is placed by it, as a qualifier places a name (findings 84 and 106 read the placement), so the
  group whose TGN chain or LoC qualifier holds the sibling stands and the others are set aside. Counted 2026-11-08 (the
  assistant's first direct query): of 106 queued keywords:sub strings, 73 have a sibling part in a parent heading
  decided as a place ("Aberdeen", "Antwerp", "Armagh", "Athens", "Boston", "Brescia", "Cambridge", "Camden",
  "Cardiff", "Christchurch", "Coventry", "Cyprus", "Dedham"), most of the keywords:sub queue. Waiting on the word.
- ~~Dates that miss by a year or two on one side~~ (built 2026-11-08 as finding 115; measured by the reconcile after) (2026-11-07, "Hall, Harry, 1815-1882" at the desk: ULAN's and LoC's
  painter of 1813-1882 vetoed, the death agreeing exactly, the birth two years out, and no record saying 1815 to excuse
  it as finding 111's shape does). Painters' birth years wobble between references; a conflict by at most two years on
  one side while the other side agrees exactly is a near miss, not another person, when no namesake with agreeing
  dates stands beside it. Count first: queued strings of wsw254 whose top group is vetoed on dates with one side
  within two years and the other agreeing, by context, with examples; then the rule, if the count and the examples say
  so (a tolerance of a year on a side, or the near miss sent to the desk at its name score rather than vetoed to 0.00).
  Counted 2026-11-08: 65 queued strings whose top group holds a record vetoed by one side a year or two out with the
  other side agreeing (keywords 44, keywords:head 20, persauthorfull 1: "Blacklock, William James, 1816-1858",
  "Cozens, John Robert, 1752-1798", "Chalon, Henry Bernard, 1771-1849", "Draper, Herbert James, 1863-1920", "Knight,
  Richard Payne, 1750-1824", "Landseer, Charles, 1799-1878", "Phillips, Claude [1848-1924] (author)"), the dated
  artists' headings almost all. Waiting on the word.
- ~~Prominence when every live group holds a project entity~~ (built 2026-11-08 as finding 116; measured by the reconcile after) (2026-11-07, "Boston" bare after the pass: the legacy Boston
  placed through TGN's Boston, Massachusetts in one group, "Boston (England)" in the other, each a project entity, and
  prominence standing aside because a project entity is among the live groups, so the bare name went from exact-name
  to the desk; "Geneva" the same). When one group holds a project entity, that entity is the evidence and prominence
  stands aside rightly; when every live group holds one, the evidence cancels, and prominence (Q100 against Boston,
  Lincolnshire) could decide as it does when none holds one. A second on the 7th at the desk: "Birmingham" in
  keywords:sub, Alabama's legacy entity (right for its one string, the Birmingham Museum of Art's 1991 Stuart legacy
  exhibition) and England's each in a group at 1.00, Alabama's first. Counted 2026-11-08: 41 queued strings whose top
  two groups both score 1.00 and each hold a project entity (place 15: "Athens", "Durham", "Gloucester", "New York,
  NY", "Washington"; keywords:sub 14: "Athens", "Boston", "Durham", "Middlesex", "St Ives", "St. Petersburg",
  "Victoria"; keywords:head 5; persauthorfull 5, the namesake authors: "Carter, George", "Roberts, William"; one each
  in keywords and publisher). For the persons prominence is no help; for the places it is. Waiting on the word. Count first: queued strings of d6h8va whose top two
  groups each hold a project entity and whose Wikidata sitelinks would separate them.
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
  element whole (the owner's query, 2026-11-06: one corpauthor string "Birmingham (England)", once, against the
  record's own "Birmingham (England). City Council."; a jurisdiction heading cut at its full stop, by the collector of
  the time or the catalogue's own export, to look at with the collector when the next export is collected).

  Read on the desk (2026-11-06, "Birmingham (England)" in keywords:head and "Birmingham" in keywords:sub, jr8hf5): no
  body of the name at all. The heading's string ties between TGN's city with LoC's heading whole and three bodies
  placed through it (five records), TGN's borough of the name (one source, placed), the keywords:sub string's
  placeholder, and three of the project's own aligned places labelled "Birmingham", each aligned to VIAF, Wikidata
  and LUX and to no TGN or LoC record, none placed, one of them Alabama's by its string's record (the Stuart legacy
  exhibition) and two England's. Nothing joins those entities to the index groups, so finding 35 (the entity in a
  two-source group) does not reach, the outvoted policy declines over firm entities, and the string is the desk's;
  the bare string in keywords:sub ties the same three against seven Birminghams of the world. So the count of 35 is
  two shapes mixed: real bodies of the name (Colchester's, with ULAN's record) and placeholders of other queued strings
  classed Type beside places, which the count took for bodies; and the shape that matters is the legacy aligned entity
  of the name that nothing joins. The rule to shape: an aligned entity whose Wikidata item is the one a TGN or LoC
  record reaches (the prominence lookup already walks from a candidate's URI to its Wikidata item) joins that record's
  group; then finding 35 takes the heading onto it, the entity gains the TGN and LoC ids, and two legacy entities of
  the one place meet at the desk as holders to merge. Three counts first:

      -- the 35 again, placeholders out: two or more non-provisional project entities among the live store members
      select count(*) filter (where classes >= 2) as different_classes, count(*) filter (where n >= 2 and classes = 1) as same_class,
             (array_agg(raw order by raw) filter (where n >= 2 and classes = 1))[1:12] as same_class_examples
      from (select s.raw,
              (select count(distinct e.class) from jsonb_array_elements(d.evidence->'groups') g, jsonb_array_elements(g->'members') m join entities e on e.id = m->>'uri'
               where coalesce(g->>'vetoed', 'false') <> 'true' and m->>'stage' = 'store' and e.status <> 'provisional') as classes,
              (select count(*) from jsonb_array_elements(d.evidence->'groups') g, jsonb_array_elements(g->'members') m join entities e on e.id = m->>'uri'
               where coalesce(g->>'vetoed', 'false') <> 'true' and m->>'stage' = 'store' and e.status <> 'provisional' and (m->>'score')::numeric >= 0.97) as n
            from decisions d join source_strings s on s.id = d.source_string_id
            where d.run_id = 'run-20261106-140520-jr8hf5' and d.method = 'queued' and d.valid) x
      where n >= 2 or classes >= 2;

      -- the join's reach: aligned entities with a Wikidata item and no record of the local indexes
      select e.class, count(*) from entities e where e.dataset = 'pmc' and e.status = 'aligned'
        and exists (select 1 from entity_equivalents q where q.entity_id = e.id and q.source = 'wikidata')
        and not exists (select 1 from entity_equivalents q where q.entity_id = e.id and q.source in ('tgn', 'loc', 'lcsh', 'ulan', 'aat'))
      group by 1 order by 2 desc;

      -- queued strings with such an entity among their live store members at 1.00
      select count(*), (array_agg(s.raw order by s.raw))[1:12]
      from decisions d join source_strings s on s.id = d.source_string_id
      where d.run_id = 'run-20261106-140520-jr8hf5' and d.method = 'queued' and d.valid
        and exists (select 1 from jsonb_array_elements(d.evidence->'groups') g, jsonb_array_elements(g->'members') m join entities e on e.id = m->>'uri'
                    where coalesce(g->>'vetoed', 'false') <> 'true' and m->>'stage' = 'store' and e.status = 'aligned' and (m->>'score')::numeric >= 0.97
                      and exists (select 1 from entity_equivalents q where q.entity_id = e.id and q.source = 'wikidata')
                      and not exists (select 1 from entity_equivalents q where q.entity_id = e.id and q.source in ('tgn', 'loc', 'lcsh')));

  Counted (the owner, 2026-11-06, on jr8hf5): different classes 4, the same class 108 (places nearly all: "Andover",
  "Antwerp", "Athens", "Birmingham", "Cambridge"); 104 of the 108 have an aligned entity with a Wikidata item and no
  local record among their cards at 1.00; and the join's reach is 2,191 aligned entities of that kind, Person 1,642,
  Place 453, Group 96, the legacy pipeline's alignments to Wikidata, VIAF and LUX with no TGN, LoC or ULAN id beside
  them. So the shape proposed is a store-side pass before any rule at evaluation time: for each such entity, the
  Wikidata record the store already holds (the prominence lookup reads them, source "wikidata", `same_as` carrying
  the TGN, LoC and ULAN URIs the item links) gives the local records the item is the same as, and the entity gains
  them as equivalents, marked by their method. Then the existing rules reach the legacy entities unchanged: finding 35
  takes a heading onto the entity whose TGN record its group holds, the holders check sends two legacy entities of
  one place to the desk to merge, and the duplicates page can list them. One count first, how far the Wikidata
  records in the store reach:

      select e.class, count(*) as entities,
             count(*) filter (where exists (select 1 from entity_equivalents q join authority_records r on r.uri = q.uri where q.entity_id = e.id and q.source = 'wikidata')) as item_in_the_index,
             count(*) filter (where exists (select 1 from entity_equivalents q join authority_records r on r.uri = q.uri where q.entity_id = e.id and q.source = 'wikidata'
                                            and exists (select 1 from unnest(r.same_as) u where u like 'http://vocab.getty.edu/%' or u like 'http://id.loc.gov/%'))) as reaching_a_local_record
      from entities e where e.dataset = 'pmc' and e.status = 'aligned'
        and exists (select 1 from entity_equivalents q where q.entity_id = e.id and q.source = 'wikidata')
        and not exists (select 1 from entity_equivalents q where q.entity_id = e.id and q.source in ('tgn', 'loc', 'lcsh', 'ulan', 'aat'))
      group by 1 order by 2 desc;

  Counted (the owner, 2026-11-06): of the 2,191, 2,126 have their Wikidata item in the store's index and 2,023 reach a
  local record through it (Person 1,566 of 1,642; Place 369 of 453; Group 88 of 96). The pass, built on the owner's
  word the same evening (doc 10 7.6 "Built 2026-11-06, the pass through Wikidata"; ThroughWikidataTests; the next
  reconcile after it is run measures it on the 104 strings): an Align command `align-through-wikidata [--dataset]`, a run of its own (so `undo-run` takes it
  back, equivalents and all). For each aligned entity of the dataset with a Wikidata equivalent and no record of the
  local indexes: read the Wikidata record from the store; of its `same_as`, the URIs of the local indexes whose record
  the store holds and whose class fits the entity's (TGN for a place; ULAN and LoC for a person or a body; AAT for a
  type); when the item links one record per source, each becomes an equivalent of the entity with method
  `through-wikidata`, confidence 1, the pass's run id; an item linking two records of one source (a city and its
  borough) is reported and left alone. The report: entities read, equivalents added by source, items skipped and why.
  Then the next reconcile measures it on the 104 strings (plan command 4): "Birmingham (England)" onto the project's
  Birmingham by finding 35 once its group holds TGN's city, two legacy Birminghams of England both holding it sent to
  the desk by the holders check, and the duplicates page listing such pairs. Test: an entity aligned to a Wikidata
  item whose record links TGN gains the TGN id; one linking two TGN records gains nothing and is reported; a heading
  of the name is then accepted onto the entity by finding 35.

- A queued string's placeholder takes the context's first class (keywords:head: a Type), and a qualified place string's Another on 2026-11-07: the "Ackermann" placeholder in corpauthor, a Person (the reading's guess from a bare
  surname; the field's first class is Group) standing for the firm, beside the firm's own entity.
  placeholder ("Colchester (England)", "Birmingham (England)") is a concept on its entity page until the desk decides it.
  The placeholder's class should follow the reading where the reading is plain: a qualifier that places it, a place.
  Count the queued strings with a placing qualifier whose placeholder is a concept, before changing MintAsync.
  Counted 2026-11-08, the queued strings' placeholders by context and class: persauthorfull Person 1,285; publisher
  Person 602 and Group 149; keywords Person 563, Type 411, Group 2; place Place 196; keywords:head Type 200, Person 188,
  Group 1; corpauthor Person 140, Group 71; keywords:sub Type 106. The keywords:sub placeholders are all Type (the
  context's first class) though the strings are places; the publisher ones Person four times in five, firms among them.
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

- One dataset per institution (2026-11-07): the library, the archive and the photo archive share the key `pmc`, so a
  person has one URI across them; a second dataset key for one of them would mint a second URI for the same person,
  since a project never sees another's entities. The manifest's contexts tell the sources apart (`library.author`,
  `archive.creator`), not the dataset key. Written down so the archive's collect stage does not start a dataset.
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

- The agent parser, to come back to (the owner, 2026-11-15: "At some stage we should go back and do the agent parser
  work, including a service for that as well"). Read against the item below the same day (a read of the code by a
  subagent, checked by the assistant only in its outline; the item's "Not started" is wrong, the three were built as a
  first version on 2026-10-17, log item 31, and none is on `main`, so none is deployed):
  - *(a) the endpoint.* There: `GET /v1/agent?s=&profile=&agent=` and `GET /v1/agent/profiles` in
    `LinkedArtNet.Dates.Service/Program.cs`, answering `AgentResultDto` (name and its parts, kind, the date text with
    the dates parser's result nested, roles and nationalities with AAT, qualifiers, the unconsumed text, confidence,
    notes, and the Linked Art Person or Group unless `agent=false`); five service tests; one row in doc 04. Lacking:
    the class hint in its three strengths and the word when the parsed kind contradicts a hard hint; the reconciler's
    shape (`StringReading`: keys, classes, a lifespan) beside the parser's own; POST and batch, a profile by name,
    capture of what is asked, as the dates endpoints have; any mention on the site's API page.
  - *(b) the playground.* There: `/agents` on the examples site, in the navigation, a profile chooser, ten samples
    (invented, PMC-shaped), the parts, kind, confidence, the date part linked to the dates playground, JSON-LD and C#
    tabs, the profiles table; three render tests. It parses in process, not through the service. Lacking against the
    dates playground: the link to the raw API answer, a samples page, real PMC strings.
  - *(c) the corpus.* There: `corpus/agents`, four files, 60 entries (57 verified), 22 with a nested dates
    expectation, authored with invented names; the ground-truth test in `LinkedArtNet.Agents.Tests`; `tools/Corpus
    agents`; the trainer's agent item kind, writing to `corpus/agents/proposed`. Lacking: strings from PMC and
    Wellcome that the reconciler has read (the store has them now, with what each was decided as); a schema for an
    agent entry and `promote` for one; a corpus browser page.
  So the work to come back to is: the class hint and the reconciler's shape on the endpoint, POST and batch; the
  corpus seeded from the store's decided strings (a person's decisions first, as ground truth for kind and name);
  the gaps from the runs in the item after next; and release, since all of it waits on `main`.
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
- Publication as a recorded act (2026-11-07, the owner's question about real-world workflows; section 5's lifecycle
  risk): an export is a run of its own, recording the store state it was taken from (the last reconcile, the rules'
  version, the counts, the queue's size, a readiness line a person reads before the consumer is told), and every
  entity and record it carries is marked with the export it first appeared in (`published_in` on entities, or an
  `exports` table with the ids carried). The delta the consumer takes (Activity Streams Create, Update, Delete) is
  computed by comparing the new output with the stored output of the previous export, never from source changes
  alone, so a decision changed at the desk reaches the consumer as an Update of every record that cited the string.
  Decided before the write stage is designed; it shapes decision 15.
- Nothing published is ever deleted (2026-11-07): undo-run, cleanup-orphans and the desk's accept treat a published
  provisional entity as merged into its successor (`merged_into`, as a hand merge does), never removed; an equivalent
  withdrawn after publication is kept as a retraction (the row kept with a `withdrawn_at`, or a method `retracted`),
  so the next export says so and a consumer that merged on it can unmerge; decision 15's question to the LUX team
  gains "how does LUX take an equivalent we withdraw". Count first: the entities the legacy export published that are
  provisional in the store today (the imported ones carry published ids; the minted ones are not yet out).
- A split, the opposite of merge, is missing (2026-11-07): a new entity, the chosen strings' decisions re-pointed to
  it, the old URI keeping the rest, the equivalents divided by hand on the two entity pages; cheap in the store, since
  decisions are per string. The one case the string key cannot express, one string that is two people across records
  ("Smith, J." the engraver and the publisher), wants a record-level exception, an override scoped to the record id
  ("this string, in this record, means Z"), designed with the collect stage, which is where the record ids come from.
- Strings that leave the catalogue (2026-11-07): collecting from a fresh export marks every string of the dataset seen
  in it (`last_seen` and the export's version), and the run's report lists the strings not seen, since a corrected
  spelling arrives as a new string beside the old one; the store stage takes the new string onto the old string's
  entity by exact-name when the name agrees, and the report says where it did not, so the desk can say "same as" and
  the entity keeps its URI. An entity none of whose strings is current is retired in the next export (a Delete, or a
  record that says so), never deleted from the store. The first real collect's report is the measure.
- A second project from the same PMC sources (the owner's thought, 2026-11-07): once the runs and the desk are trusted,
  a new project over the same exports, from scratch, with no legacy import and no desk history, to see how the toolkit
  does alone: the residue from zero, the desk's work measured in strings and minutes, and the two projects' answers
  diffed string by string (a second alignment of the same strings is the check on the first, and the places where they
  differ are the rules' or the desk's to read). It is also the first run of the collect stage from an export and so the
  rehearsal of the schedule above; the identifiers it mints are its own and never published.
  That fresh run is also when doc 16's scenarios are gone through whole (the owner, 2026-11-14): every picture retaken
  against the workbench as it then stands and the store as a new reviewer would meet it, and each scenario's text
  edited or dropped as the rules by then have changed what the desk shows. Until then a scenario's pictures are
  retaken only when a change makes them misleading, and the table at the end of doc 16 says which are stale.
- A report back to the cataloguers (the owner's observation, 2026-11-07): alignment shows up errors and imprecisions in
  the catalogue that librarians and archivists will want to fix at source, the type case a firm whose name changed
  slightly, two authorities, one string in the data; the desk decides the string one way, the cataloguers then make
  the records precise, and on the next run the two variants arrive as two strings (the old string leaves, as the item
  above has it, and the new ones reconcile; the records the old string resolved to the wrong firm are corrected by the
  next export's Updates). Actionable in a small way, with the collect stage: a report of what the desk found that the
  catalogue should carry (a string kept as a local authority with a note; a string decided "something else" from what
  its field said; a record-level exception, once there are such), per record id, in a shape a cataloguer can work
  from; and the split (above) for the case where the string must be two things before the catalogue is fixed.
- Authority reloads (2026-11-07): a reload of ULAN, TGN, LoC or LCSH lists the equivalents whose record is gone or
  now redirects to another, and marks the decisions on them for a look (doc 10 section 4's `valid = false` on an
  authority update is not built); the audit command is the start, since it already walks entities against the index.

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
