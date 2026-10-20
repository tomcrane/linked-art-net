using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>
/// What a project tells the reconciler (design doc 12, section 4; doc 10, section 5): the context rules, the
/// thresholds, the single-source rule. Stored as the project's manifest JSON; the pipeline runner (doc 10, step 4)
/// adds the sources and adapters to it.
/// </summary>
public sealed record ProjectManifest
{
    public Dictionary<string, ContextRuleJson> Contexts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public double AcceptScore { get; init; } = 0.9;
    public double ReviewScore { get; init; } = 0.6;
    public int MinIndependentSources { get; init; } = 2;
    public bool AcceptSingleSourceWithDates { get; init; } = true;
    /// <summary>Accept an undated string's single exact match in one source when it is the only candidate at any score (policy; see ReconcilerOptions).</summary>
    public bool AcceptUniqueExactSingleSource { get; init; } = true;
    public bool MintWhenNothingFound { get; init; } = true;
    /// <summary>The local indexes to consult, in order.</summary>
    public List<string> Sources { get; init; } = ["ulan", "aat", "tgn", "loc", "lcsh"];

    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static ProjectManifest FromJson(string? json) => string.IsNullOrWhiteSpace(json) ? new ProjectManifest() : JsonSerializer.Deserialize<ProjectManifest>(json, JsonOptions) ?? new ProjectManifest();

    public ReconcilerOptions ToOptions() => new()
    {
        AcceptScore = AcceptScore,
        ReviewScore = ReviewScore,
        MinIndependentSources = MinIndependentSources,
        AcceptSingleSourceWithDates = AcceptSingleSourceWithDates,
        AcceptUniqueExactSingleSource = AcceptUniqueExactSingleSource,
        MintWhenNothingFound = MintWhenNothingFound,
        Contexts = Contexts.ToDictionary(kv => kv.Key, kv => kv.Value.ToRule(), StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>How the PMC library's fields are read: the old source_string_map sources. Sale catalogues name auctioneers and dealers in corpauthor and publisher, some of them people.</summary>
    public static ProjectManifest PmcDefault => new()
    {
        Contexts = new Dictionary<string, ContextRuleJson>(StringComparer.OrdinalIgnoreCase)
        {
            ["persauthorfull"] = new([EntityClass.Person], "default"),
            ["corpauthor"] = new([EntityClass.Group, EntityClass.Person], "default"),
            ["publisher"] = new([EntityClass.Group, EntityClass.Person], "default"),
            ["place"] = new([EntityClass.Place], Qualified: true, Parts: new(PartsShape.List, ";")),
            ["keywords"] = new([EntityClass.Type, EntityClass.Person, EntityClass.Group, EntityClass.Place]),
        },
    };
}

/// <summary>A <see cref="ContextRule"/> as the manifest writes it.</summary>
public sealed record ContextRuleJson(List<EntityClass> Classes, string? AgentProfile = null, bool? Qualified = null, PartsRuleJson? Parts = null)
{
    public ContextRule ToRule() => new(Classes, AgentProfile, Qualified ?? false, Parts?.ToRule());
}

/// <summary>A <see cref="PartsRule"/> as the manifest writes it: <c>{ "shape": "list", "separator": ";" }</c>.</summary>
public sealed record PartsRuleJson(PartsShape Shape, string Separator, string? Context = null)
{
    public PartsRule ToRule() => new(Shape, Separator, Context);
}
