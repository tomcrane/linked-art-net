# Workbench scenarios

Common situations at the review desk, each with a real example and what a reviewer does about it. This is the
worked-example companion to the user guide ([13-workbench-guide.md](13-workbench-guide.md)), which describes every
part of the desk; here each entry starts from what you see on the screen.

Every entry has the same shape:

- **Scenario**: the situation in a sentence, and how to recognise it.
- **Example**: a real string, the state of the workbench when the pictures were taken, and a link to it.
- **Solution**: what the reviewer does for that example, step by step, and what to check before pressing anything.
- **Watch for**: when the usual solution is wrong.

The examples are from the PMC project. Pictures are in `img/scenarios/`, named by scenario and step. A picture
shows the store as it was on the date given; once the example is decided it leaves the queue, so the link then shows
the decided string rather than the picture's state.

Scenarios so far: [Holder pair](#holder-pair). The list to come is at the [end](#scenarios-to-write).

**Keeping the pictures true.** A picture shows the workbench as it was built on the day it was taken. Each scenario
says when its pictures were taken; when the desk or the entity page changes, the scenarios that show the changed
part are retaken, and the table at the end records which were last checked against which change.

---

## Holder pair

**Scenario.** The project has the same thing twice. Two of the project's own entities each hold an identifier from
an outside authority, and the authorities say those identifiers are one thing. The software cannot choose between
the two entities without guessing and never merges entities on its own, so the string waits for a person. You
recognise it by the line at the head of the first candidate group: "2 of the project's entities hold records of this
group".

**Example.** "Coventry (England)", a subject heading (`keywords:head`), string 82037, as queued by run v7aq7v;
pictures taken 2026-11-13. http://localhost:5200/p/pmc/review?id=82037

The string and the catalogue records it was seen in:

![The string Coventry (England) at the head of the desk, with three catalogue records](img/scenarios/holder-pair-1-string.png)

The first candidate group. Six records "agree they are one thing", and two of them are the project's own: an entity
labelled "Coventry" and another labelled "Coventry (England)". Both are places, both aligned, and both were seen in
the same book (*St Michael's Coventry*):

![The candidate group with the holder-pair notice and the merge button](img/scenarios/holder-pair-2-group.png)

**Solution.**

1. *Decide whether the two entities are the same thing.* Read the two rows: class, what each holds, which strings
   each stands for, and the records each was seen in. Here both are the city of Coventry in the West Midlands: same
   class, overlapping books, and the map puts the group in one spot. They are duplicates. If you need more, each
   entity's name opens its page in another tab.
2. *Press "Merge them and accept this string onto the survivor".* Nothing is merged yet. A confirm step opens:

   ![The confirm step: choose the survivor, the identifiers that will be added, Merge and accept](img/scenarios/holder-pair-3-confirm.png)

3. *Choose the survivor.* The other entity merges into it, and its strings, identifiers and labels follow, so
   nothing is lost either way; what you are choosing is the URI and label that remain. Prefer the one that holds
   more and says more. Here that is "Coventry (England)": it holds the Library of Congress and Wikidata identifiers
   and stands for two strings, where "Coventry" stands for one.
4. *Read the line "Then N identifiers added" before confirming.* These are the group's records that neither entity
   holds yet, and they will be attached to the survivor. Each should be the same thing as the survivor.
5. *Press "Merge and accept"*, or "Back" to leave everything as it was. A merge is permanent: "Undo last decision"
   takes back the string's decision, not the merge. Afterwards the string, and the other queued strings of the same
   name, leave the queue.

*For Coventry, step 4 stopped the reviewer.* When the pictures were taken, the three identifiers to be added were
TGN's Coventry (right) and two Library of Congress records that are not the city: "Catholic Church. Diocese of
Coventry and Lichfield" and "Church of England. Diocese of Coventry", both bodies, not places. The software had put
them in the group because they are placed in Coventry. That was a fault, mended the same day (a body placed in a
city no longer joins the city's group), so a card today lists only records of the place; the pictures are kept
because they show why the line is worth reading. When a group does hold a record that is not the thing, leave the
group's button alone and do it in two steps:

1. Press "Back". On each of the two entity rows press **Tray**, then open the tray's merge page from the bar at the
   foot of the screen, choose the survivor and merge. This merges the two entities and adds nothing else.
2. Back at the desk, press **Look again** so the card shows the store as it now is, then **just this one** on the
   surviving entity's row. The string is decided onto the entity without the group's other records.
3. Add any identifier you do want on the entity's page (for Coventry, TGN's `http://vocab.getty.edu/tgn/7011012`).

This is how Coventry was decided: the two entities merged through the tray, "Coventry (England)" the survivor.

**Watch for.**

- *Two things, not one.* The authorities sometimes lump together things the project keeps apart: the Grosvenor
  Gallery of 1877 to 1890 and the later dealer of the same name. If the two rows differ in dates, place or what they
  were seen in, do not merge; accept the string onto the right one with **just this one**. If the card shows nothing
  that tells the pair apart, that is a fault in the desk: report it.
- *Records in the group that are not the thing*, as with Coventry's dioceses. The count "6 records agree" is the
  software's claim, not a fact; the confirm step's list of identifiers is where you check it.
- *The same name, different identifiers.* Two entities of one name with different Wikidata or VIAF numbers may be
  duplicates the authorities also hold twice, or two different things. "Dagenham" and "Dagenham (London, England)"
  were the second kind: one held Wikidata's record for the town, the other its record for the former parliamentary
  constituency, an old mistaken alignment. The project means the town by both, so merging is right, but the
  constituency's identifiers come along and must then be removed on the survivor's page. Better, open the mistaken
  entity's page first ("its page"): its identifiers are boxed in sets under what they are, and "Remove all" takes
  the constituency's set off in one press; then merge. Do not relabel the entity to match its wrong identifiers
  ("Dagenham (parliamentary constituency)"): an entity is what its strings mean, and this one stands for a place of
  publication, the town.
- *Different classes.* The confirm step says so when the two entities differ in class (a person and a group, say).
  That is usually a sign they are not duplicates.

---

## Scenarios to write

The list, begun 2026-11-13 with the owner. A scenario is written when a good example is met at the desk, with its
pictures taken then; the example column names one where one is known.

**Deciding a string at the desk**

| Scenario | What the reviewer sees | Example |
|---|---|---|
| One record is plainly right | one candidate, or one far ahead, and the records agree with it: Accept | |
| Several records agree | a group of records from different files that are one thing: Accept these as one | |
| It is an entity the project already has | a group one project entity holds: Same as | |
| Two groups are the same thing | the same house as a body in one file and a person in another: tick together | an auction house in ULAN and the Library of Congress |
| A place among namesakes | several places of one name, the map, hollow markers | "Cambridge", "San Francisco, CA" |
| The right record is not offered | Something else: the search of the project's entities and the indexes | |
| The right record is greyed out | a candidate ruled out for a reason you know better than: accept anyway | a birth year two out |
| Nothing fits and the thing is real | Keep as local authority, the label, the note, "known elsewhere" | a publisher with no record anywhere |
| A spelling that recurs | Always, and what it does to later strings | |
| A person or a firm | a name in a body's field that is a person, or a firm named for one; the class question | "Ackermann and Son", "Mr Hone" (decision 16) |
| A compound subject heading | the heading decided as its parts; a part that is not a compound | "Violence--Press coverage" |
| Two placeholders of one name | the same name waiting in two fields, the same records behind both | "Lawrence" |
| The cataloguer's slip | a truncated or misspelt string: what to decide, and what to report | "Art, Baroque--Italy--ome", "Gainsville, Ga." |
| I cannot decide | Skip for now, Add note for another reviewer | |
| The card is out of date | "now held by", "the same entity as a row above", Look again | |
| Reviewing what was minted | the strings for which nothing was found | |

**Cleaning an entity**

| Scenario | What the reviewer sees | Example |
|---|---|---|
| An entity aligned to the wrong thing | the page's "stands for" panel against an identifier set that describes something else: Remove all | the bare "Dagenham" and the parliamentary constituency |
| An entity holding two things' identifiers | two boxed sets, or identifiers of another class | "Northampton" with a diocese and an archdeaconry |
| An identifier another entity holds | the refusal, the holder named, the merge offered | |
| Two entities found to be one while browsing | the tray and its merge page | |
| An identifier that no longer resolves | "LUX answered 404 Not Found" | the re-minted LUX concepts |
| What a source says | Pull in from the sources, the pictures, the map, LUX's echo of the project's own export | Cozens, Peabody |

**Putting something right**

| Scenario | What the reviewer sees | Example |
|---|---|---|
| I decided a string wrongly | Undo last decision; the decided list; reopening strings from the entity page | |
| I merged what should not have been merged | a merge is permanent: what can and cannot be taken back, and how to separate by hand | |
| Who changed this, and when | History and notes on the entity, Recent changes on the project | |

## Pictures last checked

| Scenario | Pictures taken | Shows | Stale since |
|---|---|---|---|
| Holder pair | 2026-11-13, before the changes of the same day | the desk's head, the first group, the confirm step | 2026-11-13: the pair's box, identifiers as links with what they are, fields in plain words. To retake |
