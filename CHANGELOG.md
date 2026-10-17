# Changelog

Releases are git tags on `de-pmc`; each tag builds the two container images (`examples-site`, `dates-service`) and
`main` is fast-forwarded to it (see "Branches and releases" in the README). Versions follow the tags; the parser
has its own version (`TimeSpanParser.Version`) reported in every result.

## v0.1.2 (2026-10-17)

- Grammar: a year outside the profile's plausible range ("19881", "c. 18011") is read as written but penalised below
  the accept threshold with a note, so the trainer's bulk ask and the interpreter path pick it up.
- Trainer: when a proposal is shown, the accept button says it accepts the parser's reading, and the correction form is
  how the proposal is kept.
- Images: the site Dockerfile copies the Dates.Llm project and the embedded grammar document; `.dockerignore` lets that
  one `docs/` file through; CI builds both images on every push; the release matrix no longer cancels the sibling image.
  (These fixes shipped inside the re-pointed v0.1.1 tag; recorded here because the first v0.1.1 build failed.)
- Tests: the promotion test no longer depends on the checkout's line endings.

## v0.1.1 (2026-10-17)

### Dates library
- `IDateInterpreter` (strategy 5) with `TimeSpanParser.ParseAsync`: an interpreter is asked only when the deterministic
  reading is unparsed, partial or below the accept threshold, its canonical expression is realised by the parser, capped
  at the interpreter's `MaxConfidence` and flagged `Interpreted`. `LookupInterpreter` for tables of reviewed readings;
  `ParseOptions.AlwaysInterpret` for measurement.
- PeriodO period names: `PeriodOSource` over `periods/periodo.json` (CC0, 9,446 definitions), matched as the whole
  string only, with the PeriodO URI and the defining authority in the result. `NamedPeriod` gains `Uri` and `Source`;
  `INamedPeriodSource.ForGrammar` keeps gazetteers out of the grammar; the named-period strategy accepts a leading
  "c."/band word and a trailing "period"/"era".
- Records that date several parts separately ("(1) 1980, (2) 1977", "Sheet 1; 1964. Sheet 2; ca. 1930s") read as a set:
  hull as the span, parts as alternatives, flag `MultiPart`.
- Normaliser: mojibake apostrophes and dashes repaired (cp1252 and Latin-1 readings of UTF-8); `YYYYMMDD` column values
  read as dates, with zero month or day meaning unspecified. The tokenizer's ordinal-dot rule no longer eats a sentence
  stop after a year.
- Profile advisor: a suggested profile written as changes from `default`, example strings per feature; other-language
  evidence excludes words English also knows; abbreviated range ends are not two-digit years.

### Dates service
- `POST /v1/advise`: profile advice for a dataset (JSON or `text/plain`, up to 20,000 strings).
- `/version` reports the PeriodO dataset version; `Parser:PeriodOPath` setting.
- `llm=true` is accepted and ignored with a note: the public service is deterministic by decision.

### LinkedArtNet.Dates.Llm (new)
- `ClaudeDateInterpreter`: the reference interpreter, Claude under a JSON schema (canonical, confidence, rationale,
  intent) with the canonical grammar as a cached system prompt. Used from the trainer and `tools/Corpus interpret`.

### Examples site
- `/dates/advise`: the profile advisor with corpus samples. `/dates/samples`: hundreds of corpus-backed dates by theme,
  each linking to the playground. `/dates/api`: advise section and "Run it yourself" for the dates image.
- Trainer: with an Anthropic API key configured, a reviewer can ask Claude for a proposed reading, per item or in
  batches; the proposal pre-fills the review form and is confirmed by the reviewer.
- Fixed: the GitHub source link's line anchor on example pages rendered as literal `@Model...`. Idiom cloud tags one size.

### Corpus and tools
- `tools/Corpus periodo` (download and compact PeriodO), `tools/Corpus interpret` (proposals and agreement report).
- Authored sets `encoding-and-noise.json` and `multi-part.json`; the disputed yearrange lists and mojibake entries are
  verified. Verified 716, proposed 1,600, all passing.

### Deployment
- `.env`: optional `ANTHROPIC_API_KEY` (trainer proposals); compose passes it as `Interpreter__ApiKey`. Images carry
  `periods/`.

## v0.1.0 (2026-10-16)

First release: linkedart.net and dates.linkedart.net live. Linked Art model library with every linked.art example
reproduced, the free-text date parser with profiles, canonical expressions and a 2,300-entry corpus, the dates
service, the examples site with playground, idioms, corpus browser and trainer.
