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

Planned (see the plan): `LinkedArtNet.Dates` (free-text date strings to Linked Art TimeSpans), a Kestrel
service for it, an examples website, and a ground-truth date corpus.

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
