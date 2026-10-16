using System.Net;
using LinkedArtNet.Examples;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LinkedArtNet.ExamplesSite.Tests;

public class SiteFixture : WebApplicationFactory<Program>
{
    public static readonly string RepoRoot = FindRepoRoot();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Examples:RepoRoot", RepoRoot);
        builder.UseSetting("TimeSpanService:BaseUrl", "");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root");
    }
}

public class SiteTests(SiteFixture factory) : IClassFixture<SiteFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> Ok(string url)
    {
        var response = await _client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url} -> {(int)response.StatusCode}\n{body[..Math.Min(body.Length, 600)]}");
        return body;
    }

    [Fact]
    public async Task Home_shows_coverage_and_sections()
    {
        var html = await Ok("/");
        Assert.Contains("examples on linked.art", html);
        Assert.Contains("/examples/object-production", html);
        Assert.Contains("Provenance", html);
    }

    private ExampleIndex Index => factory.Services.GetRequiredService<ExampleIndex>();

    [Fact]
    public async Task Section_lists_examples_with_status()
    {
        var section = Index.FindSection("object-production")!;
        var html = await Ok("/examples/object-production");
        Assert.Contains(section.Examples[0].Id, html);
        Assert.Contains("status-validated", html);
    }

    [Fact]
    public async Task Example_page_has_source_both_jsons_and_comparison()
    {
        var page = Index.Pages.Values.First(p => p.IsValidated && p.Properties.Count > 0 && p.Terms.Count > 0);
        var html = await Ok($"/examples/{page.Section}/{page.Id}");
        Assert.Contains("language-csharp", html);
        Assert.Contains("JSON from the C#", html);
        Assert.Contains("JSON on linked.art", html);
        Assert.Contains("equivalent", html);
        Assert.Contains($"/properties/{page.Properties[0]}", html);
        Assert.Contains($"/properties/{ExampleIndex.TermSlug(page.Terms[0])}", html);
        Assert.Contains($"<title>{page.Id}", html);
    }

    [Fact]
    public async Task Example_under_wrong_section_redirects_to_owner()
    {
        var page = Index.Find("object/spring/1")!;
        var wrong = Index.Sections.First(s => s.Slug != page.Section).Slug;
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync($"/examples/{wrong}/object/spring/1");
        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal($"/examples/{page.Section}/object/spring/1", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Property_page_lists_every_example_using_it()
    {
        var ids = Index.ByProperty["produced_by"];
        var html = await Ok("/properties/produced_by");
        foreach (var id in ids) Assert.Contains($"/{id}\"", html);
    }

    [Fact]
    public async Task Site_issue_example_shows_the_reason()
    {
        var blocked = Index.Pages.Values.First(p => p.Status == CoverageReport.Status.SiteIssue);
        var html = await Ok($"/examples/{blocked.Section}/{blocked.Id}");
        Assert.Contains("site issue", html);
        Assert.Contains("class=\"notice\"", html);
    }

    [Fact]
    public async Task Every_manifest_example_has_a_page_with_source_and_json()
    {
        Assert.Equal(183, Index.Total);
        var missingSource = new List<string>();
        foreach (var page in Index.Pages.Values)
        {
            var html = await Ok($"/examples/{page.Section}/{page.Id}");
            Assert.Contains("JSON on linked.art", html);
            if (page.Status != CoverageReport.Status.Missing && (page.Source is null || !html.Contains("language-csharp"))) missingSource.Add(page.Id);
            if (page.Status != CoverageReport.Status.Missing) Assert.Contains("&quot;type&quot;", html); // Razor encodes the JSON for display
        }
        Assert.True(missingSource.Count == 0, "No C# shown for: " + string.Join(", ", missingSource) + ". Run tools/ExampleSourceExtractor.");
    }

    [Theory]
    [InlineData("/search?q=transferred_custody_of", "provenance/")]
    [InlineData("/search?q=Rembrandt", "rembrandt")]
    [InlineData("/properties", "/properties/produced_by")]
    [InlineData("/properties", "aat:300456841")]   // a term the site writes without a _label still gets a visible tag
    [InlineData("/properties/aat-300033618", "painting")]
    [InlineData("/examples/base/object/spring/2", "ClassifiedAs = [Aat.Artwork, Aat.Painting]")]
    [InlineData("/examples/base/object/spring/2", "Plain C#")]
    [InlineData("/examples/object-aboutness/object/spring/5", "Metatype classification")]
    [InlineData("/idioms", "Statement in one call")]
    [InlineData("/dates", "Free-text dates")]
    [InlineData("/dates/api", "/v1/timespan?s=c.%201850")]
    [InlineData("/dates/api", "&quot;canonical&quot;: &quot;1850~&quot;")]
    [InlineData("/dates?s=late%201950s", "late 195X")]
    [InlineData("/dates?s=1606-1669&intent=pair", "birthDeath")]
    [InlineData("/dates?s=4.5%20Ga%20ago", "Y-4549997974/Y-4449997974")]
    [InlineData("/dates?s=nothing%20datable", "unparsed")]
    [InlineData("/dates?s=1606-1669", "/dates/result.json?s=1606-1669")]
    [InlineData("/dates/result.json?s=1606-1669", "\"canonical\": \"1606/1669\"")]
    [InlineData("/dates/corpus", "verified")]
    [InlineData("/dates/corpus?status=verified&result=fail", "0 matching")]
    [InlineData("/dates/corpus?p=2", "page 2 of")]
    [InlineData("/dates/corpus?file=imported/getty.json&status=proposed", "getty")]
    [InlineData("/about", "MIT")]
    [InlineData("/about", "WithProducedBy(p =&gt; p")]
    [InlineData("/about", "&quot;produced_by&quot;")]
    public async Task Pages_render(string url, string expected)
    {
        var html = await Ok(url);
        Assert.Contains(expected, html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/examples/nonesuch")]
    [InlineData("/examples/object-production/object/nonesuch/99")]
    [InlineData("/properties/nonesuch_property")]
    public async Task Unknown_pages_are_404(string url)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_reports_counts()
    {
        var json = await Ok("/healthz");
        Assert.Contains("\"examples\":183", json);
    }
}
