# What the PMC documents suggest (sifted 2026-10-19)

Sources, read in full from the owner's export (`C:\pmc\gdocs`, not in the repo): Rob Sanderson's brief
"PMC / LUX Data Transformation" (version 2026-08-19, begun before the 2024 work and updated during it; parts
superseded), "PMC Linked Art Notes" (owner, May 2024), "PMC Notes from Slack" (owner, Aug 2024), "PMC Initial Data
Snag list" (Yale/PMC, Sept 2024), "PMC next steps" (owner, Oct 2024). None of these is an instruction to build
anything; this note records what they suggest a *project* in this toolkit should be able to do, and what they
confirm or contradict in docs 10–13. Facts and tables below come from those documents unless marked otherwise.

## 1. Things a project should do, that the design did not yet say

1. **Several sources per project, each with an adapter and uploaded dumps.** PMC alone has: library books (flat
   XML; serials and articles later), archives (EAD from the Archives Hub export, earlier CALM flat XML), a CALM
   authority file (Person / Family / Corporate, with VIAF numbers in a free-text field), and the photo archive
   (LIDO over OAI-PMH, an EAD hierarchy, and a constituents spreadsheet). "Next steps" asks for a front end to
   "upload new library and archive source files". So a project holds *sources*, each with its own fields; contexts
   should be namespaced by source (`library.persauthorfull`, `archives.controlaccess/persname`, `photo.actor`), and
   a dump is a versioned project asset with a date, which a run is recorded against.
2. **Profile a source before mapping it.** The brief says "generate stats" against classification terms, repository
   names, event types, accession numbers, places. A *profile* job per field (distinct values with counts, sample
   records) is what a person reads before writing the manifest's contexts, and it is the variants page's older
   sibling. Doc 12 step 4 should include it.
3. **Controlled fields are contexts too, and the mapping tables are overrides.** The brief carries hand tables:
   `medium` → AAT (12 values), language codes → ISO → AAT (≈60), note-type codes → AAT note classifications (≈50),
   location codes → Places `part_of` the PMC Place, roles → AAT (≈40), archive levels → AAT, LIDO measurement
   types and units. Each is a small field whose distinct values a person maps once. In this toolkit that is a
   context whose strings resolve to AAT (or to local Places), decided by override; the overrides export *is* the
   mapping table, with provenance. The brief's tables can be imported as overrides with source `brief-2024` rather
   than re-typed, once an AAT index is loaded to verify the ids.
4. **A role-routing table.** The Slack notes list rules the mapping needs from the agent string's role: former
   owner and donor become a provenance statement, not a creator; a publisher beginning "Printed for" becomes a
   publication statement, "printed for and sold by" a provenance statement; addressee becomes a subject; binder
   and printer go to Production on the object; `(artist)` in `persauthorfull` is probably the subject, not a
   contributor. So a role is not only an AAT id but a *route*: creator part, publishing, provenance, production,
   subject, or none. The agent parser (doc 10 §6) already separates the role; the project needs the table that says
   where each role sends the agent, and which roles mean "do not reconcile as a creator".
5. **Runs started by a person or a clock, with counters.** "Enable Tom (S) to run the transformation" and "enable
   automation" are, as the owner wrote, the same feature: once a run can be started from a page it can be started by
   a schedule. "Increment counters of all the things we might be interested in" is the runs table's job; the
   brief's scattered "stats" asks are the list of counters. This is doc 12 step 3 as designed, plus a schedule.
6. **Publishing and a change stream.** Records are to be published at their identifiers (`data.paul-mellon-centre.ac.uk`
   over S3 with DNS), and harvested by ActivityStreams (IIIF Change Discovery) rather than full dumps, with the
   change date taken from the source (`entrydate`, CALM `Modified`, OAI `dateStamp`). "Design the schema with the
   intention that it provides a change discovery stream" was a 2024 next step; the alignment store's decisions and
   entities already carry timestamps, so a change stream is a view over them plus the source dates. Publishing is
   doc 10 §8, not yet designed in detail; the change stream should be part of it.
7. **Reports in two directions.** Outward to the authorities (doc 12 step 5) and inward to the cataloguers: the brief's
   "check if the class in ULAN and the class in LIDO match up and produce a report" is the audit's TypeMismatch; the
   constituents spreadsheet's "23 rows with the same ULAN" is MultipleUlan. The snag list shows PMC and Yale expect a
   running list of data oddities with questions attached; the audit CSVs are that list's raw material.
8. **Object-level reconciliation, later.** The photo archive is photographs *of* artworks, mostly not PMC's, to be
   reconciled against YCBA by accession number via LUX, else artist plus title; owners via ULAN (a ULAN number
   beginning with 5 is usable as an id) or VIAF. That is a new class of entity (object or work) beyond the agent,
   place, concept classes the store has, and a Phase 2 matter (the March 2025 quotation). Note it; do not build it
   now.

## 2. Things the documents confirm in the current design

- **Local indexes, not live services.** "Use local LCSH and other data rather than calling live web services": the
  2024 run was slow from rate-limited HTTP. Done: ULAN and LoC names are local; LCSH and AAT are the next loads
  (AAT is needed for every mapping table above).
- **The identity rules.** The Linked Art Notes ask whether "Jarman, Derek" is "Jarman, Derek [1942-1994]" (yes when
  there is one dated namesake; two sets of dates means two people; "[1942-]" and "[1942-1994]" agree) and give the
  John Walter case (four role variants, two with different dates). That is the three-state date comparison and the
  queue-when-several-holders rule of doc 11 §8. The note's counts are worth keeping as the scale of the problem:
  people with no dates 18,540; one date 2,277; two 27; three 1. Dashwood, Jarman and Walter should become tests.
- **Compound subjects** (doc 10 §7.5), the owner's own 2024 next step, "2 days".
- **LUX as a source** with self-echo (doc 10 §7.4). The Slack notes record the query shapes: identifier equivalence
  ("what do you have that is equivalent to X"), exact unstemmed name match, `created` chains (a person who created a
  work with a given title), place and concept search; and the warning that the group found by exact match may carry
  no works, because LUX's own merging moved them.
- **Honorifics in the date brackets** ("Duke of", "Sir", "Lady") belong to the name; multiple date ranges take the
  min and max. Both are agent-parser rules, to be checked against `LinkedArtNet.Agents`.
- **Identifiers.** Archive URIs minted persistently from the `unitid`; CALM record UUIDs; LIDO record ids; a sort
  identifier computed from the reference (`fhs/1/2/1` → `fhs/00001/00002/0001`). The project's identifier base and
  "never remint" rule stand.

## 3. Things the documents contradict, or that have moved on

- The brief maps people to LoC ids directly in the record (`carried_out_by: id.loc.gov/...`) *or* to a PMC record
  with equivalents. The toolkit's rule is the second only: every agent gets a PMC identifier and the authority ids
  go in `equivalent`, so the record survives a change of mind about the alignment. Rob's own LUX practice agrees
  (the Linked Art Notes' slide: "the URI identifies the entity and record on the web, identifiers are institutional
  strings within the record").
- LUX has reminted its UUIDs since 2024 (checked 2026-10-19: the LUX links in the notes are 404); PMC identifiers
  inside LUX records survived. So LUX ids are never stored as stable facts, only refreshed by the self-echo pass.
- "Rewrite in Python (tbd ???)" is answered: the toolkit is .NET and the transformation will run on it.
- The snag list is mostly mapping changes (omit Accruals and CustodialHistory, add Date, relabel Class as Call
  Numbers, keep "(PAMPHLET)", add a publishing event and an imprint statement, include DigitalObject, revisit
  "IN PROCESS", the auction-catalogue class). Those belong to the PMC project's mapping, not to the alignment
  toolkit, and should live in that project's issue list when the mapping is rebuilt on this toolkit.

## 4. Suggested additions to the plan

- Doc 12 step 4 (projects): sources and uploaded dumps; the profile job; contexts namespaced by source; the
  role-routing table and the mapping-table contexts; import of the brief's tables as credited overrides.
- Doc 12 step 3 (jobs): a schedule, and the counters list.
- Doc 10 §8 (publishing): the change stream, with source change dates.
- Tests: Dashwood, Jarman, Walter.
- Loads: AAT next, then LCSH; both are needed before the mapping tables and compound subjects.
