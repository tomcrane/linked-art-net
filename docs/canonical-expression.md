# Canonical date expressions

The text form of a `DateExpression` (`LinkedArtNet.Dates`). It is EDTF Level 2 wherever EDTF can say the
thing, and a small set of lower-case keyword prefixes where it cannot. `CanonicalReader` parses it,
`CanonicalWriter` writes it, and `Read(Write(e)) == e` for every expression. It is what the parser reports as
`Canonical`, what an LLM interpreter is asked to return, and what a reviewer types in the trainer to
correct a parse. It carries *meaning*, never policy: `century 19` says "the nineteenth century", and
whether that is 1800-1899 or 1801-1900 is the profile's business.

## Grammar

```
expression  := 'undated' | set | interval | prefixed
set         := '[' expression (',' expression)* ']'          two or more members; "1904 or 1905"
interval    := bound '/' bound                               bound := prefixed | '..'  (open)
prefixed    := (lifecycle | relation | band)* single
             | 'century' INT | 'millennium' INT              INT negative for BCE: century -5
single      := point [ '~' | '?' | '%' ]                     approximate | uncertain | both
point       := year | year '-' MM | year '-' MM '-' DD        ISO / EDTF L0
             | year '-' (21|22|23|24)                        EDTF seasons: spring, summer, autumn, winter
             | unspecified                                   195X (decade), 18XX, 1XXX, 1X50, -044X
             | 'period:' KEY                                 a named period, resolved by a period source
             | NUMBER ('years'|'ka'|'Ma'|'Ga') ('ago'|'BP')  deep time; BP counts back from 1950
year        := '-'? DDDD | 'Y' '-'? DDDDD+                   astronomical: 0000 is 1 BCE, -0499 is 500 BCE
lifecycle   := 'born' | 'died' | 'baptised' | 'buried' | 'fl'
relation    := 'before' | 'after' | 'by' | 'until' | 'not-before' | 'not-after'
band        := 'early' | 'mid' | 'late' | 'h1' | 'h2' | 'q1' | 'q2' | 'q3' | 'q4'
             | 't1' | 't2' | 't3' | 'beginning' | 'end'
```

Rules:

- Qualifiers (`~ ? %`) attach to a single date. To hedge an interval, hedge its bounds: `1850~/1900~`.
- A lifecycle keyword may precede an interval: `fl 1480/1510`, `born 1606`.
- Relations and bands apply to single dates (`before 1850`, `late 195X`, `early century 14`, `q2 century 14`).
- `195X` with a single trailing `X` is a `Decade`; any other `X` pattern is `UnspecifiedDigits`.
- A `Corrected` expression ("1962 (i.e. 1967)") is written as its correction; the original survives only in
  the parse result's notes.
- Writers emit years with four digits (`0079`), `-` for BCE (`-0499`), and `Y` beyond four digits
  (`Y-4500000000`).

## Examples

| Text | Canonical |
|---|---|
| 1984 | `1984` |
| c. 1850 | `1850~` |
| 1850? | `1850?` |
| 1950s | `195X` |
| late 1950s | `late 195X` |
| 19th century | `century 19` |
| early 14th century | `early century 14` |
| second quarter of the 14th century | `q2 century 14` |
| 5th century BC | `century -5` |
| 500 BC | `-0499` |
| c. 2500 BCE | `-2499~` |
| before 1850 | `before 1850` |
| 1971- | `1971/..` |
| 1904 or 1905 | `[1904,1905]` |
| between 1870 and 1890 | `1870/1890` |
| fl. 1480-1510 | `fl 1480/1510` |
| b. 1850 | `born 1850` |
| Winter 1945 | `1945-24` |
| Edo period | `period:edo` |
| 4.5 billion years ago | `4.5 Ga ago` |
| 10,000 BP | `10 ka BP` |
| n.d. | `undated` |

## Realisation (summary; the profile decides the numbers)

- year, month, day, season: the natural span; winter runs into the following year.
- `195X`: 1950-1959. `-044X`: -449..-440.
- `century N`: popular (1800-1899) or strict (1801-1900) by profile; BCE centuries always 500-401 BCE.
- bands: early / mid / late and beginning / end from the profile's band tables; halves, quarters and
  thirds exact.
- `~` and `?`: flags; widened only if the profile sets padding years.
- `before` and `by`: only an end bound (`before` stops a second before the date, `by` includes it);
  `after` and `not-before`: only a start bound. The profile can clamp open bounds instead.
- sets: the hull, with each member as an alternative.
- deep time: counted back from 1950 (`BP`) or the current year (`ago`), widened to the precision written
  (`4.5 Ga` is 4.45-4.55 Ga).
