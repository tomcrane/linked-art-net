# Design: the `LinkedArtNet` model library

Goal: a complete, strongly typed, round-trippable .NET implementation of Linked Art 1.0 with a fluent
authoring API, able to regenerate every example on https://linked.art, with comprehensive tests.
This is the foundation everything else (date parser, service, examples site, later alignment toolkit)
sits on. Current state is in [01-current-state.md](01-current-state.md).

## 1. Scope and non-goals

In scope: the Linked Art 1.0 classes and properties (as defined by the JSON schemas at
`https://linked.art/api/1.0/schema/`), JSON-LD output identical in meaning to the site, reading JSON-LD
back into typed objects, vocabulary helpers, authoring ergonomics.

Out of scope for the library: date-string parsing (separate library, see
[03-timespan-parser.md](03-timespan-parser.md)), reconciliation, HTTP, storage, RDF graph APIs (we
are a JSON-LD-shaped object model, like `crom`, not an RDF toolkit).

## 2. Class hierarchy (implemented 2026-10-13, Phase 1 step 3)

Every one of the 41 classes in the 1.0 schemas is a C# class, `type` is fixed by the class, and each
class has exactly the properties its schema definition allows (plus six site-compatibility extras
listed below). The hierarchy was derived from the schema property sets themselves, using
`SchemaInspector --analyze` to find which sets nest inside which, so that no class inherits a
property the schema forbids on it:

```
LinkedArtObject (abstract)        @context, id, type, _label, identified_by, classified_as
 |-- LinkedArtEntity (abstract)   referred_to_by, equivalent, representation, subject_of, attributed_by
 |    |                           (member_of is declared per class: Set for most, Group for actors)
 |    |-- HumanMadeObject         produced_by, encountered_by, dimension, shows, carries, made_of, part, part_of,
 |    |                           member_of, current_owner, current_permanent_custodian, current_custodian, used_for,
 |    |                           current_permanent_location, current_location, destroyed_by, removed_by,
 |    |                           changed_ownership_through, modified_by, held_or_supported_by
 |    |-- DigitalObject           dimension, part_of, member_of, created_by, used_for, access_point, conforms_to,
 |    |                           format, digitally_carries, digitally_shows, digitally_available_via
 |    |-- PropositionalObject     dimension, conceptually_part_of, member_of, created_by, subject_to, about
 |    |    |-- LinguisticObject   content, language, part_of, used_for, format, digitally_carried_by
 |    |    |    |-- IIIFManifest  (helper; type stays LinguisticObject)
 |    |    |-- VisualItem         represents, represents_instance_of_type, part_of, digitally_shown_by, used_for
 |    |-- Set                     dimension, member_of, members_contained_by, created_by, used_for, about,
 |    |                           members_exemplified_by
 |    |-- Actor (abstract)        contact_point, residence, carried_out, participated_in, member_of (Group)
 |    |    |-- Person             born (Birth), died (Death)
 |    |    |-- Group              formed_by (Formation), dissolved_by (Dissolution)
 |    |-- Place                   part_of, member_of, defined_by
 |    |-- Type                    broader, member_of, created_by
 |         |-- Material, Language (+notation), Currency, MeasurementUnit
 |-- TemporalEntity (abstract)    timespan, took_place_at, during, referred_to_by, caused_by, after, before
 |    |-- Birth, Death
 |    |-- Destruction             part_of
 |    |-- Dissolution             part_of, influenced_by
 |    |-- ActivityBase (abstract) carried_out_by, used_specific_object, technique, influenced_by
 |         |-- Move               moved, moved_from, moved_to
 |         |-- Payment            paid_amount, paid_from, paid_to
 |         |-- RightAcquisition   establishes, invalidates
 |         |-- TransferOfCustody  transferred_custody_of/from/to
 |         |-- EmbeddedActivity (abstract)   part_of
 |         |    |-- Creation      part (site), Formation
 |         |    |-- Production    part, attributed_by (site)
 |         |    |-- Modification  part
 |         |    |-- Acquisition   transferred_title_of/from/to, part
 |         |    |-- Encounter     encountered
 |         |    |-- PartRemoval   diminished
 |         |    |-- AttributeAssignment   assigned_property, assigned
 |         |-- Period             participant, part_of, member_of, equivalent, representation, subject_of, attributed_by
 |              |-- Event
 |                   |-- Activity part (ActivityBase)
 |-- Name                         content, language, referred_to_by, assigned_by (site), part (Name)
 |-- Identifier                   content, referred_to_by, assigned_by, part (Identifier)
 |-- Dimension                    value, unit, lower_value_limit, upper_value_limit, assigned_by
 |-- MonetaryAmount               value, currency, lower_value_limit, upper_value_limit
 |-- Right                        referred_to_by, created_by, possessed_by
 |-- LinkedArtTimeSpan            begin_of_the_begin, end_of_the_begin, begin_of_the_end, end_of_the_end, duration
 |-- DigitalService               referred_to_by, access_point, conforms_to, format (site)
 |    |-- IIIFImageService        (helper; type stays DigitalService)
 |-- InformationObject            (reference only, for conforms_to)
```

Points worth knowing:

- **Embedded activities are not `Activity`.** The schema gives `Production`, `Acquisition` and friends
  a smaller property set than the referenceable `Activity` (no `id`-level properties such as
  `equivalent`, `member_of`, `participant`). They derive from `EmbeddedActivity` / `ActivityBase`, and
  `Activity.Part` is `List<ActivityBase>` so a provenance activity can hold acquisitions, payments and
  moves. `Birth`, `Death`, `Destruction` and `Dissolution` have no `carried_out_by` and sit directly
  under `TemporalEntity`.
- **`Period`, `Event`, `Activity`** share one schema definition (event.json), so `Event : Period` and
  `Activity : Event` add nothing but `part`.
- **`Type` shadows `System.Type`** inside the `LinkedArtNet` namespace; write `System.Type` there.
- **Site vs schema.** The contract test keys on property names; six properties the site's own
  validated examples use are kept although the 1.0 schema omits them, and are the only entries in
  the gap baseline: `Creation.part`, `Production.attributed_by`, `Name.assigned_by`,
  `Language.notation`, `DigitalService.format`, and `VisualItem.representation` (inherited from
  `LinkedArtEntity`; harmless). Cardinality follows the site where it disagrees with the schema:
  `current_permanent_custodian` is a single actor, `held_or_supported_by` and `part_of` on objects are
  arrays. `object.json` also declares `used_for` twice. All of these are to be reported upstream.
- The 1.0 schemas define exactly these 41 classes (verified by
  `SchemaContractTests.Manifest_covers_the_expected_linked_art_classes`). CRM classes such as
  Addition, Removal, PartAddition, Promise and Transformation are **not** in the 1.0 schemas and are
  not modelled.

The exact property-to-class assignment is **generated from the schemas, not hand-maintained**: a tool
(`tools/SchemaInspector`) reads `core.json` and the per-class schemas and emits a manifest of
`class -> allowed properties -> range`. A test asserts the C# model matches the manifest both ways
(every schema property exists on the class; no C# property exists that the schema forbids). This is
how we get to, and stay at, "full 1.0".

`Provenance Activity`, `Exhibition`, `Auction`, `Statement`, etc. are not classes; they are `Activity`
or `LinguisticObject` with `classified_as`. We give them **factory helpers** (`Activity.Provenance()`,
`Activity.Exhibition()`), not subclasses, so the serialised `type` stays correct.

### Open modelling choices (recommendations)

- **`Type` vs `Types` enum.** Drop the enum. `new LinkedArtObject(Types.Set)` becomes `new Set()`.
- **References.** Keep "a reference is a full object with only id/type/_label" (matches crom and
  the site). Add `obj.AsReference()` (shallow copy of id/type/_label) and an `IsReference` check.
- **Collections.** `List<T>` properties initialised lazily (`Add` helpers create them), serialised
  only when non-empty.
- **Equality.** Objects are reference-equal; add `LinkedArtObject.IdEquals(a, b)` for graph work.

## 3. Serialisation

Replace per-property attributes with a central `LinkedArtJson` configuration:

- One `JsonSerializerOptions` (`LinkedArtJson.Options`) with a type-info modifier that: orders
  properties from a single global ordering table (`@context`, `id`, `type`, `_label`, `identified_by`,
  `classified_as`, `referred_to_by`, ... matching site output order); omits null and empty
  collections; writes `type` from the class.
- Polymorphic **read** by `type` discriminator via a custom `JsonConverterFactory` for
  `LinkedArtObject` and all subclasses (STJ's built-in polymorphism cannot use an existing property
  name on read, so we read the `type` property ourselves and dispatch). Result: `LinkedArtJson.Parse<T>(json)` round-trips every site example byte-for-byte in meaning.
- Unknown properties: by default error on read (`LinkedArtJsonException` with JSON path); an option to
  collect them into `Extensions` for tolerant reading of data from other producers (LUX adds fields).
- `@context`: always a single string `https://linked.art/ns/v1/linked-art.json` on top-level objects
  unless the author sets more; `WithContext()` stays.
- Dates: see §5.
- Also provide `LinkedArtJson.Write(obj, Stream)`, `WriteIndented`, and a canonical form (sorted,
  minified) for hashing and diffing.

## 4. Fluent API

Keep the existing style (it reads well in the examples) and fix the rough edges. Principles: every
list property gets `Add`-style builders; chains never lose their static type; relationships can be
built inline with lambdas; vocabulary is strongly typed; nothing requires post-hoc patching.

```csharp
var nightWatch = new HumanMadeObject()
    .WithContext()
    .WithId($"{Documentation.IdRoot}/object/nightwatch/1")
    .WithLabel("Night Watch by Rembrandt")
    .WithClassifiedAs(Aat.Painting, metaType: Aat.TypeOfWork)
    .WithName("The Night Watch", n => n.AsPrimary().InLanguage(Aat.English))
    .WithName("De Nachtwacht", n => n.InLanguage(Aat.Dutch))
    .WithIdentifier("SK-C-5", i => i.AsAccessionNumber().AssignedBy(rijksmuseum))
    .WithProduction(p => p
        .CarriedOutBy(rembrandt)
        .TookPlaceAt(amsterdam)
        .WithTimeSpan(TimeSpans.Year(1642)))          // or .WithTimeSpan("1642") from LinkedArtNet.Dates
    .WithDimension(Aat.Height, 379.5, Aat.Centimeters)
    .WithStatement(Aat.MaterialsDescription, "oil on canvas")
    .WithCurrentOwner(rijksmuseum);
```

Mechanics:

- C# 14 **extension members** (`extension<T>(T obj) where T : LinkedArtObject { ... }`) give
  grouped, typed extensions and extension properties; `LangVersion 14` is already set.
- Generic `this T` returns keep the chain type (`DigitalObject.WithFileSize(...)` returns
  `DigitalObject`).
- Relationship builders take `Action<TChild>` and create the child (`WithProduction(p => ...)`,
  `WithName(content, n => ...)`), with overloads taking a ready object.
- The ~100 `WithX`/`AddX` list helpers are produced by a **source generator** from the property
  manifest, so they stay in sync with the model and we don't hand-write them.
- `WithClassifiedAs(Type, metaType: ...)` uses a named optional parameter; the string overload is
  removed in favour of `Aat.Of("300033618", "Painting")` / `Aat.Painting`.
- Factories on `LinkedArtTimeSpan`: `Year(long)`, `Day(y,m,d)`, `Between(a, b)`, `From(begin, end)`;
  these do **not** set `_label` by default (the site examples don't); `WithLabel` is explicit.
- Statement helper: `WithStatement(Type classification, string content, Action<LinguisticObject>? cfg)`
  adds `BriefText` automatically, like the PMC `SimpleStatement`.

## 5. Dates: `LinkedArtDate`

Replace the field-bag class with a `readonly record struct LinkedArtDate` used by both the model and
the parser. Rules:

- Proleptic Gregorian calendar. Fields: `long Year`, `int Month, Day, Hour, Minute, Second`.
- **Year numbering: ISO 8601 / XSD 1.1 astronomical.** Year `0000` exists and is 1 BCE; `-0001` is
  2 BCE. Helpers `FromBce(int bceYear)` and `ToBceYear()` so callers never do the arithmetic.
  (Linked Art does not specify; this is the convention the schema's `date-time` format implies and
  what LUX's `numpy.datetime64` path produces. Recorded as decision D3 in the plan.)
- Lexical form on output: `yyyy-MM-ddTHH:mm:ssZ`; years with |year| > 9999 written with as many
  digits as needed and no `+` (XSD style, `12345-01-01T00:00:00Z`, `-4500000000-01-01T00:00:00Z`).
  Always `Z`. Input accepts offsets (normalised to UTC), `T24:00:00` (-> `23:59:59` of same day),
  bare dates `yyyy-MM-dd` (-> midnight) and `yyyy`/`yyyy-MM` (-> start of period) for tolerant reading.
- Arithmetic needed by the realiser: `StartOfYear`, `EndOfYear` (`12-31T23:59:59`), `EndOfMonth`,
  `EndOfDay`, `AddYears/Months/Days`, comparisons, `IsLeapYear(long)` correct for negative years.
- Conversions to/from `DateTimeOffset`/`DateOnly` when in range, else throw `OutOfRangeException`.
- The `LinkedArtTimeSpan` validates ordering (`botb <= eotb <= bote <= eote` for whichever are set)
  and offers `Validate()`.

## 6. Vocabulary

Formalise the hard-coded Getty identifiers into a **data-driven registry**:

- `vocabulary/aat.json`, `tgn.json`, `ulan.json`: entries `{ "id": "300033618", "name": "Painting",
  "label": "paintings (visual works)", "metaType": "TypeOfWork"?, "usedFor": "...", "since": "1.0" }`.
- A source generator emits `Aat.Painting` (returns a fresh `Type` each call, as now, to avoid shared
  mutable state), `Aat.Of(id, label)` for ad-hoc terms, `Aat.TryGet(id)`, and `KnownTerm` records
  (id, uri, label, source) for lookups and `_label` consistency.
- Tests: ids unique, names unique, every id used in Examples is in the registry (no inline
  `AatType("...", "3000...")`), and an opt-in network test that checks labels against Getty SPARQL.
- Domain tables that were code in PMC (medium -> AAT, note code -> AAT, role -> AAT + activity type,
  language codes) become JSON under `vocabulary/mappings/` and load through a `VocabularyMap` type.
  They are inputs to the later toolkit but useful immediately for examples.

## 7. Example generation and 100% site coverage

Restructure `Examples` from a console app into:

- `LinkedArtNet.Examples` (library): one static method per site example, decorated with
  `[LinkedArtExample(Id = "object/nightwatch/1", Section = "/model/object/physical/", Title = "...")]`.
  The method returns the object; nothing writes files.
- `LinkedArtNet.Examples.Tests` (xunit): a `Theory` over all examples; for each, generate, load the
  cached site JSON (`examples/cached/<id>.json`, refreshed by a CLI), compare semantically
  (JSON-LD aware: array order ignored where the model is a set, `@context` normalised), validate the
  generated JSON against the linked.art 1.0 schema, and round-trip through the deserialiser.
- `tools/ExampleManifest`: crawls `https://linked.art/model/**` (or the `linked-art/linked.art` repo
  `docs/**/*.md`) and lists every example JSON URL the site publishes -> `examples/manifest.json`.
  The coverage test reports which site examples have no C# method. Target: 100% of the manifest.
- Site bugs: an example can be marked `SiteIssue("https://github.com/linked-art/linked.art/issues/NNN",
  "two examples share id person/rembrandt/10")`. The test still runs and records "expected mismatch"
  so the dashboard shows exactly what blocks 100% and why. We file the issues upstream rather than
  hack our output (no more nulled labels or preserved typos).

Known blockers from the audit to raise upstream: id collisions (`person/rembrandt/10`,
`object/nightwatch/17`), `Context_Specific_Assignment`, the `exh*` exhibition examples missing from 1.0,
"Rembrant's" typo, "Painting" label.

## 8. Tests for the model library

| Layer | What | Tool |
|---|---|---|
| Unit | `LinkedArtDate` arithmetic and lexical forms incl. BCE, year 0, |year|>9999, leap years, `T24:00:00` | xunit + FsCheck properties |
| Unit | Fluent helpers: each builder sets the right property, keeps chain type, never mutates shared vocab | xunit |
| Contract | Model vs schema manifest (both directions) | generated test |
| Golden | Every site example: generate == cached, schema-valid, round-trips | data-driven Theory |
| Serialisation | Property order, null/empty omission, context, polymorphic read of LUX/Getty/site documents | xunit |
| Vocabulary | Registry integrity; optional live label check | xunit (network tests behind a trait) |

Coverage goal: the generated-helper code excluded, line coverage > 90% on hand-written code;
coverlet already referenced.

## 9. Packaging

- NuGet `LinkedArtNet` (model), `LinkedArtNet.Dates` (parser), `LinkedArtNet.Dates.Llm` (optional
  interpreter). SemVer; `1.0.0` when the schema contract test and 100% example coverage pass.
- Target `net10.0` only (LTS). Add `net8.0` later only if a consumer asks.
- Repository layout: see plan §4.
