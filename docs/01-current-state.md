# Current state (audit, 2026-10-13)

Branches: `main` -> `pmc-2` (56 commits, the PMC pipeline, "Final PMC version") -> `de-pmc` (one commit:
deletes the PMC projects, moves to net10.0 / C# 14, bumps Recognizers to 1.8.13). `de-pmc` therefore
holds exactly the `pmc-2` model library and parsers, minus the pipeline. The solution builds cleanly
(10 warnings, 0 errors). `PmcAsync` is not in the solution and does not compile; it should go.

## 1. Model library `LinkedArtNet` (~1,950 lines)

### Classes

| C# class | Base | Notes |
|---|---|---|
| `LinkedArtObject` | - | Base *and* catch-all: carries ~35 properties (`content`, `member`, `removed_by`, `held_or_supported_by`, ...). Also used directly for Type, Set, Material, Currency, Event, InformationObject via `Types` enum. |
| `Activity` | LAO | All events. `carried_out_by`, `timespan`, `took_place_at`, `during`, `used_specific_object`, `transferred_title_*`, `technique`, `influenced_by`, `diminished`, `caused_by`, `after`, `before`, `motivated_by`, `assigned_property`, `assigned`. |
| `Payment` | Activity | Only Activity subclass. |
| `Actor` (abstract), `Person` (`born`, `died`), `Group` (`formed_by`, `dissolved_by`) | | |
| `HumanMadeObject`, `DigitalObject`, `DigitalService`, `VisualItem`, `LinguisticObject`, `PropositionalObject`, `Place` (`defined_by`), `Period`, `Right`, `Language` (`notation`), `Name`, `Identifier`, `Dimension`, `MeasurementUnit`, `MonetaryAmount`, `LinkedArtTimeSpan` | LAO | `LinguisticObject`, `PropositionalObject`, `Period`, `Right` are empty markers. |
| `Vocabulary.IIIFManifest`, `IIIFImageService` | | Convenience constructors. |
| `Types` enum | | Acquisition, AttributeAssignment, Birth, Creation, Currency, Death, Destruction, Dissolution, Encounter, Event, Formation, InformationObject, Language, Material, Modification, PartRemoval, Payment, Production, Set, Type, VisualItem. |

Missing classes: Move, TransferOfCustody, RightAcquisition, Promise, Removal, Addition, PartAddition,
Transformation, a `Type`/Concept hierarchy with `broader`, Set, Material, Currency, Exhibition-as-class,
Procedure, Event-as-class. Missing properties include `moved_from/to`, `transferred_custody_*`,
`possessed_by`, `upper/lower_value_limit`, `assigned_to`, `used_object_of_type`, `added_by`,
`part_removed_by`, `participant`, `approximated_by`, `refers_to`, `broader`.

Assessment: the model was grown to exactly what the site examples needed. Every key in
`Examples/cached/new/**` has a property. Type safety is weak because most properties live on the
base class and most references are `List<LinkedArtObject>`.

### Serialisation (System.Text.Json)

- Order via `[JsonPropertyOrder]` on every property; numbers reused and interleaved across the hierarchy.
- Null omission via per-property `[JsonIgnore(WhenWritingNull)]`; empty lists are *not* omitted.
- `type` is a plain string set in constructors (`nameof`, `Types.ToString()`, literal `"TimeSpan"`).
- `@context` is `string[]` with `SingleStringJsonLDConverter` (single string when one entry).
- Polymorphism via `[JsonDerivedType]` without discriminators; unregistered subclasses in a
  `List<LinkedArtObject>` would fail. Deserialisation is lossy (no discriminator, abstract `Actor`).
- `LinkedArtDateConverter` writes `yyyy-MM-ddTHH:mm:ssZ` for CE dates; BCE dates bypass `DateTimeOffset`.

### Fluent style

All in `Constants.cs` (misnamed) and `DimensionExtensions.cs` as `this T where T : LinkedArtObject`
extensions: `WithContext`, `WithId`, `WithLabel`, `WithContent`, `WithClassifiedAs` (x2),
`WithLanguage`, `AsPrimaryName`, `AsAccessionNumber`, `AsSystemAssignedNumber`, `WithTechnique`,
`WithMadeOf`, `WithHeightDimension`, `WithWidthDimension`, `WithFileSize`, `WithDaysDimension`,
`WithRgbColor`, `WithShape`, `WithComponentCount`, `WithPageCount`.

Usage in Examples: `WithId` 293, `WithLabel` 284, `WithClassifiedAs` 151, `WithContext` 139.
Relationships (`ProducedBy`, `CarriedOutBy`, `TimeSpan`, `IdentifiedBy`) are set with object
initialisers or statements, so code alternates between chaining and assignment.

Rough edges: the second argument of `WithClassifiedAs(a, b)` is a meta-type of `a`, not a second
classification; AAT prefixing differs between `WithMadeOf` (adds) and `WithClassifiedAs(string)` (does
not); `WithFileSize`/`WithPageCount` return `LinkedArtObject` and lose the chain type; shared mutable
`MeasurementUnit` singletons; examples hand-patch results (`rembrandt.Born.TimeSpan.Label = null`).

### Vocabulary

`Vocabulary/Getty.cs`: 70 expression-bodied `LinkedArtObject` properties (`Painting => AatType("Painting",
"300033618")`), plus `Language()` and `Nationality()` factories. Examples still call `Getty.AatType(...)`
inline 47 times; 9 TGN/ULAN URLs are pasted literals. Data errors a registry would have caught:
`300435704` used for both System-Assigned Number and Record Identifiers; `300435452` for Physical
Statement *and* Collations Statement; `300026687`, `300264578`, `300404433`, `300026096` each with two
labels; a 10-digit typo `"3004040012"` in the PMC code.

### Dates and TimeSpan

`LinkedArtDate` is a class with public fields `DateTimeOffset? DtOffset`, `int Year, Month, Day`,
`bool serializeWithTimezone`. Negative years keep only Y/M/D and format as `-0540-01-01T00:00:00`.
**Year 0 throws, years above 9999 throw, no time-of-day for BCE, no precision concept.**
`LinkedArtTimeSpan.FromYear`/`FromDay` set `_label`; `AsBornAndDied` is an empty stub;
`LastSecondOfDay` is a no-op for BCE.

### Parsers (`LinkedArtNet/Parsers`, unused by Examples or tests)

Three entry points, all PMC-shaped:

1. `Parse(string) -> LinkedArtTimeSpan?` (mode 1, one string -> one span). Normalise
   (`CollapseWhiteSpace`, `TrimOuterBrackets`, `RemoveCirca`, `ExpandUncertainYears`) then a fast
   path for 3/4-digit years, else Microsoft.Recognizers.Text `DateTimeModel`, taking only the first
   `datetimeV2.daterange` result. Always returns a span if input is non-blank, even with no dates.
2. `ParseSimpleYearDateRange(string) -> (span?, span?, hints)` (mode 2, lifespan). Regex `\d{4}`:
   first and last four-digit numbers become whole-year spans. "d. 1957" becomes a *birth*.
3. `ParseLibraryAgentDate(string) -> TimespanParserHints` (hints only: active, circa, start/end year,
   century arithmetic), used to build LoC-style name headings ("Crane, Thomas, 1971-").

Observed defects (from code and from 118 committed PMC output spans):

| Input | Output | Problem |
|---|---|---|
| `1960s` | 1960 only | `ExpandUncertainYear` drops the 5th char |
| `[c 1960s-c 1970s]` | 1960-1969 | second decade lost |
| `8 May 1944-19 Aug 1946` | `1944-05-08T00:00:00+01:00` .. `1946-08-18T23:59:59+01:00` | local offset leak; last day lost |
| `Apr-May 1903` | April only | second month lost |
| `1 August 1876-[c 2016]` | 2016 only | first daterange result only |
| `8 May 1944` | no dates | `datetimeV2.date` results ignored |
| `1971-` | single day | open ranges unsupported |
| `n.d.` | span with label only | no "undated" concept |
| `Circa 1850` | not stripped | case-sensitive |
| `Winter 1945` | nothing | TIMEX seasons not handled |
| `500 BC` | nothing | `DateTimeOffset` only |
| `d. 1957` (pair mode) | birth 1957 | no death-only handling |

`CollapseWhiteSpace` does not collapse (alternating runs). Circa and `?` are deleted silently with
no flag in the output. `end_of_the_begin` / `begin_of_the_end` are never produced.

**Verdict on Microsoft.Recognizers.Text:** it was the heaviest dependency and the source of most of
the defects above (offset leak, first-result-only, year-range off-by-one, no BCE, no historical
idiom). It targets conversational "next Tuesday" text. We will not build on it. See the parser design.

## 2. Examples project

`Documentation.Create()` runs 16 page classes in fixed order; each example calls
`Documentation.Save(obj, validateAgainstLive)` which serialises, maps the linked.art `id` to
`output/new/...json`, fetches and caches the site JSON into `cached/new/` on first run, then compares
with `JsonAssertion.Equivalent` (semantic). First mismatch throws and stops the run.

| Metric | Value |
|---|---|
| `Save` call sites | 129 (126 active) |
| Validated against site | 118 (= files in `cached/new`) |
| Saved with `validateAgainstLive: false` | 8 |
| Commented out | 4 (`Context_Specific_Assignment` "Needs fixing on site", `Koot_Textual_Content`, `Rembrandt_Identifier`, `Ordering`) |

Reasons for the 8 unvalidated: site id collisions (`person/rembrandt/10`, `object/nightwatch/17` each
used by two examples), exhibition examples `activity/exh*`, `set/exset/1`, `set/exhset/1` not present
in 1.0 cache, `object/lepeintre/1`. Hacks to match the site: nulling TimeSpan labels, `"Painting"`
label "assume a mistake in the docs", a "Rembrant's" typo preserved.

Sections covered: base patterns, production/destruction, physical, aboutness, ownership, rights,
digital, sets, provenance (3 of many), exhibitions, conservation, actors, places, textual documents,
archives, assertions. Not covered: Concepts, Events, Vocabulary pages, most of Provenance (the
Documentation.cs comment says "Still loads to do in Provenance examples!!!"), Places beyond 4.

`Program.cs` also contains legacy "old documentation" builders (`amphora*`, `stieglitz`,
`ModelObjectProduction`) that write to `output/` without comparison; these predate 1.0.

## 3. Tests

`ParseTests` has one empty `[Fact]` and no project reference to `LinkedArtNet`. The Examples console
app is the only regression check. The PMC parser tests (`ParsedAgentTests`, 3 theory rows) were
deleted with the pipeline.

## 4. `py_datetime` (Python prototype)

`timespan_parser.py` (215 lines): regex tidy-up into `YYYY-YYYY`, then `dateutil.parser.parse(fuzzy=True)`.
Output `{"type":"TimeSpan","label":..,"start":..,"end":..}` with end at midnight of the last day.
Band rules: century early 00-49, mid 25-74, late 50-99; decade early 0-4, mid 2-7, late 5-9.
35 test strings (all library-catalogue style: `c.1928 - 32`, `[17_]`, `1948 to early 1950's`,
`late 13th century - early 14th century`, `[1861(?)]`, `1962, (i. e. 1967)`, `cop. 1980`), reproduced in
the corpus seed. Bugs: `lstrip` misuse ("circa 1900" -> "irca 1900"), no BC, only `th` centuries,
single-digit centuries crash, `UnboundLocalError` when begin != end.

## 5. The pmc-2 pipeline (for the later toolkit)

Inputs: DScribe/CALM XML (library, archive descriptions, archive authorities), Leeds EMu JSON.
Stages: filter -> per-record mapping to LinkedArtNet graph -> micro-parsers (notes `(CODE) text`,
`ParsedAgent` "Name [dates] (role)", dates, locations) -> collect distinct strings -> reconcile ->
assign -> write one JSON file per id.

Reconciliation: curated overrides (GitHub `digirati-co-uk/pmc-lux` JSON) first; then parallel
lookups against LUX search, ULAN (local Postgres dump with `levenshtein`), VIAF SRU, LoC suggest,
Wikidata; `DecideBestCandidate` voting heuristic (LUX hit plus >= 3 agreeing externals for agents,
>= 2 for places/concepts). Postgres tables `source_string_map`, `authorities`, `ulan_labels`,
`ulan_data_cache`, `pmc_subjects_csv`. Hand-rolled rate limiting with static timestamps (not
thread-safe), no HTTP response cache, blocking HTTP in static constructors, hard-coded `C:\pmc` paths.
Known bugs: `LocClient.Suggest` always reads index 0; `WikidataClient.GetName` ignores language.

Vocabulary growth in this branch was mostly archive and library statement types, plus ~25 more
inline `AatType` calls in `Media.cs` and `NotesField.cs` (medium and note-code tables that should be
data, not code).
