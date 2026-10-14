# Design: Examples site (`LinkedArtNet.ExamplesSite`)

Status (2026-10-14): implemented in `src/LinkedArtNet.ExamplesSite`, including the trainer; the axe lint is not wired in.
Trainer configuration: `Trainer:GitHub:ClientId`/`ClientSecret` (OAuth app callback `/dates/train/signin-github`),
`Trainer:Reviewers` (GitHub logins), `Trainer:Submission:Repo`/`Token`/`BaseBranch` for pull requests,
`Trainer:CapturePath` for the service's capture file, `Trainer:DataDirectory` for the queue and submissions
(two JSON files; persist it). `Trainer:AllowLocalReviewer` signs anyone in as "local" and is for development only. The C# is extracted by `tools/ExampleSourceExtractor` into `examples/source.json` rather than an MSBuild
task; run it after editing an example (a test fails otherwise). The playground parses in-process unless
`TimeSpanService:BaseUrl` is set. Run locally with `dotnet run --project src/LinkedArtNet.ExamplesSite`.

A public ASP.NET Core Razor Pages site that shows, for every Linked Art example on https://linked.art,
the C# fluent code that produces it, the JSON we generate, the JSON the site publishes, and whether
they match. It also hosts the TimeSpan parser playground and the corpus trainer. It is a learning tool
for Linked Art (C# reads more easily than JSON-LD) and a living coverage dashboard for the library.

## 1. Pages

| Route | Content |
|---|---|
| `/` | Coverage summary (examples on site, implemented, validated, blocked by site issue), links by section mirroring linked.art's navigation (Basic Patterns, Objects: Production, Physical, Aboutness, Ownership, Rights; Digital; Collections; Provenance; Exhibitions; Actors; Places; Documents; Archives; Assertions; Concepts; Events; ...). |
| `/examples/{section}` | List of examples in a section with status badges and one-line titles. |
| `/examples/{section}/{id}` | One example: title and link to the site paragraph; **C# source** (syntax highlighted, copyable); **generated JSON**; **site JSON**; semantic diff (empty when validated); status, and the upstream issue link when blocked. Toggle for "show as reference-only" vs full. |
| `/search?q=` | Search across example titles, C# identifiers and JSON property names ("find examples that use `transferred_custody_of`"). |
| `/properties/{name}` | Reverse index: every example using a given property or AAT term. |
| `/dates` | Parser playground: input box, intent and profile selectors, "allow LLM" when configured; result panel with the TimeSpan JSON, a timeline bar (log-scaled for deep time), flags, confidence, notes, alternatives; "copy as C#"; "this is wrong" -> opens the trainer form pre-filled. Calls the public service over HTTP (configurable base URL) so the demo exercises the real API. |
| `/dates/corpus` | Browse the corpus by tag and status; per-entry pass/fail against the current parser version. |
| `/dates/train` (auth) | Trainer: queue of captured unparsed/low-confidence inputs and uploaded lists; shows the parser's answer; reviewer accepts, edits the canonical expression (live re-realised), sets intent/profile, tags, and submits. Submissions become `proposed` corpus entries in a pull request branch (via GitHub API) or a downloadable JSON when not configured. |
| `/about` | What Linked Art is, what this library is, how to use the NuGet packages, licence, attribution for imported corpora. |

## 2. How the C# source is shown

At build time an MSBuild task (or a tool in `tools/ExampleSourceExtractor`, run in CI) uses Roslyn to
read `LinkedArtNet.Examples`, find every method with `[LinkedArtExample]`, and emit
`examples-source.json`: `{ id, section, title, siteUrl, sourceFile, line, methodBody }`. The method body
is the exact text, dedented. The site embeds this JSON and the cached site JSON as content files, so
the site has no dependency on the test project or the network at runtime. Each example method is
written to be self-explanatory on its own (local variables named for what they are, no shared
helpers that hide model usage), because it is being read as documentation.

## 3. Hosting and shape

- Razor Pages, server-rendered, minimal JS (highlighting, diff rendering, the timeline). No SPA.
- Light/dark theme; mobile-friendly; every example page has a stable URL and `<title>`.
- Runs in the same container image as the service or separately; configuration key
  `TimeSpanService:BaseUrl`. For local development, an in-process fallback calls the parser library
  directly.
- Trainer auth: GitHub OAuth, allow-listed logins (the owner plus invited reviewers). Everything else
  is anonymous.
- Static-site export is possible later (every page is deterministic) but not needed for v1.

## 4. Tests

- Page smoke tests with `WebApplicationFactory` for every route pattern.
- A test that every example in the manifest has a page and every page has source, generated JSON and
  (where applicable) site JSON.
- Accessibility lint (axe) in CI for the main templates.
