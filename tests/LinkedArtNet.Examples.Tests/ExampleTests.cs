using LinkedArtNet.Serialization;
using Xunit.Abstractions;

namespace LinkedArtNet.Examples.Tests;

/// <summary>
/// One theory row per <c>[LinkedArtExample]</c> method, keyed by <c>Topic.Method</c>. The generated JSON must equal
/// the site's cached JSON unless the method carries a <see cref="SiteIssueAttribute"/>, in which case the mismatch is
/// expected and reported. Schema validity is judged against the site: the generated JSON may have no schema error
/// the site's own JSON does not have (the site and the 1.0 schemas disagree in places; see docs/upstream-issues.md).
/// </summary>
public class ExampleTests(ITestOutputHelper output)
{
    internal static readonly Lazy<SchemaValidation> Schemas = new(() => SchemaValidation.Load(TestPaths.Schemas));

    public static TheoryData<string> ExampleKeys() => new(ExampleCatalog.All.Select(e => e.Key));

    [Theory]
    [MemberData(nameof(ExampleKeys))]
    public void Object_id_is_the_site_uri(string key)
    {
        var example = ExampleCatalog.FindByKey(key)!;
        var built = example.Build();
        Assert.Equal(example.Uri, built.Id);
        Assert.NotNull(built.Context);
    }

    [Theory]
    [MemberData(nameof(ExampleKeys))]
    public void Generated_json_matches_the_site(string key)
    {
        var example = ExampleCatalog.FindByKey(key)!;
        var cached = TestPaths.CachedFile(example.Id);
        Assert.True(File.Exists(cached), $"No cached site JSON for {example.Id}: run `dotnet run --project tools/ExampleManifest -- fetch`.");

        var generated = LinkedArtJson.Serialize(example.Build());
        var difference = JsonEquivalence.FirstDifference(File.ReadAllText(cached), generated);

        if (example.SiteIssue is { } issue)
        {
            output.WriteLine($"Site issue: {issue.Reason} {issue.Url}");
            output.WriteLine($"Difference: {difference ?? "(none)"}");
            Assert.True(difference is not null, $"{key} now matches the site; remove its [SiteIssue].");
            return;
        }

        if (difference is not null) output.WriteLine(generated);
        Assert.True(difference is null, $"{example.Id}: {difference}");
    }

    [Theory]
    [MemberData(nameof(ExampleKeys))]
    public void Generated_json_has_no_schema_errors_beyond_the_sites(string key)
    {
        var example = ExampleCatalog.FindByKey(key)!;
        var ours = Schemas.Value.ErrorShapes(LinkedArtJson.Serialize(example.Build()));
        var cached = TestPaths.CachedFile(example.Id);
        var site = File.Exists(cached) ? Schemas.Value.ErrorShapes(File.ReadAllText(cached)) : new HashSet<string>();

        var extra = ours.Except(site).Order(StringComparer.Ordinal).ToList();
        if (example.SiteIssue is not null && extra.Count > 0)
        {
            // A different example than the site's JSON: its errors cannot be compared with that JSON's.
            output.WriteLine("Schema errors (site issue, not compared): " + string.Join("; ", extra));
            return;
        }
        Assert.True(extra.Count == 0, $"{example.Id} has schema errors the site's JSON does not:\n  " + string.Join("\n  ", extra));
    }

    [Theory]
    [MemberData(nameof(ExampleKeys))]
    public void Generated_json_round_trips(string key)
    {
        var generated = LinkedArtJson.Serialize(ExampleCatalog.FindByKey(key)!.Build());
        var reread = LinkedArtJson.Deserialize<LinkedArtObject>(generated);
        Assert.NotNull(reread);
        var difference = JsonEquivalence.FirstDifference(generated, LinkedArtJson.Serialize(reread));
        Assert.True(difference is null, $"{key}: {difference}");
    }
}
