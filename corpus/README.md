# Date corpus

Ground truth for `LinkedArtNet.Dates`: free-text date strings with the Linked Art TimeSpan(s) they should
produce. Design: [docs/06-corpus-and-testing.md](../docs/06-corpus-and-testing.md); entry format:
[schema/corpus-entry.schema.json](schema/corpus-entry.schema.json); canonical expressions:
[docs/canonical-expression.md](../docs/canonical-expression.md).

| Folder | What | Licence |
|---|---|---|
| `seed/` | Strings from the py_datetime prototype and the PMC pipeline, expectations re-derived by hand | repo (MIT) |
| `authored/` | Sets written to cover a theme: BCE and deep time, centuries and bands, open ends and relations, lifecycles and floruit | CC0-1.0 |
| `imported/` | Other projects' test data, as `proposed` until reviewed | each file states its licence |
| `proposed/` | Entries submitted from the trainer, awaiting review | CC0-1.0 |

Status: `verified` entries gate CI (`CorpusTests.Verified_entry_passes`); `proposed` and `disputed` are reported.
Until the grammar strategy lands (Phase 2 step 3) only canonical input parses, so every free-text file is held
at `proposed` even where the expectations have been checked by hand; the files say so in their notes.

Tooling:

```
dotnet run --project tools/Corpus -- validate          schema, ids, canonical strings, bound order, flags
dotnet run --project tools/Corpus -- run --all --write  evaluate everything; write report.md / report.json
dotnet run --project tools/Corpus -- stats
```

Expectations are evaluated under the profile each entry names (default: `profiles/default.json`), so a policy
change is an explicit edit to the corpus. Imported sources keep what they said in `sourceExpectation` when we
disagree.
