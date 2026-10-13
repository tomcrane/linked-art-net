# Prior art: parsing free-text dates in cultural heritage

Survey date: 2026-10-13. This informs [03-timespan-parser.md](03-timespan-parser.md). Nothing here is a
dependency we must take; it is what exists, what each thing gets right, and what we should borrow.

## 1. Standards and conventions

### Linked Art / CIDOC-CRM TimeSpan

Source: https://linked.art/model/base/ and the 1.0 JSON schema (`core.json`).

- Four optional datetime properties: `begin_of_the_begin`, `end_of_the_begin`, `begin_of_the_end`,
  `end_of_the_end`. Schema type is `string`, `format: date-time`, **no pattern**, no year bounds.
- Values must be "fully qualified with year, month, day, hour, minute and second"; a missing timezone
  is assumed `Z`. The site calls this "the xsd:dateTime profile of ISO 8601".
- The end **is included**: the last instant of 1881 is `1881-12-31T23:59:59Z`, and "the 1400s" ends
  `1499-12-31T23:59:59Z` (a CIDOC-CRM requirement).
- `_label` is for developers; a `Name` under `identified_by` is the human-facing display string.
- `duration` is a `Dimension` (value + unit, e.g. AAT days); no `classified_as` required.
- The schema's `TimeSpan` allows: `_label`, `type`, `identified_by`, `classified_as`, the four
  datetimes, `duration`. `additionalProperties: false`, so **no** `referred_to_by` on a TimeSpan.
- The site says nothing about BCE or year zero. We therefore need our own stated convention
  (see the parser design: ISO 8601 / XSD 1.1 astronomical numbering, year `0000` = 1 BCE).

### EDTF (ISO 8601-2:2019 Extended Date/Time Format), Library of Congress

Source: https://www.loc.gov/standards/datetime/

| Level | Features (examples verbatim) |
|---|---|
| 0 | `1985-04-12`, `1985-04`, `1985`; times `1985-04-12T23:20:30Z`; intervals `1964/2008`, `2004-06/2006-08` |
| 1 | uncertain `1984?`, approximate `2004-06~`, both `2004-06-11%`; unspecified `201X`, `20XX`, `1985-04-XX`; open/unknown ends `1985-04-12/..`, `../1985-04`, `1985-04-12/`, `/1985-04`; big years `Y170000002`, `Y-170000002`; seasons `2001-21`; negative years `-1985` |
| 2 | exponent years `Y-17E7`; significant digits `1950S2`; sub-year groups 21-41 (`2001-34` = Q2); sets `[1667,1668,1670..1672]` and `{1960,1961-12}`; group vs individual qualification `2004-06~-11`, `?2004-06-~11`; mid-string `X` `156X-12-25`; qualified interval ends `2004-06-~01/2004-06-~20` |

Why it matters: EDTF is the only widely-adopted *machine* notation that carries approximation,
uncertainty, unspecified digits, open ends and billion-year magnitudes. It is a near-perfect
**intermediate representation** for a parser: human string -> EDTF-like expression -> TimeSpan(s).
It does *not* cover "early/mid/late", "fl.", "before/after" semantics, centuries as a unit, or named
periods, so we need a small superset (see design doc, "Canonical expression").

.NET EDTF implementations: `EdtfDotNet` (richardtallent / steven-r fork; L0, L1, part of L2; no
NuGet; unmaintained) and `MoreDateTime` (NuGet 1.3.0, claims EDTF but the author says full ISO 8601-2
coverage is unfinished). Verdict: write our own EDTF reader/writer. It is a few hundred lines, and we
need it to interoperate with our own big-year date type anyway.

### Getty vocabulary date guidelines

Source: TGN/CONA editorial guidelines, Appendix B (Dates). Three fields per date: free-text *display
date*, hidden *start year*, hidden *end year*, estimated generously, proleptic Gregorian. This is
exactly the "label plus machine bounds" shape Linked Art expects. The guidelines list display-date
conventions ("ca.", "before", "after", "or", "probably", "active", BCE handling) that we should mine
for the corpus and for realisation rules. The National Gallery (London) "Display Date Descriptors"
wiki page defines mappings for terms like "After 1640", "Probably", "About" to year spans and is
another ready source of conventions.

## 2. Libraries that parse human date strings

| Project | Lang | Approach | Output | Notable |
|---|---|---|---|---|
| **cultural_dates** (arttracks; Getty Provenance Index heritage) | Ruby | Parslet PEG grammar over natural-language phrases | EDTF subset plus four-point `botb/eotb/bote/eote` | The only library found that outputs the CRM four-point model natively. Handles "sometime after the 1880s until at least October 1920". MIT, dormant. |
| **node-yearrange** (jeresig, ukiyo-e.org) | JS | Ordered regex rewrites to start/end year | `{start,end,circa}` | Years only. Large real-world test corpus `test/date-tests.json` (hundreds of museum/dealer strings incl. Japanese eras, "19thC(late)", "[187-]", "1764-1792 (floruit)"). MIT. |
| **timetwister** (NYPL) | Ruby | Ordered regex matchers | ISO start/end, `date_start_full`, `index_dates`, `certainty` | Archives-flavoured ("[between 1870 and 1890]", "n.d."). `timewalk` wraps it for ArchivesSpace. MIT. |
| **python-edtf** (ixc) | Python | pyparsing grammar for EDTF plus `text_to_edtf()` natural-language heuristics | EDTF objects with `lower_strict()/upper_strict()/lower_fuzzy()/upper_fuzzy()` | The fuzzy/strict distinction maps directly to our four-point spans. Used by LUX. |
| **undate** (Princeton CDH) | Python | Typed partial dates, multiple calendars | Undate / UndateInterval | Good model of partially-known dates and calendar conversion (Hebrew, Hijri). Beta. |
| **Microsoft.Recognizers.Text.DateTime** | C# | Rule-based NLP (TIMEX output) | daterange resolutions | What the current parser uses. Multilingual, good for "winter 1945", "last week of May 1900"; poor for historical idioms, no BCE, assumes contemporary context. 1.8.13 (Feb 2025) is the latest release. Keep as an *optional* strategy, never the backbone. |
| **HeidelTime (historic extension)** | Java | Rules; AncientTimes corpus (8 languages) | TimeML | Academic evidence that news-trained taggers fail on historical text; the corpus is a multilingual test source. |
| **Yale LUX `make_datetime`** (`project-lux/data-pipeline`, `pipeline/process/utils/mapper_utils.py`) | Python | Cheap-first chain: regex fixes -> `numpy.datetime64` (handles BCE and big years) -> `dateutil` -> `edtf` -> `dateparser`; memoised | `(begin, end)` strings | Confirms the "cheap first, escalate" design and the need for a non-`DateTime` type. Real-world edge cases in comments: `0052` parsed as 2052 by dateutil; `13.07.v100` German BC; `-00` months. |
| **This repo, `py_datetime`** | Python | regex tidy -> `dateutil` fuzzy | `{start,end}` | 35 test strings from library catalogue data; early/mid/late band rules. See [01-current-state.md](01-current-state.md). |
| **This repo, pmc-2 branch** | C# | regex year extraction; Recognizers.Text | `LinkedArtTimeSpan` | See [01-current-state.md](01-current-state.md). |

## 3. Semantic conventions worth adopting (and making configurable)

Collected from the above; defaults proposed, all overridable via a `ParseProfile`.

| Idiom | Common convention | Proposed default |
|---|---|---|
| `1984` | whole year | `1984-01-01T00:00:00Z` .. `1984-12-31T23:59:59Z` |
| `19th century` | 1801-1900 (historians) vs 1800-1899 (yearrange, py_datetime, most museum data) | 1800-1899 (`CenturyConvention.Popular`), switchable to `Strict` 1801-1900 |
| `early / mid / late Nth century` | yearrange: 00-24 / 40-59 / 75-99; py_datetime: 00-49 / 25-74 / 50-99 | thirds: 00-33 / 34-66 / 67-99, with `Bands` configurable per profile |
| `first/second half`, `first..fourth quarter` | arithmetic | exact arithmetic |
| `1950s`, `early 1950s` | 1950-1959; early 0-3, mid 3-6, late 7-9 | same (bands configurable) |
| `c. 1850`, `ca.`, `circa`, `about`, `approx.` | mark approximate; Getty widens by a few years | emit the bounds unchanged, set `Approximate`, and *optionally* pad by `CircaPaddingYears` (default 0; the label carries the hedge) |
| `1850?`, `probably 1850` | uncertain | `Uncertain` flag; no widening |
| `before 1850`, `by 1850`, `ante 1850` | open start | `end_of_the_end` only (plus optional `begin_of_the_end`); flag `OpenStart` |
| `after 1850`, `post 1850`, `1850-` | open end | `begin_of_the_begin` only; flag `OpenEnd` |
| `1904 or 1905` | alternatives | one span 1904-1905 with `Alternatives` listing both, flag `Disjunction` |
| `[between 1870 and 1890]` | span | 1870-1890 |
| `fl. 1480-1510`, `active 1480-1510` | period of activity | pair intent: two single-year spans, kind `Floruit`; single intent: one span |
| `1850-1920` with lifespan hint | birth 1850, death 1920 | pair of whole-year spans |
| `n.d.`, `undated`, `s.d.`, `[s.a.]` | no date | result with no TimeSpan, flag `Undated`, confidence 1.0 |
| `[17_]`, `[187-]`, `18XX`, `17??` | unspecified digits | EDTF `X` semantics |
| `500 BC`, `c. 2500 BCE`, `-0500` | before era | astronomical numbering: 500 BCE = `-0499` |
| `4.5 billion years ago`, `Y-4500000000` | deep time | `long` years, EDTF `Y` form in canonical output |
| `Edo period`, `Meiji 27` | named periods / regnal eras | pluggable `INamedPeriodSource`; built-in Japanese eras and common Western periods |
| `Winter 1945`, `Spring 1800` | seasons | EDTF 21-24; northern-hemisphere months by default |
| `1902 October 27`, `27.10.1902`, `10/27/1902` | full dates | day precision; day/month order from profile culture |
| `1962, (i.e. 1967)` | correction | prefer corrected value, flag `Corrected` |
| `1493 (first edition published)`, `1980s (when photocopies made)` | trailing prose | ignore parentheticals that contain no date; otherwise treat as a secondary date |

## 4. Candidate corpus sources (with licences)

| Source | Size | Licence | Use |
|---|---|---|---|
| py_datetime `tests.py` | 35 (+2 todo) | ours | seed |
| pmc-2 `PmcTransformer.Tests` date tests | see current-state doc | ours | seed |
| node-yearrange `test/date-tests.json` | several hundred | MIT | import with attribution; convert to our schema; review expectations (some are wrong, e.g. `161/1000, 1983` -> 1000) |
| timetwister `spec/` | ~100 | MIT | import |
| cultural_dates specs | ~50 | MIT | import; four-point expectations |
| python-edtf `text_to_edtf` tests | ~100 | MIT | import (natural language -> EDTF) |
| EDTF spec examples | ~60 | public | exercise the EDTF reader |
| Getty TGN/ULAN display dates | millions | ODC-By | sample 1-2k `display_date` / `start` / `end` triples as ground truth. Real authority data with human-assigned bounds. |
| Wikidata date precision/qualifiers | millions | CC0 | sample "sometime between", "circa" qualified statements |
| HeidelTime AncientTimes | 8 langs | research | multilingual stress test |
| PMC data (local postgres) | thousands | PMC | real archive/library strings, pending permission |
| Hand-written adversarial set | ~300 | ours | deep time, BCE, year 0, malformed, mixed noise, non-English |

Target: 3,000+ verified entries in v1, growing via the trainer UI.
