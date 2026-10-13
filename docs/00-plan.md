# Plan: linked-art-net

Status: proposal, 2026-10-13. Branch `de-pmc` (becomes `main`). The brief is in `exclude/design.md`
(gitignored); this plan and the design docs in this folder are the committed record.

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
   manuscripts; FR/DE/IT/ES/NL/LA lexicons.
7. `IDateInterpreter` + `LinkedArtNet.Dates.Llm` reference implementation, behind opt-in.
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

- 1.0 releases of both packages; CHANGELOG; docs site pages for API usage; perf doc; weekly scheduled
  jobs (site JSON refresh, Getty label check); submit the parser and service to linked.art/software.
- Corpus growth from trainer traffic; multilingual coverage; four-point (`EmitInnerBounds`) polish.

### Phase 5 (later): Alignment toolkit

Per [07-later-alignment-toolkit.md](07-later-alignment-toolkit.md). Starts with the local-dump
authority store (QLever or Postgres) and the alignment cache, then a PMC-style pipeline rebuilt on it.

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
4. Label policy default: `_label` only (proposed) or also an `identified_by` Name? *Open.*
5. Hosting target and domain names for the service and site. *Open.*
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

Next: Phase 1 step 8 (the 60 missing examples: vocab/required, concepts, events, provenance pages, places),
then file the upstream issues and cut `LinkedArtNet 1.0.0-preview`.
