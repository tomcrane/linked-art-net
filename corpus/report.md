# Corpus run

Generated 2026-10-14 16:47 UTC by `dotnet run --project tools/Corpus -- run --all --write` with parser 0.2.0. Verified entries gate CI; proposed and disputed ones are informative.

## verified: 610/612 pass (99.7%), calibration error 0.127

- authored/bce-and-deep-time.json: 20/20
- authored/canonical-forms.json: 18/18
- authored/centuries-and-bands.json: 30/30
- authored/lifecycle-and-floruit.json: 24/24
- authored/multilingual.json: 44/44
- authored/named-periods.json: 30/30
- authored/open-ended-and-relations.json: 36/36
- imported/yearrange.json: 280/282
- seed/pmc-outputs.json: 91/91
- seed/py-datetime.json: 37/37

Status (expected->actual): parsed->parsed 596, partial->partial 2, undated->undated 8, unparsed->unparsed 6
Weakest tags: season 4/5, bracketed 82/84, yearrange 280/282, imported 280/282, range 166/167

## proposed: 1617/1698 pass (95.2%), calibration error 0.118

- imported/edtf-spec.json: 61/61
- imported/getty.json: 180/180
- imported/timetwister.json: 79/91
- imported/yearrange.json: 1297/1366

Status (expected->actual): parsed->parsed 1692, parsed->undated 1, parsed->unparsed 1, unparsed->unparsed 4
Weakest tags: single 26/37, season 24/30, timetwister 79/91, archival 79/91, bracketed 423/463, yearrange 1297/1366, imported 1617/1698, month 181/190, circa 230/241, decade 168/176, named-period 271/280, undated 42/43, range 434/444, century 305/311, band 275/280

## disputed: 0/2 pass (0.0%), calibration error 0.675

- imported/yearrange.json: 0/2

Status (expected->actual): parsed->parsed 2
Weakest tags: yearrange 0/2, imported 0/2, range 0/1, band 0/1, named-period 0/2

Since the last run: 3 newly passing, 44 newly failing: yr-0158, yr-0213, yr-0221, yr-0280, yr-0327, yr-0390, yr-0393, yr-0430, yr-0475, yr-0501, yr-0563, yr-0567, yr-0688, yr-0708, yr-0793, yr-0830, yr-0879, yr-0902, yr-0911, yr-0937, yr-0938, yr-0939, yr-0940, yr-0941, yr-0943, yr-0944, yr-0945, yr-0946, yr-0955, yr-0957, yr-0970, yr-0978, yr-1096, yr-1208, yr-1220, yr-1227, yr-1241, yr-1242, yr-1247, yr-1289, yr-1292, yr-1366, yr-1427, yr-1439

## Failures

By cause: not parsed 2, reading differs 2, bounds differ 81.

### Not parsed (2)

The source has a date and the parser found none: grammar gaps, or strings the source read generously.

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

### Reading differs (2)

A different canonical form or kind for the same string: a parser bug, or a convention the profile should set.

- `yr-0937` (verified) "1800-1900 (Bunsei 10, autumn (1827))" -> 1827-23/1900
  - expected: status parsed; kind single; canonical 1827-23; edtf 1827-23; timespan 1827-09-01T00:00:00Z..1827-11-30T23:59:59Z
  - canonical: expected '1827-23', got '1827-23/1900'
  - edtf: expected '1827-23', got '1827-23/1900'
  - timespan.end_of_the_end: expected 1827-11-30T23:59:59Z, got 1900-12-31T23:59:59Z
- `yr-1096` (verified) "Originally compiled and published by Hoitsu in 1815 (Vol.1) and 1826 (Vol.2)." -> 1815/1902
  - expected: status parsed; kind single; canonical 1815/1826; edtf 1815/1826; timespan 1815-01-01T00:00:00Z..1826-12-31T23:59:59Z
  - canonical: expected '1815/1826', got '1815/1902'
  - edtf: expected '1815/1826', got '1815/1902'
  - timespan.end_of_the_end: expected 1826-12-31T23:59:59Z, got 1902-12-31T23:59:59Z

### Bounds differ (81)

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
- `yr-0153` (disputed) "Edo period, Late to early Meiji period" -> late period:edo/early period:meiji
  - expected: status parsed; years 1603..1868
  - years.start: expected 1603, got 1802
  - years.end: expected 1868, got 1880
- `yr-0158` (proposed) "寛政6年（1794）" -> 1794/1906
  - expected: status parsed; years 1794..1794
  - years.end: expected 1794, got 1906
- `yr-0173` (proposed) "1932 1980s)" -> 1932/198X
  - expected: status parsed; years 1980..1989
  - years.start: expected 1980, got 1932
- `yr-0175` (proposed) "・07・25" -> 1907
  - expected: status parsed; years 1925..1925
  - years.start: expected 1925, got 1907
  - years.end: expected 1925, got 1907
- `yr-0213` (proposed) "1814 (Bunka 11)" -> 1814/1911
  - expected: status parsed; years 1814..1814
  - years.end: expected 1814, got 1911
- `yr-0221` (proposed) "1852 (12 month)" -> 1852/1912
  - expected: status parsed; years 1852..1852
  - years.end: expected 1852, got 1912
- `yr-0280` (proposed) "18. to 19. century" -> 1918/century 19
  - expected: status parsed; years 1800..1899
  - years.end: expected 1899, got 1918
- `yr-0327` (proposed) "1770 (Meiwa 7)" -> 1770/1907
  - expected: status parsed; years 1770..1770
  - years.end: expected 1770, got 1907
- `yr-0390` (proposed) "1828 (Bunsei 5)" -> 1828/1905
  - expected: status parsed; years 1828..1828
  - years.end: expected 1828, got 1905
- `yr-0393` (proposed) "1783, 84" -> 1783/1984
  - expected: status parsed; years 1783..1783
  - years.end: expected 1783, got 1984
- `yr-0430` (proposed) "Originally 1834, 35. This one was later printing probably in 1860s." -> 1834/186X
  - expected: status parsed; years 1860..1869
  - years.start: expected 1860, got 1834
- `yr-0475` (proposed) "1794 (Kansei 6)" -> 1794/1906
  - expected: status parsed; years 1794..1794
  - years.end: expected 1794, got 1906
- `yr-0501` (proposed) "3/7, 1985" -> 1903/1985
  - expected: status parsed; years 1985..1985
  - years.start: expected 1985, got 1903
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
- `yr-0563` (proposed) "1840 (Tenpo 11)" -> 1840/1911
  - expected: status parsed; years 1840..1840
  - years.end: expected 1840, got 1911
- `yr-0567` (proposed) "1861 (Bunkyu 1, 2nd month)" -> 1861/1901
  - expected: status parsed; years 1861..1861
  - years.end: expected 1861, got 1901
- `yr-0627` (proposed) "161/1000, 1983" -> 1983
  - expected: status parsed; years 1000..1000
  - years.start: expected 1000, got 1983
  - years.end: expected 1000, got 1983
- `yr-0688` (proposed) "安政2年（1855）" -> 1855/1902
  - expected: status parsed; years 1855..1855
  - years.end: expected 1855, got 1902
- `yr-0689` (proposed) "江戸時代＿18-19ｃ" -> century 18/period:edo
  - expected: status parsed; years 1800..1899
  - years.start: expected 1800, got 1700
  - years.end: expected 1899, got 1868
- `yr-0708` (proposed) "Ca. 1833 (Tenpo 4)." -> 1833~/1904
  - expected: status parsed; years 1833..1833; flags Approximate
  - years.end: expected 1833, got 1904
- `yr-0776` (proposed) "江戸時代＿17･18c" -> 1917
  - expected: status parsed; years 1700..1799
  - years.start: expected 1700, got 1917
  - years.end: expected 1799, got 1917
- `yr-0793` (proposed) "1862 (Bunkyu 2, 6th month)" -> 1862/1902
  - expected: status parsed; years 1862..1862
  - years.end: expected 1862, got 1902
- `yr-0830` (proposed) "(1) Ca. 1890. (2) 1883." -> 1890~/1902
  - expected: status parsed; years 1890..1890; flags Approximate
  - years.end: expected 1890, got 1902
- `yr-0879` (proposed) "明治19年(1886)" -> 1886/1919
  - expected: status parsed; years 1886..1886
  - years.end: expected 1886, got 1919
- `yr-0902` (proposed) "1833 (Tenpo 4)" -> 1833/1904
  - expected: status parsed; years 1833..1833
  - years.end: expected 1833, got 1904
- `yr-0911` (proposed) "c. 1783, 84" -> 1783~/1984
  - expected: status parsed; years 1783..1783; flags Approximate
  - years.end: expected 1783, got 1984
- `yr-0938` (proposed) "1801-1900 (Bunsei 10, autumn)" -> 1801/1910
  - expected: status parsed; years 1801..1900
  - years.end: expected 1900, got 1910
- `yr-0939` (proposed) "1827 (Bunsei 10, autumn)" -> 1827/1910
  - expected: status parsed; years 1827..1827
  - years.end: expected 1827, got 1910
- `yr-0940` (proposed) "1838 (Tempo 9)" -> 1838/1909
  - expected: status parsed; years 1838..1838
  - years.end: expected 1838, got 1909
- `yr-0941` (proposed) "1852 (Kaei 5, spring)" -> 1852/1905
  - expected: status parsed; years 1852..1852
  - years.end: expected 1852, got 1905
- `yr-0943` (proposed) "1826 (Bunsei 9, spring)" -> 1826/1909
  - expected: status parsed; years 1826..1826
  - years.end: expected 1826, got 1909
- `yr-0944` (proposed) "1828 (Bunsei 11)" -> 1828/1911
  - expected: status parsed; years 1828..1828
  - years.end: expected 1828, got 1911
- `yr-0945` (proposed) "1844 (Tempo 15, Spring)" -> 1844/1915
  - expected: status parsed; years 1844..1844
  - years.end: expected 1844, got 1915
- `yr-0946` (proposed) "1827 (Bunsei 10, New Year)" -> 1827/1910
  - expected: status parsed; years 1827..1827
  - years.end: expected 1827, got 1910
- `yr-0955` (proposed) "1834 (vol.1)" -> 1834/1901
  - expected: status parsed; years 1834..1834
  - years.end: expected 1834, got 1901
- `yr-0957` (proposed) "1807 (1 vol.)" -> 1807/1901
  - expected: status parsed; years 1807..1807
  - years.end: expected 1807, got 1901
- `yr-0970` (proposed) "1684 (Empo 5)" -> 1684/1905
  - expected: status parsed; years 1684..1684
  - years.end: expected 1684, got 1905
- `yr-0978` (proposed) "1849 (Kaei 2)" -> 1849/1902
  - expected: status parsed; years 1849..1849
  - years.end: expected 1849, got 1902
- `yr-1030` (proposed) "Originally in Edo era, this re-carved edition was made probably in11950s." -> period:edo
  - expected: status parsed; years 1950..1959
  - years.start: expected 1950, got 1603
  - years.end: expected 1959, got 1868
- `yr-1044` (proposed) "late 1750âs - early 1760âs" -> late 175X
  - expected: status parsed; years 1757..1762
  - years.end: expected 1762, got 1759
- `yr-1060` (proposed) "c. 18011" -> Y18011~
  - expected: status parsed; years 1801..1801; flags Approximate
  - years.start: expected 1801, got 18011
  - years.end: expected 1801, got 18011
- `yr-1062` (proposed) "1730âs early" -> 1730
  - expected: status parsed; years 1730..1732
  - years.end: expected 1732, got 1730
- `yr-1170` (proposed) "1843-45 (Kunisada changed his name to Toyokuni in 1844)." -> 1844
  - expected: status parsed; years 1843..1845
  - years.start: expected 1843, got 1844
  - years.end: expected 1845, got 1844
- `yr-1206` (proposed) "Originally in early Edo era, this edition was made in unknown date." -> early period:edo
  - expected: status parsed; years 1603..1868
  - years.end: expected 1868, got 1668
- `yr-1208` (proposed) "1861 (Bunkyu 1, first month)" -> 1861/1901
  - expected: status parsed; years 1861..1861
  - years.end: expected 1861, got 1901
- `yr-1220` (proposed) "1866 (Keio 2, 2nd month)" -> 1866/1902
  - expected: status parsed; years 1866..1866
  - years.end: expected 1866, got 1902
- `yr-1227` (proposed) "1885 (Meii 18)" -> 1885/1918
  - expected: status parsed; years 1885..1885
  - years.end: expected 1885, got 1918
- `yr-1228` (proposed) "Originally in late Edo era, this re-carved edition was made in unknown date." -> late period:edo
  - expected: status parsed; years 1603..1868
  - years.start: expected 1603, got 1802
- `yr-1236` (proposed) "1879 (Meiji 3, 2nd month)" -> period:meiji-3/1879
  - expected: status parsed; years 1879..1879
  - years.start: expected 1879, got 1870
- `yr-1241` (proposed) "1863 (Bunkyu 3)" -> 1863/1903
  - expected: status parsed; years 1863..1863
  - years.end: expected 1863, got 1903
- `yr-1242` (proposed) "1863, 7th month (Bunkyu 3)" -> 1863/1903
  - expected: status parsed; years 1863..1863
  - years.end: expected 1863, got 1903
- `yr-1244` (proposed) "1867 (Meiji 1, 6th month)" -> 1867/period:meiji-1
  - expected: status parsed; years 1867..1867
  - years.end: expected 1867, got 1868
- `yr-1247` (proposed) "1861 (Bunkyu 1, 1st month)" -> 1861/1901
  - expected: status parsed; years 1861..1861
  - years.end: expected 1861, got 1901
- `yr-1253` (proposed) "(1) 1980, (2) 1977, (3) ca. 1970s." -> 197X/1980
  - expected: status parsed; years 1970..1979; flags Approximate
  - years.end: expected 1979, got 1980
  - flag Approximate missing (flags: Salvaged, Unconsumed)
- `yr-1254` (proposed) "(1) and (2) ca. 1970s, (3) 1982." -> 1902/1903
  - expected: status parsed; years 1970..1979; flags Approximate
  - years.start: expected 1970, got 1902
  - years.end: expected 1979, got 1903
  - flag Approximate missing (flags: TwoDigitYear, SecondaryDate)
- `yr-1289` (proposed) "1863 (Bunkyo 3, 4th month)" -> 1863/1903
  - expected: status parsed; years 1863..1863
  - years.end: expected 1863, got 1903
- `yr-1292` (proposed) "dated 1823 (Bunsei 6)" -> 1823/1906
  - expected: status parsed; years 1823..1823
  - years.end: expected 1823, got 1906
- `yr-1352` (proposed) "Sheet 1; 1964. Sheet 2 and 3; ca. 1930s." -> 1901
  - expected: status parsed; years 1930..1939; flags Approximate
  - years.start: expected 1930, got 1901
  - years.end: expected 1939, got 1901
  - flag Approximate missing (flags: TwoDigitYear, Unconsumed)
- `yr-1366` (proposed) "Edition 20, 1984" -> 1920/1984
  - expected: status parsed; years 1984..1984
  - years.start: expected 1984, got 1920
- `yr-1427` (proposed) "寛政6年(1794)" -> 1794/1906
  - expected: status parsed; years 1794..1794
  - years.end: expected 1794, got 1906
- `yr-1430` (proposed) "Spring: July 2005; Summer: May - July 2003; Autumn: Dec 2003 - July 2004; Winter: Sept - Nov 2003." -> 2005-07
  - expected: status parsed; years 2003..2004
  - years.start: expected 2003, got 2005
  - years.end: expected 2004, got 2005
- `yr-1439` (proposed) "(1) 1980. (2) 1982." -> 1902
  - expected: status parsed; years 1980..1980
  - years.start: expected 1980, got 1902
  - years.end: expected 1980, got 1902
- `yr-1441` (proposed) "(1)1909. (2) Ca. 1900-10s." -> 1902
  - expected: status parsed; years 1900..1919; flags Approximate
  - years.start: expected 1900, got 1902
  - years.end: expected 1919, got 1902
  - flag Approximate missing (flags: TwoDigitYear, SecondaryDate, Unconsumed)
- `yr-1478` (proposed) ""Flower" in 1972. "Lake in Forest" in 1969. "Volcano Lake" ca. 1960s." -> 1972
  - expected: status parsed; years 1960..1969; flags Approximate
  - years.start: expected 1960, got 1972
  - years.end: expected 1969, got 1972
  - flag Approximate missing (flags: Unconsumed)
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

