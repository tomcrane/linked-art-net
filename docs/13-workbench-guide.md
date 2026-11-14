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
each action. The fold at the top, "What am I supposed to do here?", says the same as this section in a breath; Help in
the bar opens this guide.

At the top: the string as written, how often it occurs and in which field, and how the software read it: the name
part, the dates, roles and nationality it recognised; and "Currently", the entity the string points at today, which
for a queued string is a provisional placeholder minted from the string itself, waiting for your decision. Under
"In the records": up to three catalogue records the string was seen in, each with its title, date and record id, the
field the string came from, and the record's other naming fields, so that a corporate author can be told from the
sale catalogue it heads and a keyword from the books it describes. Below: the candidates, best first. Each card is
one authority record, or a group of records from different files that say they are the same thing, with its score,
the files that agree, the record's own label, dates and description, a link to the record, and the reasons the
software gave, such as "dates agree" or "a placeholder awaiting review: carries nothing". Greyed cards were ruled
out, and the reason says why; a greyed row keeps a quiet **accept anyway** for when you know better (a catalogue's
birth year two out from the authorities'), recorded as yours. A project entity on a card is named with where it is
when that is known ("Birmingham (Ala.)"), so two of one name can be told apart. When a record on a card has since
been given to one of the project's entities (an identifier added by hand, say), the row says "now held by" with a
Same as, without "Look again". On a card that is one of the project's own entities, the name is the link to the
entity's page, opened in another tab so the desk stays where it is, and the URI beneath it opens the URI, as every
record's does. The entity under "Currently" and the project's entities among the search results link the same way.

Actions:

- Each box is a group: one authority record, or several records the software says are the same thing, with the
  project's own entities among them when they hold those records. The group's button is its action; a row of a group
  of several has only a quiet **just this one**, for the rare case where one record is the answer and the rest are
  not.
- **Accept**: the string is this record. An entity is created for it, or reused if one already has that identifier.
  **Accept these as one**: the string is this group, one entity with all its identifiers. **Same as X** on a group:
  the string is the project's entity that holds the group's records, which gets any it does not hold yet.
- **Always**: the same, and this exact spelling will mean this from now on in any field. Use it for a spelling that
  recurs.
- **Same as this**: the string is an entity the project already has.
- When a card has no candidates at all, the desk runs the search for you under the string's name and shows the
  results under **Something else**. The rules look only among the kinds of thing the field allows (a person or a
  body for a publisher); the search looks at everything, so a house that published its own guidebook is found as the
  place the project already has.
- **Something else**: search the project's entities and the indexes for anything not among the candidates. The string's own provisional entity, the one under "Currently", is left out of the results: it is not something else.
- **Keep as local authority**: none of the candidates is right and the thing is real. Give it the label it should be
  published under and, if useful, a note, and a URL it is known at (an archive's agent page, a VIAF cluster), which is
  kept as its identifier. The software stops asking about it.
- **Accept the ticked together**: when two or more groups are the same thing (an auction house in ULAN and the same
  house as a person in the Library of Congress), tick "together" on each and accept them as one. One entity is made
  with all their identifiers. When two or more of the project's own entities are among the ticked, the desk first
  shows what will happen (which survives, which merge into it with their strings and identifiers, which identifiers
  are added) and asks; a merge is permanent, and Undo takes back the string's decision only.
- When the store has changed since the evidence was written (an entity here has since merged into another), a line
  above the candidates says so, the merged entity's row says what it is now part of, and a row that stands for an
  entity a row above already stands for says so; **Look again** shows the string as today's rules see it.
- A row that is one of the project's own entities says what it is (person, group, place; placeholder, aligned,
  confirmed), what put it in its group ("holding ulan's 500301722"; "a placeholder for 'Chris Beetles' in
  persauthorfull"), how many strings it stands for and in which fields, and the records it was seen in, so it can be
  judged without opening its page. When two of the project's entities hold records of one group, the authorities say
  they are one thing and the store has two: the group says so and offers to merge them and accept the string onto
  the survivor, through the same confirm step. The entities it means are boxed together under the sentence, and the
  merge button is at the foot of the box.
- A placeholder's page says so first. A placeholder is an entity the software made to stand in for a string nobody
  has decided; it belongs to the run that made it, and a label, class or identifier changed on it by hand, or a
  merge, is lost when the rules are next run. Decide its string at the desk instead (the page links to it): "Same as"
  an entity the project has, a record accepted, or "Keep as local authority". Those decisions are kept.
- An entity's page begins with what the entity stands for: its strings, the field each is in and the records each
  was seen in. An entity is whatever its strings mean. The label and the identifiers are there to describe that; when
  an identifier describes something else (a parliamentary constituency, on an entity that stands for a place of
  publication), remove the identifier, and do not relabel the entity to match it.
- On an entity's page, identifiers that cite one another (a Wikidata item, the VIAF and Library of Congress numbers
  it gives, a LUX record that names them) are boxed together under what they are, and "Remove all" takes a whole set
  off the entity. Two boxes that describe different things mean the entity is aligned to two things: remove the
  set that is not what the entity's strings mean. A VIAF number says which record gives it, since VIAF's own page
  rarely says what it is.
- Wherever the desk names an identifier an entity holds ("wikidata's Q3403624"), it is a link to the record, followed
  in brackets by what the record is when the store knows: its label and description. Two entities of one name whose
  identifiers describe different things (a town and a parliamentary constituency of the same name) are not
  duplicates in the authorities' eyes, whatever the project meant by them; read the brackets before a merge.
- **Also waiting under this name**, above the records: the other queued strings of the same name, in any field, each
  a link. After an action the desk goes to those first, and says so in the message, before it moves on down the
  list, so a name is finished before the desk leaves it. The entity page lists the same under "Waiting at the desk
  under this name".
- **Skip for now** sends it to the back of the desk: it comes round again after everything else, in this session or
  the next; on a skipped string the button reads **Unskip**, which puts it back in its place. **Add note** leaves a message for another reviewer. **Undo last decision**
  puts the previous decision back.
- After an action, the message at the top of the next item names the entity it made or chose, with a link, so you
  can add an identifier or a note there before going on. The entity page takes an identifier as a URI, or searches
  the local indexes for it, each result with "Add as identifier" (a Wikidata record the desk's search turned up,
  say).
- **Look again** (beside the decision date) shows what today's rules would offer for the string, without deciding
  anything. A decision shows the evidence stored with it, so a string decided before the rules changed, or by hand,
  may show more now. The actions act on what you see.

The dropdown at the top switches from the queue to the strings the software minted (nothing found), which can be
reviewed the same way, and to those a person already decided.

## 4. Entities and variants

**Entities** lists the project's entities with a search box and filters by state and class. An entity's page shows
its labels, dates, identifiers with the authority record behind each, the catalogue records its strings were seen
in ("In the records", as on the desk, the most frequent strings first, eight records at most), the strings that
resolve to it, and notes. A
reviewer can rename it, change its class (person, group, place, type, period, event: an identifier added by hand can
say what the field could not; the change is noted on the entity, and the identifier keeps its path), add an identifier
(a URI from ULAN, LoC, VIAF, Wikidata, AAT, TGN or LUX), remove one, confirm it, merge it into another entity found by
search, add a note, or *reopen* chosen strings, which takes them away from the entity so the software reconsiders them
next time. On a placeholder the class card says to prefer merging into the entity of the right class, or deciding its
waiting string there at the desk, over changing the class. When the two entities of a merge differ in class, the merge
row on the entity page and the confirm step on the desk offer the merged entity's class; the survivor's stands unless
chosen.

Every change made by hand is written as a note on the entity, with before and after: a label or class changed, an
identifier added or removed, a confirmation, a cluster, a merge (on both entities), strings reopened. The entity page's
"History and notes" shows them, newest first, with the decisions a person made on the entity's strings; the project
home's "Recent changes" shows the latest across the project, each with a link to the entity or the string.

An identifier another entity of the project already holds is refused, with the holder named, and the page lists that
entity under Merge so that, if they are one thing, this entity merges into it in one click rather than becoming its
twin; the same holds for a URL on "Keep as local authority" and for identifiers given with a cluster.

**From the sources.** Below the identifiers, the page can show what the aligned sources say about the entity: Wikidata's
description and aliases, the opening of its English Wikipedia article, its picture from Wikimedia Commons with the
author and licence Commons asks to be shown; ULAN's biography, dates and nationality; the Library of Congress's
variants and the sources its heading was "found in"; LUX's names, statements and what LUX holds it the same as.
Nothing is fetched to show the page: press "Pull in from the sources" (or "pull" on one card) and the answers are
kept on the records and shown from then on, dated, with "refresh" to ask again. Every fact is the source's own; a LUX
record that carries one of the project's own identifiers is marked as our earlier export reflected back, not a second
witness. On the desk, "show pictures" in the line above the string puts the pulled thumbnails and descriptions on the
cards whose records have them; it is off by default, since most cards have none.

A page opened before signing in bounces to the sign-in page and comes back to it after.

**The tray.** When the same thing turns out to be several entities found on several pages (the Ackermanns: a firm, a
placeholder, a misspelling), collect them: "Add to tray" at the top of an entity's page, "Tray" at the end of a row
in the entities list or beside a project entity on a desk card. The tray is a bar at the foot of the window, kept in
your browser for this project; "Merge these" opens the merge page, which shows the collected entities side by side,
suggests the survivor (the one with most strings, then the firmer status, then more identifiers), lets you choose
another, the label after the merge and, when they differ, the class, and merges the rest into it with one button.
Every merge is written on both entities' histories, and the losers' ids keep resolving to the survivor.

**The map.** An entity any of whose records places it (TGN's coordinates, or Wikidata's or LUX's after a pull) shows
where it is on a small map at the top of its page. On the desk, when any candidate for a place name is placed, one
map above the cards shows them all, a marker each with its label and where TGN puts it, the best group's markers
larger: the St Iveses apart, so the right one can be picked. A record TGN gives no coordinates (castles and buildings,
about one record in 160) stands at its TGN parent as a hollow marker, "near Windsor"; a Library of Congress or project
candidate, which never carries coordinates, stands at TGN's record of the same name in the same state when there is
one, hollow and labelled so; and a place with none anywhere
is named under the map, so a missing marker is never read as a missing candidate. The tiles are OpenStreetMap's;
hover a marker for its label when there are too many to show at once.

An identifier added by hand is in force at once for the next reconcile, for "Look again" and for the searches, and a
queued string's card says "now held by" on the record's row. On an entity's page, a string still queued links to its
desk page from the decided column, and the title's count includes it.

Merging a placeholder into another entity settles its strings: each string that was queued or minted on the
placeholder is decided onto the survivor as your decision, with the merge as the reason, and leaves the queue; Undo on
such a string puts the earlier decision back. So going from an entity's page to search for its variants and merge
them in is as good as deciding each at the desk.

A compound subject heading ("Ackermann & Son, Arthur (London)--History") is a concept of the project coordinated from
its parts, each a string of its own with its own decision. The entities list tags such an entity *compound*, and its
page lists the parts with what each resolved to; the heading holds no identifier itself.

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
- `homePlace`: where the project's places mostly are ("United Kingdom"), so a county name several countries share
  resolves to the one here.
- `trades`: the trades of the catalogue as an authority record's biography names them ("auction", "dealer",
  "bookseller" for a sale-catalogue library). A name with an honorific, "Mr Farebrother", that one tied record bears
  whole as a name is accepted when the record is a body or a person of one of these trades; a person of no listed
  trade is shown on the desk and left to the reviewer. Empty, no person is accepted by it.

Above the editor, *In effect now* says what the project is being read with: no manifest (anything goes), the PMC
library rules as the toolkit currently defines them, or the project's own rules and how they differ from those. The
library rules are built into the toolkit and change as it grows; *Replace with the PMC library rules* copies the
current version over the saved manifest, and the command line does the same with `manifest --use-default`; a manifest
saved either way is in force at the workbench's next request, with no restart. A context
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
| `tools/Align reconcile --limit 500` | works through up to 500 undecided strings: override, store, indexes, then accept, queue or mint; `--verbose` shows each string as it is decided, `--candidates` the candidates too |
| `tools/Align decisions --run <id>` | shows what a run decided, by field and method, with samples and evidence |
| `tools/Align undo-run --run <id>` | takes a run back entirely |
| `tools/Align merge-duplicates [--class Place] [--apply]` | lists the project's entities that share a label and class and what would merge; `--apply` merges them |
| `tools/Align shadow --sample 2000` | re-judges earlier decisions from scratch and reports where the software now agrees or disagrees, without changing anything |
| `tools/Align audit` | checks every entity against ULAN: missing ids, label and date mismatches, conflicting links, and ULAN's own errors |
| `tools/Align import-overrides <file.json>` / `export-overrides` | the curated file in and out |
| `tools/Align reindex-labels` | recomputes lookup keys after the matching rules change |

A reconcile report reads: *considered 500: overridden 7, matched in store 23, accepted 0, queued 88, minted 382*.
Overridden and matched were answered by the project's own knowledge; accepted by the authority files; queued is the
desk's work; minted is what no file knows.

For an operator, `settle-merged` on the command line lists the strings whose queued or minted decision points at an
entity that is no longer a placeholder (merged in before merges settled strings), and `--apply` decides them.

Worked examples of common situations at the desk, each with pictures and what to do, are in
[the worked scenarios](16-workbench-scenarios.md).

## 7. What is not there yet

Running the commands above from the workbench with progress and history; a project wizard that chooses the source
adapter and collects strings from a dump; reading the dates and agent strings of the examples site's trainer on the
same desk; reports of what the audit finds about the authority files themselves, for sending to Getty or the
Library of Congress; pictures of coverage over time.
