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

## 0. Resume point (written 2026-11-13 evening; the one place to read first)

**On return, in order:** (1) nothing is pending from the runs: rhf5rg (`run-20261113-190556-rhf5rg`, 19:05 to 19:50)
finished and is read, the last full run (doc 10 7.6 "Measured 2026-11-13 (run rhf5rg)": finding 119, ten strings, all
the rule's; nine places now queued that had been accepted onto a namesake's entity, "Madison, Wisconsin", "Newark,
Delaware", "Stamford, Lincolnshire" among them, for the desk: http://localhost:5200/p/pmc/review?context=place). (2) `git push` (the day's commits are not up: some thirty,
the last the one that wrote this). (3) Retake the holder pair's three pictures for doc 16 (stale since the same
day's changes: the pair's box, identifiers as links, fields in plain words; "Pictures last checked" at the end of
doc 16); Coventry is decided, so against a pair still queued (University of Oxford, 80550; Westminster, 107456), the
example rewritten to match, the owner asked first whether to keep Coventry's story. (4) The scenarios the store holds
live examples of, before the owner mends them: the bare "Dagenham" (`place/hvj4jgry`, aligned to a parliamentary
constituency; its sibling `place/twqpnzjy` the town) and "Northampton" (`place/q4fpqxks`, a diocese and an
archdeaconry among its identifiers), the list in doc 16 "Scenarios to write". (5) The desk, continued (command 1),
and the open items of section 7 as the owner orders them; the lifecycle design before any write stage.

**Where things stand.** Branch `de-pmc`, all work committed, every test project green. Six authority sources loaded
(ULAN, AAT, TGN, LoC names, LCSH; the Wikidata extract). The store is at schema version 10. Runs are occasional and a
run is measured by its diff against the run before (command 4), never by the queue count. The last finished run is
q9zpqk (2026-11-13 15:33: accepted 9,455, queued 3,756, minted 10,784; doc 10 7.6 "Measured 2026-11-13 (run
q9zpqk)"), which measured finding 118; it was taken back at 19:05 for rhf5rg (accepted 9,448, queued 3,764, minted
10,784), which measured finding 119 and is the last full run. Findings 1 to 119 are in doc 10 7.6, newest first. The owner's word
for the work, 2026-11-13: the aim is to clean and align the data accurately, and the tools are to make that smooth
and satisfying. Built on the 13th, all from the owner at the desk: the scenarios document for end users (doc 16:
scenario, example, solution, watch for, with pictures; the holder pair written; the list to write and the table of
pictures last checked at its end; served at /help/scenarios); finding 118 (a body placed in a city is not the city:
the dioceses off some twenty place entities); on the desk, a held identifier is a link and says what its record is,
and a holder pair is a box, the sentence its head, the entities its rows, the merge its foot; on the entity page,
what the entity stands for at the head (its strings, their fields, their records) and the identifiers in sets that
cite one another, each set under what it is and removed as one, a VIAF number described by the Wikidata record that
gives it; a field's name in plain words from the manifest ("place of publication"; the PMC labels the assistant's
reading, the cataloguers' to correct; the saved manifest refreshed); finding 119 (a bare entity aligned to a namesake
elsewhere is not "the project's one": ten of 63). Left with the owner: "Northampton" and the bare "Dagenham" to mend
by hand (or kept a day as scenario examples); six holder pairs still queued; `resplit.txt` in the repo root to
delete.

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
never SQL; the workbench is the owner's to start and stop, and the assistant builds neither the workbench nor
`tools/Align` while it runs; a commit that touches the workbench waits for a stop, and the assistant compiles to a
scratch folder with `-o` meanwhile. The answered queries and one-off SQL of earlier days are in doc 10 7.6 and doc 11
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
         where d.run_id = 'run-20261114-161045-vd2cg3' and d.method = 'queued'
           and (select count(*) from jsonb_array_elements(d.evidence->'groups'->0->'members') m where m->>'source' = 'store') >= 2
         order by s.raw;

   - Place names among namesakes, with the map ("Cambridge" in place, id 65756; "Newport", 65782; "San Francisco,
     CA" still queued among namesakes): the chosen candidate on the map, hollow when its coordinates are borrowed, and
     the unplaced named under it.
   - Ten strings each from publisher, corpauthor, keywords:sub and place (the context dropdown), deciding them as a
     reviewer would. A string you cannot decide from what the card shows is the paste that matters most: say what you
     would have needed.
   - The things built on the 8th, each once if not yet seen: "Pull in from the sources" on an entity with Wikidata
     (Cozens, `person/kmzm64y9`), the pictures and the LUX self-echo notice (Peabody, `group/a3sfhumg`); "show
     pictures" on the desk; the tray from two entity pages and the merge page; Recent changes on the project home
     after a hand change; an identifier another entity holds, refused.

2. No reconcile is pending. vd2cg3 (2026-11-14 16:10) is the last full run: findings 120 to 122 measured, queued 3,734. The
   next reconcile comes with the next rule: the last full run taken back, `reconcile`, then command 4 against it. The next reconcile comes with the next rule, as always: the last
   full run taken back, `reconcile`, then command 4 against the run before, the assistant running it with the
   workbench stopped:

       dotnet run --project tools/Align -- undo-run --run run-20261114-161045-vd2cg3
       dotnet run --project tools/Align -- reconcile

3. The records behind the strings, collected again after the next library export (the same catalogue, the same
   record ids, so the newer export's records replace the older's); the publisher strings without a record, and the
   cataloguers' report ("Art, Baroque--Italy--ome", the collector and jurisdiction headings cut at a full stop), wait
   on it:

       dotnet run --project tools/Align -- collect-examples --file "C:\pmc4-03-184-03-11_library4-03-11_library.xml" --source library-2024-03 --keywords "C:\Users\TomCrane\Dropbox\digirati\PMC\linked.art\pmc_subjects_csv.sql"

4. The standing method diff after the next run, against vd2cg3, the last full run (each run's last decision per
   string, since a string the second pass settled has two; an undone run keeps its evidence, though the undo removes
   the provisional entities it minted and nulls their decisions' entity, so `same_entity` reads null for those and
   true or false only where the entity survived). A method change on the same entity is a label, not a decision:

       with a as (select distinct on (source_string_id) source_string_id, method, entity_id from decisions where run_id = 'run-20261114-161045-vd2cg3' order by source_string_id, id desc),
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
- An entity's label when a string is accepted with its post town or county in it (2026-11-14, the owner at "Donnington", string 27535: the candidate "now held by the project's entity Donington, Spalding"). A string accepted onto a record with no project entity yet mints one labelled with the string as written, "Donington, Spalding", "Crowmarsh Gifford, Nr. Wallingford", "Padiham, Nr Burnley", "Paulton, near Bristol". The label to publish is the place's, with a placement a reader can use: the record's own label and its first parents ("Donington (Lincolnshire, England)"), the string kept as a variant. Count first: the entities a run mints whose label carries a comma or a bracket that the record's label does not.
- ~~A record matched on a variant spelling listed ahead of the records that bear the name~~ (counted and built 2026-11-14 as finding 122, an order among equals only, doc 10 7.6; the count showed a rule that demoted variants would be wrong, "Rome" being TGN's Roma. Left for the desk and "Ask for help": which Donnington a saleroom's place of publication is) (2026-11-14, the same card: "Donnington" lists TGN's Donington, Lincolnshire, first at 1.00 through its variant "Donnington", ahead of TGN's two Donningtons, in Shropshire and in Berkshire; the place of publication is most likely Donnington Priory at Newbury, the saleroom). `ByVariant` exists for LoC (finding 74); TGN's variants score as the name. A record that bears the name as its heading should stand ahead of one that only lists it. Count first: queued strings whose first group matched only on a variant while a later group bears the name.
- ~~Two headings of one source in one group~~ (counted and built 2026-11-14 as finding 121, doc 10 7.6: 57 first groups with a typed heading beside the untyped, 42 accepted by vote; one record of a source stays with a holder, the others set aside. Measured by run kg6v85 the same day. Left: the handful of typed headings a run still writes where the untyped one was not a candidate or tied, "Mainz (... : Landgerichtsbezirk)", "Iona (Scotland : Parish)", "Antrim (N.H. : Town)", "Cornwall (England : Duchy)". The scenario for doc 16 has its four pictures, `docs/img/scenarios/two-records-one-source-*.png`, taken before the rule, and is to be written) (2026-11-14, the owner at "Poughkeepsie, New York", string 66289: "why are there different LoC ids?"). The group holds LoC's "Poughkeepsie (N.Y.)" (n81090425, "City of Poughkeepsie") and LoC's "Poughkeepsie (N.Y. : Town)" (n89123175), both placed through TGN's one Poughkeepsie, and the project's two entities each hold one of them, so the desk offers a holder pair. But the Library of Congress keeps two records because they are two things, the city and the town around it, and a merge gives the survivor both. A source that holds two records of a name, neither naming the other, is saying they differ: the typed heading ("N.Y. : Town") should not join the placed record the untyped one joins, or the group should say "LoC holds these apart" and the merge not offer both identifiers. Finding 118 barred a type word only across classes; these are both places. Count first: the groups of the last full run with two or more records of one source that are not each other's sameAs, and of those the ones with a type word. A scenario for doc 16 ("the authorities hold two, the project means one").
- ~~A place written with a nearby town after the comma~~ (built 2026-11-14 as finding 120, doc 10 7.6, `Reconciler.ScoreWithNearbyAsync`; measured by run kg6v85 the same day after one mending, 41 strings placed, all rightly. Left from the count: the regions the rules should place and do not, "Enfield, Middlesex", "Swindon, Wilts", "Ulverston, Lancashire"; and the strings whose right place is not among the candidates fetched, "Wilton, Salisbury", "Botley, Oxford", "Heaton, Bradford") (2026-11-14, the owner at "Bottrop, Essen", string 20654, place of publication of "The Boydell Shakespeare Gallery" (1996), publisher "Pomp": "why is Bottrop in the place field at all?"). The reconciler reads what follows the comma as a qualifier, a region that contains the place, and TGN's Bottrop is "not placed (Essen vs North Rhine-Westphalia, Germany, Europe, World)", so the string queues beside two Bottrops. Of the 186 place strings queued in run rhf5rg, 79 carry "qualifier not placed", and many are of this kind, the English postal habit of a village and its post town: "Firle, Lewes", "Chatsworth, Bakewell", "Botley, Oxford", "Dedham, Colchester", "Culham, Abingdon", "East Bergholt, Colchester", "Goodwood, Chichester", "Heaton, Bradford". Others in the same 79 are two places of publication with a comma for the manifest's semicolon ("Kingston, Montreal", "London, New York & Bombay"), an address ("London (59 Gordon Sq., W.C.1)", "Glasgow, Central Hotel"), or the desk's new nine (finding 119). A rule to count first: when the qualifier is itself the name of a TGN place whose coordinates lie within some twenty kilometres of a candidate of the name, the qualifier places that candidate ("near Essen"), as a containing region would; TGN carries coordinates for both. The count: of the 79, how many have one candidate so placed and no other. A scenario for doc 16 either way ("a village and its post town"). Counted 2026-11-14 against rhf5rg (`reports/q-near.txt`; "Bottrop, Essen" itself decided by the owner meanwhile and not in it): 71 of the 79 are a name, a comma and a qualifier (the other eight carry a street address in brackets). In 25 the qualifier names a TGN place and exactly one of the card's candidates lies within twenty kilometres of it, and all 25 are the right place: "Barnsley, Cirencester" (6.9 km, among six Barnsleys), "Chatsworth, Bakewell" (Chatsworth House, 4.8), "Dedham, Colchester" (9.4), "Egerton, Ashford" (11.8), "Goodwood, Chichester" (6.6), "St Ives, Huntingdon" (8.8, both spellings), "Stowe, Buckingham" (2.9), "Wheelock, Sandbach", "Culham, Abingdon", "Thatcham, Newbury", "West Dean, Chichester", "Chillingham, Alnwick" the farthest at 18.1. One has two candidates near ("Beaulieu, Brockenhurst": the village and the river). In 33 the qualifier names a TGN place and no candidate on the card is near: a county, state or country that the present rules should have placed and did not ("Enfield, Middlesex", "Ulverston, Lancashire", "Swindon, Wilts", "Victoria, Australia", "Murcia, Espana"), finding 119's nine, and strings whose right place is not among the card's candidates at all ("Wilton, Salisbury", "Botley, Oxford", "Heaton, Bradford", "Firle, Lewes" with no TGN candidate of the name), which is a second question, the candidates fetched, not the qualifier. In 12 the qualifier is no TGN name: abbreviations ("Cambs.", "Eng.", "N.S.W.", "Co. Down"), several places ("London, New York & Bombay", "Rutherford, Madison, Teaneck"), a hotel. So the rule as stated settles 25 of the 186 queued places, none wrongly on this evidence; twenty kilometres is the right side of the one near miss ("Tisbury, Salisbury", 21.0).
- "Ask for help" at the desk: an LLM's advice on the card in front of the reviewer, and its hand if wanted (the owner, 2026-11-14, after following doc 16's University of Oxford and meeting "Bottrop", a place unknown to him; noted for later, the scenarios first). The owner's brief: a button on the desk that sends the review in a clear, efficient prompt, as cheap to run as it can be without losing usefulness or accuracy; the answer tells the reviewer what to do so that they can do it themselves, and can offer to do it for them, which means an instruction format the desk can execute, including work beyond the card (the tray, an entity's identifiers); a brief exchange, not one shot; and a way of learning the workbench, since each answer names the steps. The assistant's design notes, agreed in outline and not built:
  - *When.* After more scenarios are written: doc 16 and doc 13 are the prompt's stable part (what each action does, when the usual answer is wrong), and the actions it may propose are the desk's own, which are still moving.
  - *Shape.* One Messages API call per ask, through the official C# SDK, from the workbench's server; the key from the environment only (`ANTHROPIC_API_KEY`, the standing rule). The system prompt (the guide's actions, the scenarios, the project's fields and their labels, the answer's schema) is frozen and cached; the card is the user message, rendered as compact text from what `ReviewItem` already holds (the string, its field, the records, each group and member with its reasons, each project entity with what it stands for and what its identifiers are). A follow-up question appends to the same conversation. Catalogue strings and pulled source text are data in the prompt, never instructions.
  - *The answer.* Structured output: a recommendation in a sentence, the reasons tied to what is on the card, what the model knows beyond the card said as such ("from general knowledge: Bottrop is a city in North Rhine-Westphalia"), its confidence, anything it would want checked, and `steps`, a list from a closed vocabulary that mirrors the desk and the entity page (accept a group, same as, just this one, accept the ticked together, merge entities with a survivor and class, remove an identifier or a set, keep as local authority with a label, add a note, skip, look again). Each step names its target by an identifier that is on the card; the desk validates every step against the card before offering it, so nothing invented can be executed.
  - *Doing it.* "Do this for me" runs the steps through `ReviewService` and `CurationService`, the same code as a click, one at a time, shown as they go; a merge still asks, since it is permanent. Every change so made is noted as the reviewer's with the model's help (the audit log says which model and keeps the recommendation), and the decision's method stays human.
  - *Cost.* Counted per ask from `usage`, shown on the desk and totalled per project, with a monthly ceiling in the manifest. Estimate, to be measured: a card is some 3,000 to 6,000 tokens in, the cached part as much again, the answer a few hundred out; at 2026-11 prices that is a fraction of a cent on Claude Haiku 5.5, one to two cents on Claude Sonnet 5.5, two to four on Claude Opus 5.5. The model is a project setting, the owner's choice.
  - *Knowing it is right.* The store already holds the test: the strings a person decided. Replay their cards as they stood, ask, and compare the recommendation with what the person did; that says which model is good enough, and what each prompt change costs or gains, before anyone relies on it. A small eval set of the scenarios' own examples first.
  - *A teaching mode* (the owner, 2026-11-14, after a day of cards that needed explaining: the placeholder edited by hand, the publisher card with "nothing to do", the two Donningtons, Poughkeepsie's city and town: "these experiences as a user reinforce for me the need for an AI-assisted teaching mode that would guide the user through decisions until they were confident in the use of the tool"). The same call with a different manner: for a reviewer who is learning, the answer walks the card in order (what the string is and where it was seen, what each candidate is and why it is there, what the software could not decide and why, what each button would do here), asks the reviewer what they would do before saying what it would, and names the scenario of doc 16 the card is an instance of. It is a setting of the reviewer's, on by default for a new one and turned down as they go (full walk, a hint, off), and the questions asked today are its first test set: each is a card, a confusion and the explanation that settled it.
  - *What is already there* (the owner's reminder, 2026-11-14). The trainer has asked Claude for a proposed reading of a date since 2026-10-17 (doc 08 "Asking a language model"; `LinkedArtNet.Dates.Llm.ClaudeDateInterpreter`, the official C# SDK `Anthropic` 12.58.0; hosted by the examples site's `InterpreterHost`). Its pattern is the one to follow and partly to share: the key from `Interpreter:ApiKey` or `ANTHROPIC_API_KEY` (already in `deploy/linkedart/.env.example` and the compose file), no controls shown without a key, the model and effort as settings (`claude-opus-5-5`, effort `low`), the system prompt a committed document embedded in the assembly and marked for prompt caching, structured output, a refusal handled, usage counted per call (input, output, cache read and write), and the rule that the model proposes and a person confirms. New for the desk: the steps a person can have executed, the follow-up exchange, the spend shown and capped, and the workbench's own configuration and compose entry. Doc 08 says the trainer is "the project's only use of an LLM"; that sentence changes when this is built.
  - *The owner's answers, 2026-11-14.* The monthly allowance is API credit, and the owner can make a key (or may have one); it goes in the environment as `ANTHROPIC_API_KEY`, never in the repository or the chat. Catalogue records may be sent to the API. Web search is not on by default: when the model needs it, it says so and asks the reviewer, who may allow it once or for the rest of the session; the choice is the reviewer's and is kept for the session only.
- The project's home page timed out (2026-11-14, the owner signing in from a second Chrome profile after a restart: Npgsql's thirty seconds, in `CountsAsync`). The count of authority records scanned the whole table, 19.4 million rows and 1.49 million pages read cold, 23 seconds warm-ish; mended the same morning by summing `authority_sources.records`, which the loaders keep (the whole query now a second). `authority_records` had last been vacuumed on 2026-10-18 and carried 2.26 million dead rows: the Align tool has a `vacuum` command from the same morning (`PostgresAlignmentStore.VacuumAsync`: the authority records, decisions, strings, entities and equivalents, each vacuumed and analysed; no row changed), run then (151 seconds; 2,263,245 dead rows in the authority records, 378,179 in decisions; a full count after it 9 seconds). Left: the per-source totals the home page now sums come to 19,467,495 and the table holds 19,419,848, some 47,600 fewer, so one source's total is stale and is to be found and refreshed; any other page that counts or scans that table whole is to be looked for, and `vacuum` belongs after a run is taken back and after a day of pulls.
- ~~A field's name in plain words~~ (built 2026-11-13: `Label` and `About` on a context in the manifest, `ProjectManifest.FieldName` and `FieldAbout`; the PMC labels and sentences are the assistant's reading of the export's field names and of the records, the cataloguers' to correct; the desk's head, its rows and placeholders, the entity page's panel, records, waiting list and strings table say the label, the field's own name on hover; a label is not a reading, and `Differences` says "named or described differently"; the saved manifest refreshed with `manifest --use-default`. The context dropdown and the lists keep the fields' own names) (2026-11-13, with the panel at the head of the entity page, "This entity stands for 1 string ... in the field place": `place` is the place of publication, `corpauthor` a corporate author, `keywords:sub` a subdivision of a subject heading, and the page can only print the context's own name). The manifest gives each context a label and a sentence ("place of publication"), and the desk, the entity page and the lists use it wherever a context is named to a reviewer. The same panel on the desk's project-entity rows, which say "stands for 1 string in place", follows from it.
- ~~What an identifier is, on the entity page, and a VIAF or LUX identifier said through the record that cites it~~ (built 2026-11-13: `IdentifierSets`, the entity's identifiers gathered into sets that cite one another through `sameAs`, the Wikidata extract's `ids` and a pull's "same as" facts, or cite a common third; the entity page boxes each set under what it is with "Remove all N", each row says what its record is (`RecordSummary.Says`), a VIAF number "the viaf identifier that wikidata's Q3403624 gives (...)", and an identifier nothing describes says so and points at the pull; on the desk a held identifier without a record says "given by wikidata's Q...". Not built: a lookup of the whole extract for a VIAF number no held record gives, which wants an index on `data->'ids'`; a LUX identifier joins a set only once pulled) (2026-11-13, the owner at the bare "Dagenham", `place/hvj4jgry`, meaning to remove the constituency's identifiers there: "it's hard to tell what the VIAF one actually is when you go to it on VIAF.org"). The entity page's identifier table shows a record's label and dates only, so the two Wikidata rows of the Dagenhams both read "Dagenham"; it should say what the desk now says (`RecordSummary.Says`). VIAF and LUX identifiers have no record in the store, but the Wikidata extract's records carry `ids` (`"viaf": "143706493"` on Q3403624): a VIAF identifier an entity holds can be described as "the VIAF number Wikidata's Q3403624 gives (Dagenham: Parliamentary constituency ...)", first from the entity's own Wikidata identifiers, then by a lookup of the extract; and identifiers that came as one set (a Wikidata item, its VIAF, the LUX record of the same import) could be offered for removal together. Desk and entity page both.
- ~~What a held identifier is, said where a merge is confirmed~~ (the rule question built the same evening as finding 119, doc 10 7.6: the count, 10 of 63, and the bare entity aligned elsewhere not joined; measured by the next reconcile) (built 2026-11-13: on the desk every held identifier, in the confirm step, the holder-pair notice and a project entity's row, is a link to its record followed by what the record is when the store holds it, `RecordSummary.Says`: the label and the Wikidata description, a pull's, or TGN's first two parents, "wikidata's Q3403624 (Dagenham: Parliamentary constituency in the United Kingdom, 1945-2010)"; a row lists the entity's other identifiers after those of the group; VIAF and LUX identifiers, with no record in the store, are links only. Open: the rule question at the end, with its count) (2026-11-13, the owner at "Dagenham (London, England)", string 119335: "how does this come about where the two Dagenhams have different VIAF identifiers?"). The import had two entities: "Dagenham (London, England)" holding Wikidata's Q1075299, "Town in east London, England", and a bare "Dagenham" holding Q3403624, "Parliamentary constituency in the United Kingdom, 1945-2010", the earlier pipeline's alignment of a place of publication. The bare one joined the placed group as "the project's one Dagenham" and the desk offered the pair. The confirm step and the entity rows list identifiers as numbers only; the Wikidata extract's description (in `authority_records.data`) and each held record's label, shown beside the identifier, would have said the two differ before the merge. Also to consider: a project entity whose Wikidata record's `instanceOf` is not the kind the group's records are (a constituency against an inhabited place) should not join as "the project's one". Count first: the bare entities joined by that rule whose held Wikidata description differs in kind from the group's.
- The scenarios served (built 2026-11-13: /help/scenarios renders doc 16 from the assembly, its pictures at /help/img/scenarios/, the two documents' links by file name turned into the pages', a line at the head of each to the other, and the holder pair's box on the desk links "How to decide this, with an example" to #holder-pair). The document itself, below, stays open.
- The scenarios document (doc 16, begun 2026-11-13 at the owner's asking: scenario, example, solution, with pictures): one scenario written, the holder pair; a scenario is added when one is met at the desk, the pictures taken from the running workbench at the time. The list to write is in doc 16 itself ("Scenarios to write", agreed 2026-11-13: sixteen at the desk, six cleaning an entity, three putting something right), and so is the table of when each scenario's pictures were last checked: after any change to the desk's or the entity page's markup, the scenarios that show the changed part are retaken (the owner, 2026-11-13: "we'll also need to go back and refresh them as tweaks are made to the UI"); the holder pair's are stale since the same day and to retake first. The workbench's Help renders doc 13 only (embedded at build); serving doc 16 and its pictures beside it, linked from the guide and from the card where the scenario is recognised, is part of this item.
- Twins that share a record (2026-11-05: "Lawrence" in corpauthor on the desk and "Lawrence" in publisher as a search result, two placeholders, the same three Crewkerne sale catalogues behind both, the name in both fields of each record). When another placeholder of the string's name has a record in common with the string's own, the desk says so on its card or search result ("in the same records as this string"), as a hint towards "Same as" or "Accept together"; the reconciler keeps twins apart on purpose (finding 104), so the hint is the desk's, not a rule. The ticks are on the candidate cards only; a twin met among the search results has "Same as this" and nothing to tick, and the hint must say which action it points at where it appears.
- ~~The publisher strings without a record behind them~~ (closed 2026-11-14: collected again from `C:\pmc4-03-184-03-11_library4-03-11_library.xml` as source `library-2024-03`, 63,392 records, 122,384 examples; publisher 11,141 of 11,141 strings now have a record, from 10,612. Left without one: keywords:head 73, corpauthor 25, place 22, keywords:sub 12, to look at): the cause found 2026-11-14 (the owner at the publisher string "Osterley Park", "seen 1 time as publisher" and no record on the card): the export writes a publisher the cataloguer supplied in square brackets, "[Osterley Park]" on record D2805, "A guide to Osterley Park, Isleworth" ([1939]), and the 2024 strings have none; 487 of the 529 publisher strings without a record are explained so (the other 42 to look at after). `HeritageExampleCollector` offers both forms from that day, as it did for place. The export's path is the owner's, given that day; command 3 of section 0 names it. (2026-11-05: the entity page of a placeholder "Stanley", one string, publisher, no "In the records"; the collection covered 10,608 of the field's 11,141 strings, this one not among them). List them beside what the export's publisher element holds for the same records and find the cleaning the 2024 process applied, as the cleaned-keywords table closed the keywords gap; a `collect-examples` report of the export values that matched no string would do the listing.
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
