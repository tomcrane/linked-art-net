# Third-party notices for imported corpus files

This repository is MIT-licensed. The files in `corpus/imported/` are derived from other projects' test data or
published datasets. Each is listed here with its licence; the licence texts are reproduced where the licence
asks for it. Nothing here is share-alike, so the derived files can be redistributed under this repository's
licence with these notices intact.

## yearrange.json

Derived from `test/date-tests.json` in [jeresig/node-yearrange](https://github.com/jeresig/node-yearrange).

```
The MIT License (MIT)

Copyright (c) 2013 John Resig

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

## timetwister.json

Derived from `spec/dates_spec.rb` in [alexduryee/timetwister](https://github.com/alexduryee/timetwister),
written at the New York Public Library.

```
The MIT License (MIT)

Copyright (c) 2015 Alex Duryee

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

## getty.json

Contains information from the Getty Union List of Artist Names (ULAN), made available by the J. Paul Getty
Trust under the [Open Data Commons Attribution License (ODC-By) 1.0](http://opendatacommons.org/licenses/by/1.0/).
The sample was taken from the public SPARQL endpoint at https://vocab.getty.edu/sparql at import time; the
biography descriptions and estimated years are the Getty's, the expectations derived from them are ours.
Attribution required by the licence: "Contains information from the Getty Vocabularies, which is made available
under the ODC Attribution License."

## edtf-spec.json

The examples in the Library of Congress's
[Extended Date/Time Format (EDTF) Specification](https://www.loc.gov/standards/datetime/), a work of the United
States Government in the public domain in the United States. The descriptions were paraphrased by hand from the
specification; the example strings are as published.

## Not imported

- `arttracks/cultural_dates` (MIT) refers to a companion `edtf_test_cases` repository whose published branch
  holds no test files, so there was nothing to import.
- Share-alike sources (CC BY-SA, ODbL, GPL) are excluded as incompatible with an MIT repository.
