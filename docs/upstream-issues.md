# Upstream findings for linked.art

Problems found on https://linked.art while making every published example reproducible. None of them is
worked around in the library: examples that cannot match the site carry a `[SiteIssue]` attribute whose
reason is reported by the tests and in `examples/coverage.md`, and the site's own JSON is validated against
the 1.0 schemas by `SiteJsonTests` (ids listed there as `KnownInvalid`). The owner files these upstream;
add the issue URL to the `SiteIssue` attribute and to this file once filed.

Existing issues already covering a finding: [#751](https://github.com/linked-art/linked.art/issues/751)
(`notation`). Searched the tracker on 2026-10-13 for the others without finding them.

Regenerate the schema evidence with `dotnet run --project tools/ExampleManifest -- validate`.

## 1. One URL, two examples

The site generates `https://linked.art/example/{id}.json` from the last code block that claimed the id, so
where two pages (or two blocks on one page) use the same id, one example's JSON is not published anywhere.

| Id | Example shown on the page | JSON actually published |
|---|---|---|
| `text/koot_nightwatch/1` | document page: textual content (`carried_by`) | document page: abstract work with `part_of` |
| `set/exhset/1` | collection page: unclassified exhibition set | exhibition page: set classified as Exhibition Collection |
| `object/spring/31` | assertion page: context-specific identifier assignment | vocab/required page: usage example with local `vocab/terms/painting` ids |

`person/rembrandt/2`, `provenance/manet_proust/1`, `provenance/payneheirs_getty/2`, `person/rembrandt/44`
also appear on two pages; `rembrandt/2` has identical content both times, the others are still to be checked
(Phase 1 step 8).

## 2. Missing example JSON redirects for ever

`https://linked.art/example/set/exset/1.json` (any id with no example) answers `301` to
`/example/set/exset/1.json.json`, which redirects to `.json.json.json`, without end. A 404 would be kinder
to crawlers. `tools/ExampleManifest fetch` follows no redirects for this reason.

## 3. Site JSON that the 1.0 schemas reject

38 of the 183 published examples fail validation against `https://linked.art/api/1.0/schema/`. The library
follows the site in every case (decision D15) and documents each property as a site-compatibility extra.

| Discrepancy | Site examples | Schema says |
|---|---|---|
| `notation` on referenced `Language` and `Currency` | object/nightwatch/1, /4, /41; text/koot_nightwatch/1; provenance/fixme/1, provenance/sharp_sale/1 | reference shapes allow no `notation` (#751) |
| `part_of` as an array | object/spring/back, object/spring/support, object/nightwatch/support (HumanMadeObject); activity/exhb/1, event/christies_24477_16/1, event/early_roman/1 (Activity, Period); object/spring/15 `changed_ownership_through[].part_of`; object/nightwatch/25 `attributed_by[].part_of`; /26 `modified_by[].part_of`; /9 `dimension[].assigned_by[].part_of` | single object |
| `classified_as` on actor and place references | activity/exha/1, activity/exhb/1, event/christies_24477/1 (`carried_out_by`, `took_place_at`); object/yiadom-boakye/1 (`assigned_by[].carried_out_by`); place/amsterdam/1 (`part_of`); provenance/unknown_faure/1 (`before`, `after`); provenance/fixme/1 (`paid_to`) | reference shapes allow only id, type, _label |
| `carried_out_by` reference must be a `Person` | activity/exha/1, activity/exhb/1, event/christies_24477/1 (Groups) | `const Person` |
| `used_specific_object` reference must be a `HumanMadeObject` | activity/exha/1, activity/exhb/1 (Set); person/rembrandt/10 `assigned_by[].used_specific_object` (LinguisticObject) | `const HumanMadeObject` |
| `before`/`after` reference must be a `Period` or `Event` | provenance/unknown_faure/1 (Activity) | |
| `part_of` of a text must be `VisualItem` or `LinguisticObject` | text/koot_nightwatch/1 (HumanMadeObject) | |
| `Name.part` items with `id` | person/rembrandt/4 | name parts carry no id |
| `Right.applies_to`, `Right.dimension`, `Right.part` | provenance/2, /3, /4, /5 | not in the `Right` definition |
| Embedded actors without `id` | provenance/2 (`transferred_title_of`, `transferred_title_to`), /4, /5 (`possessed_by`), provenance/fixme/1 (`paid_from`, `paid_to`) | `id` required on references |
| `participant` on embedded provenance parts | provenance/coffin_promise/1, provenance/nuveen_commission/1 | embedded activities have no `participant` |
| `Transfer` as a provenance part | provenance/transfer/1 | no `Transfer` definition in the 1.0 schemas |
| `current_permanent_custodian` single object | object/nightwatch/17 | array |
| `held_or_supported_by` array | object/letter/3 | single object |
| `_label` missing on a HumanMadeObject | object/nightwatch/18 | required |
| `MonetaryAmount` (with `currency`) in `Set.dimension` | set/24477_16_objects/2 | items are `Dimension` |
| `Group.members_exemplified_by` | group/14thc_italian/1 | not in the `Group` definition |
| `created_by`, `subject_to` on an embedded statement | object/nightwatch/70 | embedded LinguisticObject allows neither |
| `id` on a Name inside a TimeSpan | object/nightwatch/43 | |
| `RightAcquisition` part without `transferred_title_of` | provenance/yuag_stora/1 | required |

## 4. Schema files

- `object.json` declares the `used_for` property twice (the second definition wins in most parsers; the
  SchemaInspector keeps the first and warns).

## 5. Text

- "Rembrant's" typo and the capitalised "Painting" label noted in the original audit (`docs/01-current-state.md`).

## 6. Labels on the vocabulary pages differ from the labels in the examples

The required/recommended vocabulary tables and the usage examples beneath them disagree on a few labels;
the library's registry uses the label the examples emit and notes the table's label:

| AAT | Table | Examples |
|---|---|---|
| 300451544 | Sort Name | Sorting Name |
| 300435599 | Promise Activity | Promise |
| 300404669 | Display Name | Display Title |
| 300418049 | Statement | Brief Text |
| 300054686 | Publication Activity | Publishing |
| 300263831 | Rectangular | rectangular |
| 300404628 | Auction Lot Number | Lot Number |
