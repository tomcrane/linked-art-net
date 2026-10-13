namespace LinkedArtNet.Examples.Tests;

/// <summary>The catalog against <c>examples/manifest.json</c>, and the committed coverage dashboard.</summary>
public class CoverageTests
{
    private static readonly SiteManifest Manifest = SiteManifest.Load(TestPaths.RepoRoot);

    [Fact]
    public void Example_keys_are_unique()
    {
        var duplicates = ExampleCatalog.All.GroupBy(e => e.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, "Duplicate example methods: " + string.Join(", ", duplicates));
    }

    [Fact]
    public void An_id_has_one_method_unless_the_site_publishes_two_examples_under_it()
    {
        // The site has several ids used by two different examples (one JSON wins); every extra method must say so.
        var unexplained = ExampleCatalog.All.GroupBy(e => e.Id)
            .Where(g => g.Count(e => e.SiteIssue is null) > 1)
            .Select(g => g.Key + ": " + string.Join(", ", g.Select(e => e.Key)))
            .ToList();
        Assert.True(unexplained.Count == 0, "Ids with more than one method and no SiteIssue explaining the collision: " + string.Join("; ", unexplained));
    }

    [Fact]
    public void Every_example_is_on_the_site()
    {
        var siteIds = Manifest.ExampleIds.ToHashSet(StringComparer.Ordinal);
        var orphans = ExampleCatalog.All.Where(e => !siteIds.Contains(e.Id)).Select(e => $"{e.Id} ({e.Topic}.{e.Name})").ToList();
        Assert.True(orphans.Count == 0, "Examples not in examples/manifest.json (re-crawl, or fix the id): " + string.Join(", ", orphans));
    }

    [Fact]
    public void Manifest_has_no_duplicate_pages_and_every_page_url_matches_its_path()
    {
        Assert.Equal(Manifest.Pages.Count, Manifest.Pages.Select(p => p.Path).Distinct().Count());
        Assert.All(Manifest.Pages, p => Assert.StartsWith("https://linked.art/model/", p.Url));
    }

    [Fact]
    public void Coverage_report_is_current()
    {
        var expected = CoverageReport.Build(Manifest, ExampleCatalog.All, TestPaths.CachedExamples);
        var path = Path.Combine(TestPaths.RepoRoot, "examples", "coverage.md");
        var actual = File.Exists(path) ? File.ReadAllText(path) : "";
        Assert.True(Normalise(expected) == Normalise(actual), "examples/coverage.md is stale: run `dotnet run --project tools/ExampleManifest -- coverage`.");
    }

    [Fact]
    public void Every_site_example_is_implemented()
    {
        // 100% since Phase 1 step 8: a re-crawl that finds new site examples fails here until they get a method.
        var missing = Manifest.ExampleIds.Where(id => ExampleCatalog.Find(id) is null).ToList();
        Assert.True(missing.Count == 0, "Site examples without a [LinkedArtExample] method: " + string.Join(", ", missing));
    }

    private static string Normalise(string text) => text.Replace("\r\n", "\n").TrimEnd();
}
