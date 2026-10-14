# Growing the corpus: trainer, review and verification

How a date string that the parser gets wrong, or is unsure about, becomes a tested commitment. This is
the operating guide; the data format and metrics are in [06-corpus-and-testing.md](06-corpus-and-testing.md),
the trainer's configuration in [05-examples-site.md](05-examples-site.md), and the canonical expression that
reviewers edit in [canonical-expression.md](canonical-expression.md).

Status (2026-10-14): implemented and tested end to end; verification is `tools/Corpus promote`. Gaps are in §7.

## 1. The path at a glance

```
 input appears            reviewer decides            maintainer verifies         CI enforces
 ------------------       --------------------        -------------------         ------------------
 service capture  -+      /dates/train/review         pull request on GitHub      Verified_entry_passes
 pasted list      -+-->   accept | correct | skip -->  run the corpus tool   -->   one theory case per
 uploaded file    -+      becomes a proposed entry     tools/Corpus promote        verified entry
 "wrong?" link             exported as a file          merge                       report.md regressions
```

Two humans look at every entry before it can break a build: the reviewer who writes the expectation in the
trainer, and the maintainer who verifies it in the pull request. Until the second step the entry is
`proposed`: it is evaluated and reported, but a parser change that disagrees with it does not fail CI.

## 2. Where inputs come from

| Source | How it reaches the queue |
|---|---|
| The dates service | When `Capture:Enabled` is on, the service appends every request whose status is not `parsed`, or whose confidence is below `Capture:MinConfidence` (default 0.75), to a JSONL file. The trainer's **Import captured inputs** button reads that file (`Trainer:CapturePath`). Nothing else about requests is stored. |
| A reviewer | The **Add inputs** form on `/dates/train` takes one string per line, or a `.txt`/`.csv`/`.jsonl` file, with a profile and an intent for the batch. |
| The playground | Every result on `/dates` ends with a "Wrong? Reviewers can correct it in the trainer" link. Anyone else can open a GitHub issue from the same line. |

Duplicates by input text and profile are dropped on the way in, so importing the same capture file twice
is harmless. The queue and the submissions live in two JSON files under `Trainer:DataDirectory`, which must
be on a persisted volume when hosted.

## 3. Reviewing

Only allow-listed GitHub logins (`Trainer:Reviewers`) can open `/dates/train`. The review page takes the
oldest pending input and shows what the parser says now: status, canonical form, EDTF, confidence,
strategy, the realised TimeSpan JSON, notes and alternative readings. The reviewer then does one of three
things.

- **Accept as is.** The parser's current answer becomes the expectation. This is the common case for
  low-confidence captures that turn out to be right, and it is how confidence calibration improves.
- **Correct it.** The reviewer sets the expected status, and for `parsed` a **canonical expression**.
  The expression is re-realised as it is typed, with the resulting bounds shown underneath, so the reviewer
  sees exactly what they are committing to before saving. Tags and a note are optional; the note should say
  why this reading and not another, because the maintainer reads it later.
- **Skip.** Leaves the item for someone else, counted separately so it does not vanish.

Reviewers edit the canonical expression, never the datetimes. The realiser turns the expression into bounds
under the entry's profile, which keeps expectations consistent with every other entry and lets a deliberate
policy change (say, a different reading of "early 19th century") be made once in the profile.

What a decision produces:

| Expected status | The entry pins |
|---|---|
| `parsed` | canonical form, EDTF, kind, and the realised bounds (`timespan`, or `begin` and `end` for pairs) as absolute datetimes |
| `undated` | the status only: the string must be recognised as carrying no date (for example "n.d.") |
| `unparsed` | the status only: the parser must not invent a date for it |

Bounds are pinned on purpose. A `proposed` entry is a snapshot of what the reviewer saw; if a later parser
change moves the bounds, the entry fails in the report and someone has to say whether the parser or the
reviewer was right. An expectation that only named the canonical form would let the realiser drift unnoticed.

Each saved decision is a submission with the reviewer's login and time. Ids are `tr-yyyyMMdd-nnn`.

## 4. Exporting

The trainer's index page lists the submissions not yet exported and offers two ways out.

- **Download the corpus file.** For a server without GitHub credentials, or to look before pushing. The
  download does not mark anything exported, so it can be repeated.
- **Open a pull request.** Requires `Trainer:Submission:Repo` and `:Token`. The trainer creates a branch
  `trainer/yyyyMMdd-HHmmss` from `Trainer:Submission:BaseBranch`, adds one file under `corpus/proposed/`, and
  opens a pull request whose body lists every input with its expectation and explains how to verify. The
  submissions are then marked exported with the pull request link, and the trainer shows them under
  **Exported**.

Either way the file has the shape of `corpus/authored/*.json`: a `source` block ("Trainer submissions",
licence CC0-1.0, the reviewers named in `notes`), `defaults` of `status: proposed` and tag `trainer`, and the
entries. The trainer validates the file against `corpus/schema/corpus-entry.schema.json` before offering
it, and the test suite evaluates an exported file with the real `CorpusEvaluator` to make sure what the
trainer writes is what the corpus tool reads.

Reviewers are contributing their expectations under CC0; the sign-in page should say so once hosted.

## 5. Verifying (a maintainer, in the pull request)

On the pull request branch:

```
dotnet run --project tools/Corpus -- validate
dotnet run --project tools/Corpus -- promote corpus/proposed/trainer-20261014-1530.json --dry-run
```

`validate` checks the schema, id uniqueness across the whole corpus, that every canonical string parses,
and that bounds are in order. `promote --dry-run` evaluates every entry in the file and lists what would
change, or prints each failing entry with the reason and stops. New trainer entries should all pass, because
they pin the parser's own realisation, unless the parser changed between the review and the merge.

Passing only means the parser agrees with the reviewer. The maintainer's question is whether the reviewer
was right, and the entry's note and tags are there to answer it. GitHub's review shows the diff, not the
evaluation, so this step is local.

Then:

```
dotnet run --project tools/Corpus -- promote corpus/proposed/trainer-20261014-1530.json
```

With no ids the whole file is promoted: `defaults.status` becomes `verified`, and any explicit entry
statuses with it. With ids, only those entries change and each gets its own `"status": "verified"`. Either
way only the status tokens are edited, so the diff is one token per entry and the reviewer's formatting and
notes are untouched. Promotion to `verified` refuses if any targeted entry fails, and writes nothing. On
success the command re-runs the whole corpus and rewrites `corpus/report.md` and `report.json`, so the next
run's "newly failing" list stays meaningful.

For an entry the maintainer disagrees with:

```
dotnet run --project tools/Corpus -- promote corpus/proposed/trainer-20261014-1530.json tr-20261014-007 --to disputed
```

or fix the canonical expression by hand and remove the pinned bounds (the evaluator checks only the fields
that are set), then promote. An entry not worth keeping is deleted.

Finally `dotnet test tests/LinkedArtNet.Dates.Tests` confirms the new verified entries pass as a CI gate.
Commit the corpus file and the two report files, and merge.

The file stays in `corpus/proposed/` after verification: the folder names where the entries came from,
the `status` field says whether they are enforced. The corpus browser at `/dates/corpus` filters by file,
status and result, so a merged file can be checked there too.

## 6. What CI guarantees afterwards

`CorpusTests.Verified_entry_passes` is a theory with one case per verified entry across every file under
`corpus/`. A parser change that moves a verified answer fails the build with the input, the expected and
the actual value. `proposed` and `disputed` entries are run by the corpus tool and reported, never enforced.
`File_is_valid_against_the_schema`, `Entries_are_well_formed` and `Ids_are_unique_across_the_corpus` catch a bad file
regardless of status.

| Status | Set by | Enforced | Reported |
|---|---|---|---|
| `proposed` | trainer export, importers | no | yes, including regressions since the last run |
| `verified` | a maintainer, in the pull request | yes, one test case each | yes |
| `disputed` | a maintainer, when reviewers disagree | no | listed separately |

## 7. What is not built yet

- **No review in the GitHub UI.** The pull request shows the diff, not the evaluation, so the maintainer
  runs `promote --dry-run` locally. A CI check that evaluates new proposed files and comments the result on
  the pull request would remove that step.
- **No "review import" mode.** The imported corpora (yearrange, timetwister, Getty, EDTF spec) are
  `proposed` and are reviewed by editing their files directly, not through the trainer. `promote` with ids
  works on them too.
- **Ids are per trainer instance.** `tr-yyyyMMdd-nnn` comes from one server's counter. Two trainer
  deployments exporting on the same day could clash; `validate` would catch it at merge.
- **Disputed is a status, not a workflow.** Nothing routes a disagreement to a second reviewer; the
  maintainer sets it with `promote --to disputed` and the report lists it.
