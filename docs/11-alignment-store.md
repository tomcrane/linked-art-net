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
| `projects`, `users`, `memberships`, `notes` | the workbench's projects (name, identifier base, manifest), signed-in people, their roles per project, and free-text notes on strings, entities or runs (schema version 2; doc 12 section 4) | |

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
  has no authority record still get an override to the URI the identifier would have. An entity of the dataset that
  already shares an authority id with a curated one (an earlier run's own minted Agnew's, say) is merged into it: the
  curated identifier survives, its labels and decisions follow.
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

`LocNamesLoader` does the same for the Library of Congress Name Authority File, from the MADS/RDF N-Triples bulk
download (7 GB gzipped, streamed without unpacking): personal, corporate, conference, family and geographic headings
become records typed Person, Group, Event, Group and Place; the authoritative label and the variants are indexed,
and a personal heading's embedded dates ("Dürer, Albrecht, 1471-1528") are split off so the name alone is a key and
the dates become the lifespan when the real-world-object node has none; VIAF and Wikidata links come through as
sameAs (VIAF's `sourceID` form, which is LoC's own id reflected back, is dropped). EDTF years are read as ranges
("185X" is 1850 to 1859) and a negative year, BCE, stays text. Each load stages into its own table, dropped at the
end, and retires the source's records the new dump no longer has, so a vanished id is noticed. The shared staging
lives in `TripleStaging`. Credit: Library of Congress Name Authority File.

### 8.2 Reading a string (`Reconciliation/`)

A `ContextRule` per context (the field a string came from) says what classes it may name and which agent-string
profile reads it. `StringReading.Read` runs the agent parser when a profile is named, so "Dürer, Albrecht,
1471-1528" becomes the name "Dürer, Albrecht", a lifespan, and any nationality or occupation the string carried;
`NameMatcher.Keys` then gives the lookup keys: the normalised name, its folded form, the bare form without
punctuation, the comparison form with conventional abbreviations expanded and firm noise removed ("thos. agnew &
sons ltd" and "messrs langford" become "thomas agnew sons" and "langford"; `Tables/name-forms.json`, written from
general knowledge and extended as data shows the need), and the other word order ("albrecht dürer"). Labels are
indexed under the same variants (`StringKey.Variants`), so any of them finds the record.

### 8.3 Candidates, scoring, groups

`IAuthoritySource.FindAsync(reading)` returns `Candidate`s; `LocalIndexSource` is the store's records for one
source (ulan, aat, tgn, loc) looked up by the keys within the allowed classes. `NameMatcher.Score` ranks a name
against a record's labels: exact 1.0, folded 0.97, the same comparison form 0.95, the same words in another order
0.93, a surname with initials against full given names 0.85, then word overlap and a bounded edit distance (one
letter off scores about 0.82).
`Reconciler.Score` then applies the design's rules: a candidate of a class the context forbids is vetoed; dates are
compared three ways (`Lifespan.Compare`): a side both know that overlaps is agreement and lifts the score, a side
both know that is disjoint, or a birth after the other's death or more than a lifetime before it, is a conflict and
vetoes, and dates known on different sides that neither check bites are not comparable and change nothing; a shared
nationality lifts the score. `Reconciler.Groups` joins candidates through their URIs and `sameAs` links
(union-find), so ULAN's record and the LoC record it points at vote as one group with two sources. A group is vetoed
when all its members are, or when a vetoed member is linked by the authorities to a live one: they say it is the
same person, so the conflict is the live record's too. The store's own entities do not count as a source.

### 8.4 Decisions

For each undecided string, stopping at the first that answers:

1. **Override** for the whole string or the name part: method `override`; the target becomes the entity (created
   under the curated identifier if it is ours, or with the authority URI as its equivalent).
2. **The store**: entities of the dataset whose labels match a key and whose lifespan does not conflict; an entity
   without dates of its own (an import) is given its authority records' dates for the check. One acceptable match,
   or one with authority behind it among provisional lookalikes, is taken: method `exact-name`. An entity minted for
   a string that is itself awaiting review never answers silently, so siblings of a queued string are queued too; an
   entity minted because nothing was found anywhere does, so one unknown name across contexts stays one local
   authority. This is the common case on a rerun.
3. **The local indexes**, scored and grouped. The top group is accepted (method `vote`) when its score reaches
   `AcceptScore` and it carries: two independent sources agree, or an entity with authority behind it is already in
   the store, or (`AcceptSingleSourceWithDates`, on by default while ULAN is the only index; doc 10 sends a lone
   source to review) one source matches the name exactly and the dates agree, both known on the same side. A rival
   group at acceptance score is a disagreement and sends the string to review; a provisional lookalike counts as a
   rival only against another lookalike. A lookup that returned more records than it was asked for never accepts,
   since the rival may be among the unseen, and queues instead.
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
dotnet run --project tools/Align -- load-getty <ulan.nt | unzipped-folder> --vocab ulan --version 2026-10
dotnet run --project tools/Align -- reconcile [--dataset pmc] [--limit 1000] [--accept 0.9] [--review 0.6] [--no-single-source] [--no-mint]
```

`reconcile` records a run with its options and report; `decisions --run <id> [--method m]` shows a run's breakdown
by context and method and a sample with evidence; `undo-run --run <id>` invalidates a run's decisions, removes the
equivalents it added (an entity left with none goes back to provisional) and the entities it minted that nothing
else points at, so the same strings can be reconciled again after a change and cannot find the run's own answer;
`reindex-labels` recomputes every lookup key after the key rules change (the comparison form, the abbreviation
table), since keys are computed when a row is written. `merge-duplicates [--class Place,Type] [--apply]` is the
one-off pass over the earlier pipeline's duplicate entities (`DuplicateMerger`): twins of one class and label, case
folded, merge where their external ids do not conflict (none on one side, or an id shared with no authoritative
source differing; VIAF and LUX are clusters, so a differing VIAF or LUX id beside a shared Wikidata or TGN id is not
a conflict), people and bodies twins by the words of the name in any order as well ("Piper, John" and "John
Piper"), a person's twins only with a shared id or the same dates; a confirmed entity survives first, then the one with the
most ids, then the oldest; a dry run lists every label with what would merge and what is kept apart, and `--apply`
carries it out through the store's merge, so the merged-away identifiers stay as `merged_into` for the export to
write as equivalents of the survivor. The PMC context rules live in the tool for now (persauthorfull:
Person; corpauthor and publisher: Group then Person, since sale catalogues name auctioneers and dealers there;
place: Place; keywords: anything), and move to the dataset manifest in step 4.

### 8.6 First real run (2026-10-18)

ULAN explicit (January 2026 dump, 65 million triples, 404,624 records) loaded in eight minutes on the owner's
laptop. The first 500 undecided PMC strings were almost all corpauthor residue the 2024 run had failed on: 7
matched the store, 49 were queued (ULAN has them, at full score through alternate labels, but a lone undated source
goes to review), 444 minted. The minted sample was mostly genuine residue (local committees, fairs, small dealers)
plus misses that the bare and canonical keys now cover ("Thos. Agnew & Sons Ltd", "Messrs Langford", names with
punctuation) and auctioneers ULAN holds as people, which the corpauthor rule now allows.

### 8.7 Measuring the earlier alignment: shadow and audit

The imported decisions were made in 2024 by fuzzy name matching against live LUX, VIAF and LoC results, with dates
and type rarely available to veto. Two commands measure them without a human; the third measure, a reviewed random
sample, waits for the review UI.

`tools/Align shadow` re-judges decided strings (the imported ones by default) as if from scratch: the reconciler's
`EvaluateAsync` runs with the store's entities hidden, so an old decision cannot answer itself, and the new
evaluation is compared with the old entity's authority ids. Verdicts: **Agree** (the new answer points at the old
entity or one of its ids), **Disagree** (the new answer is accepted and names a different record in a source the old
entity also has an id in: one of them is wrong), **Unverifiable** (accepted, but in a source the old entity never had),
**QueuedWithOld**, **QueuedConflict** and **QueuedUnlinked** (the new run would send it to review: the old answer is
among the candidates; or a candidate shares a source with the old ids but names a different record; or the two id
sets have no source in common, a ULAN person against LoC and VIAF ids, so nothing can be compared), **OldOnly** (the new run found nothing), **OldLocal** (the old entity had no authority ids, so there is
nothing to compare). Nothing is decided; the run is recorded with the totals and by-context breakdown, and everything
but Agree and OldLocal goes to a CSV under `reports/` for the review spreadsheet. `--sample n` takes a random page.

`tools/Align audit` checks every entity against the local ULAN index, by its ULAN id or, when it has none, through
the ULAN record that links to its LoC, Wikidata or VIAF id: **UlanMissing** (the id is not in the dump: retired,
merged or mistyped), **LabelMismatch**, **DatesConflict**, **TypeMismatch**, **LinkConflict** (the entity has a LoC,
Wikidata or VIAF id and ULAN links to a different one: a wrong cluster on one side, or LoC's two headings for one
body under its earlier and later names; review decides), **LinkGain** (ULAN links to an id the entity lacks),
**UlanViaLink** (the ULAN id the entity could gain), **UlanAmbiguous** (several ULAN records claim its ids) and
**MultipleUlan** (the entity carries more than one ULAN id, each of which is checked). The
first run over the PMC store found only 799 entities with a ULAN id, and its label mismatches were mostly LoC-style
parenthesised qualifiers, which the comparison form now drops. The flagged entities go to a CSV.

### 8.8 The LoC names index, and what it changed (2026-10-19)

**The load.** 724 million triples read, 81 million staged, 10,496,266 records, 3 h 23 min on the owner's laptop:
about 40 min reading and staging, 45 min indexing the staging table, an hour aggregating records, an hour inserting
them. The insert was bound by writing the transaction log, which is what live GIN maintenance costs per row; a batch
of 500,000 or more now drops the label indexes first and rebuilds them after, then vacuums the table itself,
unthrottled (autovacuum would have taken an hour or more, slowing every lookup meanwhile).

**Three lookup lessons, each measured with `explain (analyze, buffers)` on the real store:**

1. *One label index for all sources* made a ULAN lookup for a common label read thousands of LoC rows from disk only
   to filter them out by source. *A composite GIN index* (`source, type, normalised_labels` with btree_gin, schema v4)
   answered ULAN questions in a millisecond but gave the LoC source an index entry listing ten million rows, which
   every LoC lookup walked. The shape that works is *one partial GIN index per source*
   (`authority_records_labels_<source> ... where source = 'loc'`, schema v5), matched by a literal source in the
   predicate; a lookup across sources is a union of one branch per loaded source, and `authority_sources` records
   what is loaded. A lookup for a source with no records is answered without a query: with per-source indexes an
   unloaded source (the manifest lists aat and tgn) would otherwise mean a full table scan per string.
2. *A limit within reach of the planner* let it choose a sequential scan, expecting to meet 201 matches early; for a
   name LoC does not have, that read all 10.9 million rows: 25.2 s, `Rows Removed by Filter: 10900890`. Each branch
   now finds its rows in a materialised CTE before the limit applies: 52 ms, index scan 0.08 ms.
3. *Default statistics* estimated 164,000 matches for that name and launched two parallel workers per lookup, most
   of the 52 ms. `alter column normalised_labels set statistics 1000` is in the schema and in the post-load step.

**What LoC changed, same 500 corpauthor strings:** ULAN only: accepted 0, store 23, queued 88, minted 382. ULAN + LoC:
accepted 12, store 34, queued 173, minted 277. A hundred and five fewer invented entities; the queue nearly doubled
with strings LoC knows something about.

**Shadow, 2,000 of the 2024 decisions:** Agree 279, Disagree 2, Unverifiable 5, QueuedWithOld 1,011, QueuedConflict
185, QueuedUnlinked 197, OldOnly 320 (this morning, ULAN only, OldOnly was 400 of 490). Both disagreements are the
2024 run's errors: "Flight, Claude" had been given Edith Lawrence, his partner; "Canney, Michael (artist)" a Michael
S. Canney born 1982; ULAN and LoC agree on the artists. Of the 1,011 queued with the old answer among the candidates,
677 are a single exact match in one source (LoC in 648 of them) with no rival at all, and the 2024 decision chose
exactly that record; only one of the 677 strings carries dates, so the single-source-with-dates rule cannot reach
them. Whether a unique exact single-source match should be accepted without a person is the owner's policy call
(§8.9 below records it when made); at this sample's rate it is about a third of the corpus, some 12,000 strings.
keywords OldOnly 222 are LCSH ids (not loaded yet); place has no Agree because TGN is not loaded.

### 8.9 Policy: the unique exact single-source match (owner, 2026-10-19)

Accepted. An undated string whose one exact match in one source is the only candidate at any score, vetoed or not,
all members authority records (no provisional lookalike, no store entity), and a name of at least two words ("Mr
Farebrother" read as "Farebrother" matched an auctioneer's record on the surname alone in the first sample: a person
decides those), is decided under its own method,
`exact-single`, with the equivalents marked the same way; the string's own context rules and the holders check
still apply. Off by `acceptUniqueExactSingleSource: false` in the manifest or `--no-unique-exact` on the tool. The
project page links "review single-source acceptances", the desk filtered to that method, so the set can be sampled;
`undo-run` reverses a run of them. A dated string is never reached by this rule: the dates rule governs it.

## 9. Not yet

Step 3b: the remote sources (VIAF, LoC suggest, LUX with the self-echo rule, Wikidata by known id) behind a cache
and a per-run budget, with tests against recorded responses; acting on the audit (adopting LinkGain equivalents,
reopening LinkConflict decisions) once the owner has read the first reports; a loader for the LoC name file; a seed of the index
from the owner's earlier `ulan_labels`/`aat_labels` caches; PeriodO as a source for Period strings. Then the
pipeline runner and PMC instance (step 4) and the review UI for alignment decisions (step 5). The legacy import first ran against the owner's database on 2026-10-18: 27,443 authorities became
entities with 76,152 equivalents, and all 72,904 strings were visited (72,714 after strings that share a normalised key collapsed), 35,333 of them decided, with nothing skipped
for an unknown type or a dangling authority. A second, smaller run over corporate names only (December 2024, 1,282
strings) was compared first: of 808 strings both runs decided, 5 disagreed and the first run was right in 3, so its
answers stand and the second copy was imported with the reuse and only-referenced options as version `legacy-stealth`:
45 entities added, 42 mapped onto existing ones, 139 equivalents, 35 new strings and 93 new decisions. The store then
held 27,488 entities and 72,749 strings, 35,426 decided.
