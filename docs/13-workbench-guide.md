# Using the alignment workbench

Status: first draft, 2026-10-19, written for the people who will use the workbench rather than for developers.
The design behind it is [12-workbench.md](12-workbench.md); the machinery is [11-alignment-store.md](11-alignment-store.md).
Wording to be revised with the owner.

## 1. What the workbench is for

A collection's records name people, organisations, places and subjects as strings of text: "Thos. Agnew & Sons
Ltd", "Dürer, Albrecht, 1471-1528", "Scottish Gallery". Publishing the collection as Linked Art means saying, for
each of those strings, which thing it names, giving that thing one identifier of our own, and recording which
entries in the big authority files (ULAN, the Library of Congress names, VIAF, Wikidata) are the same thing.

Software does most of this. It reads each string, looks it up in the authority files, and decides when the evidence
is strong enough. The workbench is where a person does the rest: judges the cases the software could not settle,
tells it things only a specialist knows (that twenty-four spellings in the sale catalogues are one firm; that a
dealer who appears in no authority file is real), and sees what the software did and why. Every decision a person
makes is kept, with their name on it, and the software never overrides it.

## 2. The words

**Project.** One collection being transformed: the Paul Mellon Centre's library is one project, Wellcome would be
another. A project has a short *dataset key* that never changes (`pmc`), an *identifier base* (the hostname and
path under which its identifiers are published, so that identifiers already in LUX stay the same), a *manifest*
(the rules below), and its own *members*. Everything a project holds is kept under its key; one project cannot see
or touch another's strings or entities.

**Members and roles.** A *reviewer* decides items and clusters variants. An *operator* can also run the software,
load indexes and change the manifest. An *owner* can also add and remove members. Some people own every project.

**Indexes.** The authority files loaded onto the machine: ULAN (artists and institutions), AAT (concepts), TGN
(places), the Library of Congress name file. They are shared by every project. Loading one is an operator's job and
takes from minutes to a couple of hours depending on the file.

**Strings.** The text as it occurs in the records, with its *context*, the field it came from (`persauthorfull`,
`corpauthor`, `publisher`, `place`, `keywords` for the PMC library), and how many records it occurs in. The same
text in two fields is two strings, because a name in the publisher field and the same name as a subject may not be
the same thing.

**Entities.** The things the project has given identifiers to. An entity has a label, other labels, dates where
known, and its *identifiers* in the authority files. It is in one of four states:

- *provisional*: minted by the software because a string needed something to point at and nothing better was found;
- *aligned*: the software matched it to one or more authority records;
- *confirmed*: a person has vouched for it, with identifiers or as a *local authority* in no external file;
- *merged*: it turned out to be the same as another entity and now points to it; anything that referred to it still
  works.

**Decisions.** What a string refers to, how that was decided, and the evidence. A string has one valid decision at
a time; a new decision replaces the old one, and *undo* brings the old one back. The *method* says how:

| method | meaning |
|---|---|
| override | a curated rule said this spelling always means this |
| vote | authority records of two or more sources agreed, or an entity already in the project with authority behind it did |
| single | one source's exact match, carried by the string itself: its dates agreed with the record's, or its qualifier ("Hammersmith, London") was borne out by the record's placement; reviewable as a set |
| exact-single | one source's exact match was the only candidate and the string had no dates to check: accepted by project policy, reviewable as a set |
| exact-name | an entity already in the project matched |
| human | a person decided it on the desk or in the variants view |
| queued | candidates exist but none was good enough: waiting for a person |
| minted | nothing was found; a provisional entity was created |
| list | the string names several things ("Dublin; New York"); each is a string of its own with its own decision, and the list has no entity |
| composite | a compound heading ("Painting--Canada") nothing holds whole; its head and subdivisions are strings of their own, and the heading is a concept entity of the project coordinated from them |
| imported | carried over from the earlier system |

**The queue.** The queued strings. Each already points at a provisional entity so that publishing is never blocked;
a person's decision on the desk replaces it.

**Overrides.** "This exact spelling always means this entity, in any field." They win over everything else. The
desk's *Always* button and the variants view write them. They can be exported as a file, which is how the curated
knowledge leaves the workbench and comes back.

**Runs.** Each time the software works through strings, loads an index or checks the data, that is a run, with a
report and the option to undo it.

## 3. The desk

Review in the menu, or Start on the project page. The desk shows one string at a time and moves to the next after
each action.

At the top: the string as written, how often it occurs and in which field, and how the software read it: the name
part, the dates, roles and nationality it recognised. Below: the candidates, best first. Each card is one authority
record, or a group of records from different files that say they are the same thing, with its score, the files that
agree, the record's own label, dates and description, a link to the record, and the reasons the software gave, such
as "dates agree" or "type Group not allowed here". Greyed cards were ruled out, and the reason says why.

Actions:

- **Accept**: the string is this record. An entity is created for it, or reused if one already has that identifier.
- **Always**: the same, and this exact spelling will mean this from now on in any field. Use it for a spelling that
  recurs.
- **Same as this**: the string is an entity the project already has.
- **Something else**: search the project's entities and the indexes for anything not among the candidates.
- **Keep as local authority**: none of the candidates is right and the thing is real. Give it the label it should be
  published under and, if useful, a note. The software stops asking about it.
- **Skip for now** leaves it in the queue. **Add note** leaves a message for another reviewer. **Undo last decision**
  puts the previous decision back.

The dropdown at the top switches from the queue to the strings the software minted (nothing found), which can be
reviewed the same way, and to those a person already decided.

## 4. Entities and variants

**Entities** lists the project's entities with a search box and filters by state and class. An entity's page shows
its labels, dates, identifiers with the authority record behind each, the strings that resolve to it, and notes. A
reviewer can rename it, add an identifier (a URI from ULAN, LoC, VIAF, Wikidata, AAT, TGN or LUX), remove one,
confirm it, merge it into another entity found by search, add a note, or *reopen* chosen strings, which takes them
away from the entity so the software reconsiders them next time.

**Variants** is for the knowledge only a specialist has. Type a string or a word. The workbench picks its most
distinctive word ("agnew" from "Thos. Agnew & Sons Ltd") and lists every string containing it, grouped by what each
resolves to now. Tick the ones that are one thing, choose the entity they belong to or type a label for a new local
authority, tick any identifiers found by the inline index search or type one, and press Cluster. Each ticked
spelling gets an override, any provisional entities they pointed at fold into the chosen one, and the labels come
across. Clustering is how the pmc-lux file of 128 spellings and 32 firms gets its next hundred entries.

**Export overrides** on the project page writes the project's overrides in that file's shape: the spellings and
the identifiers they map to, and for each identifier the entity's label and its authority ids. The file imports
back into the store, so it can be edited outside, versioned, or handed to another system.

## 5. The manifest

The manifest is the project's reading rules, shown and edited on the project page as JSON. The parts that matter:

- `contexts`: for each field a string can come from, the kinds of thing it may name (person, group, place, type,
  material, language, period, event) in order of preference, and the agent-string *profile* that reads it. The
  profile knows the field's conventions: how dates, roles and honorifics are written. A field not listed may name
  anything and is read as a whole. The PMC library's rules are one button away for a project that uses that catalogue.
- `acceptScore` and `reviewScore`: a candidate group at or above the first may be accepted outright; one between
  the two goes to the queue; below the second, nothing was found.
- `minIndependentSources`: how many authority files must agree for acceptance without a person (two).
- `acceptSingleSourceWithDates`: whether one file alone is enough when the name is exact and the dates agree on
  both sides. On by default; a cautious project turns it off.
- `acceptUniqueExactSingleSource`: whether an undated string's one exact match in one file is accepted when it is the
  only candidate there is. On by default, since the alternative is a queue too long to work; such decisions carry the
  method `exact-single` and the project page offers them for review as a set.
- `sources`: which indexes to consult, in order.

Above the editor, *In effect now* says what the project is being read with: no manifest (anything goes), the PMC
library rules as the toolkit currently defines them, or the project's own rules and how they differ from those. The
library rules are built into the toolkit and change as it grows; *Replace with the PMC library rules* copies the
current version over the saved manifest, and the command line does the same with `manifest --use-default`. A context
may also set `qualified` (place names with their container, "Hammersmith, London"), `parts` (a list or a compound
heading made of strings of their own), `home` (the vocabulary whose one exact term decides: another vocabulary's one
exact term of the same class becomes an equivalent, several are set aside), `parseDatedOnly` (the agent
profile only for strings carrying dates) and `collections` (the vocabulary's own collections the strings belong to, in
order of preference: LCSH's `Subdivisions` for a heading's subdivisions, `LCSHAuthorizedHeadings` for its heads).

An example for a project whose records come from a library catalogue:

```json
{
  "contexts": {
    "persauthorfull": { "classes": ["person"], "agentProfile": "default" },
    "corpauthor": { "classes": ["group", "person"], "agentProfile": "default" },
    "place": { "classes": ["place"] }
  },
  "acceptScore": 0.9,
  "reviewScore": 0.6,
  "minIndependentSources": 2,
  "acceptSingleSourceWithDates": true,
  "sources": ["ulan", "loc"]
}
```

## 6. For operators: running things

Until the jobs pages arrive, the runs are commands in a terminal on the machine that holds the store, with the
store's connection string in the `LINKEDART_PG` environment variable:

| command | what it does |
|---|---|
| `tools/Align load-getty <folder> --vocab ulan` | loads a Getty vocabulary from its downloaded files (also `aat`, `tgn`) |
| `tools/Align load-loc <file.nt.gz>` | loads the Library of Congress name file |
| `tools/Align reconcile --limit 500` | works through up to 500 undecided strings: override, store, indexes, then accept, queue or mint |
| `tools/Align decisions --run <id>` | shows what a run decided, by field and method, with samples and evidence |
| `tools/Align undo-run --run <id>` | takes a run back entirely |
| `tools/Align shadow --sample 2000` | re-judges earlier decisions from scratch and reports where the software now agrees or disagrees, without changing anything |
| `tools/Align audit` | checks every entity against ULAN: missing ids, label and date mismatches, conflicting links, and ULAN's own errors |
| `tools/Align import-overrides <file.json>` / `export-overrides` | the curated file in and out |
| `tools/Align reindex-labels` | recomputes lookup keys after the matching rules change |

A reconcile report reads: *considered 500: overridden 7, matched in store 23, accepted 0, queued 88, minted 382*.
Overridden and matched were answered by the project's own knowledge; accepted by the authority files; queued is the
desk's work; minted is what no file knows.

## 7. What is not there yet

Running the commands above from the workbench with progress and history; a project wizard that chooses the source
adapter and collects strings from a dump; reading the dates and agent strings of the examples site's trainer on the
same desk; reports of what the audit finds about the authority files themselves, for sending to Getty or the
Library of Congress; pictures of coverage over time.
