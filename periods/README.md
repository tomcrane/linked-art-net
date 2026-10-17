# Period data

`periodo.json` is a compact copy of the [PeriodO](https://perio.do) dataset: a gazetteer of period definitions
("Iron Age", "Early Bronze II", "Edo") collected from published sources, each with its bounds, the place it applies
to and the source that defines it. The parser's `PeriodOSource` reads it so that a period name in a date string
resolves to a dated span and, when one source defines the name, to PeriodO's permanent URI
(`http://n2t.net/ark:/99152/p0f65r2qmh2`), which the canonical form carries as `period:p0f65r2qmh2`.

**Provenance and licence.** The data is PeriodO's, downloaded from <https://data.perio.do/dataset/> and reduced to
the fields the parser needs (id, authority, label, alternative labels, language, start and stop years, spatial
description). PeriodO's contributors have dedicated the dataset to the public domain under
[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/); the definitions belong to the authorities PeriodO
cites, and the compact file keeps each authority's title so results can say where a definition comes from. PeriodO
asks that uses be attributed, which the About page and the parser's notes do. The `version` and `modified` fields
in the file record the dataset version (PeriodO's ETag) and its last-modified date at download.

**Refreshing.** `dotnet run --project tools/Corpus -- periodo` downloads the current dataset and rewrites the file;
`-- periodo --from <downloaded.json>` compacts a file already on disk (gzipped or not). Commit the result; a test
checks the file loads and resolves common names.

**How names resolve.** The library's own tables (`src/LinkedArtNet.Dates/Periods/*.json`: Japanese eras, Chinese
dynasties, British periods, a few archaeological periods) are asked first, so `Edo period` keeps its key `edo`.
A name that exactly one PeriodO source defines resolves to that definition. A name that many define, as "Iron Age"
does in some forty regions, resolves to the hull of all of them with a note saying so; `PeriodOOptions` narrows by
place, authority or language, and a reviewer can name one definition directly with `period:<id>`.
