# linked-art-net

A .NET implementation of [Linked Art](https://linked.art/), the JSON-LD profile of CIDOC-CRM used by
museums, libraries and archives to describe art and cultural heritage.

| Project | What |
|---|---|
| `src/LinkedArtNet` | The model library: classes for Linked Art 1.0, JSON-LD serialisation, fluent authoring API, Getty vocabulary helpers. |
| `src/LinkedArtNet.Dates` | Free-text date strings to Linked Art TimeSpans: canonical EDTF-style expressions, profiles, realiser, grammar in seven languages, named periods, fluent bridge (`WithTimeSpan("c. 1642")`). |
| `src/LinkedArtNet.Dates.Service` | Kestrel service over the parser: `GET /v1/timespan?s=late%201950s`, batch POST, profiles, OpenAPI at `/docs`; Dockerfile. |
| `src/LinkedArtNet.ExamplesSite` | Razor Pages site: every linked.art example as C# beside its JSON, coverage dashboard, date playground, corpus browser. |
| `src/LinkedArtNet.Examples` | One C# method per example published on linked.art; the tests compare each method's JSON with the site's. Coverage: [examples/coverage.md](examples/coverage.md). |
| `tools/` | Code generators and the site crawler (`ExampleManifest`). |
| `tests/` | Test projects. |
| `docs/` | Plan and design documents, starting with [docs/00-plan.md](docs/00-plan.md). |

Live at [linkedart.net](https://linkedart.net) (the examples site, playground, corpus browser and trainer) and
[dates.linkedart.net](https://dates.linkedart.net) (the dates API). How it is hosted is in
[docs/09-hosting.md](docs/09-hosting.md).

## Run the dates service yourself

The date parser is useful outside .NET. Its service is a public container image with no dependencies, so a
Python, R or JavaScript project can run it locally and call `GET /v1/timespan?s=...`:

```
docker run --rm -p 8080:8080 ghcr.io/tomcrane/linked-art-net/dates-service:latest
curl "http://localhost:8080/v1/timespan?s=late%201950s"
```

The API, the settings (your own profiles, rate limits, capture of unreadable inputs) and a Python example are on
[linkedart.net/dates/api](https://linkedart.net/dates/api#docker); the OpenAPI document is served at `/docs`.

## Branches and releases

**`main` is what is deployed. `de-pmc` is where work happens.** Day to day, commit to `de-pmc`; corpus pull
requests from the trainer target it too. To release:

1. Tag on `de-pmc`: `git tag v0.1.x && git push origin v0.1.x`. The "Container images" workflow builds both
   images from that commit and publishes them to GHCR.
2. Fast-forward `main` to the same commit, so the repository's front page always shows the code that is running:
   `git switch main && git merge --ff-only de-pmc && git push origin main && git switch de-pmc`.
3. On the server, `cd /srv/linkedart && docker compose pull && docker compose up -d`.

`main` never receives work directly, so the fast-forward never conflicts; if `--ff-only` ever refuses,
something was committed to `main` by mistake and should be moved to `de-pmc` first. The `pmc` and `pmc-2`
branches are the earlier Paul Mellon Centre pipeline, kept for reference.

## Quick look

```csharp
var nightWatch = new HumanMadeObject()
    .WithContext()
    .WithId("https://linked.art/example/object/nightwatch/1")
    .WithLabel("Night Watch by Rembrandt")
    .WithClassifiedAs(Aat.Painting, metaType: Aat.TypeOfWork)
    .WithName("The Night Watch", n => n.AsPrimaryName().WithLanguage(Aat.English))
    .WithProducedBy(p => p
        .WithCarriedOutBy(Ulan.Person("500011051", "Rembrandt"))
        .WithTookPlaceAt(Tgn.Place("7006952", "Amsterdam"))
        .WithTimeSpan(1642))
    .WithHeightDimension(379.5, Aat.Centimeters, "379.5 cm high")
    .WithStatement(Aat.MaterialStatement, "oil on canvas")
    .WithCurrentOwner(Ulan.Group("500246547", "Rijksmuseum"));

string json = LinkedArtJson.Serialize(nightWatch);          // Linked Art JSON-LD
var back = LinkedArtJson.Deserialize<HumanMadeObject>(json); // reads any Linked Art document by its type
```

## Building

Requires the .NET 10 SDK (`global.json`).

```
dotnet build linked-art-net.slnx
dotnet test  linked-art-net.slnx
dotnet run --project tools/ExampleManifest -- coverage   # compares the examples with linked.art, writes examples/coverage.md
dotnet run --project src/LinkedArtNet.Dates.Service             # the date parsing service on http://localhost:5000/docs
dotnet run --project src/LinkedArtNet.ExamplesSite               # the examples site on http://localhost:5000
dotnet run --project tools/ExampleSourceExtractor                # refreshes examples/source.json after editing an example
```

## Status

Work in progress towards a 1.0 release; see [docs/00-plan.md](docs/00-plan.md) for phases and
[docs/01-current-state.md](docs/01-current-state.md) for an audit of where things stand. The earlier
Paul Mellon Centre transformation pipeline lives in the `pmc-2` branch for reference.

## Licence

MIT. See [LICENSE](LICENSE).
