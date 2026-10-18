# The alignment store and reconciler (`LinkedArtNet.Alignment`)

Steps 2 and 3 of the toolkit (doc 10, section 12); the store built 2026-10-17, the local authority index and the
reconciler 2026-10-18 (section 8 below). The store is the repeatable part of a transformation:
the identifiers we minted, the strings we saw, what we decided each string refers to and on what evidence, the
authority records we fetched, and the runs. A collection can be re-processed from a fresh dump without re-running
alignment because stages 5 and 6 of the pipeline read from here.

## 1. Shape

PostgreSQL only (decision 2), plain Npgsql, one schema file applied idempotently
(`src/LinkedArtNet.Alignment/Store/Schema.sql`, embedded; `PostgresAlignmentStore.InitialiseAsync` runs it, a
`schema_version` table records the version). No ORM: the queries are short and the shapes are the design's.

| Table | Row | Key |
|---|---|---|
| `entities` | something we minted a URI for: class, best label, alternate labels, lifespan as four instants plus the text, nationalities, roles, status, `merged_into` | URI |
| `entity_equivalents` | an authority URI an entity is the same as, with source, method, confidence, when, dataset version, run | (entity, uri) |
| `source_strings` | a string as it occurred: dataset, context, raw, normalised, parsed JSON, first/last seen, occurrences | (dataset, context, normalised) |
| `decisions` | what a string refers to: entity or null, method, evidence JSON, reviewer, when, run, `valid` | serial; one valid per string |
| `overrides` | curated answers: (dataset, context or any, normalised) → entity or authority URI, with the source | (dataset, context, normalised) |
| `authority_records` | a cached external record: source, labels, type, lifespan, sameAs, raw data, fetched, version | URI |
| `runs` | one pipeline execution: dataset, manifest, dumps, started, finished, git SHA, counts, report | id |

Labels are matched through `normalised_labels text[]` with a GIN index, so "find entities called X" is an index read.
Lifespans are stored as `begin_earliest`, `begin_latest`, `end_earliest`, `end_latest` (timestamptz) so overlap and
conflict are SQL; the model's `Lifespan.Compatible` applies the rule in code (both sides known and disjoint is a
conflict, a silent side is neutral). Dates outside years 1-9999 keep their text only.

## 2. API

`IAlignmentStore` (`Store/IAlignmentStore.cs`) is what pipeline stages and tests program against; the one
implementation is `PostgresAlignmentStore`. The groups:

- **Entities**: `AddEntityAsync(dataset, NewEntity, scheme)` mints a URI under the scheme unless the `NewEntity`
  carries one (an import of published identifiers); `GetEntityAsync`, `ResolveEntityAsync` (follows merges),
  `FindEntitiesByLabelAsync`, `FindEntitiesByEquivalentAsync`, `UpdateEntityAsync`, `AddEquivalentAsync` (a
  provisional entity with an equivalent becomes aligned), `MergeAsync` (equivalents move, valid decisions re-point,
  the loser is marked merged and still resolves).
- **Source strings**: `CollectAsync` upserts by the normalised key and bumps `occurrences` and `last_seen`;
  `UndecidedAsync` lists the reconciler's work (no valid decision at all).
- **Decisions**: `DecideAsync` invalidates the earlier decision for the string and records the new one, entity or
  null ("looked at, unresolved"); `DecisionForAsync`; `ResolveAsync(dataset, context, raw)` goes string → decision →
  entity following merges; `InvalidateDecisionsForEntityAsync` reopens every string decided to an entity.
- **Overrides**: `PutOverrideAsync`, `OverrideForAsync` (the context's own override first, then the one for any
  context).
- **Authority records**: `GetAuthorityAsync`, `PutAuthorityAsync`, `FindAuthoritiesByLabelAsync(key, source)`.
- **Runs**: `StartRunAsync`, `FinishRunAsync`, `GetRunAsync`, `RunsAsync`; `CountsAsync` for the overview.

`StringKey.Normalise` is the key for strings, overrides and label matches: Unicode compatibility form, one kind of
apostrophe and dash, whitespace collapsed, trailing punctuation dropped, case folded. Two strings with the same key
are the same string for the store; nothing more is claimed.

## 3. Identifiers

`IdentifierScheme(Base, Segments, IdLength)` gives each dataset its hostname (decision 1) and a path segment per
class; `IdentifierScheme.Pmc` is `https://data.paul-mellon-centre.ac.uk/` with `person`, `group`, `place`,
`concept` (Type, Material and Language), `period`, `event`, which is what the published PMC identifiers use.
`IdMinter` keeps the PMC alphabet (lower-case letters and digits without 0, 1, i, l, o; letter first; eight
characters), generated from a cryptographic source and checked unused on insert. The Leeds thing/document/
representation layering (doc 10, section 8) is a publishing concern and is not in the scheme yet.

## 4. Importers (`Import/`)

- `PmcLuxOverrides` reads the `digirati-co-uk/pmc-lux` `authority-strings/*.json` files (MIT): the `authorities`
  become entities with their identifiers kept and their ULAN, VIAF, LoC, Wikidata, AAT, TGN and LUX ids as
  equivalents (method `override`); the `local_string_map` becomes overrides for any context. Strings whose target
  has no authority record still get an override to the URI the identifier would have.
- `LegacyPmcImporter` reads the owner's earlier `authorities` and `source_string_map` tables from a second
  connection: authorities become entities (identifiers kept, equivalents method `imported`), each source string is
  collected under its old `source` as the context with a decision of method `imported`, strings that only had an
  `unrec_` placeholder stay undecided with the placeholder in their parsed JSON, rows with an unknown type or a
  dangling authority are counted and skipped. `LegacyImportOptions` is for a second copy of those tables from another
  run, with its own minted identifiers: `ReuseByEquivalent` maps an authority onto an entity of the same dataset and
  class that already has one of its ULAN, VIAF, LoC or Wikidata ids (the label joins the alternates), `OnlyReferenced`
  adds entities only for authorities a new decision needs, `DatasetVersion` tags what the import writes. A string the
  store has already decided keeps its decision, so the first import's answers stand.

Both are idempotent: entities already present are left alone, overrides are replaced, decisions are not duplicated.

## 5. Command line (`tools/Align`)

```
dotnet run --project tools/Align -- init
dotnet run --project tools/Align -- stats [--dataset pmc|all]
dotnet run --project tools/Align -- import-overrides authority-strings/groups.json [--dataset pmc] [--base https://...] [--class Group]
dotnet run --project tools/Align -- import-legacy [--dataset pmc]
dotnet run --project tools/Align -- import-legacy --reuse-by-equivalent --only-referenced --version legacy-stealth
```

Connection strings come only from the environment: `LINKEDART_PG` for the store, `LINKEDART_LEGACY_PG` for the
earlier PMC database. Nothing in the repository holds one.

## 6. Tests

`tests/LinkedArtNet.Alignment.Tests` runs the store against a disposable PostgreSQL 15: the connection string in
`LINKEDART_TEST_PG` when set (its tables are dropped and recreated, so point it at a scratch database), else a
container through Testcontainers, which needs Docker. Tests marked `[PostgresFact]` are skipped with the reason when
neither is available; the scheme, normalisation, lifespan and parsing tests always run. CI's Ubuntu runner has
Docker, so the store tests run there.

## 8. The local authority index and the reconciler (step 3, local half)

The old pipeline's cost was the network: every string went to LUX, VIAF and the Library of Congress live, with
their crawl delays, and a rerun paid it again. The reconciler answers from local data first and asks a remote
service only for what is left, through a cache and a budget (the remote half is step 3b).

### 8.1 Loading a Getty vocabulary (`Authorities/`)

`NTriples` streams an N-Triples file (`.nt` or `.nt.gz`), one triple per line, skipping and counting malformed
lines. `GettyVocabLoader` takes a Getty full download (ULAN, AAT or TGN; monthly, ODC-By) and turns each concept
into an `AuthorityRecord`: the display label from `gvp:prefLabelGVP`, every SKOS-XL term's `literalForm` as a
label, `skos:exactMatch`/`closeMatch` as `sameAs` (ULAN carries LoC and Wikidata links, so those identifiers come
free), the preferred biography's `estStart`/`estEnd` as the lifespan, `nationalityPreferred`, agent or place type,
broader term, coordinates and the description in the record's data. The triples the index needs are staged in an
unlogged table and joined in SQL, since a full TGN is tens of millions of statements, and the records are written
with a binary copy (`PutAuthoritiesAsync`). Labels are indexed twice, normalised and diacritic-folded
(`StringKey.Fold`), so "Durer" finds "Dürer" whichever side has the accent. The shapes were checked against
vocab.getty.edu on 2026-10-18 and the test fixture is a sliver of ULAN in those shapes.

Credit line, as Getty asks: "Contains information from the J. Paul Getty Trust, Getty Research Institute, Union
List of Artist Names, which is made available under the ODC Attribution License" (likewise for the AAT and TGN).

### 8.2 Reading a string (`Reconciliation/`)

A `ContextRule` per context (the field a string came from) says what classes it may name and which agent-string
profile reads it. `StringReading.Read` runs the agent parser when a profile is named, so "Dürer, Albrecht,
1471-1528" becomes the name "Dürer, Albrecht", a lifespan, and any nationality or occupation the string carried;
`NameMatcher.Keys` then gives the lookup keys: the normalised name, its folded form, and the other word order
("albrecht dürer").

### 8.3 Candidates, scoring, groups

`IAuthoritySource.FindAsync(reading)` returns `Candidate`s; `LocalIndexSource` is the store's records for one
source (ulan, aat, tgn, loc) looked up by the keys within the allowed classes. `NameMatcher.Score` ranks a name
against a record's labels: exact 1.0, folded 0.97, the same words in another order 0.93, a surname with initials
against full given names 0.85, then word overlap and a bounded edit distance (one letter off scores about 0.82).
`Reconciler.Score` then applies the design's rules: a candidate of a class the context forbids is vetoed; when both
sides have dates, agreement lifts the score and conflict vetoes; a shared nationality lifts it. `Reconciler.Groups`
joins candidates through their URIs and `sameAs` links (union-find), so ULAN's record and the LoC record it points
at vote as one group with two sources; a group whose members are all vetoed drops out.

### 8.4 Decisions

For each undecided string, stopping at the first that answers:

1. **Override** for the whole string or the name part: method `override`; the target becomes the entity (created
   under the curated identifier if it is ours, or with the authority URI as its equivalent).
2. **The store**: entities of the dataset whose labels match a key and whose lifespan does not conflict. One
   acceptable match, or one with authority behind it among provisional lookalikes, is taken: method `exact-name`.
   This is the common case on a rerun.
3. **The local indexes**, scored and grouped. The top group is accepted (method `vote`) when its score reaches
   `AcceptScore` and it carries: two independent sources agree, or an entity already in the store is in it, or
   (`AcceptSingleSourceWithDates`, on by default while ULAN is the only index; doc 10 sends a lone source to review)
   one source matches the name exactly and both sides have compatible dates. A rival group at acceptance score,
   unless it is only a provisional lookalike, is a disagreement and sends the string to review instead.
4. **Review** when the best group reaches `ReviewScore` but is not accepted: a provisional entity is minted for the
   string meanwhile and the decision, method `queued`, carries the candidates as evidence. The review UI (step 5)
   reads these.
5. **Minted** when nothing reached `ReviewScore`: a provisional entity with the string's name, dates and roles,
   method `minted` (or, with `MintWhenNothingFound` off, the string is left for a later run).

The entity an accepted group produces reuses any entity that already has one of the group's URIs as an equivalent,
otherwise it is minted aligned with the best candidate's label, the string's name among its alternates, the
candidate's lifespan, and every member URI and `sameAs` link as equivalents at the group's score. Every decision's
evidence JSON holds the reading and the top groups with each member's score and reason.

### 8.5 Command line

```
dotnet run --project tools/Align -- load-getty ulan.nt.gz --vocab ulan --version 2026-10
dotnet run --project tools/Align -- reconcile [--dataset pmc] [--limit 1000] [--accept 0.9] [--review 0.6] [--no-single-source] [--no-mint]
```

`reconcile` records a run with its options and report. The PMC context rules live in the tool for now
(persauthorfull: Person; corpauthor and publisher: Group; place: Place; keywords: anything), and move to the
dataset manifest in step 4.

## 9. Not yet

Step 3b: the remote sources (VIAF, LoC suggest, LUX with the self-echo rule, Wikidata by known id) behind a cache
and a per-run budget, with tests against recorded responses; a loader for the LoC name file; a seed of the index
from the owner's earlier `ulan_labels`/`aat_labels` caches; PeriodO as a source for Period strings. Then the
pipeline runner and PMC instance (step 4) and the review UI for alignment decisions (step 5). The legacy import first ran against the owner's database on 2026-10-18: 27,443 authorities became
entities with 76,152 equivalents, and all 72,904 strings were visited (72,714 after strings that share a normalised key collapsed), 35,333 of them decided, with nothing skipped
for an unknown type or a dangling authority. A second, smaller run over corporate names only (December 2024, 1,282
strings) was compared first: of 808 strings both runs decided, 5 disagreed and the first run was right in 3, so its
answers stand and the second copy was imported with the reuse and only-referenced options as version `legacy-stealth`:
45 entities added, 42 mapped onto existing ones, 139 equivalents, 35 new strings and 93 new decisions. The store then
held 27,488 entities and 72,749 strings, 35,426 decided.
