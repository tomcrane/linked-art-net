# Design: `LinkedArtNet.Dates`, the TimeSpan parser

Clean-slate design for turning a free-text date string into the best possible Linked Art TimeSpan(s),
with confidence and explanation. Standalone class library referencing `LinkedArtNet`. Prior art is in
[03-timespan-prior-art.md](03-timespan-prior-art.md); the current code's defects are in
[01-current-state.md](01-current-state.md) §1.

## 0. Two terms used throughout

- **AST (abstract syntax tree)**: the structured object a parser builds from text, with surface
  details removed. "late 1950s", "Late 1950's" and "1950s (late)" all become the same tree: a
  `Decade(1950)` node wrapped in `Banded(Late)`. The parser's only job is to produce the right tree;
  a separate realiser turns the tree into datetimes under a policy. See §2.4.
- **EDTF (Extended Date/Time Format)**: the Library of Congress notation, now ISO 8601-2:2019, that
  extends ISO dates with uncertainty (`1984?`), approximation (`1984~`), unspecified digits (`195X`),
  intervals and open ends (`../1850`), negative and very large years (`-0499`, `Y-4500000000`), sets
  (`[1904,1905]`) and seasons (`1945-24`). We use it as the written form of the AST, extended with a
  few keywords EDTF lacks. See §2.5 and the prior-art doc.

## 1. Requirements

1. Input: any string, plus an optional **intent** (one span, or a pair such as birth+death) and an
   optional **profile** (domain and culture hints). Output: 0, 1 or 2 `LinkedArtTimeSpan`s, each
   labelled with the original string, plus confidence, flags, a canonical machine form, alternatives
   and a human-readable explanation.
2. Range: from deep time (billions of years) through BCE to the future; nothing may depend on
   `System.DateTime`.
3. Deterministic, fast and offline by default. Expensive or non-deterministic strategies (LLM) are
   opt-in and pluggable, and their output is validated and realised by the same deterministic code.
4. Every interpretive rule (how long is "late", does "c." widen, which century convention) is a
   named, documented, overridable policy, never a magic number in a regex handler.
5. Testable in layers and against a large ground-truth corpus
   ([06-corpus-and-testing.md](06-corpus-and-testing.md)).
6. Multilingual-ready: month names, hedges, era words and century idioms in English first, then
   French, German, Italian, Spanish, Dutch, Latin, via data tables, not code.

Explicitly **not** a goal: parsing times of day or contemporary conversational expressions ("next
Tuesday"). Microsoft.Recognizers.Text is dropped: it targets that problem, and in our data it ignored
single dates, leaked local offsets, lost decades and seconds, could not do BCE, and is a 10 MB
dependency. Nothing in the new design needs it.

## 2. Architecture: parse, then realise

```
 string ──► Normaliser ──► Strategies (ordered, cheap first) ──► DateExpression (AST)
                                                                     │
                                       Intent + ParseProfile ──► Realiser ──► TimeSpanParseResult
                                                                                (0..2 LinkedArtTimeSpan)
```

The key separation: **parsing** produces a `DateExpression`, a small AST that says what the string
*means* ("the late part of the decade 1950s, approximate"). **Realisation** turns an expression into
datetimes under a profile ("late = last three years"). Parsing has one right answer per string;
realisation is policy. Ground truth can therefore be recorded at both levels, and changing a policy
does not invalidate parsing tests.

### 2.1 Normaliser

Pure string -> string + provenance (so the result can report what was stripped):

- Unicode: NFKC, dashes (`–`, `—`, `‐`, `−`, cp1252 `\x96`) to `-`, curly quotes, non-breaking
  spaces, collapse whitespace, trim.
- Case-fold only for keyword matching (preserve original for the label).
- Strip outer `[ ]` and `( )` when they wrap the whole string; record `Bracketed` (archival
  "inferred" convention) and `Parenthesised`.
- Peel trailing/leading punctuation (`.`, `,`, `;`) and MARC noise (`©`, `cop.`, `[sic]`, `i.e.`).
- Do **not** delete hedges (`c.`, `?`): the tokenizer needs them.

### 2.2 Tokenizer and grammar (the main strategy)

A hand-written tokenizer produces typed tokens: `Number`, `Ordinal` (`14th`, `XIVe`, `14.`),
`RomanNumeral`, `Month`, `Season`, `CenturyWord` (`century`, `c.`, `C`, `s.`, `siècle`, `Jh.`),
`DecadeSuffix` (`s`, `'s`), `Band` (`early`, `mid`, `late`, `first half`, `third quarter`, `beginning
of`, `end of`), `Hedge` (`c.`, `ca.`, `circa`, `about`, `approx.`, `probably`, `?`), `Era` (`BC`,
`BCE`, `AD`, `CE`, `a.C.`, `v. Chr.`), `Relation` (`before`, `after`, `by`, `until`, `since`, `post`,
`ante`, `pre-`, `not before`, `not after`), `Range` (`-`, `/`, `to`, `and`, `between`, `through`,
`bis`, `à`), `Or`, `Floruit` (`fl.`, `flourished`, `active`, `florit`), `Lifecycle` (`b.`, `born`,
`*`, `d.`, `died`, `†`, `bapt.`, `bur.`), `Undated` (`n.d.`, `s.d.`, `undated`, `s.a.`, `no date`),
`Unspecified` (`X`, `_`, `-`, `?`, `u` as digit placeholders), `Magnitude` (`million`, `billion`,
`ka`, `Ma`, `Ga`, `years ago`, `BP`), `Word` (anything else), `Punct`.

Keyword tables are per-language JSON resources (`lexicon/en.json`, `fr.json`, ...) selected by the
profile's cultures; several can be active at once.

A recursive-descent grammar over the token stream, roughly:

```
expression   := undated | set | interval | single
set          := single (OR single)+                           // "1904 or 1905"
              | clause (SEP clause)+                          // "(1) 1980, (2) 1977", "Sheet 1; 1964. Sheet 2; ca. 1930s": parts dated separately (2026-10-17)
                                                              // SEP = ";" | sentence "." | "(n)"; a clause without a date is a label; edition sentences are not parts
interval     := [BETWEEN] bound RANGE bound | relation single | single RANGE          // open end "1971-"
bound        := single | OPEN
single       := qualifiers* point qualifiers*
point        := date | month-year | year | season-year | decade | century | millennium
              | unspecified-year | named-period | deep-time | lifecycle point
year         := NUMBER [ERA]           // 3-4 digits by default; 1-2 with ERA or profile permission
decade       := NUMBER DECADE-SUFFIX   // 1950s, 1950's, 195X
century      := [BAND] ORDINAL CENTURY-WORD [ERA] | CENTURY-WORD ROMAN | "C" NUMBER   // 14th century, C14, s. XIV
qualifiers   := HEDGE | BAND | FLORUIT | LIFECYCLE | RELATION
```

Error recovery: when the grammar cannot consume the whole string, it returns the best prefix/infix
parse with `UnconsumedText` and a confidence penalty, rather than failing. Parentheticals that
contain no date tokens are skipped (`1493 (first edition published)`); those that contain dates are
parsed as secondary expressions (`1977 (1978)`, `1962 (i.e. 1967)` -> `Corrected`). A clause about an
edition, printing or reproduction dates the object and the first date is the original: `Originally in Edo
era. This edition was made in the early 20th century.` -> `early century 20`, `1932 (printed between 1963
and 1981)` -> `1963/1981`; but `Meiji era reprint of Edo period design` -> `period:meiji`, because the date
after "reprint of" is the original. A parenthesised bound narrows rather than widens: `Early 20th century
(before 1923)` -> `early century 20/1922`.

Abbreviated second bounds are expanded contextually: `1847-48` -> 1848; `c.1928 - 32` -> 1932;
`1900-10s` -> 1910s. Two-digit years alone are refused unless the profile sets a `TwoDigitYearWindow`.

### 2.3 Strategy chain

Ordered, cheapest first; each returns `Candidate(DateExpression, confidence, notes)` or nothing;
the first candidate above the profile's `AcceptThreshold` wins; otherwise all candidates are kept
as `Alternatives` and the best is used.

| # | Strategy | Handles | Confidence |
|---|---|---|---|
| 1 | `IsoEdtfStrategy` | Strict ISO 8601 and EDTF L0-L2 (our own parser) | 1.0 |
| 2 | `GrammarStrategy` | The cultural-heritage grammar above (the workhorse) | 0.7-0.98, reduced by hedges resolved heuristically, unconsumed text, ambiguous day/month order |
| 3 | `NamedPeriodStrategy` | Lookup in `INamedPeriodSource` ("Edo period", "Meiji 27", "Regency") | source-specific, typically 0.8 |
| 4 | `SalvageStrategy` | Pull plausible years out of noise when the grammar fails ("Not dated (1780's)", "72/127, 2005") | 0.3-0.5, flag `Salvaged` |
| 5 | `IDateInterpreter` (optional, async) | Anything still unparsed or below threshold; e.g. an LLM | Reported by interpreter, capped at 0.6, flag `Interpreted` |

Bare ISO shapes ("1984", "2024-04", "2024-04-27") take a fast path before the normaliser and the chain: one
regex, the canonical reader, the realiser. Strategies 1-4 are synchronous and allocation-light; `Parse()` never does I/O. `ParseAsync()` adds
strategy 5. A `MemoryCache`-backed memo (keyed by input + profile + intent) sits in front of the
chain because real datasets repeat strings heavily.

### 2.4 The `DateExpression` AST

Sealed record hierarchy, immutable, with a canonical textual form (below) and JSON serialisation:

- `YearPoint(long Year)`, `MonthPoint(long Year, int Month)`, `DayPoint(...)`, `SeasonPoint(long Year,
  Season)`, `Decade(long StartYear)`, `Century(int Number, Era)`, `Millennium(int Number, Era)`,
  `UnspecifiedDigits("18XX")`, `NamedPeriod(string Key, string Label, DateExpression Resolved)`,
  `DeepTime(long YearsAgoOrAstronomicalYear, Magnitude)`, `Undated`.
- Wrappers: `Qualified(inner, Approximate|Uncertain|Both)`, `Banded(inner, Band)` (Early/Mid/Late/
  FirstHalf/SecondHalf/Quarter(n)/Third(n)/BeginningOf/EndOf), `Relative(Relation, inner)` (Before/
  After/By/Until/NotBefore/NotAfter), `Lifecycle(Marker, inner)` (Born/Died/Baptised/Buried/Floruit),
  `Interval(start?, end?)`, `OneOf(members)`, `Corrected(original, corrected)`.

### 2.5 Canonical expression

Every AST has a canonical string. It is **EDTF Level 2 wherever EDTF can say it**, extended with a
few prefix keywords for what EDTF cannot. Examples:

| Input | Canonical |
|---|---|
| `1984` | `1984` |
| `c. 1850` | `1850~` |
| `1850?` | `1850?` |
| `1950s` | `195X` |
| `late 1950s` | `late 195X` |
| `19th century` | `century 19` (policy-free: the profile decides 1800-1899 or 1801-1900) |
| `early 14th century` | `early century 14` |
| `second quarter of the 14th century` | `q2 century 14` |
| `500 BC` | `-0499` |
| `c. 2500 BCE` | `-2499~` |
| `before 1850` | `before 1850` (EDTF `../1850` is emitted alongside) |
| `1971-` | `1971/..` |
| `1904 or 1905` | `[1904,1905]` |
| `between 1870 and 1890` | `1870/1890` |
| `fl. 1480-1510` | `fl 1480/1510` |
| `b. 1850` | `born 1850` |
| `1850-1920` (pair intent) | `1850/1920` (kind decided at realisation) |
| `Winter 1945` | `1945-24` |
| `Edo period` | `period:edo` (realises to `1603/1868`) |
| `4.5 billion years ago` | `4.5 Ga ago` (keeps the written precision; `Edtf` gives the realised years) |
| `n.d.` | `undated` |
| `1962 (i.e. 1967)` | `1967` (`Corrected` flag, original kept) |

The grammar of the canonical form is documented in `docs/canonical-expression.md` (written
with the implementation) and is itself parsed by strategy 1, so canonical strings round-trip:
`Parse(Canonical(e)) == e`. This is what the LLM interpreter is asked to produce, and what the
trainer UI lets a human type to correct a parse.

### 2.6 Realiser

`TimeSpanRealiser.Realise(DateExpression, Intent, ParseProfile) -> TimeSpanParseResult`.

Policies in `ParseProfile` (all serialisable; profiles live in `profiles/*.json` and can be
inherited and overridden):

| Policy | Default | Notes |
|---|---|---|
| `CenturyConvention` | `Popular` (19th = 1800-1899) | `Strict` = 1801-1900 |
| `CenturyBands` | early 0-33, mid 34-66, late 67-99; halves and quarters exact | `BeginningOf` = first decade, `EndOf` = last decade |
| `DecadeBands` | early 0-3, mid 3-6, late 7-9 | |
| `CircaPaddingYears` | 0 | > 0 widens outer bounds by N years for `~`; the hedge is always recorded as a flag |
| `UncertainPaddingYears` | 0 | for `?` |
| `OpenEndPolicy` | `OmitBound` | alternatives `ClampToNow`, `ClampToYear(n)`; `before X` -> only `end_of_the_end`; `after X` -> only `begin_of_the_begin` |
| `EmitInnerBounds` | false | when true, approximate ends produce `end_of_the_begin`/`begin_of_the_end` too (four-point) |
| `Hemisphere` | North | season months |
| `DefaultEra` | CE | profiles for classical archaeology may set BCE for bare small numbers |
| `MinYearDigits` | 3 | bare `50` is not a year unless era or profile allows |
| `TwoDigitYearWindow` | null | e.g. 1900 for library data where `'08` means 1908 |
| `DayMonthOrder` | from culture (en-GB: day first) | ambiguous numeric dates get reduced confidence and an alternative |
| `PlausibleYearRange` | -10000..now+100 | salvage guard |
| `NamedPeriods` | built-in table | plus any `INamedPeriodSource` |
| `LabelPolicy` | `Label` | `_label` = input; `LabelAndName` also adds `identified_by` Name; `Name` only |
| `AcceptThreshold` | 0.75 | below this, continue down the strategy chain |
| `LeadingDashIsOpenStart` | true | free text "-1850" is an open start (the EDTF reading, 1851 BCE, becomes an alternative); zero-padded "-0499" is always EDTF |
| `Winter` | `CrossesYearEnd` | "Winter 1776" = Dec 1776-Feb 1777; `StartOfYear` = Jan-Mar 1776 (US archival) |

Intent and kind:

| Intent | Expression | Kind | Output |
|---|---|---|---|
| `Single` | anything | `Single` | one span: botb = earliest start, eote = latest end |
| `Pair` | `Interval(a, b)` | `BirthDeath` (or `FormationDissolution` for Groups, chosen by caller) | `Begin` = span of `a`, `End` = span of `b` |
| `Pair` | `Lifecycle(Born, x)` / `Lifecycle(Died, x)` | `BirthDeath` | only the matching half |
| `Pair` | `Lifecycle(Floruit, Interval)` | `Floruit` | `TimeSpan` = whole activity span and `Begin`/`End` halves |
| `Pair` | a single point with no marker | `Single` + note | caller decides; status `Partial` |
| `Auto` | `Interval` with lifecycle/floruit markers | pair kinds | as above |
| `Auto` | otherwise | `Single` | |

Realisation rules for each node type (year -> `01-01T00:00:00Z`..`12-31T23:59:59Z`; month -> last
day of month, leap-aware for any year; `UnspecifiedDigits("18XX")` -> 1800..1899; `Decade`; `Century`
per convention; `Season` -> month ranges; `Banded` -> sub-range per bands; `Relative` -> drop a bound;
`OneOf` -> hull of members with `Disjunction` flag and members as alternatives; `DeepTime` -> years
ago converted to astronomical year relative to 1950 (BP convention) or to the current year, per
magnitude token, with precision widening by significant digits) are each a small pure function with
their own tests.

Invariants enforced: `botb <= eotb <= bote <= eote` where present; at least one bound unless
`Undated`/`Unparsed`; every TimeSpan `_label` equals the original input.

### 2.7 Result type

```csharp
public sealed record TimeSpanParseResult(
    string Input,
    ParseStatus Status,                  // Parsed, Partial, Undated, Unparsed
    ResultKind Kind,                     // Single, BirthDeath, FormationDissolution, Floruit, None
    double Confidence,                   // 0..1
    LinkedArtTimeSpan? TimeSpan,         // Single or Floruit whole span
    LinkedArtTimeSpan? Begin,            // pair kinds
    LinkedArtTimeSpan? End,
    DateExpression? Expression,
    string? Canonical,
    string? Edtf,                        // strict EDTF approximation of the realised bounds
    IReadOnlySet<DateFlag> Flags,        // Approximate, Uncertain, OpenStart, OpenEnd, Disjunction, Bracketed,
                                         // Salvaged, Interpreted, Corrected, Floruit, Bce, DeepTime, NamedPeriod,
                                         // Season, Band, AmbiguousDayMonth, Unconsumed
    IReadOnlyList<Alternative> Alternatives,
    IReadOnlyList<string> Notes,
    string Strategy,
    string? UnconsumedText,
    string Profile,
    string ParserVersion);
```

## 3. Public API

Implemented: `TimeSpanParser`, `ParseOptions`, `ProfileCatalog.Load("profiles")`, the fluent bridge in `TimeSpanBridge`
(`WithTimeSpan`, `WithLifespan`, `WithExistence`) and `ProfileAdvisor.Analyse` (since 2026-10-17 with example strings per
feature and a `Suggested` profile, written as changes from `default` by `SuggestedProfileJson`; exposed as `POST /v1/advise`
and `/dates/advise`). `ParseAsync` with an `IDateInterpreter` is implemented (2026-10-17, section 4);
`TimeSpanParserBuilder` is still to come.


```csharp
// simplest
var parser = TimeSpanParser.Default;
var r = parser.Parse("late 1950s");                       // Intent.Auto, profile "default"
LinkedArtTimeSpan ts = r.TimeSpan!;

// intent and profile
var life = parser.Parse("1850-1920", Intent.Pair);
person.Born = new Birth { TimeSpan = life.Begin };
person.Died = new Death { TimeSpan = life.End };

var med = parser.Parse("fl. C14", new ParseOptions { Intent = Intent.Pair, Profile = Profiles.MedievalManuscripts });

// configured
var parser2 = new TimeSpanParserBuilder()
    .UseProfile("profiles/pmc-library.json")
    .AddNamedPeriods(new JsonNamedPeriodSource("periods/japanese-eras.json"))
    .UseInterpreter(new AnthropicDateInterpreter(apiKey))       // from LinkedArtNet.Dates.Llm
    .Build();
var r2 = await parser2.ParseAsync("the yeare of oure lorde MCCCCLXXXV", ct);

// fluent bridge onto the model (lives in LinkedArtNet.Dates, so the model has no parser dependency)
production.WithTimeSpan("c. 1642");
person.WithLifespan("1606-1669");
group.WithExistence("fl. 1480-1510");
```

`ParseOptions` carries `Intent`, `Profile` (name or object), `AllowInterpreter`, `MaxAlternatives`.
`TimeSpanParser` is thread-safe and intended as a singleton.

## 4. LLM interpreter (pluggable)

```csharp
public interface IDateInterpreter
{
    Task<InterpreterResponse?> InterpretAsync(string input, InterpreterContext context, CancellationToken ct);
}
public sealed record InterpreterResponse(string Canonical, double SelfConfidence, string Rationale);
```

Implemented 2026-10-17 (`src/LinkedArtNet.Dates/Interpretation/`, `src/LinkedArtNet.Dates.Llm/`). `IDateInterpreter`
has `Name`, `MaxConfidence` (0.6 by default) and `InterpretAsync`; `InterpreterContext` carries the profile (with its
languages), the intent and the deterministic result; `InterpreterResponse` is a canonical expression, the interpreter's
own confidence, a rationale and an optional intent. `TimeSpanParser.ParseAsync(input, interpreter, options)` runs
`Parse` and asks the interpreter only when the result is unparsed, partial or below the profile's accept threshold
(never for undated text); the answer is read by the canonical reader and realised under the profile, capped at
`MaxConfidence`, flagged `Interpreted`, with the rationale in the notes and the deterministic reading kept as an
alternative. A non-canonical answer, no answer or a failure leaves the deterministic result with a note. `Parse`
itself never calls an interpreter and never does I/O.

`LookupInterpreter` is a table of reviewed readings (text to canonical, from a JSON file or in memory) with a higher
cap, for curated corrections and for tests. `ClaudeDateInterpreter` (`LinkedArtNet.Dates.Llm`, the Anthropic SDK)
asks Claude (default `claude-opus-5-5`, effort low) for JSON under the schema `{canonical, confidence, rationale,
intent}`; the system prompt is this repository's `docs/canonical-expression.md`, embedded and marked for prompt
caching; answers are memoised and token usage counted. By the owner's decision (2026-10-17) the LLM is a
corpus-growing assistant: it is reached from the trainer (08-corpus-contributions.md section 7) and from
`tools/Corpus interpret`, never from the public service, whose `llm=true` is accepted and ignored with a note. The
reviewer typing a canonical expression in the trainer is the review step itself, not an interpreter implementation.

## 5. Named periods

Implemented (Phase 2 step 4): `JsonNamedPeriodSource.BuiltIn` loads the four embedded tables under
`src/LinkedArtNet.Dates/Periods/`; a profile or caller adds its own with `new JsonNamedPeriodSource().AddFile(...)`
and `CompositePeriodSource`. Names are matched whole after lower-casing and stripping diacritics and
punctuation, so "Shōwa", "Showa era" and "昭和" all resolve; regnal years ("Meiji 27", "Kangxi 12",
"Qianlong 60th year") count from the era's first year and get the key `meiji-27`. The grammar reads a
period wherever a date could stand, so "early Edo period", "c. Georgian" and "Ming - Qing dynasty" work.
Provenance: the built-in tables were written by the assistant from general knowledge (standard chronologies
as found in Wikipedia and museum practice), not copied from any source, and each table's `notes` says what to
check it against. They carry no authority URIs: the PeriodO source below is the intended credited, sourced
replacement, and the tables are the interim.

`INamedPeriodSource.TryResolve(string text, CultureInfo, out NamedPeriod)`. Built-in JSON: Japanese
eras (incl. regnal years, `Meiji 27` -> 1894), Chinese dynasties, British regnal periods, broad
Western archaeological periods only where bounds are conventional. Art-historical styles ("Baroque")
are **not** resolved by default (too fuzzy); a profile can add them.

**PeriodO (done 2026-10-17).** `PeriodOSource` reads `periods/periodo.json`, a compact copy of the PeriodO dataset
(https://perio.do, CC0; 9,446 definitions from 501 authorities at version 301; provenance and refresh in
`periods/README.md`, `tools/Corpus periodo`). `PeriodOSource.ForRepository(root)` composes it after the built-in
tables, and the service (`Parser:PeriodOPath`), the site, the corpus evaluator and the Corpus tool all use that
composite, so "Iron Age" parses the same everywhere. A name one authority defines resolves to that definition with its
PeriodO URI (`NamedPeriod.Uri`) and the key is the PeriodO id, so the canonical `period:p0f65r2qmh2` is a stable,
dereferenceable reference; a name many authorities define resolves to the hull of all of them under a
`periodo-<label>` key with a note saying how many and from where, and `PeriodOOptions` (place, authority, language)
narrows it. `NamedPeriod.Source` carries the citation into the result's notes. Still to come: emitting `during:
Period` with the URI on the TimeSpan, and a profile setting for the PeriodO options.

## 6. Error handling and edge cases

- Inverted ranges (`1998-1902`) -> swap with `Inverted` flag and reduced confidence.
- `1900-10s` and `1847-48`: abbreviation expansion, with the unexpanded reading as an alternative.
- Year 0 input ("0 AD") -> treated as 1 BCE with a note; year 0 in ISO input accepted as-is.
- `T24:00:00` on input -> `23:59:59`.
- Mixed scripts: full-width digits and kanji era names normalised by NFKC and the lexicon.
- Extremely long inputs: truncated to 500 chars before parsing; flag.
- Nothing throws for bad input; `Status = Unparsed` with notes. Exceptions only for programmer
  errors (null profile, invalid canonical string passed to `Realise`).

## 7. Testing

See [06-corpus-and-testing.md](06-corpus-and-testing.md). Summary: tokenizer tests; grammar tests
(string -> canonical); realiser tests (canonical + profile -> datetimes); corpus tests (end to end);
property tests (canonical round-trip, invariants, idempotence of the normaliser); benchmarks.
Coverage target 95% for the library.

## 8. Performance and footprint

No dependencies beyond `LinkedArtNet`. Lexicons as embedded resources, loaded once. Target under
2 µs for `1984`, under 50 µs for a typical idiom, under 1 ms worst case for salvage on a 500-char
string. `GeneratedRegex` only in the tokenizer where it beats hand code.

## 9. Open questions for the owner

See plan §6. The ones that most affect this library: century convention default, circa padding
default, whether `before X` should emit a `begin_of_the_end` as well, and the label/Name policy.
