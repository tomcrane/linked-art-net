# Corpus run

Generated 2026-10-17 11:12 UTC by `dotnet run --project tools/Corpus -- run --all --write` with parser 0.2.0. Verified entries gate CI; proposed and disputed ones are informative.

## verified: 702/702 pass (100.0%), calibration error 0.143

- authored/bce-and-deep-time.json: 20/20
- authored/canonical-forms.json: 18/18
- authored/centuries-and-bands.json: 30/30
- authored/encoding-and-noise.json: 8/8
- authored/lifecycle-and-floruit.json: 24/24
- authored/multilingual.json: 44/44
- authored/named-periods.json: 30/30
- authored/open-ended-and-relations.json: 36/36
- imported/timetwister.json: 8/8
- imported/yearrange.json: 355/355
- proposed/trainer-20261016-1334.json: 1/1
- seed/pmc-outputs.json: 91/91
- seed/py-datetime.json: 37/37

Status (expected->actual): parsed->parsed 621, partial->partial 2, undated->undated 73, unparsed->unparsed 6

## proposed: 1600/1600 pass (100.0%), calibration error 0.150

- imported/edtf-spec.json: 61/61
- imported/getty.json: 180/180
- imported/timetwister.json: 83/83
- imported/yearrange.json: 1276/1276

Status (expected->actual): parsed->parsed 1596, unparsed->unparsed 4

## disputed: 0/19 pass (0.0%), calibration error 0.684

- imported/yearrange.json: 0/19

Status (expected->actual): parsed->parsed 18, parsed->unparsed 1
Weakest tags: yearrange 0/19, imported 0/19, range 0/3, band 0/1, named-period 0/3, decade 0/7, circa 0/7, bracketed 0/5, month 0/1, season 0/1, century 0/3

Since the last run: 2 newly passing, 0 newly failing.

## Failures

By cause: not parsed 1, bounds differ 18.

### Not parsed (1)

The source has a date and the parser found none: grammar gaps, or strings the source read generously.

- `yr-0175` (disputed) "・07・25" -> Unparsed
  - expected: status parsed; years 1925..1925
  - status: expected parsed, got Unparsed
  - years.start: expected 1925, got (none)
  - years.end: expected 1925, got (none)

### Bounds differ (18)

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
- `yr-0553` (disputed) ""lamp" made in 1956. Others ca. 1970s." -> 1956
  - expected: status parsed; years 1970..1979; flags Approximate
  - years.start: expected 1970, got 1956
  - years.end: expected 1979, got 1956
  - flag Approximate missing (flags: Unconsumed)
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
- `yr-1253` (disputed) "(1) 1980, (2) 1977, (3) ca. 1970s." -> 1977/197X~
  - expected: status parsed; years 1970..1979; flags Approximate
  - years.start: expected 1970, got 1977
- `yr-1254` (disputed) "(1) and (2) ca. 1970s, (3) 1982." -> 197X~/1982
  - expected: status parsed; years 1970..1979; flags Approximate
  - years.end: expected 1979, got 1982
- `yr-1352` (disputed) "Sheet 1; 1964. Sheet 2 and 3; ca. 1930s." -> 1964
  - expected: status parsed; years 1930..1939; flags Approximate
  - years.start: expected 1930, got 1964
  - years.end: expected 1939, got 1964
  - flag Approximate missing (flags: Unconsumed)
- `yr-1430` (disputed) "Spring: July 2005; Summer: May - July 2003; Autumn: Dec 2003 - July 2004; Winter: Sept - Nov 2003." -> 2005-07
  - expected: status parsed; years 2003..2004
  - years.start: expected 2003, got 2005
  - years.end: expected 2004, got 2005
- `yr-1441` (disputed) "(1)1909. (2) Ca. 1900-10s." -> 1909
  - expected: status parsed; years 1900..1919; flags Approximate
  - years.start: expected 1900, got 1909
  - years.end: expected 1919, got 1909
  - flag Approximate missing (flags: Unconsumed)
- `yr-1478` (disputed) ""Flower" in 1972. "Lake in Forest" in 1969. "Volcano Lake" ca. 1960s." -> 1972
  - expected: status parsed; years 1960..1969; flags Approximate
  - years.start: expected 1960, got 1972
  - years.end: expected 1969, got 1972
  - flag Approximate missing (flags: Unconsumed)
- `yr-1595` (disputed) "Second century 17th century." -> century 2/century 17
  - expected: status parsed; years 1600..1699
  - years.start: expected 1600, got 100
- `yr-1618` (disputed) "Virgin, end of 5th century; Annunciation, 533-535; Saints, 705." -> end century 5
  - expected: status parsed; years 533..535
  - years.start: expected 533, got 475
  - years.end: expected 535, got 499
- `yr-1625` (disputed) "1640 century." -> 1640
  - expected: status parsed; years 3900..3999
  - years.start: expected 3900, got 1640
  - years.end: expected 3999, got 1640

