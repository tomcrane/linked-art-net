using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LinkedArtNet.Examples;
using LinkedArtNet.Serialization;
using LinkedArtNet.Vocabulary;
using Microsoft.Extensions.Options;

namespace LinkedArtNet.ExamplesSite.Services;

public sealed class ExamplesOptions
{
    /// <summary>The repository root (holds examples/, corpus/, profiles/); relative paths resolve against the content root.</summary>
    public string RepoRoot { get; set; } = "../..";
}

/// <summary>One example method's C#, from examples/source.json (written by tools/ExampleSourceExtractor).</summary>
public sealed record SourceEntry(string Id, string Key, string Topic, string Method, string File, int Line, string Source, string? SiteIssue, IReadOnlyList<string>? Idioms = null);

/// <summary>A section of the site, mirroring one linked.art model page.</summary>
public sealed record Section(string Slug, string Path, string Title, string Url, IReadOnlyList<ExamplePage> Examples)
{
    public int Implemented => Examples.Count(e => e.Status is not CoverageReport.Status.Missing);
}

/// <summary>Everything the site shows for one example id.</summary>
public sealed record ExamplePage(
    string Id, string Section, string? Heading, string? Anchor, string? Description, string SiteUrl, string JsonUrl,
    CoverageReport.Status Status, string? Detail, SourceEntry? Source, string? GeneratedJson, string? SiteJson, string? Difference,
    IReadOnlyList<string> Properties, IReadOnlyList<string> Terms, string TypeName, string? PlainCSharp = null)
{
    public bool IsValidated => Status == CoverageReport.Status.Validated;
}

/// <summary>
/// Built once at startup: the manifest, the catalog, the source text, generated and cached JSON for every example,
/// the first semantic difference, and reverse indexes by JSON property and vocabulary term.
/// </summary>
public sealed class ExampleIndex
{
    public ExampleIndex(IOptions<ExamplesOptions> options, IHostEnvironment environment)
    {
        RepoRoot = Path.GetFullPath(Path.IsPathRooted(options.Value.RepoRoot) ? options.Value.RepoRoot : Path.Combine(environment.ContentRootPath, options.Value.RepoRoot));
        Manifest = SiteManifest.Load(RepoRoot);
        var sourcePath = Path.Combine(RepoRoot, "examples", "source.json");
        var sources = File.Exists(sourcePath)
            ? JsonSerializer.Deserialize<List<SourceEntry>>(File.ReadAllText(sourcePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? []
            : [];
        var sourceByKey = sources.ToDictionary(s => s.Key, StringComparer.Ordinal);
        var cachedDir = Path.Combine(RepoRoot, "examples", "cached");

        var pages = new Dictionary<string, ExamplePage>(StringComparer.Ordinal);
        var byProperty = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var byTerm = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var byIdiom = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var sections = new List<Section>();

        foreach (var page in Manifest.Pages.Where(p => p.Examples.Count > 0))
        {
            var slug = page.Path.Replace('/', '-');
            var examplePages = new List<ExamplePage>();
            foreach (var e in page.Examples)
            {
                if (!pages.TryGetValue(e.Id, out var built))
                {
                    built = Build(e, slug, cachedDir, sourceByKey);
                    pages[e.Id] = built;
                    foreach (var p in built.Properties) Add(byProperty, p, e.Id);
                    foreach (var t in built.Terms) Add(byTerm, t, e.Id);
                    foreach (var i in built.Source?.Idioms ?? []) Add(byIdiom, i, e.Id);
                }
                examplePages.Add(built with { Section = built.Section }); // the first page an example appears on owns it
            }
            sections.Add(new Section(slug, page.Path, page.Title, page.Url, examplePages));
        }
        Sections = sections;
        Pages = pages;
        ByProperty = byProperty.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.Distinct().ToList(), StringComparer.Ordinal);
        ByTerm = byTerm.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.Distinct().ToList(), StringComparer.Ordinal);
        ByIdiom = byIdiom.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.Distinct().ToList(), StringComparer.Ordinal);
    }

    public string RepoRoot { get; }
    public SiteManifest Manifest { get; }
    public IReadOnlyList<Section> Sections { get; }
    public IReadOnlyDictionary<string, ExamplePage> Pages { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ByProperty { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ByTerm { get; }

    /// <summary>Example ids by idiom key (see Idioms.cs in the examples project); the keys come from examples/source.json.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ByIdiom { get; }

    public int Total => Pages.Count;
    public int Implemented => Pages.Values.Count(p => p.Status is not CoverageReport.Status.Missing);
    public int Validated => Pages.Values.Count(p => p.Status is CoverageReport.Status.Validated);
    public int Blocked => Pages.Values.Count(p => p.Status is CoverageReport.Status.SiteIssue);

    public Section? FindSection(string slug) => Sections.FirstOrDefault(s => s.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public ExamplePage? Find(string id) => Pages.GetValueOrDefault(id);

    /// <summary>Title, description, method name, property names and term labels, case-insensitive substring match.</summary>
    public IReadOnlyList<ExamplePage> Search(string query)
    {
        var q = query.Trim();
        if (q.Length == 0) return [];
        return Pages.Values.Where(p =>
                p.Id.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (p.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Heading?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Source?.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (p.Source?.Source.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || p.Properties.Any(x => x.Contains(q, StringComparison.OrdinalIgnoreCase))
                || p.Terms.Any(x => x.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
    }

    private static ExamplePage Build(SiteManifest.Example e, string section, string cachedDir, Dictionary<string, SourceEntry> sourceByKey)
    {
        var example = ExampleCatalog.Find(e.Id);
        var cachedPath = Path.Combine(cachedDir, e.Id.Replace('/', Path.DirectorySeparatorChar) + ".json");
        var siteJson = File.Exists(cachedPath) ? File.ReadAllText(cachedPath) : null;
        string? generated = null, difference = null, typeName = "", plain = null;
        var properties = new HashSet<string>(StringComparer.Ordinal);
        var terms = new HashSet<string>(StringComparer.Ordinal);
        var row = example is null ? new CoverageReport.Row(e.Id, CoverageReport.Status.Missing, null, null) : CoverageReport.Evaluate(example, cachedDir);

        if (example is not null)
        {
            var built = example.Build();
            generated = LinkedArtJson.Serialize(built);
            plain = LinkedArtCSharp.Write(built, Variable(built));
            typeName = built.Type;
            if (siteJson is not null) difference = JsonEquivalence.FirstDifference(siteJson, generated);
            Collect(JsonNode.Parse(generated), properties, terms, true);
        }
        else if (siteJson is not null)
        {
            var node = JsonNode.Parse(siteJson);
            typeName = node?["type"]?.GetValue<string>() ?? "";
            Collect(node, properties, terms, true);
        }

        return new ExamplePage(e.Id, section, e.Heading, e.Anchor, e.Description, Example.SiteRoot + e.Id, Example.SiteRoot + e.Id + ".json",
            row.Status, row.Detail, example is null ? null : sourceByKey.GetValueOrDefault(example.Key), generated, siteJson, difference,
            properties.Order(StringComparer.Ordinal).ToList(), terms.Order(StringComparer.Ordinal).ToList(), typeName, plain);
    }

    /// <summary>A variable name for the plain C#: the class, camel-cased (<c>humanMadeObject</c>, <c>person</c>).</summary>
    private static string Variable(LinkedArtObject built)
    {
        var name = built.GetType().Name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static void Collect(JsonNode? node, HashSet<string> properties, HashSet<string> terms, bool root)
    {
        switch (node)
        {
            case JsonObject o:
                if (!root && o["id"]?.GetValue<string>() is { } id && o["type"]?.GetValue<string>() is { } type && IsVocabulary(id))
                    terms.Add($"{type}:{id}|{o["_label"]?.GetValue<string>() ?? FallbackLabel(id)}");
                foreach (var (k, v) in o)
                {
                    if (k is not ("@context" or "id" or "type" or "_label")) properties.Add(k);
                    Collect(v, properties, terms, false);
                }
                break;
            case JsonArray a:
                foreach (var item in a) Collect(item, properties, terms, false);
                break;
        }
    }

    private static bool IsVocabulary(string id) => id.StartsWith("http://vocab.getty.edu/", StringComparison.Ordinal) || id.StartsWith("https://vocab.getty.edu/", StringComparison.Ordinal);

    private static void Add(Dictionary<string, List<string>> index, string key, string id)
    {
        if (!index.TryGetValue(key, out var list)) index[key] = list = [];
        list.Add(id);
    }

    /// <summary>
    /// A term written without a <c>_label</c> (some site examples omit it) is shown under the registry's label when
    /// the id is registered, so it joins the labelled uses of the same concept, and otherwise as its short id.
    /// </summary>
    private static string FallbackLabel(string id) =>
        (id.StartsWith(Aat.Base, StringComparison.Ordinal) ? Aat.TryGet(id)?.Label : null)
        ?? Regex.Replace(id, "^https?://vocab.getty.edu/", "").Replace('/', ':');

    public static string TermLabel(string term) => term.Contains('|') ? term[(term.IndexOf('|') + 1)..] : term;
    public static string TermId(string term) => term.Contains('|') ? term[(term.IndexOf(':') + 1)..term.IndexOf('|')] : term;
    public static string TermType(string term) => term.Contains(':') ? term[..term.IndexOf(':')] : "";
    public static string TermSlug(string term) => Regex.Replace(TermId(term), "^https?://vocab.getty.edu/", "").Replace('/', '-');
}
