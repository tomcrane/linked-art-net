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
            // a precoordinated LCSH heading: looked up whole, else decided as its parts; the head may be a dated name heading
            // a heading without "--" is decided whole, and a dated name heading ("Atken, J, fl 1783", "Reuss, Albert, 1889-1975") reads
            // through the MARC profile as its head would (finding 46, 2026-10-22); the manifest needs refreshing for this
            ["keywords"] = new([EntityClass.Type, EntityClass.Person, EntityClass.Group, EntityClass.Place], "marc", Home: "lcsh", ParseDatedOnly: true, Parts: new(PartsShape.Compound, "--", Context: "keywords:sub", HeadContext: "keywords:head"), Collections: ["LCSHAuthorizedHeadings"]),
            ["keywords:head"] = new([EntityClass.Type, EntityClass.Person, EntityClass.Group, EntityClass.Place], "marc", Home: "lcsh", ParseDatedOnly: true, Collections: ["LCSHAuthorizedHeadings"]),
            // a library's subdivisions are more often forms ("--Catalogs", "--Exhibitions") than topics where LCSH has both
            ["keywords:sub"] = new([EntityClass.Type, EntityClass.Place, EntityClass.Period], Home: "lcsh", Collections: ["GenreFormSubdivisions", "Subdivisions"]),
        },
    };

    /// <summary>
    /// What differs from another manifest, in words for a page or a console (contexts present on one side only or read
    /// differently, the sources, the thresholds and policies), naming the two sides as the caller says.
    /// </summary>
    public IReadOnlyList<string> Differences(ProjectManifest other, string thisName = "this manifest", string otherName = "the other")
    {
        var d = new List<string>();
        var cmp = StringComparer.OrdinalIgnoreCase;
        foreach (var k in Contexts.Keys.Except(other.Contexts.Keys, cmp).OrderBy(x => x, cmp)) d.Add($"context {k} is in {thisName} only");
        foreach (var k in other.Contexts.Keys.Except(Contexts.Keys, cmp).OrderBy(x => x, cmp)) d.Add($"context {k} is in {otherName} only");
        foreach (var k in Contexts.Keys.Intersect(other.Contexts.Keys, cmp).OrderBy(x => x, cmp))
            if (JsonSerializer.Serialize(Contexts[k], JsonOptions) != JsonSerializer.Serialize(other.Contexts[k], JsonOptions)) d.Add($"context {k} is read differently");
        if (!Sources.SequenceEqual(other.Sources, cmp)) d.Add($"the sources ({string.Join(", ", Sources)} against {string.Join(", ", other.Sources)})");
        if (AcceptScore != other.AcceptScore || ReviewScore != other.ReviewScore || MinIndependentSources != other.MinIndependentSources
            || AcceptSingleSourceWithDates != other.AcceptSingleSourceWithDates || AcceptUniqueExactSingleSource != other.AcceptUniqueExactSingleSource || MintWhenNothingFound != other.MintWhenNothingFound)
            d.Add("the thresholds or policies");
        return d;
    }
}

/// <summary>A <see cref="ContextRule"/> as the manifest writes it.</summary>
public sealed record ContextRuleJson(List<EntityClass> Classes, string? AgentProfile = null, bool? Qualified = null, PartsRuleJson? Parts = null, string? Home = null, bool? ParseDatedOnly = null, List<string>? Collections = null)
{
    public ContextRule ToRule() => new(Classes, AgentProfile, Qualified ?? false, Parts?.ToRule(), Home, ParseDatedOnly ?? false, Collections);
}

/// <summary>A <see cref="PartsRule"/> as the manifest writes it: <c>{ "shape": "list", "separator": ";" }</c> or <c>{ "shape": "compound", "separator": "--", "context": "keywords:sub", "headContext": "keywords:head" }</c>.</summary>
public sealed record PartsRuleJson(PartsShape Shape, string Separator, string? Context = null, string? HeadContext = null)
{
    public PartsRule ToRule() => new(Shape, Separator, Context, HeadContext);
}
