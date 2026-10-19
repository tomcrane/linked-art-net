# Design: the alignment workbench (`LinkedArtNet.Workbench`)

Status: design, 2026-10-18. The human side of the toolkit in doc 10: where a collection specialist gives judgement on
authorities, dates and agent strings, contributes what only they know, and runs the pipeline without a command line.
Section 11 is the order to build it in; section 12 records the owner's decisions and the questions still open.

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
to the PostgreSQL store through `IAlignmentStore` and the reconciler, loaders, shadow and audit as services. It is not
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
- entities already in the store that resemble the string, from the store stage, marked as such;
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
- How much of a dump's records the desk shows as examples, and from where: the collected strings only know their
  contexts and counts today; example records need the pipeline runner to keep a sample per string (step 4).
- Whether the model's proposals are asked for automatically for every queued item within the project's budget, or
  only on request, as in the trainer now.
