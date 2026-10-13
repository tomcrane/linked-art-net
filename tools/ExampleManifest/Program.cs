// Keeps examples/ in step with linked.art.
//
//   dotnet run --project tools/ExampleManifest -- crawl      crawl every /model/ page in the sitemap, write examples/manifest.json
//   dotnet run --project tools/ExampleManifest -- fetch      download the site JSON of manifest examples missing from examples/cached/
//   dotnet run --project tools/ExampleManifest -- fetch --all   re-download every manifest example (review the diff before committing)
//   dotnet run --project tools/ExampleManifest -- coverage   compare the manifest with the [LinkedArtExample] methods, write examples/coverage.md
//
// The site has no example index of its own: each example on a model page is followed by an
// "Other Representations" line whose first link is https://linked.art/example/{id}; the JSON is at {id}.json.
// CoverageTests checks that coverage.md is current.

using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinkedArtNet.Examples;

var command = args.Length > 0 ? args[0] : "coverage";
var repoRoot = FindRepoRoot();
var examplesDir = Path.Combine(repoRoot, "examples");
var manifestPath = Path.Combine(examplesDir, "manifest.json");
var cachedDir = Path.Combine(examplesDir, "cached");

switch (command)
{
    case "crawl":
        await Crawl();
        break;
    case "fetch":
        await Fetch(args.Contains("--all"));
        break;
    case "coverage":
        Coverage();
        break;
    case "diff":
        Diff(args.Skip(1).FirstOrDefault());
        break;
    case "validate":
        Validate(args.Skip(1).FirstOrDefault());
        break;
    default:
        Console.Error.WriteLine("Usage: ExampleManifest crawl | fetch [--all] | coverage | diff [filter] | validate [filter]");
        return 2;
}
return 0;

// diff [filter]: every example (or those whose id or method contains the filter) that does not match the site, with the
// first difference and, for a single example, the generated JSON.
void Diff(string? filter)
{
    var examples = ExampleCatalog.All.Where(e => filter is null || e.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                                                || $"{e.Topic}.{e.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    var mismatches = 0;
    foreach (var example in examples)
    {
        var cached = Path.Combine(cachedDir, example.Id.Replace('/', Path.DirectorySeparatorChar) + ".json");
        var generated = LinkedArtNet.Serialization.LinkedArtJson.Serialize(example.Build());
        var difference = File.Exists(cached) ? JsonEquivalence.FirstDifference(File.ReadAllText(cached), generated) : "(no cached site JSON)";
        if (difference is null && examples.Count > 1) continue;
        mismatches++;
        Console.WriteLine($"{example.Id}  {example.Topic}.{example.Name}{(example.SiteIssue is null ? "" : "  [SiteIssue]")}");
        Console.WriteLine($"    {difference ?? "matches the site"}");
        if (examples.Count == 1) Console.WriteLine(generated);
    }
    Console.WriteLine($"{examples.Count} examples, {mismatches} differ from the site");
}

// validate [filter]: schema errors in the cached site JSON and in the generated JSON, grouped by shape so that one
// discrepancy between the site and the 1.0 schemas shows up once, with the records it affects.
void Validate(string? filter)
{
    var schemas = SchemaValidation.Load(Path.Combine(repoRoot, "schemas", "linked-art", "1.0"));
    var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
    void Record(string label, string json)
    {
        try
        {
            foreach (var error in schemas.LeafErrors(json))
            {
                var key = $"{SchemaValidation.SchemaFileFor(System.Text.Json.JsonDocument.Parse(json).RootElement)}  {error.Shape}: {error.Message}";
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
                list.Add(label);
            }
        }
        catch (Exception ex) { Console.WriteLine($"{label}: {ex.Message}"); }
    }

    foreach (var file in Directory.GetFiles(cachedDir, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
    {
        var id = Path.GetRelativePath(cachedDir, file).Replace('\\', '/')[..^".json".Length];
        if (filter is not null && !id.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        Record("site:" + id, File.ReadAllText(file));
    }
    foreach (var example in ExampleCatalog.All)
    {
        if (filter is not null && !example.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
        Record("ours:" + example.Id, LinkedArtNet.Serialization.LinkedArtJson.Serialize(example.Build()));
    }

    foreach (var (key, labels) in groups.OrderByDescending(g => g.Value.Count))
    {
        Console.WriteLine($"{labels.Count,4}  {key}");
        Console.WriteLine($"      {string.Join(" ", labels.Distinct().Take(8))}{(labels.Distinct().Count() > 8 ? " ..." : "")}");
    }
    Console.WriteLine($"{groups.Count} distinct error shapes");
}

async Task Crawl()
{
    using var http = NewHttpClient();
    var sitemap = await http.GetStringAsync("https://linked.art/sitemap.xml");
    var pageUrls = Regex.Matches(sitemap, "<loc>([^<]+)</loc>").Select(m => m.Groups[1].Value)
        .Where(u => u.StartsWith("https://linked.art/model/", StringComparison.Ordinal))
        .ToList();

    var pages = new List<SiteManifest.Page>();
    foreach (var url in pageUrls)
    {
        var html = await http.GetStringAsync(url);
        var examples = ParseExamples(html);
        var title = Regex.Match(html, "<title>([^<]*)</title>").Groups[1].Value.Replace(" - Linked Art", "").Trim();
        var path = url["https://linked.art/model/".Length..].TrimEnd('/');
        Console.WriteLine($"{url}: {examples.Count} examples");
        pages.Add(new SiteManifest.Page(path.Length == 0 ? "model" : path, url, WebUtility.HtmlDecode(title), examples));
    }

    var manifest = new SiteManifest("https://linked.art/sitemap.xml", DateOnly.FromDateTime(DateTime.UtcNow), pages);
    Directory.CreateDirectory(examplesDir);
    File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, SiteManifest.JsonOptions) + "\n", new UTF8Encoding(false));
    Console.WriteLine($"{manifest.Examples.Count} examples ({manifest.ExampleIds.Count} distinct) on {pages.Count} pages -> {Path.GetRelativePath(repoRoot, manifestPath)}");
}

// Each example block: ... <p><strong>Example:</strong></p> <p>description</p> <pre>json</pre> <div mermaid/> Other Representations: <a href="https://linked.art/example/{id}">JSON-LD (raw)</a>
static List<SiteManifest.Example> ParseExamples(string html)
{
    var result = new List<SiteManifest.Example>();
    var headings = Regex.Matches(html, "<h([23]) id=\"([^\"]+)\">(.*?)</h\\1>", RegexOptions.Singleline);
    var markers = Regex.Matches(html, "<p><strong>Example:?</strong>:?\\s*</p>\\s*<p>(.*?)</p>", RegexOptions.Singleline);
    var links = Regex.Matches(html, "Other Representations: <a href=\"https://linked.art/example/([^\"]+)\">JSON-LD \\(raw\\)</a>");

    var previousLinkEnd = 0;
    foreach (Match link in links)
    {
        var heading = headings.LastOrDefault(h => h.Index < link.Index);
        var marker = markers.LastOrDefault(m => m.Index < link.Index && m.Index > previousLinkEnd);
        result.Add(new SiteManifest.Example(
            link.Groups[1].Value,
            heading is null ? null : "#" + heading.Groups[2].Value,
            heading is null ? null : StripTags(heading.Groups[3].Value),
            marker is null ? null : StripTags(marker.Groups[1].Value)));
        previousLinkEnd = link.Index + link.Length;
    }
    return result;
}

static string StripTags(string html) =>
    WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(html, "<[^>]+>", ""), "\\s+", " ")).Trim();

async Task Fetch(bool all)
{
    var manifest = SiteManifest.Load(repoRoot);
    // Missing examples 301 to {id}.json.json for ever, so follow no redirects and treat 301 as absent.
    using var http = NewHttpClient(allowRedirects: false);
    int fetched = 0, skipped = 0, missing = 0;
    foreach (var id in manifest.ExampleIds)
    {
        var target = Path.Combine(cachedDir, id.Replace('/', Path.DirectorySeparatorChar) + ".json");
        if (!all && File.Exists(target)) { skipped++; continue; }

        using var response = await http.GetAsync($"https://linked.art/example/{id}.json");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            Console.WriteLine($"{id}: {(int)response.StatusCode} {response.StatusCode} (not fetched)");
            missing++;
            continue;
        }
        var json = await response.Content.ReadAsStringAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, json, new UTF8Encoding(false));
        Console.WriteLine($"{id}: fetched");
        fetched++;
    }
    Console.WriteLine($"fetched {fetched}, already cached {skipped}, unavailable {missing}");
}

void Coverage()
{
    var manifest = SiteManifest.Load(repoRoot);
    var report = CoverageReport.Build(manifest, ExampleCatalog.All, cachedDir);
    var path = Path.Combine(examplesDir, "coverage.md");
    File.WriteAllText(path, report, new UTF8Encoding(false));
    Console.WriteLine(report.Split('\n').Skip(2).FirstOrDefault(l => l.Length > 0));
    Console.WriteLine($"-> {Path.GetRelativePath(repoRoot, path)}");
}

static HttpClient NewHttpClient(bool allowRedirects = true)
{
    var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = allowRedirects });
    client.DefaultRequestHeaders.UserAgent.ParseAdd("linked-art-net-example-manifest/1.0 (+https://github.com/tomcrane/linked-art-net)");
    return client;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (linked-art-net.slnx).");
}
