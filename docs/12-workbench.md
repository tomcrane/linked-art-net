# Design: the alignment workbench (`LinkedArtNet.Workbench`)

Status: design, 2026-10-18. The human side of the toolkit in doc 10: where a collection specialist gives judgement on
authorities, dates and agent strings, contributes what only they know, and runs the pipeline without a command line.
Section 11 is the order to build it in; section 12 records the owner's decisions and the questions still open.
Section 14 (2026-11-15, a design not yet built) is the record as a thing in the store: decisions for one record, the
cataloguers' list, and the pictures of the work.

## 1. What this is for

The toolkit now decides most strings on its own and records why. What it cannot do is the part that needs a person
who knows the collection: that the twenty-four spellings of Agnew's in the sale catalogues are one firm; that
"Scottish Gallery" is a dealer in Edinburgh and neither national gallery; that a dealer ULAN has never heard of is
real and should stay a local authority; that a date string's odd form means what the cataloguer meant. Today those
judgements arrive through CSV files and command-line runs. The workbench puts them on a screen, one item at a time,
decided in seconds, with the evidence beside the question and every answer written back to the store as a decision,
an override or a merge that the pipeline honours on its next run.

It is also the operator's console: loading an index, importing a curated file, reconciling, shadow-running, auditing,
undoing, and setting up a new source as a project, are jobs with progress and history rather than commands to type.

## 2. Principles

- **Evidence beside every question.** The reviewer sees what the reconciler saw: the reading, the candidates, their
  scores and reasons, the sources that agree, the external records a click away. No decision is asked for blind.
- **Everything a person says is kept and honoured.** An acceptance is a decision with method `human`; a cluster of
  variants is a set of overrides; "this is local" is a positive decision, so the pipeline stops asking. Nothing a
  person decided is overwritten by a run; a run can only propose.
- **Undo is cheap.** Runs are undoable already; human decisions are too, because a decision is invalidated, never
  deleted, and the previous one stands again.
- **Fast when it can be.** Keyboard first, one item on screen, the next loading while this one is decided, bulk
  actions over a filtered list when the reviewer has seen enough to trust a pattern.
- **Projects, not a PMC tool.** PMC is one project with its own members, identifier base, context rules and sources.
  Wellcome is the next. The owner belongs to several; most reviewers to one.
- **Deterministic service, assisted review.** As for dates (doc 10, decision 6): the model proposes, a person
  confirms, the provenance says which.

## 3. Where it lives

A separate ASP.NET Razor Pages app, `src/LinkedArtNet.Workbench`, same stack and look as the examples site, talking
to the PostgreSQL store through `IAlignmentStore` and the reconciler, loaders, shadow and audit as services. The look
is the site's own, not a cousin of it (the owner, 2026-11-05: the workbench, like the site, is for people who work in
the arts and should not look cooked up): the site's stylesheet's tokens and conventions, one grotesque face, near-black
on paper, one warm accent, category labels in small capitals, a strong rule above each section, square corners, black
buttons; the workbench's own pieces (the string under review set like a title, the candidate cards each under a rule
each a flat highlight panel with the best on the warm tint, the score as a bold figure, the file marks as small
capitals on the tint, the actions the only solid or outlined rectangles) are drawn with the site's parts, in
`wwwroot/site.css`; the owner's eye set each of these on the day, against the first draft's boxes and blocks. It is not
part of the examples site: that site is public, stateless and deterministic; the workbench is signed-in, stateful and
holds a database connection. It runs on the owner's laptop first (section 10) and is packaged to run anywhere a
PostgreSQL is.

The dates trainer (`/dates/train` in the examples site) moves into the workbench as one kind of queue item and keeps
its behaviour: corpus entries built from review, pull requests to the repository, the interpreter's proposals. Its
services (`Trainer`, `TrainerStore`, `GitHubSubmitter`, the interpreter host) move to a shared project,
`LinkedArtNet.Review`, that both apps can reference during the transition; the examples site's trainer pages are then
retired and `/dates/train` redirects to the workbench. The agents playground stays public in the examples site.

## 4. Users and projects

**Project**: a dataset in the store's sense, with a name, an identifier scheme (base and path segments), a manifest
(sources and adapters, context rules, thresholds, indexes used, LLM budget), members, and runs. The PMC project is
created from what the store already holds. **User**: signed in (section 9), with a display name used as the reviewer
on decisions. **Membership**: a user's role in a project, `reviewer` (decide items, curate variants, propose), `operator`
(also run jobs, load indexes, import files, undo runs, edit the manifest) or `owner` (also manage members). A user with
no membership sees nothing of that project. Indexes (ULAN, AAT, TGN, LoC) are shared across projects; loading them is
an operator action in any project.

Store additions: `users`, `projects` (the manifest as JSON; dataset is the project key), `memberships`, `jobs`
(section 7), `notes` (free text a reviewer attaches to a string, an entity or a run), `assignments` (optional: an
item claimed by a reviewer for a while, so two people do not decide the same string). Decisions gain `reviewer` from
the signed-in user; they have the column already.

## 5. The review desk

The default screen for a reviewer: the queue for the project, filtered by kind (authority, date, agent), context,
score band, run, assignment, and a search box; a count of what is left; and the current item.

**An authority item** is a queued string. The card shows:

- the string as written, its contexts and occurrence count, and up to three example records from the dump (title,
  date, the field it came from) so the reviewer knows what kind of thing is being named;
- how the parser read it: name, dates, roles, nationality, honorifics, with the unconsumed remainder highlighted;
- the candidate groups, best first: each group as one card with its score, the sources that agree, each member's
  label, dates, type, description and a link to the external record, and the reasons the scorer gave ("dates agree",
  "type Group not allowed here"); vetoed members shown greyed with their veto;
- entities already in the store that resemble the string, from the store stage, marked as such; a card that is one
  of the project's own entities carries its class and status, what put it in its group (the record it holds, or the
  string it is a placeholder for), the strings it stands for and the records behind them (2026-11-06, the owner at
  "Chris Beetles Ltd": two aligned entities and two placeholders of the name in one group, told apart only by their
  pages); a group holding two firm entities of the project's says the store disagrees with itself and offers the
  merge as its own action, through "Accept the ticked together", which with two or more of our entities among the
  ticked shows the plan (survivor, merging, identifiers added) and asks before it is done; the other queued strings
  under the string's name (its key, its reading's keys, its entity's labels) are listed on the item, and after an
  action the desk goes to them before it moves on down the list (2026-11-06, the owner after the first merge: the
  desk had moved on while the name still had work in the queue); the entity page lists the same as waiting; the group
  is the card: its tick and its action ("Accept these as one", "Same as X" when it holds the project's entity, "Always,
  in this field") are in its header and the rows of a group of several carry only a quiet "just this one" (2026-11-06,
  the owner at the next Chris Beetles string: ten actions on the rows of a box that is one thing); and when an entity
  in the evidence has merged since it was written, the desk says so above the candidates and on the row, and marks a
  row that stands for an entity a row above already stands for;
- the model's proposal when one has been asked for, with its capped confidence and the note it gave.

Actions, each a key: **accept** a group (decision `human`; the entity is created or reused exactly as a vote would do
it, with the group's ids as equivalents); **same as** an existing entity, found by search (the provisional entity
merges into it); **local**: none of these, keep the provisional entity as a local authority, optionally renaming it and
adding a note ("Edinburgh dealer, not the national gallery"), recorded as a human decision so the string is settled;
**override** this spelling, or every string sharing its comparison key, to the chosen target; **defer** (later);
**flag** for another reviewer with a note; **ask** the model. Undo re-opens the previous state.

**A date item** and **an agent item** are the trainer's review pages as they are today, restyled into the desk: the
parsed reading, a correction form, the canonical preview, the interpreter's proposal, accept or save, with the corpus
entries and pull request built as now.

**The records behind the string** (built 2026-10-26 evening, the desk's first item, from the owner's first question at
the desk: "Messrs Langford" in corpauthor could not be told from its catalogue record). The store keeps up to three
example records per string in `string_examples`: the record's id, title and date, the element the string came from,
and the record's naming fields as JSON. `tools/Align collect-examples --file <library export .xml>` fills it from a
Heritage library export (DScribe's MAIN_CAT XML, one record element per work), yielding each record's strings the way
the 2024 pmc-2 processor did (persauthorfull and corpauthor values, a trailing publisher role routing to publisher and
addressee to keywords; the publisher element; keywords and their " -- " parts; the place element with its brackets
trimmed, the notes' (HIE) hierarchical places, and the parts of a place list) and matching them to the store's strings
by context and normalised text; a source collected again is replaced. The desk shows the examples under the string as
"In the records", the string's own field first on each card; a part string with none of its own (a keyword's head, a
place in a list) shows its parents'. The entity page shows the same for the strings that resolve to the entity (the
most frequent first, a record once, eight at most), and on the desk's cards, under "Currently" and in the search
results a project entity's name is the link to its page, the URI beneath it the URI's own link, the two told apart by
their words (2026-11-05: three placeholders of one name beside "Mr Stanley", told apart only by their records; a
separate "entity page" tag was tried the same day and was loud). Not reached: keywords the 2024 process cleaned before the string map was made
(the cleaned form is the store's string and the export carries the raw one), which the coverage line of the command
counts; the cleaned subjects table of the legacy database would close that.

**Bulk**: with a filter applied, select items and apply one action, for the cases where a pattern is obvious once seen:
"all queued corpauthor strings whose single ULAN candidate is an exact label match, accept". The evidence is still
recorded per item, with the bulk action noted.

## 6. Collection knowledge: the variants view and entities

**Variants** groups the project's strings by comparison key and near matches (the name matcher's scores above a
threshold) so a cluster appears as one screen: every spelling of Agnew's, with counts and contexts, the entity or
entities they currently resolve to, and the external candidates for the cluster. The reviewer ticks the variants that
are one thing, picks or creates the entity, attaches external ids by searching the local indexes inline (ULAN, AAT,
TGN, LoC by label), or marks the entity as local with a note. Saving writes the overrides and the merges. The overrides
export to the pmc-lux JSON shape (`local_string_map` and `authorities`), so the curated file becomes an output of
review.

**Entities** is the browse and search view: label, class, status, equivalents, the strings that resolve to it, the
decisions and their evidence, notes. Actions: edit labels and lifespan, add or remove an equivalent, merge into
another, split (re-open the decisions of chosen strings), mark confirmed. Each entity's equivalents draw as a small
graph: the entity, its ids, and the links the authorities assert between them, so a link conflict is visible as a
triangle that does not close.

## 7. Jobs, runs and indexes

Long work runs as a **job**: a row in `jobs` (project, kind, parameters, state, progress text, started, finished,
result, error, the user who started it), executed by a hosted background service inside the app, one at a time per
database, with progress pushed to the page (the loaders and the reconciler already report progress). Kinds: load an
index (Getty vocabulary from a folder, LoC names from a file or URL, the download itself as part of the job),
import overrides, import a legacy table, collect strings from a dump (step 4), reconcile, shadow, audit, export.
A job can be cancelled; a reconcile can be undone from its run.

**Runs** lists each run with its report, its decisions by context and method, and for a shadow run the comparison
table by verdict and context with the differing items as a review list rather than a CSV. **Indexes** shows each
index with its source version, record count, load date and a load button; the Getty download page and the LoC
download page are linked, since both refuse automated fetches.

## 8. Projects and new sources

**New project** is a wizard: name and description; identifier base and path segments (the `IdentifierScheme`);
the source adapter and where its dumps are (DScribe XML, the Heritage library export, MARC, the Wellcome API, CSV;
step 4 provides these); the contexts found in a sample of the dump, each given a class list and an agent profile,
with the agents playground inline to try real strings from the sample; thresholds and the single-source rule;
which indexes to use; the LLM budget. Saving writes the manifest. The project page then offers the stages as
buttons with their last run: collect strings, reconcile, review (the desk), export Linked Art, and for PMC the
re-export to LUX with the self-echo mapping. Wellcome is the second project and the test that nothing is PMC-shaped.

## 9. Sign-in

GitHub OAuth with per-project membership, as the trainer does today with its allow-list, plus a local development
sign-in. GitHub suits the first users, who already have accounts and whose corpus contributions go there anyway;
an institution that wants its own identity provider later adds a scheme, since memberships are the store's and not
the provider's. The first user is the owner, who creates projects and adds members.

## 10. Hosting

The laptop is home for the first months: the app runs against the local PostgreSQL that holds the store and the
indexes, which is where the gigabytes are. It is packaged as the other apps are, a Dockerfile and a compose file with
PostgreSQL, so it can move to a machine with the disk and memory the indexes need when more than one person needs it
at once. It will not share the small virtual server that runs the examples site. Secrets (GitHub client, the
Anthropic key for proposals) come from the environment, as everywhere else.

## 11. Order of work

1. **The review desk for authority items**, read-only first, then the actions: accept, same as, local, override,
   defer, undo. Sign-in and the PMC project created from the store. Uses the 88 queued strings and the shadow and
   audit items that exist now. Decision method `human` and the entity handling shared with the reconciler.
   *Built 2026-10-18 (first cut): `src/LinkedArtNet.Workbench` with sign-in (GitHub or a typed name in
   development), projects with members and roles, the project overview, and the desk with accept, always
   (override), same as, keep local, note, skip and undo; `ReviewService` in the Alignment library carries the
   actions; schema version 2 adds users, projects, memberships and notes. Run it with `dotnet run --project
   src/LinkedArtNet.Workbench` and `LINKEDART_PG` set (the launch settings put it in Development on
   http://localhost:5200); the development settings allow a typed sign-in and make the name `local` an owner. Tests in `tests/LinkedArtNet.Workbench.Tests` drive the pages over HTTP against a
   throwaway PostgreSQL.*
2. **Entities and variants**: browse, search, merge, overrides, the inline index search, the pmc-lux export.
   *Built 2026-10-18 (first cut): `/p/{dataset}/entities` with search and filters; `/p/{dataset}/entity` with
   labels, identifiers (add a known or recognised authority URI, remove one), the strings that resolve to the
   entity with reopen, confirm, merge into another entity found by search, notes; `/p/{dataset}/variants` lists the
   strings containing a seed's distinctive word grouped by what they resolve to, and clusters the ticked ones into
   an existing entity or a new confirmed local authority, attaching identifiers from an inline index search, with an
   override per spelling; `/p/{dataset}/export/overrides.json` writes the project's overrides in the pmc-lux shape.
   `CurationService` in the library carries the actions; `tools/Align export-overrides` and `cleanup-orphans`.*
3. **Jobs, runs and indexes**: the background service, progress, undo, the comparison table for shadow runs, the
   audit as a review list, the index loads with download.
4. **Projects**: the wizard and the manifest, together with the pipeline runner (doc 10, step 4), PMC first.
5. **Reports outward**: what the audit finds about the authorities themselves, exported for Getty and LoC, with a
   record of what was reported; candidates for contribution.
6. **The trainer moves in**; date and agent items on the desk; the examples site's trainer pages redirect.
7. **Pictures**: coverage by context run over run, the equivalents graph, places on a map.

Steps 1 to 3 need nothing from step 4 and are the first thing a reviewer can use.

## 12. Decisions and open questions

Decided by the owner, 2026-10-18:

1. **A separate app**, not part of the examples site.
2. **A wider user base, by project.** PMC is one project; others follow with their own users; the owner is a member
   of several.
3. **The laptop is home to start with.** The examples site's virtual server cannot host this.

Open:

- The app's name. `LinkedArtNet.Workbench` is the project name here; the name people see is the owner's.
- Whether items are assigned to reviewers or any member may take any item. The design allows both; the first build
  does without assignments.
- ~~How much of a dump's records the desk shows as examples, and from where~~: three a string since 2026-10-26
  (section 5, "The records behind the string"); all of them, as occurrences, in section 14.
- Whether the model's proposals are asked for automatically for every queued item within the project's budget, or
  only on request, as in the trainer now.

## 13. The earlier Authority Server (ual-dac), compared (2026-11-08)

The owner asked, as the project's entity store grows into the local authority librarians and archivists will correct
MARC and CALM records against, that nothing from the earlier Digirati authority server for University of the Arts
London be lost (`digirati-co-uk/ual-dac`, `docs/tools/items-and-authorities.md`, read with its code on 2026-11-08).
That server was a Quart app over Postgres: an authority is a cluster of *sources* (`source:type:id`, each with the
label it bears in its system and a `seeAlso` link), with a label and a type (Agent, Category, Period, Medium); the
synchroniser attached incoming source records to an authority by exact label and type or made a new one; a person
merged authorities through a *tray* collected from any page, chose the surviving id, label and type, and saw the
history; items linked to authorities by role, coverage or property, and an authority's page linked to its usages.

What the workbench already has, and mostly better: decisions per string with evidence rather than a label-and-type
attach; a merge that keeps the loser's id resolving (`merged_into`) where theirs hard-deleted the row, so old ids
returned 404; labels that follow a merge, so "Clemens" still finds "Mark Twain" in both; the strings and the catalogue
records behind an entity on its page; undo of a decision; roles per project; the manifest; runs. Their `seeAlso`
data was inlined into API responses only for the UAL systems' own files, never fetched for Wikidata, which is the
feature the owner now wants built the other way round (on demand, cached; section 7 of the plan, Workbench and desk).
Built the same day: `SourcePull` fetches Wikidata (with Wikipedia and Commons), ULAN, LoC and LUX on "Pull in", caches
the answer on the record, and the entity page shows a "From the sources" panel from the cache.

What it had that the workbench lacks, taken as items (plan section 7, Workbench and desk):

1. **Recent changes** on the home page (their last 200 history rows: id, label, source, change, by, date) and a
   **history** on the authority page (per source: change type, by, when, previous id). Ours has "Recent runs" and the
   notes. The shape for us: notes as the audit log, written by the system for every hand change with before and
   after ("Class changed from Person to Group", as the class control does already; the label, an identifier added or
   removed, a merge and what merged), so the entity page's History is its notes and the human decisions on its
   strings, and the project home's Recent changes is the same across the project. Theirs stored no before and after,
   which the owner's "it was X all along" case needs.
2. **A tray for merging across pages**, held in the browser (theirs in `localStorage`), filled from search results,
   list rows and entity pages, acted on at a merge page with the survivor, label and class chosen; our merge is from
   one entity's page by search, or the desk's together tick on one card. The owner's walk through the Ackermanns
   ("once I was in the various Ackermanns it seemed the easiest flow to go back and search for more") is the case.
   Built 2026-11-08: `wwwroot/tray.js` and the merge page, `Pages/Projects/Merge.cshtml`.
3. **An identifier in use says so.** Their Add form checked live whether `source:type:id` already belonged to an
   authority and linked to it. Our "Add identifier" on an entity page writes the equivalent without asking whether
   another entity holds that URI, which would make a twin; it should say "held by X" and offer the merge.
4. **The survivor's id chosen for the bookmarks' sake**: their merge page let the person keep the id already in
   circulation. Ours chooses the survivor at the desk's confirm step and by direction on the entity page; the thought
   to keep is that the published id should win, which the lifecycle items (publication as a recorded act) make
   decidable.

And one thing neither had, from the owner on the same day: a map. An entity that is a place shows where it is, and
wherever namesake places stand side by side (the desk's cards, a merge tray, the entities list filtered to places)
they stand together on one map, since seeing the St Iveses apart is how a person picks the right one. Coordinates
from TGN's record and Wikidata's P625, cached on the record; a small Leaflet map over OpenStreetMap tiles with the
attribution kept; one marker per candidate, the chosen one emphasised; no map when nothing has coordinates. Built the
same day on the entity page and the desk (`_Map.cshtml`, `Authorities/Coordinates`); the entities list and the tray
still to come.

What not to take: the attach-by-exact-label rule at ingest (arbitrary when several match, no review); history without
values; ids that could be re-minted after a merge; no export, no bulk operations, no split, no roles. The identifier
scheme (8 characters, no 0/o/i/l/1, a letter first) is close to ours.

## 14. Records: decisions for one record, the cataloguers' list, and seeing the work (design, 2026-11-15)

Status: a design talked through with the owner on 2026-11-15, not built. It follows the owner's ruling of that morning
at "Plymouth" (plan section 7, "One spelling, more than one record"): the toolkit serves two ends, alignment so that
records can be turned into Linked Art, and finding what a source could say better; a decision for one record is
required and not an edge; and "the whole process has to survive corrections and improvements in source data, including
those that were made as a result of people using the tool". PMC's library export is the worked case and must not
dictate the shape; where a choice below is PMC's and not the design's, it says so.

### 14.1 Source systems and records

A project draws on one or more **source systems**: for PMC the library catalogue, the archive catalogue and the
photographic archive; for another project a museum catalogue beside them. The manifest describes each: a short key
(`library`), a name people see, what its record identifier is in the cataloguer's words ("standard number"), and a
URL template the identifier fits into where the system has pages to link to (`https://.../record/{id}`; optional,
"this would be a project-specific setting and may not always be possible", the owner). A **record** is named by
source system and identifier together, never by the identifier alone. A **collect** is one reading of one export of
one system, dated; today's `source` on an example ("library-2024-03") runs the system and the export together, and
they part.

The identifier is taken as unique within one export of one system and **not as permanent**. In PMC's library export
(profiled 2026-11-15, 63,392 records) the record ID is the standard number: a valid ISBN for some 18,600 books, a
lettered number by kind of material for the rest (D for texts and exhibition catalogues without an ISBN, E for the
photographic archive's folders, S, T, ZA, ZB and others for auction catalogues), and 117 that look like ISBNs and fail
the checksum. An ISBN that a cataloguer corrects is a record whose identifier changes. The owner: "I think we have to
assume this is the norm."

### 14.2 Occurrences

The store keeps every **occurrence**: this string, in this field, of this record of this source system. It is what
the transformation to Linked Art reads (this record's publisher is that entity) and what a decision or a finding may
be about. `string_examples` (three records a string at most, 124,065 rows over 38,858 of the 63,392 records) is a
sample of it and is replaced by it.

    records      (dataset, system, record_id, title, date, fields jsonb, fingerprint, first_collect, last_collect, gone_at)
    occurrences  (dataset, system, record_id, field, string_id, position, parent_string_id)

`fields` is the record's naming fields as the desk shows them now; `fingerprint` is a hash of what a record says
apart from its identifier (title, date, the naming fields), used in 14.5. A collect replaces a system's occurrences and
marks records it no longer finds as gone, keeping the row.

### 14.3 A decision at two levels

The string's decision stays as it is and is the default for every occurrence. An occurrence may carry its own, which
wins for that record: "Cambridge" is Cambridge in England, and in this Harvard University Press book it is Cambridge,
Massachusetts.

    record_decisions (id, dataset, system, record_id, field, normalised, entity_id, method, evidence, reviewer,
                      decided_at, run_id, valid, supersedes, closed_at, closed_why)

It is keyed on the record and the field and the string's normalised text, not on row numbers, so that it survives a
collect. **It may be for a part** (the owner, 2026-11-15: "A record decision should be able to apply to one part, I
think"): the string is whichever string the store decides, the whole field value or a part of it (one place of
"Lewisburg [Pa.] ; Plymouth", the head or a subdivision of a subject heading), so `normalised` is the part's and the
occurrence of a part is stored beside its parent's (`occurrences` gains `parent_string_id` for a part, null for a
whole value). A part decided for a record leaves the field's other parts on their strings' decisions. It closes at a
collect when the record's field no longer yields that part, however the rest of the field changed. It has the same
lifecycle as a decision (invalidated, never deleted; undone by undoing its run or by hand).
**Who may make one:** a person at the desk, and the rules where they can be trusted to ("the rules should, if we are
confident that they will work. As long as someone can check what's happened", the owner). A rule's record decision
carries its method and its evidence as any decision does (which other field decided it: the publisher, the other
places on the imprint, the class mark), and every string that has been split by a rule is listed for checking (14.7).
The first rule to try is the one the count of 14.8 supports, and it is measured as findings are: count, build, run,
read every decision it made.

### 14.4 Findings: the cataloguers' list

A **finding** is something the source could say better, seen by a person at the desk or the entity page, or by an
audit in bulk. The project's findings are a standing list, "persistent and acts as a task list for cataloguers" (the
owner), not a report run now and then.

    findings (id, dataset, system, record_id, field, value, kind, suggestion, why, string_id, raised_by, raised_at,
              status, status_by, status_at, closed_collect, value_after, note)

`value` is what the field said when the finding was raised; `kind` is from a short list a project can add to
(ambiguous name, two values in one field, truncated, wrong field, wrong class, spelling, supplied in brackets, a
link to the wrong authority record held in the source's own data); `suggestion` and `why` are free text, filled in by the desk where it can (the entity the string
was decided as, the other candidate). `status` is open, sent, mended as the person says, changed in the source,
declined, or still a problem.

Two activities, apart and together (the owner: not known who works the list, it will vary; "in a small institution it
may be the same person who has the workbench open AND the catalogue management tool open at the same time, so should
be nice and usable for them too"):

- *Apart.* The list exports as a spreadsheet (CSV), a row a finding, by source system and record identifier, with the
  link where there is a template; exporting marks the rows sent, with the date. Nothing comes back but the next export
  of the source.
- *Together.* The list is a page of the workbench, filtered by system, kind, status and field, ordered so that one
  record's findings stand together. Each row has the identifier with a copy button, the link into the source system,
  what is there and what is suggested, and "I've mended this", which the next collect confirms or contradicts.
What is wrong in an external authority (a Wikidata item that gives another place's LoC number, two LoC headings for
one body, a TGN record without coordinates) is not a finding of this list: it is not the cataloguers' to mend and
does not close when the project's sources are collected again. It is a different list (the owner, 2026-11-15:
"Problems with external authorities sound like a different list to me"), the reports outward of section 11 step 5,
by authority and not by source record, with its own record of what was reported and to whom.

- *Raising one* is one step where the thing is seen: on a record under "In the records" at the desk, on the entity
  page, and from an audit's list in bulk (the mislabels, the comma-joined places, and the old import's wrong Wikidata
  links where the source's own data still holds them). Deciding a record differently from its string (14.3) offers to raise the finding in the same step, since an
  exception is usually also something the catalogue could say.

### 14.5 What a collect does

A collect is where the source's own corrections arrive, and where the round trip is kept honest:

1. Records are matched to the last collect's by identifier. A record whose identifier is gone is looked for among the
   new identifiers by fingerprint; one match of the same fingerprint is the same record renumbered, and its record
   decisions and findings move to the new identifier, with a note. No match, or several, goes to a short list for a
   person ("these records are gone; these are new and look like them"). Nothing is dropped silently.
2. A string no record bears any longer retires: its decision is kept, marked as having no occurrences, and stops
   being counted or asked about. If the string comes back, the decision stands again.
3. A new string is reconciled as now. A string the desk has already decided that appears in a new record inherits
   the decision; that is what a string-level decision means.
4. A record decision whose record no longer bears the string in that field is closed, "the source changed", with what
   the field says now. It is not carried to the new string: if the cataloguer wrote "Cambridge, Mass.", the new string
   is decided on its own evidence.
5. A finding whose field changed is closed as "changed in the source" with the value after, and is reopened as "still
   a problem" if the new value is itself queued or has a record decision against it. (The owner had no view on
   whether a change should close a finding; this is the assistant's default, to be watched in use.) A finding marked
   "mended" by a person whose field has not changed is shown as contradicted.
6. Entities keep their identifiers through all of it.

The measure is the round trip of the owner's ruling: decide, list, correct the export, collect again, and nothing a
person decided is asked twice.

### 14.6 The desk and the entity page

"In the records" shows all of a string's records, not three: grouped where their other fields point different ways
(by the field a rule or a person would split on: publisher, the other places, class), the largest group first, each
group folded past its first few. A group, or one record, can be decided as something else ("in these records it is"),
and a finding raised on it. The record's identifier is a link into its source system where the manifest has a
template, here and wherever a record is named. The entity page lists the records that reach the entity by a record
decision apart from those that reach it by their string.

### 14.7 Seeing the work

The owner: "any UI ideas you have about visualising/understanding the work are important, I feel this is an unsolved
problem". Four, in the order to build; the first three are part of this design, the fourth follows from 14.4.

1. **The record is the unit of progress.** A record is ready when every occurrence in it is decided (by its string or
   for itself) onto a firm entity. The project's home says how many records are ready, by source system and by kind of
   material, and the queue can be ordered by how many records a string holds back, so that the next decision is the
   one that frees most. "Ready" is defined per project by which fields the transformation needs.
2. **What a run changed.** A page for each run: strings moving between states since the run before (accepted, queued,
   minted, matched in the store), each flow a list to open; the candidates that stood before and do not stand now,
   by the rule that set them aside; the record decisions the rules made. This is the reading the assistant does by
   hand with SQL after each rule's first run, and it is what caught finding 124's fault at "Glasgow (Strathclyde)".
3. **A split seen as a split.** On a string's card and its entity's page, the records in their groups with the entity
   each group goes to and what decided it, a rule's splits marked for checking with "right" and "wrong" on each. A
   list of every split string in the project.
4. **The loop over time.** Findings raised, sent, changed in the source, and the queued strings and record decisions
   each change removed: the catalogue getting better because the tool was used.

### 14.8 Order of work, and what is counted first

1. Source systems in the manifest; `records` and `occurrences` filled by collect, `string_examples` read from them;
   the record link; all records on the desk, grouped. Nothing decided differently yet.
2. Findings: the table, raising one at the desk, the list page, the export. The audits' lists feed it.
3. Collect's matching and closing (14.5), tested as a round trip on a copy of the export with records edited by hand:
   an ISBN corrected, a place qualified, a record removed.
4. Record decisions by a person; the transformation reads occurrences.
5. Records ready on the home page; the run page.
6. Record decisions by a rule, after the count: bare place names borne by more than one record whose publishers or
   other places point to different countries, and what share a rule on the publisher's country would get right.

Open: what "ready" needs for the archive and photographic archive records, which are not collected
yet.
