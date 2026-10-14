# Corpus run

Generated 2026-10-14 16:14 UTC by `dotnet run --project tools/Corpus -- run --all --write` with parser 0.2.0. Verified entries gate CI; proposed and disputed ones are informative.

## verified: 572/572 pass (100.0%), calibration error 0.127

- authored/bce-and-deep-time.json: 20/20
- authored/canonical-forms.json: 18/18
- authored/centuries-and-bands.json: 30/30
- authored/lifecycle-and-floruit.json: 24/24
- authored/multilingual.json: 44/44
- authored/named-periods.json: 30/30
- authored/open-ended-and-relations.json: 36/36
- imported/yearrange.json: 242/242
- seed/pmc-outputs.json: 91/91
- seed/py-datetime.json: 37/37

Status (expected->actual): parsed->parsed 556, partial->partial 2, undated->undated 8, unparsed->unparsed 6

## proposed: 1656/1738 pass (95.3%), calibration error 0.116

- imported/edtf-spec.json: 61/61
- imported/getty.json: 180/180
- imported/timetwister.json: 79/91
- imported/yearrange.json: 1336/1406

Status (expected->actual): parsed->parsed 1731, parsed->undated 1, parsed->unparsed 2, unparsed->unparsed 4
Weakest tags: single 26/37, timetwister 79/91, archival 79/91, season 29/32, circa 233/252, named-period 275/297, decade 169/180, band 275/292, month 181/192, yearrange 1336/1406, imported 1656/1738, range 434/453, uncertain 50/52, bracketed 457/474, century 306/317

## disputed: 0/2 pass (0.0%), calibration error 0.675

- imported/yearrange.json: 0/2

Status (expected->actual): parsed->parsed 2
Weakest tags: yearrange 0/2, imported 0/2, range 0/1, band 0/1, named-period 0/2

Since the last run: 27 newly passing, 0 newly failing.

## Failures

By cause: not parsed 3, bounds differ 77, flags only 4.

### Not parsed (3)

The source has a date and the parser found none: grammar gaps, or strings the source read generously.

- `yr-0175` (proposed) "・07・25" -> Unparsed
  - expected: status parsed; years 1925..1925
  - status: expected parsed, got Unparsed
  - years.start: expected 1925, got (none)
  - years.end: expected 1925, got (none)
- `yr-0363` (proposed) "18690000" -> Unparsed
  - expected: status parsed; years 1869..1869
  - status: expected parsed, got Unparsed
  - years.start: expected 1869, got (none)
  - years.end: expected 1869, got (none)
- `yr-1458` (proposed) "730/1000, not dated" -> undated
  - expected: status parsed; years 1000..1000
  - status: expected parsed, got Undated
  - years.start: expected 1000, got (none)
  - years.end: expected 1000, got (none)

### Bounds differ (77)

The same reading realised to different datetimes: usually a band, season, circa or open-bound convention, or the source's own estimate.

- `tt-074` (proposed) "1776, 1789, 1812" -> 1776/1812
  - expected: status parsed; timespan 1776-01-01T00:00:00Z..1776-12-31T23:59:59Z
  - timespan.end_of_the_end: expected 1776-12-31T23:59:59Z, got 1812-12-31T23:59:59Z
- `tt-076` (proposed) "July 1776 - August 1789, 1812 - 1820, 1900" -> 1776-07/1900
  - expected: status parsed; timespan 1776-07-01T00:00:00Z..1789-08-31T23:59:59Z
  - timespan.end_of_the_end: expected 1789-08-31T23:59:59Z, got 1900-12-31T23:59:59Z
- `tt-078` (proposed) "July twenty-first 1776" -> 1776
  - expected: status parsed; timespan 1776-07-21T00:00:00Z..1776-07-21T23:59:59Z
  - timespan.begin_of_the_begin: expected 1776-07-21T00:00:00Z, got 1776-01-01T00:00:00Z
  - timespan.end_of_the_end: expected 1776-07-21T23:59:59Z, got 1776-12-31T23:59:59Z
- `tt-079` (proposed) "July twenty-first, 1776" -> 1776
  - expected: status parsed; timespan 1776-07-21T00:00:00Z..1776-07-21T23:59:59Z
  - timespan.begin_of_the_begin: expected 1776-07-21T00:00:00Z, got 1776-01-01T00:00:00Z
  - timespan.end_of_the_end: expected 1776-07-21T23:59:59Z, got 1776-12-31T23:59:59Z
- `tt-083` (proposed) "août 4 1776" -> 1776
  - expected: status parsed; timespan 1776-08-04T00:00:00Z..1776-08-04T23:59:59Z
  - timespan.begin_of_the_begin: expected 1776-08-04T00:00:00Z, got 1776-01-01T00:00:00Z
  - timespan.end_of_the_end: expected 1776-08-04T23:59:59Z, got 1776-12-31T23:59:59Z
- `tt-084` (proposed) "agosto 4 1776" -> 1776
  - expected: status parsed; timespan 1776-08-04T00:00:00Z..1776-08-04T23:59:59Z
  - timespan.begin_of_the_begin: expected 1776-08-04T00:00:00Z, got 1776-01-01T00:00:00Z
  - timespan.end_of_the_end: expected 1776-08-04T23:59:59Z, got 1776-12-31T23:59:59Z
- `tt-085` (proposed) "july 4 and 5 1776" -> 1776-07-04/1776-07-05
  - expected: status parsed; timespan 1776-07-04T00:00:00Z..1776-07-04T23:59:59Z
  - timespan.end_of_the_end: expected 1776-07-04T23:59:59Z, got 1776-07-05T23:59:59Z
- `tt-086` (proposed) "4 & 5 july 1776" -> 1776-07-04/1776-07-05
  - expected: status parsed; timespan 1776-07-04T00:00:00Z..1776-07-04T23:59:59Z
  - timespan.end_of_the_end: expected 1776-07-04T23:59:59Z, got 1776-07-05T23:59:59Z
- `tt-087` (proposed) "1776 july 4 and 5" -> 1776-07-04/1776-07-05
  - expected: status parsed; timespan 1776-07-04T00:00:00Z..1776-07-04T23:59:59Z
  - timespan.end_of_the_end: expected 1776-07-04T23:59:59Z, got 1776-07-05T23:59:59Z
- `tt-088` (proposed) "july and august 1776" -> 1776-07/1776-08
  - expected: status parsed; timespan 1776-07-01T00:00:00Z..1776-07-31T23:59:59Z
  - timespan.end_of_the_end: expected 1776-07-31T23:59:59Z, got 1776-08-31T23:59:59Z
- `tt-089` (proposed) "1776 july and august" -> 1776-07/1776-08
  - expected: status parsed; timespan 1776-07-01T00:00:00Z..1776-07-31T23:59:59Z
  - timespan.end_of_the_end: expected 1776-07-31T23:59:59Z, got 1776-08-31T23:59:59Z
- `tt-090` (proposed) "ca. mid 19th century" -> mid century 19~
  - expected: status parsed; timespan 1830-01-01T00:00:00Z..1880-12-31T23:59:59Z
  - timespan.begin_of_the_begin: expected 1830-01-01T00:00:00Z, got 1834-01-01T00:00:00Z
  - timespan.end_of_the_end: expected 1880-12-31T23:59:59Z, got 1866-12-31T23:59:59Z
- `yr-0034` (proposed) "62/200, 69-10" -> 0200
  - expected: status parsed; years 1969..1969
  - years.start: expected 1969, got 200
  - years.end: expected 1969, got 200
- `yr-0035` (proposed) "72/127, 2005" -> 0127/2005
  - expected: status parsed; years 2005..2005
  - years.start: expected 2005, got 127
- `yr-0087` (proposed) "1936 or earlier." -> not-after 1936
  - expected: status parsed; years 1936..1936
  - years.start: expected 1936, got (none)
- `yr-0138` (proposed) "江戸時代＿19ｃ" -> century 19/period:edo
  - expected: status parsed; years 1800..1899
  - years.end: expected 1899, got 1868
- `yr-0153` (disputed) "Edo period, Late to early Meiji period" -> late period:edo/early period:meiji
  - expected: status parsed; years 1603..1868
  - years.start: expected 1603, got 1802
  - years.end: expected 1868, got 1880
- `yr-0168` (proposed) "1847 (1852)" -> 1847/1852
  - expected: status parsed; years 1847..1847
  - years.end: expected 1847, got 1852
- `yr-0173` (proposed) "1932 1980s)" -> 1932/198X
  - expected: status parsed; years 1980..1989
  - years.start: expected 1980, got 1932
- `yr-0228` (proposed) "Japanese, Edo period–Meiji era" -> period:edo/period:meiji
  - expected: status parsed; years 1603..1868
  - years.end: expected 1868, got 1912
- `yr-0286` (proposed) "Ca. 1936 or earlier." -> not-after 1936~
  - expected: status parsed; years 1936..1936; flags Approximate
  - years.start: expected 1936, got (none)
- `yr-0299` (proposed) "5/150, 69-12" -> 0150
  - expected: status parsed; years 1969..1969
  - years.start: expected 1969, got 150
  - years.end: expected 1969, got 150
- `yr-0301` (proposed) "Japanese, Meiji era, late 19th to early 20th century" -> late century 19/period:meiji
  - expected: status parsed; years 1875..1924
  - years.end: expected 1924, got 1912
- `yr-0504` (proposed) "1806 or early 1801-02" -> [1806,early 1801/1802]
  - expected: status parsed; years 1801..1802
  - years.end: expected 1802, got 1806
- `yr-0508` (proposed) "Meiji/early 20th century" -> period:meiji/early century 20
  - expected: status parsed; years 1900..1924
  - years.start: expected 1900, got 1868
- `yr-0514` (proposed) "Late Edo - Meiji period." -> late period:edo/period:meiji
  - expected: status parsed; years 1603..1868
  - years.start: expected 1603, got 1802
  - years.end: expected 1868, got 1912
- `yr-0521` (proposed) "19881" -> Y19881
  - expected: status parsed; years 1988..1988
  - years.start: expected 1988, got 19881
  - years.end: expected 1988, got 19881
- `yr-0537` (disputed) "Japanese, Modern impression from Edo-period block" -> not-before period:edo
  - expected: status parsed; years 1603..1868
  - years.end: expected 1868, got (none)
- `yr-0553` (proposed) ""lamp" made in 1956. Others ca. 1970s." -> 1956
  - expected: status parsed; years 1970..1979; flags Approximate
  - years.start: expected 1970, got 1956
  - years.end: expected 1979, got 1956
  - flag Approximate missing (flags: Unconsumed)
- `yr-0558` (proposed) "Original series in ca. 1834. This is re-carved edition in March 1951." -> 1951-03
  - expected: status parsed; years 1834..1834; flags Approximate
  - years.start: expected 1834, got 1951
  - years.end: expected 1834, got 1951
  - flag Approximate missing (flags: SecondaryDate)
- `yr-0569` (proposed) "December 1743 or January 1744" -> [1743-12,1744-01]
  - expected: status parsed; years 1743..1743
  - years.end: expected 1743, got 1744
- `yr-0627` (proposed) "161/1000, 1983" -> 1983
  - expected: status parsed; years 1000..1000
  - years.start: expected 1000, got 1983
  - years.end: expected 1000, got 1983
- `yr-0673` (proposed) "1936 (early post-1945 printing)" -> after 1945
  - expected: status parsed; years 1936..1936
  - years.start: expected 1936, got 1946
  - years.end: expected 1936, got (none)
- `yr-0675` (proposed) "winter 1883" -> 1883-24
  - expected: status parsed; years 1883..1883
  - years.end: expected 1883, got 1884
- `yr-0689` (proposed) "江戸時代＿18-19ｃ" -> century 18/period:edo
  - expected: status parsed; years 1800..1899
  - years.start: expected 1800, got 1700
  - years.end: expected 1899, got 1868
- `yr-0706` (proposed) "Late Meiji Period" -> late period:meiji
  - expected: status parsed; years 1868..1912
  - years.start: expected 1868, got 1899
- `yr-0711` (proposed) "Meiji/Taisho Period" -> period:meiji/period:taisho
  - expected: status parsed; years 1868..1912
  - years.end: expected 1912, got 1926
- `yr-0712` (proposed) "Late Meiji to Taisho Period" -> late period:meiji/period:taisho
  - expected: status parsed; years 1868..1912
  - years.start: expected 1868, got 1899
  - years.end: expected 1912, got 1926
- `yr-0776` (proposed) "江戸時代＿17･18c" -> period:edo
  - expected: status parsed; years 1700..1799
  - years.start: expected 1700, got 1603
  - years.end: expected 1799, got 1868
- `yr-0883` (proposed) "17thC-18thC (1747)" -> 1747
  - expected: status parsed; years 1600..1699
  - years.start: expected 1600, got 1747
  - years.end: expected 1699, got 1747
- `yr-0899` (proposed) "Edo period (1615-1868), c. 1705-1715" -> 1705~/1715
  - expected: status parsed; years 1615..1868; flags Approximate
  - years.start: expected 1615, got 1705
  - years.end: expected 1868, got 1715
- `yr-0915` (proposed) "Probably in late Meiji period." -> late period:meiji?
  - expected: status parsed; years 1868..1912
  - years.start: expected 1868, got 1899
- `yr-0921` (proposed) "c. 1780s (1782?)" -> 1782?
  - expected: status parsed; years 1780..1789; flags Approximate
  - years.start: expected 1780, got 1782
  - years.end: expected 1789, got 1782
  - flag Approximate missing (flags: Uncertain)
- `yr-0937` (proposed) "1800-1900 (Bunsei 10, autumn (1827))" -> 1827-23
  - expected: status parsed; years 1800..1900
  - years.start: expected 1800, got 1827
  - years.end: expected 1900, got 1827
- `yr-1022` (proposed) "Heisei period, 1989-present" -> 1989/..
  - expected: status parsed; years 1989..2024
  - years.end: expected 2024, got (none)
- `yr-1030` (proposed) "Originally in Edo era, this re-carved edition was made probably in11950s." -> period:edo
  - expected: status parsed; years 1950..1959
  - years.start: expected 1950, got 1603
  - years.end: expected 1959, got 1868
- `yr-1044` (proposed) "late 1750âs - early 1760âs" -> late 175X
  - expected: status parsed; years 1757..1762
  - years.end: expected 1762, got 1759
- `yr-1047` (proposed) "1680s or 1690s" -> [168X,169X]
  - expected: status parsed; years 1680..1689
  - years.end: expected 1689, got 1699
- `yr-1060` (proposed) "c. 18011" -> Y18011~
  - expected: status parsed; years 1801..1801; flags Approximate
  - years.start: expected 1801, got 18011
  - years.end: expected 1801, got 18011
- `yr-1062` (proposed) "1730âs early" -> 1730
  - expected: status parsed; years 1730..1732
  - years.end: expected 1732, got 1730
- `yr-1082` (proposed) "c. 1810/20 (Meiji Facsimilie)" -> 1810~/period:meiji
  - expected: status parsed; years 1810..1820; flags Approximate
  - years.end: expected 1820, got 1912
- `yr-1170` (proposed) "1843-45 (Kunisada changed his name to Toyokuni in 1844)." -> 1844
  - expected: status parsed; years 1843..1845
  - years.start: expected 1843, got 1844
  - years.end: expected 1845, got 1844
- `yr-1206` (proposed) "Originally in early Edo era, this edition was made in unknown date." -> early period:edo
  - expected: status parsed; years 1603..1868
  - years.end: expected 1868, got 1668
- `yr-1228` (proposed) "Originally in late Edo era, this re-carved edition was made in unknown date." -> late period:edo
  - expected: status parsed; years 1603..1868
  - years.start: expected 1603, got 1802
- `yr-1230` (proposed) "ca. 1910 or later" -> not-before 1910~
  - expected: status parsed; years 1910..1910; flags Approximate
  - years.end: expected 1910, got (none)
- `yr-1236` (proposed) "1879 (Meiji 3, 2nd month)" -> period:meiji-3/1879
  - expected: status parsed; years 1879..1879
  - years.start: expected 1879, got 1870
- `yr-1244` (proposed) "1867 (Meiji 1, 6th month)" -> 1867/period:meiji-1
  - expected: status parsed; years 1867..1867
  - years.end: expected 1867, got 1868
- `yr-1253` (proposed) "(1) 1980, (2) 1977, (3) ca. 1970s." -> 1977/197X~
  - expected: status parsed; years 1970..1979; flags Approximate
  - years.start: expected 1970, got 1977
- `yr-1254` (proposed) "(1) and (2) ca. 1970s, (3) 1982." -> 197X~/1982
  - expected: status parsed; years 1970..1979; flags Approximate
  - years.end: expected 1979, got 1982
- `yr-1277` (proposed) "late 1777 or early in 1778" -> [late 1777,early 1778]
  - expected: status parsed; years 1777..1777
  - years.end: expected 1777, got 1778
- `yr-1321` (proposed) "1842 or 1854 or 1866" -> [1842,1854,1866]
  - expected: status parsed; years 1842..1854
  - years.end: expected 1854, got 1866
- `yr-1322` (proposed) "Early Meiji." -> early period:meiji
  - expected: status parsed; years 1868..1912
  - years.end: expected 1912, got 1880
- `yr-1352` (proposed) "Sheet 1; 1964. Sheet 2 and 3; ca. 1930s." -> 1964
  - expected: status parsed; years 1930..1939; flags Approximate
  - years.start: expected 1930, got 1964
  - years.end: expected 1939, got 1964
  - flag Approximate missing (flags: Unconsumed)
- `yr-1353` (proposed) "c. early 1800s (this item: Taisho-era strike)" -> period:taisho
  - expected: status parsed; years 1800..1824
  - years.start: expected 1800, got 1912
  - years.end: expected 1824, got 1926
- `yr-1354` (proposed) "Ca. later Edo era." -> late period:edo~
  - expected: status parsed; years 1603..1868; flags Approximate
  - years.start: expected 1603, got 1802
- `yr-1430` (proposed) "Spring: July 2005; Summer: May - July 2003; Autumn: Dec 2003 - July 2004; Winter: Sept - Nov 2003." -> 2005-07
  - expected: status parsed; years 2003..2004
  - years.start: expected 2003, got 2005
  - years.end: expected 2004, got 2005
- `yr-1441` (proposed) "(1)1909. (2) Ca. 1900-10s." -> 1909
  - expected: status parsed; years 1900..1919; flags Approximate
  - years.start: expected 1900, got 1909
  - years.end: expected 1919, got 1909
  - flag Approximate missing (flags: Unconsumed)
- `yr-1478` (proposed) ""Flower" in 1972. "Lake in Forest" in 1969. "Volcano Lake" ca. 1960s." -> 1972
  - expected: status parsed; years 1960..1969; flags Approximate
  - years.start: expected 1960, got 1972
  - years.end: expected 1969, got 1972
  - flag Approximate missing (flags: Unconsumed)
- `yr-1512` (proposed) "Japanese, Taishô era–Shôwa era" -> period:taisho/period:showa
  - expected: status parsed; years 1926..1989
  - years.start: expected 1926, got 1912
- `yr-1529` (proposed) "Japanese, Meiji era, Meiji 27?" -> period:meiji-27?
  - expected: status parsed; years 1868..1912
  - years.start: expected 1868, got 1894
  - years.end: expected 1912, got 1894
- `yr-1540` (proposed) "Japanese, Meiji era, 19th–early 20th century" -> period:meiji
  - expected: status parsed; years 1800..1924
  - years.start: expected 1800, got 1868
  - years.end: expected 1924, got 1912
- `yr-1542` (proposed) "Japanese, Edo period, 17th–18th century" -> period:edo/century 18
  - expected: status parsed; years 1600..1799
  - years.start: expected 1600, got 1603
- `yr-1543` (proposed) "Japanese, Meiji era–Taishô era" -> period:meiji/period:taisho
  - expected: status parsed; years 1868..1912
  - years.end: expected 1912, got 1926
- `yr-1595` (proposed) "Second century 17th century." -> century 2/century 17
  - expected: status parsed; years 1600..1699
  - years.start: expected 1600, got 100
- `yr-1618` (proposed) "Virgin, end of 5th century; Annunciation, 533-535; Saints, 705." -> end century 5
  - expected: status parsed; years 533..535
  - years.start: expected 533, got 475
  - years.end: expected 535, got 499
- `yr-1625` (proposed) "1640 century." -> 1640
  - expected: status parsed; years 3900..3999
  - years.start: expected 3900, got 1640
  - years.end: expected 3999, got 1640
- `yr-1633` (proposed) "After 1580, probably 1584." -> 1584?
  - expected: status parsed; years 1580..1580
  - years.start: expected 1580, got 1584
  - years.end: expected 1580, got 1584

### Flags only (4)

The bounds agree and only a hedge flag differs: usually how the importer mapped the source's flag.

- `yr-0541` (proposed) "1795 (c. 11th month)" -> 1795
  - expected: status parsed; years 1795..1795; flags Approximate
  - flag Approximate missing (flags: )
- `yr-1006` (proposed) "1788 (artist;circa)" -> 1788
  - expected: status parsed; years 1788..1788; flags Approximate
  - flag Approximate missing (flags: )
- `yr-1336` (proposed) "c. 1953 (this item: 1970s/80s)" -> 197X/198X
  - expected: status parsed; years 1970..1989; flags Approximate
  - flag Approximate missing (flags: SecondaryDate)
- `yr-1620` (proposed) "18th century. Copy of a mosaic of c.800." -> century 18
  - expected: status parsed; years 1700..1799; flags Approximate
  - flag Approximate missing (flags: Unconsumed)

