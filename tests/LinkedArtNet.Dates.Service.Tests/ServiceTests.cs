using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LinkedArtNet.Dates.Service.Tests;

public class ServiceFixture : WebApplicationFactory<Program>
{
    public static readonly string RepoRoot = FindRepoRoot();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Parser:ProfilesDirectory", Path.Combine(RepoRoot, "profiles"));
        builder.UseSetting("Parser:MaxInputsPerRequest", "5");
        builder.UseSetting("Parser:RequestsPerMinute", "10000");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root");
    }
}

public class ServiceTests(ServiceFixture factory) : IClassFixture<ServiceFixture>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly Lazy<JsonSchema> ResultSchema = new(() => JsonSchema.FromFile(Path.Combine(ServiceFixture.RepoRoot, "schemas", "timespan-result.schema.json")));

    private static void AssertMatchesSchema(JsonNode? node)
    {
        using var document = JsonDocument.Parse(node!.ToJsonString());
        var results = ResultSchema.Value.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(results.IsValid, string.Join("\n", (results.Details ?? []).Where(d => d.Errors is { Count: > 0 }).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}"))));
    }

    [Fact]
    public async Task Get_parses_one_string()
    {
        var response = await _client.GetAsync("/v1/timespan?s=late%201950s");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("parsed", json["status"]!.GetValue<string>());
        Assert.Equal("single", json["kind"]!.GetValue<string>());
        Assert.Equal("late 195X", json["canonical"]!.GetValue<string>());
        Assert.Equal("1957/1959", json["edtf"]!.GetValue<string>());
        Assert.Equal("late 1950s", json["timespan"]!["_label"]!.GetValue<string>());
        Assert.Equal("TimeSpan", json["timespan"]!["type"]!.GetValue<string>());
        Assert.Equal("1957-01-01T00:00:00Z", json["timespan"]!["begin_of_the_begin"]!.GetValue<string>());
        Assert.Equal("grammar", json["strategy"]!.GetValue<string>());
        Assert.Equal(TimeSpanParser.Version, json["parserVersion"]!.GetValue<string>());
        AssertMatchesSchema(json);

        Assert.StartsWith("public, max-age=", response.Headers.CacheControl!.ToString());
        Assert.NotNull(response.Headers.ETag);
    }

    [Fact]
    public async Task Get_honours_if_none_match()
    {
        var first = await _client.GetAsync("/v1/timespan?s=1984");
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/timespan?s=1984");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Get_pair_intent_returns_begin_and_end()
    {
        var json = JsonNode.Parse(await _client.GetStringAsync("/v1/timespan?s=1606-1669&intent=pair"))!;
        Assert.Equal("birthDeath", json["kind"]!.GetValue<string>());
        Assert.Null(json["timespan"]?.AsValue());
        Assert.Equal("1606-01-01T00:00:00Z", json["begin"]!["begin_of_the_begin"]!.GetValue<string>());
        Assert.Equal("1669-12-31T23:59:59Z", json["end"]!["end_of_the_end"]!.GetValue<string>());
        AssertMatchesSchema(json);
    }

    [Fact]
    public async Task Get_shape_timespan_returns_only_the_span()
    {
        var single = JsonNode.Parse(await _client.GetStringAsync("/v1/timespan?s=c.%201850&shape=timespan"))!;
        Assert.Equal("TimeSpan", single["type"]!.GetValue<string>());
        Assert.Equal("c. 1850", single["_label"]!.GetValue<string>());

        var pair = JsonNode.Parse(await _client.GetStringAsync("/v1/timespan?s=1606-1669&intent=pair&shape=timespan"))!;
        Assert.NotNull(pair["begin"]);
        Assert.NotNull(pair["end"]);
    }

    [Fact]
    public async Task Get_profile_changes_the_answer()
    {
        var popular = JsonNode.Parse(await _client.GetStringAsync("/v1/timespan?s=19th%20century"))!;
        var strict = JsonNode.Parse(await _client.GetStringAsync("/v1/timespan?s=saec.%20XIX&profile=medieval-manuscripts"))!;
        Assert.Equal("1800-01-01T00:00:00Z", popular["timespan"]!["begin_of_the_begin"]!.GetValue<string>());
        Assert.Equal("1801-01-01T00:00:00Z", strict["timespan"]!["begin_of_the_begin"]!.GetValue<string>());
        Assert.Equal("medieval-manuscripts", strict["profile"]!.GetValue<string>());
    }

    [Fact]
    public async Task Get_label_policy_and_inner_bounds_options()
    {
        var json = JsonNode.Parse(await _client.GetStringAsync("/v1/timespan?s=c.%201850&labelPolicy=labelAndName"))!;
        Assert.Equal("c. 1850", json["timespan"]!["identified_by"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task Unparsed_input_is_200_with_status_unparsed()
    {
        var json = JsonNode.Parse(await _client.GetStringAsync("/v1/timespan?s=nothing%20datable%20here"))!;
        Assert.Equal("unparsed", json["status"]!.GetValue<string>());
        Assert.Equal("none", json["kind"]!.GetValue<string>());
        Assert.Null(json["timespan"]?.AsValue());
        AssertMatchesSchema(json);
    }

    [Theory]
    [InlineData("/v1/timespan", HttpStatusCode.BadRequest)]
    [InlineData("/v1/timespan?s=", HttpStatusCode.BadRequest)]
    [InlineData("/v1/timespan?s=1984&intent=sideways", HttpStatusCode.BadRequest)]
    [InlineData("/v1/timespan?s=1984&shape=cube", HttpStatusCode.BadRequest)]
    [InlineData("/v1/timespan?s=1984&profile=nonesuch", HttpStatusCode.NotFound)]
    [InlineData("/v1/profiles/nonesuch", HttpStatusCode.NotFound)]
    [InlineData("/v1/canonical?e=not%20canonical", HttpStatusCode.BadRequest)]
    public async Task Malformed_requests_are_4xx(string url, HttpStatusCode expected)
    {
        var response = await _client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.NotNull(json["error"]);
    }

    [Fact]
    public async Task Over_long_input_is_400()
    {
        var response = await _client.GetAsync("/v1/timespan?s=" + new string('1', 501));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_parses_many_in_order_with_refs()
    {
        var body = new
        {
            profile = "default", intent = "auto",
            inputs = new object[] { "1984", new { s = "1850-1920", intent = "pair", @ref = "row-17" }, new { s = "fl. C14", profile = "medieval-manuscripts" }, "n.d." },
        };
        var response = await _client.PostAsJsonAsync("/v1/timespan", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["results"]!.AsArray();
        Assert.Equal(4, results.Count);
        Assert.Equal("1984", results[0]!["canonical"]!.GetValue<string>());
        Assert.Equal("row-17", results[1]!["ref"]!.GetValue<string>());
        Assert.Equal("birthDeath", results[1]!["kind"]!.GetValue<string>());
        Assert.Equal("medieval-manuscripts", results[2]!["profile"]!.GetValue<string>());
        Assert.Equal("1301-01-01T00:00:00Z", results[2]!["timespan"]!["begin_of_the_begin"]!.GetValue<string>());
        Assert.Equal("undated", results[3]!["status"]!.GetValue<string>());
        foreach (var r in results) AssertMatchesSchema(r);
    }

    [Fact]
    public async Task Post_limits_and_validation()
    {
        var tooMany = await _client.PostAsJsonAsync("/v1/timespan", new { inputs = Enumerable.Range(0, 6).Select(i => (1900 + i).ToString()).ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);

        var empty = await _client.PostAsJsonAsync("/v1/timespan", new { inputs = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var badItem = await _client.PostAsJsonAsync("/v1/timespan", new { inputs = new object[] { 42 } });
        Assert.Equal(HttpStatusCode.BadRequest, badItem.StatusCode);

        var badProfile = await _client.PostAsJsonAsync("/v1/timespan", new { profile = "nonesuch", inputs = new[] { "1984" } });
        Assert.Equal(HttpStatusCode.NotFound, badProfile.StatusCode);
    }

    [Fact]
    public async Task Profiles_endpoints()
    {
        var list = JsonNode.Parse(await _client.GetStringAsync("/v1/profiles"))!.AsArray();
        Assert.Equal("default", list[0]!["name"]!.GetValue<string>());
        Assert.Contains(list, p => p!["name"]!.GetValue<string>() == "archive-us");

        var one = JsonNode.Parse(await _client.GetStringAsync("/v1/profiles/archive-us"))!;
        Assert.Equal("monthFirst", one["dayMonthOrder"]!.GetValue<string>());
    }

    [Fact]
    public async Task Canonical_endpoint_realises_without_parsing_text()
    {
        var json = JsonNode.Parse(await _client.GetStringAsync("/v1/canonical?e=early%20period:edo"))!;
        Assert.Equal("1603-01-01T00:00:00Z", json["timespan"]!["begin_of_the_begin"]!.GetValue<string>());
        Assert.Equal("early period:edo", json["canonical"]!.GetValue<string>());
        Assert.Equal("canonical", json["strategy"]!.GetValue<string>());
        AssertMatchesSchema(json);
    }

    [Fact]
    public async Task Health_version_and_docs()
    {
        var health = await _client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var version = JsonNode.Parse(await _client.GetStringAsync("/version"))!;
        Assert.Equal(TimeSpanParser.Version, version["parser"]!.GetValue<string>());
        Assert.Contains("default", version["profiles"]!.AsArray().Select(p => p!.GetValue<string>()));
        Assert.Equal(4, version["periodTables"]!.GetValue<int>());

        var openApi = JsonNode.Parse(await _client.GetStringAsync("/openapi/v1.json"))!;
        var paths = openApi["paths"]!.AsObject().Select(p => p.Key).ToList();
        Assert.Contains("/v1/timespan", paths);
        Assert.Contains("/v1/profiles", paths);
        Assert.Contains("/v1/canonical", paths);

        var docs = await _client.GetAsync("/docs");
        Assert.Equal(HttpStatusCode.OK, docs.StatusCode);
    }

    [Fact]
    public async Task Cors_allows_any_origin()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/timespan?s=1984");
        request.Headers.Add("Origin", "https://example.org");
        var response = await _client.SendAsync(request);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
