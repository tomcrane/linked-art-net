# Parse profiles

A profile is every interpretive rule the parser and realiser apply, as data (`ParseProfile`). The parser never
guesses a convention from the data it is parsing; the caller picks a profile, and the profile advisor
(`dotnet run --project tools/Corpus -- advise <strings.txt>`) can recommend one from a sample of a dataset.

A file may say `"inherits": "default"` and state only what it changes. The file name and the `name` must agree.
`ProfileCatalog.Load("profiles")` loads them all; the corpus evaluator resolves entry profiles from this directory.

| Profile | For | Changes from `default` |
|---|---|---|
| `default` | general cultural-heritage text, British English | popular centuries (19th = 1800-1899), thirds for early/mid/late, hedges as flags only, open bounds omitted, day-first numeric dates, no two-digit years |
| `library` | MARC-style library records | two-digit years read within 1900-1999; early/late as halves of a century or decade |
| `archive-us` | US archival (DACS/EAD) | month-first numeric dates, "Winter 1776" = Jan-Mar 1776, decade thirds 0-3 / 4-6 / 7-9 |
| `catalogue-quarters` | museum catalogues that read early/late as quarters | early 0-24, mid 25-74, late 75-99; early 1790s = 1790-1792 |
| `medieval-manuscripts` | manuscript cataloguing with Latin | Latin lexicon, strict centuries (1301-1400), hedges widened 10 years, plausible years 400-1600 |
| `multilingual` | mixed European collections | FR, DE, IT, ES, NL and LA lexicons alongside English |

The default profile's JSON is generated from the code (`LINKEDARTNET_WRITE_PROFILES=1 dotnet test`) and a test keeps
it in sync; the others are hand-written and loaded by `ProfileCatalogTests`.
