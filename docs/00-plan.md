# Plan: linked-art-net

Status: proposal 2026-10-13; Phases 0-3 delivered and live at linkedart.net as v0.1.0 on 2026-10-16 (status
review in §7). Branch `de-pmc` is the working branch and `main` is what is deployed (README, "Branches and
releases"). The brief is in `exclude/design.md` (gitignored); this plan and the design docs in this folder are the
committed record.

## 0. Resume point (written 2026-10-22 night; the one place to read first)

This section is rewritten at the end of every working day. It lists what stands, what the owner has to decide, what
is built and waiting for a go, and the commands to run on return. The detail behind each line is in
[10-alignment-toolkit.md](10-alignment-toolkit.md) section 7.6 (the findings, each dated) and in the open items of
section 7 below; section 13 of doc 10 holds the owner's decisions already taken.

**Where things stand.** Branch `de-pmc`, all work committed, CI green. Five authority sources loaded (14.5M records),
the LoC "-781" subdivision forms deleted from both files. The store has had two duplicate passes (292 and 89
merges) and two hand merges ("England", "London"); the manifest was refreshed on 2026-10-22 for the keywords field's
parser. The last measured run is run-20261022-131239-sfny58: accepted 8,737 (vote 3,682, single 1,792,
exact-single 3,263), queued 4,572, matched in store 5,859, minted 11,085, lists 404, compounds 15,558; by context
queued persauthorfull 1,325, keywords 1,122, publisher 863, keywords:head 553, place 331, corpauthor 198,
keywords:sub 180. The two days' path: 8,518 (Monday) -> 6,647 -> 5,763 -> 5,586 -> 5,296 -> 5,009 -> 4,828 ->
4,572. Doc 10 7.6 has a "Measured" paragraph per run with the samples read; the last is "Measured ... sfny58". Hand
merges done 2026-10-22: Italy, Kolkata; Dumfries kept apart (the town and the old county) and the county relabelled
Dumfriesshire.

**Decisions taken by the owner on 2026-10-22:** the Agent parser as an objective (objectives 2 and 6 rewritten,
section 7 item 34); prominence waits for the Wikidata extract; decision 9 of the morning (bare TGN-only namesakes)
closed on a 0-row ceiling; finding 32 (a cluster-id entity beside a dated group) to the desk; findings 35 and 40
(the project's aligned entity breaks a place tie; two sources each with one exact record of a body join) yes, built;
finding 48 (exact-single tolerates lookalikes below acceptance score) yes for concepts and places, built; "Cornwall"
in keywords:sub was run order, not a duplicate (finding 51, a second pass, in the next builds). Yesterday's: finding
3 yes, outvoted rivals for places and concepts yes, `unrec_` kept, finding 14 yes, the duplicate pass, decisions 6
to 8 (home sets aside inexact rivals; a dated string's lone undated record; the "-781" twins).

**Decisions still owed by the owner:**

11. *Outvoted rivals for bodies when the qualifier places the winner* (finding 60, doc 10 7.6 "Measured ... sfny58"):
    "National Maritime Museum (London)" has TGN's museum placed by "London" with LoC's heading placed through it,
    and ULAN's three records of the museum unplaced beside them; the outvoted-rivals policy excludes bodies, since
    a name-only tie is weak evidence, but a qualifier that places the winner is not name-only. Proposed: for a body,
    a two-source group one of whose members the string's qualifier places outvotes single-source rivals at
    acceptance score, as for places. Recommendation: yes. Ceiling (queued strings with a qualifier, exactly one
    two-source group at acceptance score with a placed member, and a single-source rival at acceptance score), the
    owner runs: `select ss.context, count(*) from decisions d join source_strings ss on ss.id = d.source_string_id
    cross join lateral (select count(*) filter (where jsonb_array_length(g->'sources') >= 2 and
    (g->>'score')::numeric >= 0.9 and exists (select 1 from jsonb_array_elements(g->'members') m where m->>'reason'
    like '%qualifier places it%')) as placedmulti, count(*) filter (where jsonb_array_length(g->'sources') = 1 and
    (g->>'score')::numeric >= 0.9) as single from jsonb_array_elements(d.evidence->'groups') g where g->'vetoed' is
    null) x where d.run_id = 'run-20261022-131239-sfny58' and d.valid and d.method = 'queued' and
    d.evidence->'reading'->>'qualifier' is not null and x.placedmulti = 1 and x.single >= 1 group by 1 order by 2
    desc;` Measured 2026-10-22: 18 strings (keywords:head 15, place 2, keywords:sub 1). The owner's yes that
    evening, built before the next run (`EvaluateAsync`: a placed member of the winning group lets a body's rivals
    be outvoted).
12. *A concept against TGN hamlets alone* (finding 63): "Peers" has AAT's "peers (aristocrats)" exact and two TGN
    hamlets called Peers; "Dating", "Hall" and "Cross" are the same shape. Proposed: in the keywords family, when the
    only exact concept record (AAT or LCSH) stands against TGN-only groups, the concept wins; a bare common noun in a
    subject field names the idea, and a place would carry its qualifier. Recommendation: yes. Ceiling (queued
    undated keywords-family strings with exactly one live non-TGN group, exact, and every other live group TGN-only),
    the owner runs: `select ss.context, count(*) from decisions d join source_strings ss on ss.id =
    d.source_string_id cross join lateral (select count(*) filter (where g->'sources' <> '["tgn"]'::jsonb) as
    others, bool_and(g->'sources' = '["tgn"]'::jsonb or exists (select 1 from jsonb_array_elements(g->'members') m
    where m->>'source' in ('aat', 'lcsh') and (m->>'reason' like 'name 1.00%' or m->>'reason' like 'name 0.97%')))
    as shape from jsonb_array_elements(d.evidence->'groups') g where g->'vetoed' is null) x where d.run_id =
    'run-20261022-131239-sfny58' and d.valid and d.method = 'queued' and d.evidence->'reading'->>'lifespan' is null
    and ss.context in ('keywords', 'keywords:head', 'keywords:sub') and x.others = 1 and x.shape group by 1 order by
    2 desc;` Measured 2026-10-22: 20 strings (keywords:sub 11, keywords:head 8, keywords 1). The owner's yes that
    evening, built before the next run (`Reconciler.Score`, after the home rule: the one exact AAT or LCSH concept
    sets aside TGN's inhabited places of the name; a nation or county of the name does not count as a hamlet).
13. *"Thomas, Thomas"*: ULAN's undated record should join LoC's dated one by finding 14 and does not; the one
    condition left is the class. The owner runs: `select uri, label, type, lifespan_text from authority_records
    where uri in ('http://vocab.getty.edu/ulan/500451686', 'http://vocab.getty.edu/ulan/500077832');` If the first
    is a Group, the join is rightly stopped and the string is the desk's; if it is a Person, the assistant looks
    again. Run 2026-10-22: 500451686 is a Group (a firm), 500077832 a Person dated 1824~-~1926. The join rightly
    stopped; but a firm is no rival to a dated personal heading either, so finding 64 was built that evening: a
    heading the profile read as a person with a lifespan vetoes a body of the name whose dates do not agree, as
    finding 31 does for places. Closed.
14. *The LUX export's shape* (doc 10 7.5 item 2, before it is designed):
ask the LUX team whether a source reload replaces by URI and whether records absent from a full dump are retired;
the merged-away identifiers (now `merged_into` on 381 entities and the two hand merges) are written as equivalents
of the survivor so LUX merges its copies. A reviewed Wikidata contribution export of confirmed local authorities
belongs beside it (next builds, item 4).

**Built 2026-10-22 night from sfny58's samples, waiting for the next rerun** (no reload, no manifest refresh):
finding 60 (decision 11: bodies outvoted when the qualifier places the winner, "National Maritime Museum (London)");
finding 63 (decision 12: a concept against TGN hamlets alone, "Peers", "Dating", "Hall"); finding 64 (a heading read
as a person with a lifespan vetoes a body of the name, "Thomas, Thomas"); a heading's several parentheticals are
each a placement of their own in the placed-through check (finding 58 had joined them);
finding 56 corrected (a qualifier's TGN ancestors widen agreement and never narrow it: "Raglan Castle (Wales)");
finding 58 (a body's LoC parenthetical that is not a type of body places it: "Hunterian Art Gallery (University of
Glasgow)"); finding 59 (the cataloguer's doubt and brackets are not the name or the qualifier: "Cuddington,
Buckinghamshire?", "[Warwick]"); finding 61 (among inhabited places of one name under a country heading, the one that
names the division above it: Portsmouth the city). Expect keywords and keywords:head down by the castles and halls
with county qualifiers that finding 56's fault held back, and a few places.

**Built 2026-10-22 night from t8wzjm's samples, measured on sfny58** (kept for the record):
finding 52 (a record's outer end bound never makes a lifetime too long: "Gilbert, 1943-" against ULAN's
1943-~2090); finding 54 (a source's agreeing record makes its other records of the name namesakes, dated or not:
"Clarke, Geoffrey, -1950"); finding 55 (finding 14 counts live exact records only and reads a window of possibility
as undated: "Thomas, Thomas", "Chandra, Mohini"); finding 56 (a qualifier's ancestors from TGN place a heading's
coarser parenthetical: "Fountains Hall (Yorkshire)" against "(England)"); finding 57 (a fuller form dropped, then
punctuation alone, is the same name: "Jope, E M"). Expect: keywords and keywords:head queued down by the dated
names, a few castles and halls out of keywords, persauthorfull down a little.

**Where the cycles stand** (the owner's question, 2026-10-22): each cycle now gives two or three hundred, and each
sample of ten has three or four entries that are the desk's. Two or three cycles more at most, to a stopping point
around 4,000 to 4,500 queued, where a sample yields no new rule; then the levers change: the Wikidata extract
(prominence, dates for LoC's undated records), the 2024 shadow (what the old pipeline knew), the desk, the LUX export
so the work reaches anyone, and the Wellcome instance as the test of whether the rules are general. What remains is
mostly: persauthorfull's undated names with one ULAN artist and several LoC namesakes (1,400); headings LCSH does
not hold whole (keywords 1,200, keywords:head 600); publisher's bare surnames and small firms (900); the famous-name
place ties (about a hundred of place's 334).

**Next builds, in the order the assistant would do them** (each on the owner's go, or in sequence when the owner
says "continue with the list"):

1. Hyphen and space in keys (finding 8): needs a full reload of all five sources, so it waits for the next one.
2. Keying workbench users by GitHub id (`github:<id>`, the local user `local:<name>`), with the first user besides
   the owner.
3. Outvoted rivals for people and bodies, only after a shadow against the 2024 alignment says what it would do to
   them.
4. A local Wikidata extract as a sixth index (the owner's question, 2026-10-21 evening): not the full dump (over a
   hundred gigabytes compressed) but the items carrying a ULAN, TGN, LoC, VIAF, AAT or GND id or typed human,
   organisation or place, with labels, aliases, dates, those ids and a sitelink count; a few million rows, CC0. What
   it adds: prominence for tied places (finding 17: sitelinks, not TGN name counts), dates for LoC's undated records
   (finding 24 checked rather than trusted), a crosswalk between TGN and LoC, which never link to each other, and a
   check on VIAF clusters. ULAN and LoC already vote through their own Wikidata links, so the gain is in the
   residue. Size M; remote Wikidata lookups by external id (step 3b) are the cheap first step. The files to
   download ahead (the owner's question, 2026-10-22; sizes approximate, from the assistant's memory): the truthy
   RDF dump `dumps.wikimedia.org/other/wikibase/wikidatawiki/latest-truthy.nt.gz` (71.7 GB on 2026-10-22; N-Triples as the Getty
   loader reads, with labels, aliases, instance-of, dates and the external ids P214 VIAF, P244 LoC, P245 ULAN,
   P1667 TGN, P1014 AAT, P227 GND) and the sitelinks table
   `dumps.wikimedia.org/wikidatawiki/latest/wikidatawiki-latest-wb_items_per_site.sql.gz` (a few GB; one row per
   item per Wikipedia, counted for prominence). Not the full JSON dump. The owner's
   further thought the same evening: Wikidata is the one authority an archive can contribute to, so an entity
   confirmed on the desk as a local authority with no identifiers (an obscure sitter, printer or dealer the
   archive's own catalogue documents) can be given an item there, gain an external id, and be found by every other
   project and, in time, by VIAF and LUX. A contribution export (a QuickStatements batch of label, dates, roles,
   nationality and the archive's URI as the reference, for a person to review and submit; confirmed entities only,
   never provisional ones) belongs beside the LUX export in doc 10 7.5 item 2 when that is designed.
5. Finding 51, a second pass: at the end of a run, re-evaluate the strings it queued, since entities it created
   later in the run (the keywords:head "Cornwall (England : County)") can settle them ("Cornwall" in keywords:sub,
   decision 7 of 2026-10-22). Size S; counts as its own line in the report.
6. Finding 26, the working-date gaps (doc 10 7.6 "Measured ... qr7fbx"): the MARC profile does not read "fl.
   1775-1795", "op 1849-1890" or a lone "1730?", and the LoC loader does not read "active 1840-1876" in a heading,
   so namesakes whose working dates cannot overlap do not conflict. A working span is "born no later than its
   start, died no earlier than its end" (the `Lifespan` record can say so: BeginLatest and EndEarliest) and the
   lifetime bound then applies. Size S in the parser, S in the loader; with the parser-gaps open item below.
7. Finding 42, a one-word firm name against a record that is plainly a firm ("Macmillan" against LoC's one
   "Macmillan, Inc."): the one-word rule for bodies could stand aside when the record's own label carries a legal
   suffix or a body-type qualifier. Not measured; small.
8. The Agent parser's endpoint, playground and corpus (section 7, item 34), after the alignment list.

**Commands on return** (the owner runs all of them; the assistant never runs anything against the real store):

    git push
    dotnet run --project tools/Align -- undo-run --run run-20261022-131239-sfny58
    dotnet run --project tools/Align -- reconcile --verbose
    dotnet run --project tools/Align -- decisions --run <new run id> --context keywords --method queued --limit 10
    dotnet run --project tools/Align -- decisions --run <new run id> --context corpauthor --method queued --limit 10
    dotnet run --project tools/Align -- decisions --run <new run id> --context keywords:sub --method queued --limit 10

Run decisions 11 and 12's ceiling queries and decision 13's record query against sfny58 at any time; the undo keeps
the evidence. If the owner says yes to 11 or 12 before the run, it is built first. "Evanston" and the other place
twins with different Wikidata ids are the desk's to judge from their ids.

**Open items not in this list** (parser gaps, store-stage speed, per-context redo, compound export and entity page,
place drift between runs, the printer's three groups, remote sources 3b, audit against LoC, workbench steps 3 and 4)
are in section 7 under "Open items from the day"; none blocks the list above.

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
   tests, the rest is Phase 5 (section 7, item 34).
3. **Corpus**: a large ground-truth set of date strings, plus a trainer UI to grow it. ([06-corpus-and-testing.md](06-corpus-and-testing.md))
4. **Service**: a Kestrel API returning rich TimeSpan results for GET/POST. ([04-timespan-service.md](04-timespan-service.md))
5. **Examples site**: C# code for every site example, generated vs site JSON, parser playground. ([05-examples-site.md](05-examples-site.md))
6. **Alignment toolkit**: the consumer of every parser, in three tiers. *Read only*: the parsers, no data. *Align
   through the vocabularies' own APIs*: remote sources, no setup, slow. *Align against local indexes*: the loaded
   authority files, fast, with a store of entities, strings and decisions, a review workbench and exports. Docs 10
   to 14; doc 07 holds the earlier notes.

## 2. Phases

Sizes are rough (S = a session or two, M = several, L = many). Phases 1 and 2 are independent after
Phase 0 and can interleave; the parser's `LinkedArtDate` dependency is the one shared piece and is
done first in Phase 1.

### Phase 0: Housekeeping (S)

- Move to a conventional layout (`src/`, `tests/`, `tools/`, `corpus/`, `docs/`, solution at root);
  delete `PmcAsync`; remove `.idea`/`.vs` from the tree; add `.editorconfig`, `Directory.Build.props`
  (net10.0, C# 14, nullable, warnings as errors, deterministic builds), `global.json`.
- GitHub Actions: build + test on PR; CodeQL; Dependabot.
- README that points at `docs/`. Commit these docs.
- Acceptance: `dotnet build` and `dotnet test` green in CI from a clean clone.

### Phase 1: Model library to 1.0 (L)

1. `LinkedArtDate` struct (BCE, year 0, big years, lexical forms) with full tests. Needed by Phase 2.
2. `tools/SchemaInspector`: read linked.art 1.0 schemas -> property manifest; contract test.
3. Class hierarchy per design §2; move properties off the base class; drop `Types` enum;
   `Set`, `Type` family, Activity subclasses, missing properties.
4. Central serialisation (`LinkedArtJson`): ordering table, empty omission, `type` from class,
   polymorphic read, canonical form. Round-trip tests on all cached site JSON.
5. Vocabulary registry (`vocabulary/*.json` + source generator), replace inline `AatType` calls, fix
   the duplicate-id errors found in the audit.
6. Fluent API v2 (generated list helpers, lambda relationship builders, named `metaType`, chain-type
   preservation). Migrate examples; remove post-hoc patches.
7. Examples restructure: `LinkedArtNet.Examples` library + `Examples.Tests` + `tools/ExampleManifest`;
   coverage dashboard output; `SiteIssue` tracking; file upstream issues for the known site bugs.
8. Fill the gaps: Concepts, Events, Vocabulary pages, remaining Provenance, Places, any example in the
   manifest without a method.
- Acceptance: contract test passes both ways; 100% of manifest examples implemented; all validate
  except those tagged `SiteIssue` with an upstream issue link; NuGet `LinkedArtNet 1.0.0-preview`.

### Phase 2: TimeSpan parser core (L)

1. Project `LinkedArtNet.Dates`; `DateExpression` AST; canonical form reader/writer (EDTF L0-L2 plus
   extensions); `TimeSpanRealiser` with `ParseProfile` and the default profile; realiser tests.
2. Corpus scaffolding: schema, `tools/Corpus` CLI (validate, run, stats), seed files from py_datetime
   and PMC outputs (expectations re-derived by hand), authored BCE/deep-time/century/open-ended sets.
3. Normaliser and tokenizer with English lexicon; grammar strategy; salvage strategy; result type,
   confidence model, alternatives, notes.
4. Named periods (Japanese eras, dynasties, regnal periods) as JSON.
5. Import corpora (yearrange, timetwister, cultural_dates, EDTF, Getty sample) as `proposed`; review
   in batches; drive accuracy up; calibration report.
6. Fluent bridge (`WithTimeSpan("...")`, `WithLifespan`), profiles for library, archive and medieval
   manuscripts; FR/DE/IT/ES/NL/LA lexicons. Profile advisor (`tools/Corpus advise` or a library call): scans a
   dataset's strings, reports the vocabulary in use (early/mid/late, dash styles, century notations, era
   words) and recommends profile settings, e.g. a halves band table where "mid" never occurs. Parsing itself
   never depends on other strings (deterministic per string and profile); the advisor runs beforehand and
   the owner pins the result as a profile. Owner's observation, 2026-10-13.
7. `IDateInterpreter` + `LinkedArtNet.Dates.Llm` reference implementation, behind opt-in. Done 2026-10-17 (§7, item 27):
   used as a corpus-growing assistant from the trainer and the Corpus tool, not at request time in the service.
- Acceptance: >= 98% exact match on `verified` corpus (>= 3,000 entries), 100% on seed; property
  tests green; p50 < 50 µs on idioms; NuGet `LinkedArtNet.Dates 1.0.0-preview`.

### Phase 3: Service and site (M)

1. `LinkedArtNet.Dates.Service`: endpoints, limits, caching, OpenAPI, result schema, Dockerfile,
   integration tests, GHCR image.
2. `LinkedArtNet.ExamplesSite`: coverage home, section/example pages with Roslyn-extracted source,
   search, property index, parser playground calling the service, about page.
3. Trainer (GitHub OAuth, allow-list), capture of unparsed inputs (opt-in), PR-creating submissions.
4. Deploy both (owner's hosting choice); custom domains; uptime check.
- Acceptance: public URLs; examples site shows 100% coverage with blockers explained; playground
  works against the deployed service; trainer round-trips a proposed entry into a PR.

### Phase 4: Hardening and release (M)

- 1.0 releases of both packages (decoupled: NuGet publishing is deferred, owner 2026-10-13); ~~CHANGELOG~~ (CHANGELOG.md from v0.1.1, 2026-10-17);
  ~~docs site pages for API usage~~ (done 2026-10-16: `/dates/api`); perf doc and benchmarks; weekly scheduled
  jobs (site JSON refresh, Getty label check; no workflow has a `schedule` trigger yet); ~~a backup of
  `/srv/linkedart/data`~~ (nightly snapshot script + cron and an off-box pull script, 2026-10-17, 09-hosting.md section 8;
  the owner installs them on the server); submit the parser and service to linked.art/software.
- Corpus growth from trainer traffic (first trainer entry merged 2026-10-16, PR #2); multilingual coverage;
  four-point (`EmitInnerBounds`) polish.

### Phase 5: Alignment toolkit

Designed 2026-10-17 in [10-alignment-toolkit.md](10-alignment-toolkit.md) (doc 07 holds the earlier notes). Order of
work there, section 12: the agent-string parser first, then the store with entities, strings and decisions, then
authority sources and the reconciler, then the pipeline runner with PMC as the first instance, the review UI, the
Wellcome instance, publishing and the QLever loader. Open questions for the owner in its section 13. The Agent
parser is an objective of its own (objective 2), so Phase 5 also gives it the TimeSpan parser's other three pieces:
a service endpoint beside the dates one, a playground page on the examples site, and a corpus of ground truth
seeded from the PMC strings, with the trainer (section 7, item 34).

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

## 4. Target repository layout

```
linked-art-net.slnx
Directory.Build.props, global.json, .editorconfig
src/LinkedArtNet/                 model, serialisation, vocabulary, LinkedArtDate
src/LinkedArtNet.Dates/           parser (AST, canonical, realiser, strategies, profiles, lexicons)
src/LinkedArtNet.Dates.Llm/       IDateInterpreter reference implementation (optional package)
src/LinkedArtNet.Examples/        one method per site example
src/LinkedArtNet.Dates.Service/   Kestrel API
src/LinkedArtNet.ExamplesSite/    Razor Pages site + playground + trainer
tests/*.Tests/                    per project
benchmarks/
tools/SchemaInspector, ExampleManifest, ExampleSourceExtractor, VocabGen, Corpus
corpus/                           ground truth
vocabulary/                       aat.json, tgn.json, ulan.json, mappings/
examples/manifest.json, cached/   site JSON snapshots
profiles/                         parser profiles
docs/
```

## 5. Risks

- **Site drift**: linked.art examples change; cached JSON refresh is a scheduled job and diffs are
  reviewed, not auto-accepted.
- **Grammar sprawl**: the idiom space is large; the corpus-driven loop and the salvage/interpreter
  fallbacks keep the grammar focused on high-frequency forms.
- **Policy disagreement**: bands and conventions are subjective; profiles make disagreement explicit
  and testable rather than argued in code.
- **Imported corpus quality**: yearrange has known wrong expectations; imports stay `proposed` until
  reviewed.
- **Scope creep into alignment**: Phase 5 is fenced off; only the plug-in shapes noted in doc 07 are
  built early.

## 6. Open questions for the owner (none block Phase 0-1)

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

## 7. First concrete steps

1. ~~Commit `docs/` to `de-pmc`.~~ Done 2026-10-13.
2. ~~Phase 0 restructure.~~ Done 2026-10-13 (`src/`, `tests/`, `tools/`, `linked-art-net.slnx`, build props, CI).
3. ~~`LinkedArtDate` struct + tests.~~ Done 2026-10-13 (73 tests incl. FsCheck properties).
4. ~~Corpus schema + `seed/py-datetime.json` + `seed/pmc-outputs.json`.~~ Done 2026-10-13 (37 + 91 entries).
5. ~~`tools/SchemaInspector` + contract test.~~ Done 2026-10-13. Gap at baseline: 28 missing properties,
   913 extra properties, 3 unmapped classes (see `docs/model-gap-report.md`).

6. ~~Phase 1 step 3: class hierarchy.~~ Done 2026-10-13. 40 schema classes plus InformationObject, zero missing properties, six
   documented site-compatibility extras (D15), 118 site examples still validate.

7. ~~Phase 1 step 4: central serialisation.~~ Done 2026-10-13. `LinkedArtJson` (strict and tolerant
   options, empty-collection omission, relaxed escaping), `LinkedArtObjectConverterFactory` (reads by
   `type`), `LinkedArtTypes` registry. All 118 cached site examples read strictly and round-trip.
   Property order stays attribute-based (a central table added nothing the attributes don't).

8. ~~Phase 1 step 5: vocabulary registry.~~ Done 2026-10-13. `vocabulary/{aat,tgn,ulan}.json` (127 + 4 + 6
   terms) -> `tools/VocabGen` -> `Aat.g.cs`, `Tgn.g.cs`, `Ulan.g.cs`; typed terms (`Aat.Oil` is a Material,
   `Aat.English` a Language); aliases for the site's inconsistent labels; no inline AAT ids remain in
   the examples; tests keep JSON and generated code in sync.

9. ~~Phase 1 step 6: fluent API v2.~~ Done 2026-10-13. `tools/FluentGen` generates `With{Property}` helpers
   for every model property (params append, lambda-configured child, scalar set), keeping the chain's
   static type; shared properties get generated `IHas…` interfaces. Hand-written `WithName`, `WithIdentifier`,
   `WithTimeSpan(year)`, `AsReference`; TimeSpan factories no longer set labels; label-nulling patches
   removed from the examples.

10. ~~Phase 1 step 7: examples as a library.~~ Done 2026-10-13. `LinkedArtNet.Examples` library with
    `[LinkedArtExample]` methods, `LinkedArtNet.Examples.Tests` (site match, schema parity, round-trip,
    coverage), `tools/ExampleManifest` (crawl, fetch, coverage, diff, validate), `examples/manifest.json`
    (183 site examples), `examples/coverage.md`, `SiteIssue` for five id collisions, `docs/upstream-issues.md`
    (38 site examples invalid against the 1.0 schemas, 20 distinct discrepancies). Model gained the site's
    extras needed to read all 183 examples (`Transfer`, `Right.applies_to/dimension/part`,
    `Currency.notation`, `Group.members_exemplified_by`, `MonetaryAmount : Dimension`, `Period.part_of`).
    123/183 implemented, 122 validated.

11. ~~Phase 1 step 8: fill the gaps.~~ Done 2026-10-13. 183/183 site examples implemented, 182 validated, 3
    blocked by site id collisions (`SiteIssue`). New topics: RequiredTerms, Auctions, Concepts, Events and one
    class per provenance sub-page. Four registry labels now follow what the site emits (Sorting Name, Promise,
    rectangular, Lot Number). `CoverageTests.Every_site_example_is_implemented` keeps it at 100%.

12. ~~Phase 2 step 1: `LinkedArtNet.Dates` core.~~ Done 2026-10-13. `DateExpression` AST (points, decade, century,
    millennium, unspecified digits, named period, deep time, undated; qualified, banded, relative, lifecycle,
    interval, set, corrected), canonical reader/writer (`docs/canonical-expression.md`; property-tested
    round trip), `EdtfWriter`, `ParseProfile` (+ `profiles/default.json`), `TimeSpanRealiser` (every node rule,
    conventions, bands, padding, open ends, seasons, deep time, intents, label policy), `TimeSpanParseResult`,
    `TimeSpanParser` facade with the canonical strategy. 229 tests.

13. ~~Phase 2 step 2: corpus scaffolding.~~ Done 2026-10-13. `LinkedArtNet.Dates.Corpus` (loader with defaults, schema
    validation, evaluator checking only the fields an expectation sets, metrics incl. calibration error, report),
    `tools/Corpus` (validate / run / stats, `corpus/report.md` + `report.json` for regressions), `CorpusTests`
    (every file schema-valid and well-formed, ids unique, every `verified` entry passes). Four authored sets
    (BCE and deep time, centuries and bands, open ends and relations, lifecycles and floruit: 110 entries) plus
    `canonical-forms.json` (18 verified). Free-text files are held at `proposed` until step 3; 256 entries in all.

14. ~~Phase 2 step 3: normaliser, tokenizer, grammar, salvage.~~ Done 2026-10-13. `Normaliser` (NFKC, dashes, quotes,
    wrapping brackets as Bracketed/Parenthesised, trailing punctuation, [sic]), `Lexicon` (embedded `Lexicon/en.json`,
    multi-word phrases, dotted abbreviations), `Tokenizer`, `GrammarStrategy` (recursive descent: eras, hedges, bands,
    centuries in five notations, decades, unspecified digits, months/days/seasons, ranges with abbreviated or
    incomplete bounds, open ends, relations, sets, lifecycle and floruit, deep time, parentheticals as noise,
    corrections or secondary dates, repeats), `SalvageStrategy`, ISO fast path, `LeadingDashIsOpenStart` policy.
    Corpus: 256/256 pass; the seed py_datetime file and the four authored sets are now `verified` (151 verified,
    105 proposed: the PMC strings await the owner's review).

15. ~~Phase 2 step 4: named periods.~~ Done 2026-10-13. `INamedPeriodSource`, `JsonNamedPeriodSource` with four
    embedded tables (Japanese periods and eras with regnal years, Chinese dynasties and Ming/Qing reign periods with
    regnal years, British periods and reigns, conventional archaeological periods and the world wars), name
    normalisation (diacritics, scripts), `period:key` resolution in the canonical reader, periods as a grammar core
    (bands, hedges, ranges, "Meiji 27 (1894)"), `NamedPeriodStrategy`, `CompositePeriodSource` for custom tables;
    styles deliberately absent. Corpus `authored/named-periods.json` (30, verified); 286/286 pass.

16. ~~Phase 2 step 5: import corpora.~~ Done 2026-10-14. `tools/Corpus import` for jeresig/node-yearrange (1650, MIT),
    alexduryee/timetwister (91, MIT) and a Getty ULAN biography sample (180, ODC-By, fetched with curl: the endpoint
    answers .NET's HttpClient with 400); the EDTF specification examples curated by hand (61, public domain);
    cultural_dates's companion test repository is empty and share-alike sources are excluded (MIT repo).
    `corpus/imported/THIRD-PARTY-NOTICES.md` carries the licence texts. Review of the first batches drove the
    grammar: leading prose skipped at a cost ("Italian artist, 15th century"), explicit dates refine a period
    name, parenthetical and trailing hedges, year-first archival forms ("1776 December", "1776 4 July"), day and
    month ranges that share a year or month ("4-10 July 1776", "May 4 - July 10 1776"), "after" as an end bound,
    cardinal and German ordinal centuries ("18c", "19. century"), MARC "187-", season abbreviations. Proposed
    pass rate 49.5% -> 60%; the rest are mostly the sources' own conventions (yearrange: late = last quarter;
    timetwister: US month-first, winter at the start of the year; ULAN: padded estimates), prose and typos.

17. ~~Phase 2 step 6: fluent bridge, profiles, advisor, languages.~~ Done 2026-10-14. `TimeSpanBridge`: `WithTimeSpan("c. 1642")`,
    `WithLifespan("1606-1669")` (floruit becomes a professional activity), `WithExistence(...)`, unparseable text kept
    as a label-only TimeSpan, `LastResult` for the details. `ProfileCatalog` loads `profiles/*.json` with `inherits`;
    profiles `library`, `archive-us` (month-first, winter at the start of the year, decade thirds), `catalogue-quarters`
    (yearrange's early/late quarters), `medieval-manuscripts` (Latin, strict centuries, hedges widened), `multilingual`;
    new `winter` policy. `ProfileAdvisor` scans a dataset's strings and recommends settings (`tools/Corpus advise`).
    Lexicons for fr, de, it, es, nl, la (written from general knowledge; provenance stated in each file), merged with
    earlier languages owning conflicting words; grammar reads roman numerals before century words, postfix Latin
    bands ("saec. XIV ex."), German ordinal dots, filler words inside dates ("12 de marzo de 1850").
    Corpus `authored/multilingual.json` (44, verified). The imported timetwister and yearrange files evaluate under
    `archive-us` and `catalogue-quarters`: proposed pass rate 60.0% -> 62.3%. `tools/Corpus tokens` for debugging.

18. ~~Phase 3 step 1: `LinkedArtNet.Dates.Service`.~~ Done 2026-10-14. Minimal API on Kestrel: GET/POST `/v1/timespan`
    (intent, profile, shape, labelPolicy, emitInnerBounds; per-item overrides and `ref` echo in POST), `/v1/profiles`,
    `/v1/profiles/{name}`, `/v1/canonical`, `/healthz`, `/version`, OpenAPI at `/openapi/v1.json` and Scalar at
    `/docs`. Deterministic responses with `ETag` and `Cache-Control`, in-memory memo, per-IP fixed-window rate
    limit, CORS for any origin, 500-char / 500-input / 256 KB limits, opt-in JSONL capture of weak results,
    `llm=true` accepted and noted until an interpreter exists. `schemas/timespan-result.schema.json` is checked by
    the integration tests (21, WebApplicationFactory). Multi-stage Dockerfile on the chiseled ASP.NET image with a
    built-in `--healthcheck`; `.github/workflows/docker.yml` pushes to GHCR on `v*` tags.

19. ~~Phase 3 step 2: `LinkedArtNet.ExamplesSite`.~~ Done 2026-10-14. Razor Pages, server-rendered: coverage home,
    `/examples/{section}` and `/examples/{section}/{id}` (C# source, generated JSON, site JSON, semantic comparison,
    status and site-issue reason, properties and terms used), `/search`, `/properties` and `/properties/{name}`
    (reverse index by JSON property or Getty term), `/dates` playground (in-process or against the service via
    `TimeSpanService:BaseUrl`; timeline, flags, notes, alternatives, copy-as-C#), `/dates/corpus` browser with
    filters and paging, `/about` with licences and attributions. `tools/ExampleSourceExtractor` (Roslyn) writes
    `examples/source.json`; `CoverageTests.Example_source_is_current` keeps it fresh. 25 WebApplicationFactory tests
    including one that renders all 183 example pages. Dockerfile; the GHCR workflow now builds both images.
    Not done: the trainer (`/dates/train`, step 3) and the axe accessibility lint.

20. ~~Phase 3 step 3: the trainer.~~ Done 2026-10-14. `/dates/train` in the examples site: GitHub OAuth (generic OAuth
    handler, no extra package) with an allow-list `Trainer:Reviewers`, or `Trainer:AllowLocalReviewer` for
    development; queue from pasted or uploaded lists and from the service's capture JSONL (`Trainer:CapturePath`);
    review page shows the parser's answer and takes accept / correct (canonical expression with live re-realisation,
    status, intent, profile, tags, notes) / skip; submissions become `proposed` entries with the realised bounds
    pinned; export as a downloadable corpus file or a pull request under `corpus/proposed/` through the GitHub REST
    API (`Trainer:Submission:Repo`, `:Token`, `:BaseBranch`). Store is two JSON files in `Trainer:DataDirectory`
    (gitignored; mount a volume in a container). 5 tests incl. a round trip against a fake GitHub.

21. ~~Corpus contribution workflow.~~ Done 2026-10-14. `docs/08-corpus-contributions.md` is the operating guide from
    capture or paste, through trainer review and export, to verification and the CI gate. `tools/Corpus promote <file>
    [id ...] [--to verified|disputed|proposed] [--dry-run]` flips statuses in place (status tokens only, refuses a
    failing entry, rewrites the report). `tools/Corpus review` writes the Markdown that
    `.github/workflows/corpus-review.yml` posts on every pull request touching `corpus/` (job summary always, an
    updated comment for in-repo branches). 17 tests.

22. ~~Corpus failure triage.~~ Done 2026-10-14. The report groups failures by cause. Parser fixes from the imported
    corpora: single-digit abbreviated ends (`1843-5`), ISO fragments (`1985-04-12T23:20:30`, `1985-04-12/`, `/1985`),
    shared-suffix century ranges (`18-19th century`), joined band ranges (`mid-late`), edition numbers (`62/200`,
    `Edition of 100`), spaced decade suffixes (`1500 s c.`), year-first archival forms (`1776 4-10 July`, `1776 Spring`,
    `July 4?, 1776`, `July 21st, 1776`), a second lifecycle marker after a comma, EDTF level 2 sets in the canonical
    reader, and the edition rule: an edition, printing or reproduction clause dates the object, the first date is the
    original (owner's decision). Profiles: `archive-us` seasons by equinox and solstice (`seasonDates`) and
    timetwister's overlapping decade halves; `catalogue-quarters` mid = the middle fifth. Imports: yearrange expects
    Approximate only where the text has circa wording; ULAN expectations come from the string, with the padded
    estimate in `sourceExpectation`; EDTF examples we do not support expect the partial reading. Proposed pass rate
    60.3% -> 79.8% (EDTF 61/61, ULAN 168/180, timetwister 78/91, yearrange 1275/1650).

23. ~~Hosting.~~ Done 2026-10-16. linkedart.net registered at Mythic Beasts (2026-10-15); VPS bought and set up by the
    owner from the runbook in [09-hosting.md](09-hosting.md); v0.1.0 deployed from the GHCR images; trainer verified
    end to end (PR #2). Release rhythm: tag on `de-pmc`, fast-forward `main`, `docker compose pull && up -d`.
24. ~~Site polish (2026-10-16).~~ Plain C# form and idiom notes for every example with an `/idioms` catalogue
    (`Idioms.cs`, detected by the source extractor); the dates API page `/dates/api`; playground snippets for
    constructing each TimeSpan in C# and from .NET `DateTime`; raw JSON links; About page sample compiled from an
    embedded source file.
25. ~~Phase 2 step 6, exposed.~~ Done 2026-10-17. The profile advisor writes a suggested profile (as changes from
    `default`), keeps example strings per feature, and is reachable as `POST /v1/advise` and the site's `/dates/advise`
    (with corpus samples). Other-language evidence excludes words English also knows; abbreviated range ends are not
    two-digit years.
26. ~~Phase 2: PeriodO period source.~~ Done 2026-10-17. `PeriodOSource` over `periods/periodo.json` (CC0, 9,446
    definitions from 501 authorities, version 301, refreshed by `tools/Corpus periodo`); matched as the whole string
    only, with PeriodO URIs and citations in the result's notes; the service, site, evaluator and tool share it.
27. ~~Phase 2 step 7.~~ Done 2026-10-17. `IDateInterpreter` + `ParseAsync`, `LookupInterpreter`,
    `LinkedArtNet.Dates.Llm` (Claude, JSON-schema answer, cached grammar prompt), `tools/Corpus interpret`, and Claude
    proposals in the trainer. Decision: the LLM is a corpus-growing assistant; the public service stays deterministic.

28. ~~First measured interpreter run.~~ Done 2026-10-17 by the owner over the 22 disputed yearrange entries (about a tenth
    of a dollar): every answer a valid canonical expression; 4 matched the parser, 1 declined ("・07・25"), 17 differed,
    mostly by writing a set for records that date several parts separately (sheets, numbered items, works), decoding
    mojibake apostrophes, reading "18690000" as a date-column year, and keeping a written Western year over a conflicting
    era year. Two of those became parser rules the same day (normaliser: mojibake repair, YYYYMMDD values; corpus
    `authored/encoding-and-noise.json`; three yearrange entries moved from disputed to verified). The set reading for
    enumerated parts is the open finding.

29. ~~Set reading for enumerated parts.~~ Done 2026-10-17 (item 28's open finding). Clauses split at ";", a sentence
    "." or an enumerator "(2)" that each carry a date become a set: hull as the span, parts as alternatives, flag
    `MultiPart`; labels without dates are skipped; edition and "original" sentences keep their own rule. Authored
    corpus `multi-part.json`; the disputed yearrange lists that now read as sets were accepted and verified.

30. ~~Samples page and standalone-service notes.~~ Done 2026-10-17 (owner's suggestions). `/dates/samples` lists every
    verified authored corpus entry by theme plus one example of each shape of reading from the imported collections,
    each linking to the playground, so the page cannot drift from the parser. `/dates/api#docker` and the README tell a
    non-.NET project how to run the dates service from the public image, with the settings that matter (own profiles,
    rate limit, capture) and a Python example.

31. ~~Phase 5 step 1: the agent-string parser.~~ First version 2026-10-17 (doc 10 section 6): `LinkedArtNet.Agents` with
    profiles and tables, Person/Group builder, agent corpus with nested dates expectations (60 entries, 57 verified),
    `/v1/agent`, the `/agents` playground, `tools/Corpus agents`, and the trainer's agent item kind: agent strings
    reviewed as parts, saved as agent entries whose date part also enters the dates corpus (doc 08 section 8).
32. ~~Phase 5 step 2: the alignment store.~~ 2026-10-17 (doc 11): `LinkedArtNet.Alignment` on PostgreSQL with
    entities, equivalents, source strings, decisions, overrides, authority records and runs; `IdentifierScheme`
    per dataset keeping the PMC paths and alphabet; importers for the pmc-lux override files and the earlier PMC
    tables; `tools/Align`; 25 tests against a Testcontainers PostgreSQL 15. Step 3, authority sources and the
    reconciler: local half built 2026-10-18 (Getty N-Triples loader, name matching, scoring and grouping, the
    decision ladder, `tools/Align load-getty|reconcile`; doc 11 section 8); remote half (3b) next. The owner's
    earlier reconciliation (72,904 strings, 35,426 decisions) was imported into the store the same day. The human
    side is designed in docs/12-workbench.md (2026-10-18): a separate app with a review desk, variants and entities
    views, jobs and runs, projects, reports outward; the trainer moves into it. Step 5 of doc 10 becomes its first
    three steps. Its first cut, sign-in, projects, members and the review desk, was built the same day; entities,
    variants and the overrides export followed, and docs/13-workbench-guide.md is the user guide's first draft.

33. ~~Phase 5 step 3, first real indexes and the first corpus-wide run.~~ 2026-10-19 (doc 11 sections 8.8-8.9). LoC
    names (10.5M records) and AAT (57k) loaded beside ULAN; five store lessons measured and fixed (per-source partial
    label indexes, materialised lookups, label statistics, unloaded sources answered without a query, post-load
    vacuum and index rebuild); ULAN's estimated years read as outer bounds unless the biography states them, and its
    person-typed corporate bodies read as groups. Policy (owner): a unique exact single-source match on an undated
    string is accepted as `exact-single`. Whole corpus reconciled (36,823 strings, 19 min): overridden 57, store
    2,050, accepted 1,532, queued 5,715, minted 27,469. Shadow of 2,000 old decisions: 2 disagreements, both the old
    run's. Workbench: generic projects with a manifest editor, scoped overrides, counts, linked lozenges, user guide
    (doc 13); the 2024 PMC documents sifted for what a project needs (doc 14); compound subjects designed (doc 10
    section 7.5).

    **Open items from the day, in rough order:**
    - Store: the entity side of a run took 12 of the 19 minutes (store stage 738 s; 968 s on 2026-10-20 with 9,437 part
      strings more): plan it on the real store as the
      authority side was. Turn off Npgsql's infinity conversion (year 1 became -infinity; LoC n82239358). The
      reconcile report should split accepted into vote and exact-single. A run that dies should be marked
      interrupted at the next start, not sit as "running or interrupted".
    - ~~Loads: LCSH, then TGN.~~ Both loaded 2026-10-19 (lcsh 513,362; tgn 2,991,010); five sources, 14.5M records.
    - ~~Home vocabularies per context~~ (built 2026-10-20 with the compound shape: `home: lcsh` on the keyword contexts,
      `Reconciler.JoinToHome`; measure on the next run). The evening shadow (run-20261019-195509-a2wv6z, 2000 strings, 9 disagreements)
      had one that is design rather than data: "Juvenile literature", which the 2024 process linked to LCSH
      sh85071184, was accepted on AAT "children's literature" alone, because lcsh was not yet in the manifest. With
      lcsh in play the two become rival groups and the string is queued, and so will most keywords: AAT and LCSH both
      hold a term for most library subjects and never link to each other. A library catalogue's keywords are LCSH
      strings, so the context should name its home vocabulary (keywords: lcsh first, aat as an equivalent to carry
      when its exact term agrees), and the reconciler should treat a home-vocabulary exact match as decisive while
      recording the other vocabulary's exact term as an equivalent rather than a rival. Needs a manifest field on
      ContextRule and a grouping rule; measure on the keywords context after the rerun.
    - The other eight disagreements in that shadow: four were the 2024 process's own errors (Claude Flight linked
      to Edith Lawrence, Dublin City Council to its Culture Company, Sir Charles Lock Eastlake to his nephew,
      Stoke-on-Trent's museum to Carlisle's) and four were 2024 lumpings of a body into its parent or successor
      (the NGS trustees into the NGS, RCHME into English Heritage, the Fine Arts Museums of San Francisco into the
      de Young, the Arts Council Gallery into the Hayward). The new answers are the bodies the strings name,
      checked against the ULAN records. Whether PMC wants the lumping is an editorial choice for the desk; an
      override expresses it. No disagreements at all on persauthorfull in the 2000.
    - Audit: check entities against their LoC records as well as ULAN; report ULAN's person-typed corporate bodies
      (1,552) and stale LUX ids; the reports-outward step (doc 12 step 5) sends the ULAN findings to Getty.
    - Measure again: a shadow sample after today's stricter date agreement, and the human sample through the desk's
      "review single-source acceptances".
    - Per-context redo (for the jobs step, noted 2026-10-20): a change to one context's rule today costs a full
      undo and a twenty-minute corpus rerun. Wanted: `redo --context place [--method minted]`, which invalidates the
      valid decisions matching the filter, removes the provisional entities only those decisions held, and
      reconciles those strings again; the same operation is a job on the runs page. Constraint found 2026-10-20:
      equivalents carry the run id, not the decision id, so a redo by context cannot take back the ids the other
      contexts' decisions added in the same run, and the entities it leaves aligned would answer at the store stage
      and pollute the measurement. Either equivalents get a decision id (schema change) or the redo is measured
      with the store hidden, as the shadow is.
    - ~~The Align tool reads the sources from the project's saved manifest but the context rules from the code's PMC
      default~~ (done 2026-10-20: both from the saved manifest, a "manifest:" line on every run saying whether it
      still equals the library rules, `manifest --use-default`, and the project page's "In effect now" sentence).
    - Parser gaps from the 2026-10-20 minted samples (also doc 10 section 7.6 item 6): "The Hunterian" and "Hunting
      Art prizes" read as Person; "McSloy, Tom, op 1946" (a working date written "op"); "Mainz/Rhein" (a slash
      variant for name-forms). From qr7fbx (2026-10-21, finding 26): "Bolton, James, fl. 1775-1795" and "Goldsmith,
      Oliver, 1730?" read undated by the MARC profile; LoC's "Bolton, James, active 1840-1876" undated by the loader
      (section 0, next builds, item 5).
    - Workbench step 3 (jobs): runs, loads and indexes pages (authority_sources is the indexes table), a schedule,
      the counters list; step 4 as extended by doc 14 section 4 (sources and uploaded dumps, the profile job,
      contexts namespaced by source, role routing, mapping tables as overrides, the brief's tables imported as
      credited overrides).
    - Strings made of parts (doc 10 section 7.6, designed 2026-10-20 from the minted samples: compound headings,
      place lists, qualified places): qualified reading first, then derived strings (list shape for places), then
      the compound shape for keywords with the home-vocabulary rule. Then the remote sources (3b), LUX self-echo
      first. Measured baseline 2026-10-20 (run-20261020-090207-x7q3sw, all five sources): accepted 1,468 (vote 412,
      exact-single 1,056), queued 6,674, minted 26,594; LCSH alone moved ~650 strings out of minted. Measured
      again 2026-10-20 with the qualified and list shapes built (run-20261020-100906-ccv3h8): accepted 1,632 (vote
      531, exact-single 1,101), queued 7,005, minted 25,866, 404 lists as 160 new parts; place minted 931 -> 203.
      Numbers and the two findings in doc 10 section 7.6 "Measured". Both built and measured the same day
      (run-20261020-112520-sxyxbc: place single 44 + vote 98, minted 198, legacy LoC places gaining TGN ids); the
      state and province abbreviations ("Secaucus (N.J.)") then read as names, to measure on the next run. The
      compound shape with the home vocabulary built the same evening (doc 10 section 7.6 "Built ... (evening)") and
      measured (run-20261020-124120-aqn6dz): keywords minted 17,849 -> 3,033, composites 15,749 over 9,437 part
      strings (keywords:head 7,741, mostly dated personal names; keywords:sub 1,536); accepted 4,827, queued 8,657,
      minted 12,287; store stage 968 s. Seven findings in doc 10 section 7.6 "Measured ... aqn6dz", of which the
      "Dance" person bug and the report wording were fixed that evening. The rest, in order of value:
    - ~~LCSH subdivision records (doc 10 7.6 aqn6dz finding 2)~~: built 2026-10-20 night (doc 10 7.6 "Built ...
      (night)"): the loader keeps `collections`, `ContextRule.Collections`, `Reconciler.PreferCollections`, PmcDefault
      keyword contexts. To measure, the owner: reload lcsh (`load-loc <subjects file> --vocab subjects --version <as
      before>`, the same file as 2026-10-19), `manifest --use-default`, `undo-run --run run-20261020-124120-aqn6dz`,
      `reconcile`; expect keywords:sub queued (632) to fall and "Catalogs", "Diaries", "20th century" to be accepted on
      LCSH, "Catalogs" carrying AAT. The first attempt (run-20261020-140820-q7jhh7) decided every part string twice,
      because part rows outlive an undo and the main loop reached them after their parents had decided them; fixed
      (RunAsync passes over a string already decided, and says how many). Measured on run-20261020-150054-tzm64p (doc
      10 7.6 "Measured ... tzm64p"): keywords:sub queued 632 -> 581, vote 46 -> 88; the rule works where it applies,
      and what remains queued is AAT's qualified terms (finding 5, next) and TGN place namesakes (below).
    - ~~AAT qualified terms (finding 5)~~: built 2026-10-20 night (doc 10 7.6 "Built ... finding 5"): `BareTerm` in
      the AAT loader, `JoinToHome` decisive. To measure, the owner: `load-getty <the aat files> --vocab aat` (the same
      files as before), `undo-run --run run-20261020-150054-tzm64p`, `reconcile`, `decisions --run <id> --context
      keywords:sub --limit 10`; expect "Catalogs", "20th century", "21st century", "Biography" accepted (vote with AAT
      carried, or exact-single), "Private collections" accepted on LCSH with AAT's two set aside. No manifest refresh.
    - Place agreement by placement (finding 9, doc 10 7.6 after finding 5's Built paragraph; the largest class in the
      owner's exports: 248 of 843 queued place-like strings have one live exact TGN record and are queued because
      LoC's qualified heading "Abbeyleix (Ireland)" scores 0.85 beside it): compute `Candidate.Placement` for every
      candidate and generalise placed-through to any string, so a candidate whose placement sits inside exactly one
      other-source candidate's placement joins it and TGN and LoC vote together. Confirm first, the owner runs:
      `with q as (select d.id, ss.raw, d.evidence from decisions d join source_strings ss on ss.id =
      d.source_string_id where d.run_id = 'run-20261020-150054-tzm64p' and d.valid and d.method = 'queued' and
      ss.context in ('place', 'keywords:sub')), members as (select q.id, q.raw, m->>'source' as source, m->>'label' as
      label, (m->>'score')::numeric as score, (m->>'veto') is not null as veto from q cross join lateral
      jsonb_array_elements(q.evidence->'groups') g cross join lateral jsonb_array_elements(g->'members') m), single as
      (select id from members where source = 'tgn' and score >= 0.97 and not veto group by id having count(*) = 1)
      select m.source, round(m.score, 2) as score, count(distinct m.id) as strings, (array_agg(m.raw || ' ~ ' ||
      m.label order by m.raw))[1:3] as examples from members m join single s on s.id = m.id where not m.veto and
      m.source <> 'tgn' group by m.source, round(m.score, 2) order by strings desc;` Confirmed 2026-10-20 (doc 10
      7.6 "Confirmed"): loc 0.85 on 148, store 1.00 on 59, store 0.85 on 41, loc 0.90 on 39, loc 1.00 on 25, lcsh 0.85
      on 12, another class 25; finding 9 reaches about 226 of 248. Build on the owner's word, with finding 10.
    - Finding 10, the project's one bare entity of the name (59 strings: "Adelaide, South Australia" beside the
      project's "Adelaide"): in a qualified reading with a placed candidate, a store entity exact on the name with no
      placement of its own, the only store entity of that name, joins the placed group. Build with finding 9.
    - "Outvoted rivals" (owner's decision; doc 10 7.6 "Confirmed"): at a tie at acceptance score, a group carried by
      two independent sources beats a rival carried by one; two two-source groups stay queued. Reaches "Finance" and
      "Aberdeen" shapes. Measure after finding 9.
    - Place-type preference for the nation shape ("Canada", "England", "China": TGN's nation beside hamlets, and LoC's
      heading unqualified): join an unqualified LoC heading to TGN's record of the highest level among namesakes of
      differing type. Design after finding 9 is in; the exports hold the type combinations.
    - ~~AAT's spans of time as Period (finding 11)~~: built 2026-10-21 (the design's facet was wrong, checked against
      vocab.getty.edu: the centuries sit under "dates (spans of time)" in the Associated Concepts facet, and the loader
      types those by parent string). Needs the owner's aat reload before the rerun.
    - TGN place namesakes among subdivisions and places ("Canada", "England", "Cornwall", "London", "Kent", "Norfolk",
      "Austria": 8 of 15 sampled queued subdivisions): the place-type preference item. TGN's `placeType` is in each
      record's data (an AAT URI). Measure first, the owner runs:
      `select ss.raw, string_agg(coalesce(t.label, r.data->>'placeType'), ' | ' order by t.label) as tgn_types from
      decisions d join source_strings ss on ss.id = d.source_string_id cross join lateral
      jsonb_array_elements(d.evidence->'groups') g cross join lateral jsonb_array_elements(g->'members') m join
      authority_records r on r.uri = m->>'uri' and r.source = 'tgn' left join authority_records t on t.uri =
      r.data->>'placeType' where d.run_id = 'run-20261020-150054-tzm64p' and d.valid and d.method = 'queued' and
      ss.context in ('place', 'keywords:sub') and (m->>'score')::numeric >= 0.97 and (m->>'veto') is null group by
      ss.id, ss.raw order by ss.raw;` (one row per queued string with its live exact TGN namesakes' types; the
      design follows from which type combinations recur).
    - Hyphen and space in keys (finding 8): "Stage setting and scenery" does not retrieve LCSH's "Stage-setting and
      scenery" because keys keep hyphens. Equating them needs the index rebuilt: `StringKey.Normalise` with hyphens as
      spaces and a full reload of all five sources, so it waits for the next full reload.
    - Place drift between aqn6dz and tzm64p with no rule change on places (exact-name 146 -> 128, vote 116 -> 125,
      minted 198 -> 204): undo and redo are not exactly idempotent on the store side; look at it with the store-stage
      work.
    - The `decisions` printer shows three groups of three members; order tied groups with the context's home source
      first, or print the home record always, so "Dance" and "Catalogs" show their LCSH record.
    - Dated string, undated namesake (finding 3; owner's decision owed): when the string's dates agree with a
      record's, veto the same source's records that have no dates ("Doyle, Richard, 1824-1883" vs LoC's undated
      "Doyle, Richard"). Measured 2026-10-20 (ceiling, run aqn6dz): keywords:head 754, persauthorfull 29, publisher 50
      queued strings with a date-agreeing record. The assistant recommends making it policy (doc 10 7.6, after
      finding 7: LoC's own uniqueness rule says the undated namesake is another person); not built until the owner
      says so.
    - ~~Mis-keyed compound separator (finding 4)~~: built 2026-10-21 in `StringReading.Read` (a lower-case part
      rejoined with one hyphen; 101 of 1,536 subdivisions began in lower case); the rerun measures it in keywords.
    - AAT qualified terms (finding 5): index the AAT term without its parenthetical qualifier, qualifier kept as
      placement, so "dance (performing arts genre)" is exact on "dance" and the home rule carries it. Loader change
      and aat reload.
    - Still to come for compounds: export as coordinated concepts (7.5 item 2), the entity page listing a heading's
      parts and the desk showing a part's parents (7.5 item 4). ~~Whether `unrec_` identifiers are kept~~: kept, the
      owner's decision 2026-10-21, built that day.
    - ~~"Placed through" (doc 10 section 7.6 finding 1)~~ and ~~the `single` method (finding 2)~~: both built
      2026-10-20 after the owner's yes; the rerun measures them (expect place votes up, place minted down, the
      legacy LoC-identified places gaining TGN ids rather than duplicates).
    - Shadow of run ccv3h8 (run-20261020-111213-rpb925, 2000 strings): Agree 1,022, Disagree 4 (three already
      classified: the de Young lumping, the Eastlake nephew, the Carlisle museum; the fourth, "Queen Victoria
      Museum" read as "Victoria Museum", was the agent parser stripping a title from a body's name, fixed the same
      day), OldOnly 100, Unverifiable 17. No place string agreed or disagreed: the 2024 places carry VIAF, Wikidata
      and LUX ids, never TGN, so they are Unverifiable where the new run accepted TGN; a place shadow needs the LUX
      self-echo (3b) or a Wikidata-to-TGN bridge to say anything. Among the queued places: "United States", "Spain",
      "Ontario" meet a TGN namesake at 1.00 (a hamlet or a county with the nation's name); TGN's place type is in
      the records' data and a bare string could prefer the nation or region over an inhabited place of the same name,
      to measure before building. "Dunedin" and "Passau" (city vs Landkreis) are genuine desk work.
    - Parser gap, the hyphenated "Country-City" form: "Portugal-Lisbon", "Italy-Vercelli", "Spain-Madrid",
      "England-Coventry" were minted (4 of 92 place strings in the rpb925 shadow, all OldOnly). A qualified context
      could try the whole string first and, when no index has it and it splits on one hyphen into two capitalised
      parts, read it again with the first part as the qualifier ("Lisbon" placed by "Portugal"). Hyphenated names
      proper ("Clermont-Ferrand", "Saint-Denis") survive because the first lookup finds them; three-part names
      ("Stoke-on-Trent") are never split.

Status review (2026-10-17): Phases 0-3 are complete and live; Phase 2's leftovers are closed except benchmarks.
Remaining, in rough order of value:

- Phase 2: `interpret` over the server's capture file once the API key is in the server's `.env`;
  benchmarks; `during: Period` with PeriodO URIs on TimeSpans; PeriodO narrowing (place, authority) as profile settings.
- Phase 4: the items listed above; the backup scripts exist and need installing on the server (09-hosting.md section 8).
- Housekeeping: the axe accessibility lint; renaming `de-pmc` to `main` is a deliberate later sweep (19 references
  across CI, compose, the trainer's base branch, docs and site links).
- The owner's: filing upstream issues (docs/upstream-issues.md), open question 7 (PMC strings), NuGet when ready.

34. The Agent parser as a first-class output (the owner's thought 2026-10-21, the go 2026-10-22; objectives 2 and 6
    rewritten that day). To build, size S each: (a) a service endpoint beside the dates one, taking a string and a
    profile name and returning the parsed agent (name, lifespan with the dates parser's result nested, roles,
    nationalities, kind, confidence) in the shape the reconciler reads; (b) a playground page on the examples site
    beside the dates playground, with the profiles to choose from and the PMC-shaped examples; (c) a corpus folder
    for agent strings in the dates corpus's shape, each entry nesting a dates expectation for its date substring
    (doc 10 section 13, decision 5), seeded from the PMC strings the reconciler has read, and the trainer page
    extended to it. Not yet started; after the alignment list in section 0.
