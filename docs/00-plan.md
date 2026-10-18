# Plan: linked-art-net

Status: proposal 2026-10-13; Phases 0-3 delivered and live at linkedart.net as v0.1.0 on 2026-10-16 (status
review in §7). Branch `de-pmc` is the working branch and `main` is what is deployed (README, "Branches and
releases"). The brief is in `exclude/design.md` (gitignored); this plan and the design docs in this folder are the
committed record.

## 1. Objectives (in priority order)

1. **Model library**: a top-class Linked Art 1.0 implementation in .NET, fluent to write, able to
   generate every example on linked.art, with comprehensive tests. ([02-model-library.md](02-model-library.md))
2. **TimeSpan parser**: a clean-slate, standalone library turning free-text dates into Linked Art
   TimeSpans with confidence, one-or-two-span intent, domain profiles, pluggable LLM fallback, deep
   time and BCE. ([03-timespan-parser.md](03-timespan-parser.md), [03-timespan-prior-art.md](03-timespan-prior-art.md))
3. **Corpus**: a large ground-truth set of date strings, plus a trainer UI to grow it. ([06-corpus-and-testing.md](06-corpus-and-testing.md))
4. **Service**: a Kestrel API returning rich TimeSpan results for GET/POST. ([04-timespan-service.md](04-timespan-service.md))
5. **Examples site**: C# code for every site example, generated vs site JSON, parser playground. ([05-examples-site.md](05-examples-site.md))
6. Later: transformation and alignment toolkit. ([07-later-alignment-toolkit.md](07-later-alignment-toolkit.md))

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
Wellcome instance, publishing and the QLever loader. Open questions for the owner in its section 13.

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
    earlier reconciliation (72,904 strings, 35,426 decisions) was imported into the store the same day.

Status review (2026-10-17): Phases 0-3 are complete and live; Phase 2's leftovers are closed except benchmarks.
Remaining, in rough order of value:

- Phase 2: `interpret` over the server's capture file once the API key is in the server's `.env`;
  benchmarks; `during: Period` with PeriodO URIs on TimeSpans; PeriodO narrowing (place, authority) as profile settings.
- Phase 4: the items listed above; the backup scripts exist and need installing on the server (09-hosting.md section 8).
- Housekeeping: the axe accessibility lint; renaming `de-pmc` to `main` is a deliberate later sweep (19 references
  across CI, compose, the trainer's base branch, docs and site links).
- The owner's: filing upstream issues (docs/upstream-issues.md), open question 7 (PMC strings), NuGet when ready.
