# Design documents

| Doc | What |
|---|---|
| [00-plan.md](00-plan.md) | Objectives, phases with acceptance criteria, decisions, layout, risks, open questions |
| [01-current-state.md](01-current-state.md) | Audit of the `de-pmc` branch, the `pmc-2` pipeline and the `py_datetime` prototype |
| [02-model-library.md](02-model-library.md) | `LinkedArtNet`: class hierarchy, serialisation, fluent API, `LinkedArtDate`, vocabulary registry, 100% example coverage |
| [03-timespan-parser.md](03-timespan-parser.md) | `LinkedArtNet.Dates`: parse-then-realise architecture, AST, canonical form, profiles, intents, LLM plug-in, API |
| [03-timespan-prior-art.md](03-timespan-prior-art.md) | Standards (Linked Art, EDTF, Getty) and libraries surveyed; conventions; corpus sources |
| [04-timespan-service.md](04-timespan-service.md) | Kestrel API: endpoints, request/response shapes, limits, deployment |
| [05-examples-site.md](05-examples-site.md) | Razor Pages site: example pages with C# source, playground, trainer |
| [06-corpus-and-testing.md](06-corpus-and-testing.md) | Corpus format and lifecycle, metrics, whole-repo test strategy |
| [08-corpus-contributions.md](08-corpus-contributions.md) | Operating guide: how trainer inputs become proposed entries, pull requests, and verified corpus; what is manual today |
| [09-hosting.md](09-hosting.md) | One virtual server at Mythic Beasts: Caddy, the site and the service under compose; DNS, images, GitHub app, backups |
| [10-alignment-toolkit.md](10-alignment-toolkit.md) | Design for the transformation and alignment toolkit: goals, what to keep from PMC, the store (entities, strings, decisions), pipeline stages, the agent-string parser, reconciliation and the LUX self-echo rule, PMC and Wellcome instances, order of work |
| [07-later-alignment-toolkit.md](07-later-alignment-toolkit.md) | Earlier notes the design grew from (LUX, QLever, Postgres) |
| [upstream-issues.md](upstream-issues.md) | Findings to report to linked.art: id collisions, redirect loop, 38 examples the 1.0 schemas reject, schema file issues |
