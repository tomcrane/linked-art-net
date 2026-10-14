# Design: ground-truth corpus and testing strategy

## 1. Corpus

### 1.1 Purpose

A large set of real and adversarial date strings with agreed Linked Art TimeSpan expectations. It
drives test-first development of the parser, guards against regressions, measures accuracy and
confidence calibration, and is a publishable artefact in its own right (CC0) that others can use.

### 1.2 Layout

```
corpus/
  README.md                    purpose, licence (CC0 for our entries; attributions for imports)
  schema/corpus-entry.schema.json
  seed/py-datetime.json        35 strings from py_datetime/tests.py
  seed/pmc-outputs.json        118 real PMC strings (expectations re-derived by hand)
  seed/pmc-agents.json         "Name [dates] (role)" strings from the archive
  imported/yearrange.json      MIT, jeresig/node-yearrange (reviewed)
  imported/timetwister.json    MIT, NYPL
  imported/cultural-dates.json MIT, arttracks
  imported/edtf-spec.json      LoC EDTF examples (parsing level)
  imported/getty-display-dates.json  sampled ULAN/TGN display_date with start/end (ODC-By)
  authored/bce-and-deep-time.json
  authored/centuries-and-bands.json
  authored/open-ended-and-relations.json
  authored/lifecycle-and-floruit.json
  authored/noise-and-malformed.json
  authored/multilingual.json
  proposed/*.json              from the trainer; not yet verified
```

### 1.3 Entry format

```json
{
  "id": "py-datetime-0007",
  "input": "1948 to early 1950's",
  "intent": "single",
  "profile": "default",
  "tags": ["range", "decade", "band", "apostrophe"],
  "expect": {
    "status": "parsed",
    "kind": "single",
    "canonical": "1948/early 195X",
    "edtf": "1948/1953",
    "timespan": { "begin_of_the_begin": "1948-01-01T00:00:00Z", "end_of_the_end": "1953-12-31T23:59:59Z" },
    "flags": ["Band"],
    "minConfidence": 0.8
  },
  "status": "verified",
  "source": { "name": "py_datetime tests", "licence": "repo" },
  "notes": "py_datetime expected 1954 (early = 0-4); new default bands are 0-3."
}
```

Rules: `expect` may be partial (imported corpora often only know years: `"years": {"start": 1948,
"end": 1953}`); pair expectations use `"begin"`/`"end"` spans; `"status": "undated"` or
`"unparsed"` entries need no spans; `profile` defaults to `default`; expectations are evaluated
against the profile named in the entry so policy changes are explicit edits to the corpus, never
silent.

### 1.4 Lifecycle

`proposed` (from the trainer or an import) -> `verified` (a human has checked it; CI enforces) ->
`disputed` (two reviewers disagree; excluded from pass/fail, listed in reports). Imports arrive as
`proposed` and are reviewed by editing their files; the planned trainer "review import" mode is not built.
The end-to-end operating guide is [08-corpus-contributions.md](08-corpus-contributions.md).

### 1.5 Metrics (reported by `tools/Corpus run`)

- Exact match: canonical equal and all expected datetimes equal.
- Bounds within tolerance (per-tag tolerance, e.g. 1 year for `circa` imports whose source widened).
- Status accuracy (parsed/undated/unparsed confusion matrix).
- Confidence calibration: expected calibration error over 10 bins; a report plot in CI artefacts.
- Per-tag and per-source accuracy, and a list of newly failing or newly passing entries versus the
  previous run (regressions block CI on `verified`; `proposed` is informative).
- Failures grouped by cause, likely parser gaps first: not parsed, parsed unexpectedly, status differs,
  reading differs (canonical or kind), bounds differ (usually a band, season or open-bound convention, or
  the source's own estimate), flags only (usually the importer's mapping of a source flag).

### 1.6 Trainer

Described in [05-examples-site.md](05-examples-site.md) §1. Key behaviours: shows the parser's
current answer and alternatives; reviewer edits the **canonical expression**, not datetimes, and sees
the re-realised spans instantly; tags are suggested from flags; submissions open a PR with the new
`proposed/*.json` entries. The CLI (`tools/Corpus import|validate|run|promote|stats`) covers the same operations
headlessly; `promote` flips statuses in place and refuses a failing entry, and `review` writes the Markdown that
`.github/workflows/corpus-review.yml` posts on pull requests (see
[08-corpus-contributions.md](08-corpus-contributions.md) §5).

## 2. Testing strategy (whole repo)

| Project | Layer | Notes |
|---|---|---|
| `LinkedArtNet.Tests` | model | `LinkedArtDate` (BCE, year 0, big years, leap rules, lexical forms, `T24:00:00`); fluent builders; serialisation options; polymorphic read; schema-manifest contract (both directions); vocabulary registry integrity |
| `LinkedArtNet.Examples.Tests` | golden | per example method: id is the site URI, generated JSON == cached site JSON (or an expected mismatch under `SiteIssue`), no schema errors beyond the site's own, round-trips; site JSON validated against the 1.0 schemas with the invalid ids pinned; coverage report current and every method in `examples/manifest.json` |
| `LinkedArtNet.Dates.Tests` | parser | tokenizer tables; grammar (string -> canonical) theories; realiser (canonical + profile -> datetimes) theories; corpus theories (one `Theory` per corpus file, `verified` only); property tests (FsCheck): canonical round-trip, bound ordering, normaliser idempotence, never throws on arbitrary strings; interpreter contract with a fake |
| `LinkedArtNet.Dates.Service.Tests` | HTTP | endpoints, limits, CORS, caching headers, result JSON schema, OpenAPI document snapshot |
| `LinkedArtNet.ExamplesSite.Tests` | HTTP | route smoke, content presence |
| `benchmarks/` | perf | BenchmarkDotNet for parser hot paths and serialisation; run on demand, numbers recorded in `docs/perf.md` |

CI (GitHub Actions): build, test with coverage (coverlet -> summary in PR), corpus report artefact,
schema contract, Docker image build; on tag: NuGet publish and GHCR push. Network-dependent tests
(site JSON refresh, Getty labels, LLM) run behind a trait and a scheduled weekly job.
