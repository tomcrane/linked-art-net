# Date corpus

Ground truth for `LinkedArtNet.Dates`: free-text date strings with the Linked Art TimeSpan(s) they should
produce. Design: [docs/06-corpus-and-testing.md](../docs/06-corpus-and-testing.md); entry format:
[schema/corpus-entry.schema.json](schema/corpus-entry.schema.json); canonical expressions:
[docs/canonical-expression.md](../docs/canonical-expression.md).

| Folder | What | Licence |
|---|---|---|
| `seed/` | Strings from the py_datetime prototype and the PMC pipeline, expectations re-derived by hand | repo (MIT) |
| `authored/` | Sets written to cover a theme: canonical forms, BCE and deep time, centuries and bands, open ends and relations, lifecycles and floruit, named periods | CC0-1.0 |
| `imported/` | Other projects' test data, as `proposed` until reviewed: `yearrange.json` (1650, MIT), `timetwister.json` (91, MIT), `getty.json` (180 ULAN biographies, ODC-By), `edtf-spec.json` (61, public domain); licences and attributions in `imported/THIRD-PARTY-NOTICES.md` | each file states its licence |
| `proposed/` | Entries submitted from the trainer, awaiting review | CC0-1.0 |

How entries get here and how they become `verified`: [docs/08-corpus-contributions.md](../docs/08-corpus-contributions.md).

Status: `verified` entries gate CI (`CorpusTests.Verified_entry_passes`); `proposed` and `disputed` are reported.
Both seed files are fully `verified` (reviewed 2026-10-14); the imported corpora stay `proposed` until reviewed.

Tooling:

```
dotnet run --project tools/Corpus -- validate          schema, ids, canonical strings, bound order, flags
dotnet run --project tools/Corpus -- run --all --write  evaluate everything; write report.md / report.json
dotnet run --project tools/Corpus -- stats
dotnet run --project tools/Corpus -- promote <file> [id ...] [--to verified|disputed|proposed] [--dry-run]   set statuses in place; verified refuses failing entries
dotnet run --project tools/Corpus -- review <file ...> [--out <markdown>]      pull request review of the files (posted by corpus-review.yml)
dotnet run --project tools/Corpus -- import yearrange <date-tests.json> | import timetwister <dates_spec.rb> | import getty <dir of ulan-*.json>
dotnet run --project tools/Corpus -- advise <strings.txt>                 profile advice for a dataset
dotnet run --project tools/Corpus -- tokens <profile> "<text>"            how a string tokenizes and parses
```

Expectations are evaluated under the profile each entry names (default: `profiles/default.json`), so a policy
change is an explicit edit to the corpus. Imported sources keep what they said in `sourceExpectation` when we
disagree.
