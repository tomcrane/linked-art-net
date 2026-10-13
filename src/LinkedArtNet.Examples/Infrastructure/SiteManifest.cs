using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet.Examples;

/// <summary>
/// <c>examples/manifest.json</c>: every example linked.art publishes, page by page, as crawled by
/// <c>tools/ExampleManifest</c>. An example id can appear on more than one page.
/// </summary>
public sealed record SiteManifest(string Source, DateOnly Crawled, IReadOnlyList<SiteManifest.Page> Pages)
{
    public sealed record Page(string Path, string Url, string Title, IReadOnlyList<Example> Examples);

    /// <param name="Id">Path under <c>https://linked.art/example/</c>.</param>
    /// <param name="Anchor">Fragment of the nearest heading above the example on the page.</param>
    /// <param name="Heading">Text of that heading.</param>
    /// <param name="Description">The paragraph introducing the example, when the page has one.</param>
    public sealed record Example(string Id, string? Anchor, string? Heading, string? Description);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string PathIn(string repoRoot) => System.IO.Path.Combine(repoRoot, "examples", "manifest.json");

    public static SiteManifest Load(string repoRoot) =>
        JsonSerializer.Deserialize<SiteManifest>(File.ReadAllText(PathIn(repoRoot)), JsonOptions)
        ?? throw new InvalidDataException("examples/manifest.json is empty.");

    /// <summary>Every (page, example) occurrence in site order.</summary>
    [JsonIgnore]
    public IReadOnlyList<(Page Page, Example Example)> Examples =>
        Pages.SelectMany(p => p.Examples.Select(e => (p, e))).ToList();

    /// <summary>Distinct example ids in site order.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> ExampleIds => Examples.Select(x => x.Example.Id).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>The pages on which an example appears.</summary>
    public IReadOnlyList<Page> PagesFor(string id) => Pages.Where(p => p.Examples.Any(e => e.Id == id)).ToList();
}
