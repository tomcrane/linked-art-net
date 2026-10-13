using System.Text.Json;
using System.Text.Json.Serialization;
using Json.Schema;

namespace LinkedArtNet.Dates.Corpus;

public enum EntryStatus { Verified, Proposed, Disputed }

/// <summary>A TimeSpan's four bounds as the corpus writes them (xsd:dateTime strings).</summary>
public sealed record ExpectedTimeSpan
{
    [JsonPropertyName("begin_of_the_begin")] public string? BeginOfTheBegin { get; init; }
    [JsonPropertyName("end_of_the_begin")] public string? EndOfTheBegin { get; init; }
    [JsonPropertyName("begin_of_the_end")] public string? BeginOfTheEnd { get; init; }
    [JsonPropertyName("end_of_the_end")] public string? EndOfTheEnd { get; init; }
}

public sealed record ExpectedYears(long? Start, long? End);

/// <summary>What the parser should produce; every field is optional and only set fields are checked.</summary>
public sealed record Expectation
{
    public string? Status { get; init; }
    public string? Kind { get; init; }
    public string? Canonical { get; init; }
    public string? Edtf { get; init; }
    [JsonPropertyName("timespan")] public ExpectedTimeSpan? TimeSpan { get; init; }
    public ExpectedTimeSpan? Begin { get; init; }
    public ExpectedTimeSpan? End { get; init; }
    public ExpectedYears? Years { get; init; }
    public IReadOnlyList<string>? Flags { get; init; }
    public IReadOnlyList<string>? NotFlags { get; init; }
    public double? MinConfidence { get; init; }
    public double? MaxConfidence { get; init; }
    public int ToleranceYears { get; init; }
}

public sealed record CorpusSource(string Name, string Licence, string? Url = null, string? Attribution = null, string? Notes = null);

public sealed record CorpusDefaults(string? Intent = null, string? Profile = null, EntryStatus? Status = null, IReadOnlyList<string>? Tags = null);

/// <summary>One ground-truth string with its expectation, after the file's defaults have been applied.</summary>
public sealed record CorpusEntry
{
    public required string Id { get; init; }
    public required string Input { get; init; }
    public string Intent { get; init; } = "auto";
    public string Profile { get; init; } = "default";
    public IReadOnlyList<string> Tags { get; init; } = [];
    public required Expectation Expect { get; init; }
    public EntryStatus Status { get; init; } = EntryStatus.Proposed;
    public string? Notes { get; init; }
    public string? SourceExpectation { get; init; }

    /// <summary>The file this entry came from, relative to the corpus root.</summary>
    [JsonIgnore] public string File { get; init; } = "";

    [JsonIgnore] public Intent ParsedIntent => Intent switch { "single" => Dates.Intent.Single, "pair" => Dates.Intent.Pair, _ => Dates.Intent.Auto };
}

/// <summary>A corpus JSON file: a source, defaults, and entries.</summary>
public sealed record CorpusFile
{
    [JsonPropertyName("$schema")] public string? Schema { get; init; }
    public required CorpusSource Source { get; init; }
    public CorpusDefaults? Defaults { get; init; }
    public required IReadOnlyList<CorpusEntry> Entries { get; init; }

    [JsonIgnore] public string Path { get; init; } = "";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Loads a file and applies its defaults to every entry.</summary>
    public static CorpusFile Load(string path, string? corpusRoot = null)
    {
        var file = JsonSerializer.Deserialize<CorpusFile>(System.IO.File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"{path}: empty corpus file.");
        var relative = corpusRoot is null ? System.IO.Path.GetFileName(path) : System.IO.Path.GetRelativePath(corpusRoot, path).Replace('\\', '/');
        var d = file.Defaults;
        var raw = JsonDocument.Parse(System.IO.File.ReadAllText(path));
        var entries = new List<CorpusEntry>();
        var rawEntries = raw.RootElement.GetProperty("entries").EnumerateArray().ToList();
        for (var i = 0; i < file.Entries.Count; i++)
        {
            var e = file.Entries[i];
            var r = rawEntries[i];
            entries.Add(e with
            {
                File = relative,
                Intent = r.TryGetProperty("intent", out _) ? e.Intent : d?.Intent ?? "auto",
                Profile = r.TryGetProperty("profile", out _) ? e.Profile : d?.Profile ?? "default",
                Status = r.TryGetProperty("status", out _) ? e.Status : d?.Status ?? EntryStatus.Proposed,
                Tags = (d?.Tags ?? []).Concat(e.Tags).Distinct(StringComparer.Ordinal).ToList(),
            });
        }
        return file with { Path = relative, Entries = entries };
    }

    /// <summary>Every corpus file under a root (seed/, authored/, imported/, proposed/), in path order.</summary>
    public static IReadOnlyList<CorpusFile> LoadAll(string corpusRoot) =>
        Directory.GetFiles(corpusRoot, "*.json", SearchOption.AllDirectories)
            .Where(p => !p.Replace('\\', '/').Contains("/schema/") && System.IO.Path.GetFileName(p) != "report.json")
            .Order(StringComparer.Ordinal)
            .Select(p => Load(p, corpusRoot))
            .ToList();

    private static readonly Dictionary<string, JsonSchema> Schemas = new(StringComparer.Ordinal);

    /// <summary>Validates a file against corpus/schema/corpus-entry.schema.json; returns the error messages.</summary>
    public static IReadOnlyList<string> SchemaErrors(string path, string schemaPath)
    {
        // JsonSchema.Net registers a schema's $id globally on load and refuses to load it twice.
        JsonSchema schema;
        lock (Schemas)
        {
            if (!Schemas.TryGetValue(schemaPath, out schema!)) Schemas[schemaPath] = schema = JsonSchema.FromFile(schemaPath);
        }
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(path));
        var results = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid) return [];
        return (results.Details ?? [])
            .Where(d => d.Errors is { Count: > 0 })
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))
            .Distinct()
            .ToList();
    }
}
