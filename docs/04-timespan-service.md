# Design: TimeSpan parsing service (`LinkedArtNet.Dates.Service`)

Status (2026-10-14): implemented as designed in `src/LinkedArtNet.Dates.Service`, with these differences: `strategy`
values are `canonical`, `grammar`, `named-period`, `salvage`; `llm=true` is accepted and answered with a note until an
`IDateInterpreter` is configured; capture writes JSONL only; `/healthz` checks the profile catalog and a parse of "1984"
(the corpus is not loaded by the service). Run locally with `dotnet run --project src/LinkedArtNet.Dates.Service` and open
`/docs`. Build the image from the repository root with `docker build -f src/LinkedArtNet.Dates.Service/Dockerfile .`.

A small ASP.NET Core (Kestrel, Minimal API, .NET 10) service that wraps the parser library
([03-timespan-parser.md](03-timespan-parser.md)) for deployment. Stateless; no database; optional
LLM escalation; optional capture of unparsed inputs to feed the corpus.

## 1. Endpoints (all under `/v1`)

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/timespan?s=1984` | Parse one string. Query: `s` (required), `intent=auto\|single\|pair` (default `auto`), `profile=<name>` (default `default`), `llm=true` (opt-in escalation, only if the server has an interpreter configured), `shape=full\|timespan` (default `full`). |
| POST | `/v1/timespan` | Parse many. Body below. Max 500 inputs per call (configurable). |
| GET | `/v1/profiles` | List available profiles with descriptions. |
| GET | `/v1/profiles/{name}` | A profile's effective settings (JSON). |
| GET | `/v1/canonical?e=late%20195X` | Realise a canonical expression without parsing text (debug/trainer aid). |
| GET | `/v1/agent?s=...&profile=` | Read an agent string (2026-10-17): name parts, honorifics, date part (as a dates result), roles, nationality, qualifiers, and the Linked Art Person or Group (`agent=false` omits it). `GET /v1/agent/profiles` lists the profiles. |
| POST | `/v1/advise` | Profile advice for a dataset (added 2026-10-17): counts of the conventions in use with example strings, recommendations, and a suggested profile stated as changes from `default`. Body `{ "inputs": [...], "languages"?: [...] }` or `text/plain` with one string per line; up to 20,000 strings and 2 MB; strings over 500 characters are skipped and counted. Nothing is stored. The site's `/dates/advise` calls it. |
| GET | `/healthz`, `/version` | Liveness; parser + corpus version and git SHA. |
| GET | `/openapi/v1.json`, `/docs` | OpenAPI document and a UI (Scalar). |

### POST body

```json
{
  "profile": "default",
  "intent": "auto",
  "options": { "llm": false, "emitInnerBounds": false, "labelPolicy": "label" },
  "inputs": [
    "1984",
    { "s": "1850-1920", "intent": "pair" },
    { "s": "fl. C14", "profile": "medieval-manuscripts", "ref": "row-17" }
  ]
}
```

Per-item fields override request-level fields. `ref` is echoed back so callers can correlate.

### Response (one result; POST returns `{ "results": [ ... ] }` in input order)

```json
{
  "input": "late 1950s",
  "ref": null,
  "status": "parsed",
  "kind": "single",
  "confidence": 0.9,
  "canonical": "late 195X",
  "edtf": "1957/1959",
  "flags": ["Approximate", "Decade", "Band"],
  "strategy": "pattern",
  "timespan": {
    "type": "TimeSpan",
    "_label": "late 1950s",
    "begin_of_the_begin": "1957-01-01T00:00:00Z",
    "end_of_the_end": "1959-12-31T23:59:59Z"
  },
  "begin": null,
  "end": null,
  "alternatives": [],
  "notes": ["'late' interpreted as the last three years of the decade (profile 'default')."],
  "unconsumed": null,
  "profile": "default",
  "parserVersion": "0.3.0"
}
```

For `kind: "birthDeath"` or `"floruit"` or `"formationDissolution"`, `begin` and `end` hold the two
TimeSpans and `timespan` is null (or, for floruit, `timespan` holds the single activity span and
`begin`/`end` the two bounds as spans, so both uses are served). `shape=timespan` returns just the
Linked Art TimeSpan object(s). `status` is one of `parsed`, `partial`, `undated`, `unparsed`.
Unparsed inputs still return 200 with `status: "unparsed"`; only malformed requests return 4xx.

The `_label` of every TimeSpan is the exact input string. `labelPolicy=labelAndName` also adds an
`identified_by` `Name` with the input; `name` adds only the Name.

## 2. Behaviour

- **Deterministic by default.** Same input + profile + parser version => same output. Suitable for
  `Cache-Control: public, max-age=86400` and an `ETag` of `hash(version, profile, intent, input)`.
- **Fast.** In-memory LRU memo (inputs repeat heavily in real data). Target p50 < 1 ms, p99 < 20 ms
  for non-LLM paths on a small container.
- **No LLM at request time.** Decided 2026-10-17: the public service stays deterministic. `llm=true` is accepted and
  ignored with a note saying so. The interpreter (`LinkedArtNet.Dates.Llm`) proposes readings for review in the trainer
  and through `tools/Corpus interpret`; anyone hosting their own service can call the library's `ParseAsync` for
  escalation, with results capped in confidence and flagged `Interpreted`.
- **Limits.** Input string max 500 chars; 500 inputs per POST; request body 256 KB (`/v1/advise`: 20,000 strings, 2 MB); built-in rate
  limiter (fixed window per IP, generous for GET). CORS: allow any origin for GET and POST without
  credentials, so the examples site and others can call it from the browser.
- **Capture (opt-in).** If `Capture:Enabled`, inputs with `status != parsed` or confidence below a
  threshold are appended (input, profile, result) to a rolling JSONL file or table for triage in the
  trainer. Documented in the privacy note; off by default; never captures `ref` or headers.
- **Versioning.** `/v1` path; breaking response changes bump the path. `parserVersion` in every result.

## 3. Implementation notes

- Project `src/LinkedArtNet.Dates.Service`, Minimal API, `TypedResults`, `Microsoft.AspNetCore.OpenApi`
  + Scalar for `/docs`. Request/response DTOs are the parser library's result types with a thin
  mapping; JSON via the model's `LinkedArtJson` options so TimeSpan output is byte-identical to the
  library's.
- DI: `TimeSpanParser` singleton built from `ParserOptions` (profiles directory, interpreter config);
  `IDateInterpreter` registered only when configured.
- Health: `/healthz` runs a parse of `"1984"` and checks the corpus smoke set loads.
- Tests: `WebApplicationFactory` integration tests for every endpoint, limits, CORS, caching
  headers, and a contract test that the POST result schema matches the documented JSON schema
  (`schemas/timespan-result.schema.json`, published with the service).
- Container: multi-stage Dockerfile (sdk -> `aspnet:10.0-noble-chiseled`), non-root, port 8080,
  `HEALTHCHECK`. GitHub Actions: build, test, push to GHCR on tag. Hosting target is the owner's call
  (Azure Container Apps, Fly.io, or a VM behind Caddy all fit a stateless container).

## 4. Standalone use

The image `ghcr.io/tomcrane/linked-art-net/dates-service` (tags `v0.1.x` and `latest`, built by `.github/workflows/docker.yml`
from each release tag) runs anywhere Docker does: `docker run --rm -p 8080:8080 ghcr.io/tomcrane/linked-art-net/dates-service:latest`.
Profiles and the PeriodO table are inside the image; `Parser__ProfilesDirectory` can be pointed at a mounted folder of the
caller's own profiles, `Parser__RequestsPerMinute` raised on a private network, and `Capture__Enabled` turned on with a
volume for later review. The site's `/dates/api#docker` section documents this for non-.NET users (2026-10-17).

## 5. Example calls

```
GET /v1/timespan?s=c.%201850
GET /v1/timespan?s=1850-1920&intent=pair
GET /v1/timespan?s=fl.%20C14&profile=medieval-manuscripts
GET /v1/timespan?s=4.5%20billion%20years%20ago
GET /v1/timespan?s=n.d.
POST /v1/timespan   { "inputs": ["1984", "late 1950s", "500 BC", "Winter 1945"] }
```
