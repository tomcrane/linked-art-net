<!-- corpus-review -->
## Corpus review

Parser 0.2.0. 1 file changed, 38 entries: 0 pass, 38 fail. Only `verified` entries gate CI; `proposed` and `disputed` ones are informative.

### `corpus/imported/yearrange.json`

jeresig/node-yearrange test data (MIT): 38 entries (38 proposed), 0 pass, 38 fail.
> The strings are museum catalogue dates (largely Japanese print collections). The source knows only start and end years and a circa flag, so expectations are coarse (years) and reflect yearrange's own conventions, e.g. 'late' = the last quarter of a century, 'mid' = the middle fifth, and 'circa' does not widen (the catalogue-quarters profile). yearrange's circa flag also covered 'probably' and '?', which we flag Uncertain, so the Approximate flag is expected only where the text has circa wording (2026-10-14). Where an original and a later edition are both dated, we date the object by the edition (owner's decision, 2026-10-14) while yearrange is inconsistent, so those entries fail by design. Other differences should be reviewed, not copied.

| | id | input | expected | parser says |
|---|---|---|---|---|
| **fail** | yr-0153 | `Edo period, Late to early Meiji period` | parsed `1603..1868` | parsed `late period:meiji/early period:meiji` |
| **fail** | yr-0209 | `Japanese, Taishô era–Shôwa era, early 20th century` | parsed `1900..1924` | parsed `period:taisho/early century 20` |
| **fail** | yr-0228 | `Japanese, Edo period–Meiji era` | parsed `1603..1868` | parsed `period:edo/period:meiji` |
| **fail** | yr-0235 | `Not dated (late Meiji Period)` | parsed `1868..1912` | parsed `late period:meiji` |
| **fail** | yr-0260 | `Japanese, Taishô era–Shôwa era, 20th century` | parsed `1900..1999` | parsed `period:taisho/century 20` |
| **fail** | yr-0263 | `Japanese, Meiji era, beginning of 20th century` | parsed `1900..1924` | parsed `beginning century 20` |
| **fail** | yr-0271 | `Meiji Period (1867-1912) repoduction` | parsed `1867..1912` | parsed `period:meiji/1912` |
| **fail** | yr-0352 | `Late Meiji Period (1886-1912)` | parsed `1886..1912` | parsed `late period:meiji/1912` |
| **fail** | yr-0508 | `Meiji/early 20th century` | parsed `1900..1924` | parsed `period:meiji/early century 20` |
| **fail** | yr-0514 | `Late Edo - Meiji period.` | parsed `1603..1868` | parsed `late period:edo/period:meiji` |
| **fail** | yr-0534 | `Japanese, Meiji era–Taishô era, 20th century` | parsed `1900..1999` | parsed `period:meiji/century 20` |
| **fail** | yr-0537 | `Japanese, Modern impression from Edo-period block` | parsed `1603..1868` | parsed `not-before period:edo` |
| **fail** | yr-0706 | `Late Meiji Period` | parsed `1868..1912` | parsed `late period:meiji` |
| **fail** | yr-0711 | `Meiji/Taisho Period` | parsed `1868..1912` | parsed `period:meiji/period:taisho` |
| **fail** | yr-0712 | `Late Meiji to Taisho Period` | parsed `1868..1912` | parsed `late period:meiji/period:taisho` |
| **fail** | yr-0733 | `Made after a painting in Edo era. Ca. 1930s.` | parsed `1930..1939` | parsed `period:edo` |
| **fail** | yr-0739 | `Originally published in Edo era. This is a re-carved edition, published early 20th centuly.` | parsed `1900..1924` | parsed `period:edo` |
| **fail** | yr-0783 | `Originally in Edo era. This re-carved print is from 1927.` | parsed `1927..1927` | parsed `not-before 1927` |
| **fail** | yr-0877 | `Later Meiji period (1890 - 1910s).` | parsed `1890..1919` | parsed `late period:meiji/191X` |
| **fail** | yr-0899 | `Edo period (1615-1868), c. 1705-1715` | parsed `1615..1868` | parsed `1705~/1868` |
| **fail** | yr-0915 | `Probably in late Meiji period.` | parsed `1868..1912` | parsed `late period:meiji?` |
| **fail** | yr-1030 | `Originally in Edo era, this re-carved edition was made probably in11950s.` | parsed `1950..1959` | parsed `period:edo` |
| **fail** | yr-1082 | `c. 1810/20 (Meiji Facsimilie)` | parsed `1810..1820` | parsed `period:meiji/1820~` |
| **fail** | yr-1206 | `Originally in early Edo era, this edition was made in unknown date.` | parsed `1603..1868` | parsed `early period:edo` |
| **fail** | yr-1226 | `1898 (31 Meiji)` | parsed `1898..1898` | parsed `period:meiji/1898` |
| **fail** | yr-1228 | `Originally in late Edo era, this re-carved edition was made in unknown date.` | parsed `1603..1868` | parsed `late period:edo` |
| **fail** | yr-1236 | `1879 (Meiji 3, 2nd month)` | parsed `1879..1879` | parsed `period:meiji-3/1879` |
| **fail** | yr-1244 | `1867 (Meiji 1, 6th month)` | parsed `1867..1867` | parsed `period:meiji-1/1867` |
| **fail** | yr-1307 | `Originally in Edo era. This re-carved edition was made probably in 1890 - 1900s.` | parsed `1890..1909` | parsed `1890?/19XX` |
| **fail** | yr-1316 | `Originally in Edo era. This re-carved edition was made probably in early 20th centuly.` | parsed `1900..1924` | parsed `period:edo` |
| **fail** | yr-1322 | `Early Meiji.` | parsed `1868..1912` | parsed `early period:meiji` |
| **fail** | yr-1353 | `c. early 1800s (this item: Taisho-era strike)` | parsed `1800..1824` | parsed `period:taisho/early 18XX~` |
| **fail** | yr-1354 | `Ca. later Edo era.` | parsed `1603..1868` | parsed `late period:edo~` |
| **fail** | yr-1436 | `Not dated, Meiji Period (1867-1912)` | parsed `1867..1912` | parsed `period:meiji/1912` |
| **fail** | yr-1492 | `Not dated (Late Meiji Peroid)` | parsed `1868..1912` | parsed `late period:meiji` |
| **fail** | yr-1512 | `Japanese, Taishô era–Shôwa era` | parsed `1926..1989` | parsed `period:taisho/period:showa` |
| **fail** | yr-1529 | `Japanese, Meiji era, Meiji 27?` | parsed `1868..1912` | parsed `period:meiji/period:meiji-27?` |
| **fail** | yr-1543 | `Japanese, Meiji era–Taishô era` | parsed `1868..1912` | parsed `period:meiji/period:taisho` |

Failures:

- **yr-0153** `Edo period, Late to early Meiji period`
  - years.start: expected 1603, got 1868
  - years.end: expected 1868, got 1912
- **yr-0209** `Japanese, Taishô era–Shôwa era, early 20th century`
  - years.start: expected 1900, got 1912
- **yr-0228** `Japanese, Edo period–Meiji era`
  - years.end: expected 1868, got 1912
- **yr-0235** `Not dated (late Meiji Period)`
  - years.start: expected 1868, got 1899
- **yr-0260** `Japanese, Taishô era–Shôwa era, 20th century`
  - years.start: expected 1900, got 1912
- **yr-0263** `Japanese, Meiji era, beginning of 20th century`
  - years.end: expected 1924, got 1909
- **yr-0271** `Meiji Period (1867-1912) repoduction`
  - years.start: expected 1867, got 1868
- **yr-0352** `Late Meiji Period (1886-1912)`
  - years.start: expected 1886, got 1899
- **yr-0508** `Meiji/early 20th century`
  - years.start: expected 1900, got 1868
- **yr-0514** `Late Edo - Meiji period.`
  - years.start: expected 1603, got 1802
  - years.end: expected 1868, got 1912
- **yr-0534** `Japanese, Meiji era–Taishô era, 20th century`
  - years.start: expected 1900, got 1868
- **yr-0537** `Japanese, Modern impression from Edo-period block`
  - years.end: expected 1868, got (none)
- **yr-0706** `Late Meiji Period`
  - years.start: expected 1868, got 1899
- **yr-0711** `Meiji/Taisho Period`
  - years.end: expected 1912, got 1926
- **yr-0712** `Late Meiji to Taisho Period`
  - years.start: expected 1868, got 1899
  - years.end: expected 1912, got 1926
- **yr-0733** `Made after a painting in Edo era. Ca. 1930s.`
  - years.start: expected 1930, got 1603
  - years.end: expected 1939, got 1868
  - flag Approximate missing (flags: NamedPeriod, Unconsumed)
- **yr-0739** `Originally published in Edo era. This is a re-carved edition, published early 20th centuly.`
  - years.start: expected 1900, got 1603
  - years.end: expected 1924, got 1868
- **yr-0783** `Originally in Edo era. This re-carved print is from 1927.`
  - years.end: expected 1927, got (none)
- **yr-0877** `Later Meiji period (1890 - 1910s).`
  - years.start: expected 1890, got 1899
- **yr-0899** `Edo period (1615-1868), c. 1705-1715`
  - years.start: expected 1615, got 1705
- **yr-0915** `Probably in late Meiji period.`
  - years.start: expected 1868, got 1899
- **yr-1030** `Originally in Edo era, this re-carved edition was made probably in11950s.`
  - years.start: expected 1950, got 1603
  - years.end: expected 1959, got 1868
- **yr-1082** `c. 1810/20 (Meiji Facsimilie)`
  - years.start: expected 1810, got 1820
  - years.end: expected 1820, got 1912
- **yr-1206** `Originally in early Edo era, this edition was made in unknown date.`
  - years.end: expected 1868, got 1668
- **yr-1226** `1898 (31 Meiji)`
  - years.start: expected 1898, got 1868
- **yr-1228** `Originally in late Edo era, this re-carved edition was made in unknown date.`
  - years.start: expected 1603, got 1802
- **yr-1236** `1879 (Meiji 3, 2nd month)`
  - years.start: expected 1879, got 1870
- **yr-1244** `1867 (Meiji 1, 6th month)`
  - years.end: expected 1867, got 1868
- **yr-1307** `Originally in Edo era. This re-carved edition was made probably in 1890 - 1900s.`
  - years.end: expected 1909, got 1999
- **yr-1316** `Originally in Edo era. This re-carved edition was made probably in early 20th centuly.`
  - years.start: expected 1900, got 1603
  - years.end: expected 1924, got 1868
- **yr-1322** `Early Meiji.`
  - years.end: expected 1912, got 1880
- **yr-1353** `c. early 1800s (this item: Taisho-era strike)`
  - years.end: expected 1824, got 1926
- **yr-1354** `Ca. later Edo era.`
  - years.start: expected 1603, got 1802
- **yr-1436** `Not dated, Meiji Period (1867-1912)`
  - years.start: expected 1867, got 1868
- **yr-1492** `Not dated (Late Meiji Peroid)`
  - years.start: expected 1868, got 1899
- **yr-1512** `Japanese, Taishô era–Shôwa era`
  - years.start: expected 1926, got 1912
- **yr-1529** `Japanese, Meiji era, Meiji 27?`
  - years.end: expected 1912, got 1894
- **yr-1543** `Japanese, Meiji era–Taishô era`
  - years.end: expected 1912, got 1926

### Next

A maintainer checks that each expectation is right (the entry's note says why the reviewer chose it), then on this branch:

```
dotnet run --project tools/Corpus -- promote corpus/imported/yearrange.json yr-0153 yr-0209 yr-0228 yr-0235 yr-0260 yr-0263 yr-0271 yr-0352 yr-0508 yr-0514 yr-0534 yr-0537 yr-0706 yr-0711 yr-0712 yr-0733 yr-0739 yr-0783 yr-0877 yr-0899 yr-0915 yr-1030 yr-1082 yr-1206 yr-1226 yr-1228 yr-1236 yr-1244 yr-1307 yr-1316 yr-1322 yr-1353 yr-1354 yr-1436 yr-1492 yr-1512 yr-1529 yr-1543 --to disputed   # or fix these first
dotnet run --project tools/Corpus -- promote corpus/imported/yearrange.json
```

`promote` edits only the status tokens, refuses a failing entry, and rewrites `corpus/report.md`; commit the result to this branch. See `docs/08-corpus-contributions.md` §5.
