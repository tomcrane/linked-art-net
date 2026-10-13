# linked-art-net

A .NET implementation of [Linked Art](https://linked.art/), the JSON-LD profile of CIDOC-CRM used by
museums, libraries and archives to describe art and cultural heritage.

| Project | What |
|---|---|
| `src/LinkedArtNet` | The model library: classes for Linked Art 1.0, JSON-LD serialisation, fluent authoring API, Getty vocabulary helpers. |
| `src/LinkedArtNet.Examples` | C# that regenerates the examples on linked.art and compares the output with the published JSON. |
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
    .WithClassifiedAs(Getty.Painting, Getty.TypeOfWork);

var production = new Activity(Types.Production) { TimeSpan = LinkedArtTimeSpan.FromYear(1642) };
production.CarriedOutBy = [new Person().WithId("http://vocab.getty.edu/ulan/500011051").WithLabel("Rembrandt")];
nightWatch.ProducedBy = production;

Console.WriteLine(JsonSerializer.Serialize(nightWatch, new JsonSerializerOptions { WriteIndented = true }));
```

## Building

Requires the .NET 10 SDK (`global.json`).

```
dotnet build linked-art-net.slnx
dotnet test  linked-art-net.slnx
dotnet run --project src/LinkedArtNet.Examples   # regenerates and validates the site examples
```

## Status

Work in progress towards a 1.0 release; see [docs/00-plan.md](docs/00-plan.md) for phases and
[docs/01-current-state.md](docs/01-current-state.md) for an audit of where things stand. The earlier
Paul Mellon Centre transformation pipeline lives in the `pmc-2` branch for reference.

## Licence

MIT. See [LICENSE](LICENSE).
