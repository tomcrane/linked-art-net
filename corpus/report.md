# Corpus run

Generated 2026-10-17 11:23 UTC by `dotnet run --project tools/Corpus -- run --all --write` with parser 0.2.0. Verified entries gate CI; proposed and disputed ones are informative.

## verified: 716/716 pass (100.0%), calibration error 0.143

- authored/bce-and-deep-time.json: 20/20
- authored/canonical-forms.json: 18/18
- authored/centuries-and-bands.json: 30/30
- authored/encoding-and-noise.json: 8/8
- authored/lifecycle-and-floruit.json: 24/24
- authored/multi-part.json: 7/7
- authored/multilingual.json: 44/44
- authored/named-periods.json: 30/30
- authored/open-ended-and-relations.json: 36/36
- imported/timetwister.json: 8/8
- imported/yearrange.json: 362/362
- proposed/trainer-20261016-1334.json: 1/1
- seed/pmc-outputs.json: 91/91
- seed/py-datetime.json: 37/37

Status (expected->actual): parsed->parsed 635, partial->partial 2, undated->undated 73, unparsed->unparsed 6

## proposed: 1600/1600 pass (100.0%), calibration error 0.150

- imported/edtf-spec.json: 61/61
- imported/getty.json: 180/180
- imported/timetwister.json: 83/83
- imported/yearrange.json: 1276/1276

Status (expected->actual): parsed->parsed 1596, unparsed->unparsed 4

## disputed: 1/12 pass (8.3%), calibration error 0.621

- imported/yearrange.json: 1/12

Status (expected->actual): parsed->parsed 11, parsed->unparsed 1
Weakest tags: band 0/1, named-period 0/3, decade 0/2, century 0/2, yearrange 1/12, imported 1/12, bracketed 1/3, range 1/2, circa 1/2

Since the last run: 7 newly passing, 0 newly failing.

## Failures

By cause: not parsed 1, bounds differ 10.

### Not parsed (1)

The source has a date and the parser found none: grammar gaps, or strings the source read generously.

- `yr-0175` (disputed) "・07・25" -> Unparsed
  - expected: status parsed; years 1925..1925
  - status: expected parsed, got Unparsed
  - years.start: expected 1925, got (none)
  - years.end: expected 1925, got (none)

### Bounds differ (10)

The same reading realised to different datetimes: usually a band, season, circa or open-bound convention, or the source's own estimate.

- `yr-0153` (disputed) "Edo period, Late to early Meiji period" -> late period:edo/early period:meiji
  - expected: status parsed; years 1603..1868
  - years.start: expected 1603, got 1802
  - years.end: expected 1868, got 1880
- `yr-0173` (disputed) "1932 1980s)" -> 1932/198X
  - expected: status parsed; years 1980..1989
  - years.start: expected 1980, got 1932
- `yr-0521` (disputed) "19881" -> Y19881
  - expected: status parsed; years 1988..1988
  - years.start: expected 1988, got 19881
  - years.end: expected 1988, got 19881
- `yr-0537` (disputed) "Japanese, Modern impression from Edo-period block" -> not-before period:edo
  - expected: status parsed; years 1603..1868
  - years.end: expected 1868, got (none)
- `yr-1030` (disputed) "Originally in Edo era, this re-carved edition was made probably in11950s." -> period:edo
  - expected: status parsed; years 1950..1959
  - years.start: expected 1950, got 1603
  - years.end: expected 1959, got 1868
- `yr-1060` (disputed) "c. 18011" -> Y18011~
  - expected: status parsed; years 1801..1801; flags Approximate
  - years.start: expected 1801, got 18011
  - years.end: expected 1801, got 18011
- `yr-1236` (disputed) "1879 (Meiji 3, 2nd month)" -> period:meiji-3/1879
  - expected: status parsed; years 1879..1879
  - years.start: expected 1879, got 1870
- `yr-1244` (disputed) "1867 (Meiji 1, 6th month)" -> 1867/period:meiji-1
  - expected: status parsed; years 1867..1867
  - years.end: expected 1867, got 1868
- `yr-1595` (disputed) "Second century 17th century." -> century 2/century 17
  - expected: status parsed; years 1600..1699
  - years.start: expected 1600, got 100
- `yr-1625` (disputed) "1640 century." -> 1640
  - expected: status parsed; years 3900..3999
  - years.start: expected 3900, got 1640
  - years.end: expected 3999, got 1640

