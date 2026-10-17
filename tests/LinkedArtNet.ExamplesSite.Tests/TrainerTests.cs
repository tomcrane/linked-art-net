using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using LinkedArtNet.Dates.Corpus;
using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LinkedArtNet.ExamplesSite.Tests;

/// <summary>Records the GitHub API calls the trainer makes and answers like GitHub would.</summary>
public sealed class FakeGitHub : HttpMessageHandler
{
    public List<(string Method, string Path, string? Body)> Calls { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        Calls.Add((request.Method.Method, path, body));
        string json = path switch
        {
            _ when path.Contains("/git/ref/heads/") => """{"object":{"sha":"abc123"}}""",
            _ when path.EndsWith("/git/refs") => """{"ref":"refs/heads/trainer/x"}""",
            _ when path.Contains("/contents/") => """{"content":{"path":"x"}}""",
            _ when path.EndsWith("/pulls") => """{"html_url":"https://github.com/tomcrane/linked-art-net/pull/1"}""",
            _ => "{}",
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}

public class TrainerFixture : WebApplicationFactory<Program>
{
    public string DataDirectory { get; } = Path.Combine(Path.GetTempPath(), "lan-trainer-tests", Guid.NewGuid().ToString("N"));
    public string CapturePath => Path.Combine(DataDirectory, "capture.jsonl");
    public FakeGitHub GitHub { get; } = new();
    /// <summary>When set, the trainer offers this interpreter's proposals under the model name "scripted-model".</summary>
    public LinkedArtNet.Dates.Interpretation.IDateInterpreter? Interpreter { get; init; }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Examples:RepoRoot", SiteFixture.RepoRoot);
        builder.UseSetting("TimeSpanService:BaseUrl", "");
        builder.UseSetting("Trainer:AllowLocalReviewer", "true");
        builder.UseSetting("Trainer:DataDirectory", DataDirectory);
        builder.UseSetting("Trainer:CapturePath", CapturePath);
        builder.UseSetting("Trainer:Submission:Repo", "tomcrane/linked-art-net");
        builder.UseSetting("Trainer:Submission:Token", "test-token");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<GitHubSubmitter>(c => c.BaseAddress = new Uri("https://api.github.com/")).ConfigurePrimaryHttpMessageHandler(() => GitHub);
            services.AddRazorPages(o => o.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));
            if (Interpreter is not null) services.AddSingleton(new InterpreterHost(Interpreter, "scripted-model", batchSize: 25));
        });
    }
}

/// <summary>Stands in for Claude: the same reading for everything, and a record of what it was asked.</summary>
public sealed class ScriptedInterpreter(string canonical, double confidence, string rationale) : LinkedArtNet.Dates.Interpretation.IDateInterpreter
{
    public List<string> Asked { get; } = [];
    public string Name => "scripted";
    public Task<LinkedArtNet.Dates.Interpretation.InterpreterResponse?> InterpretAsync(string input, LinkedArtNet.Dates.Interpretation.InterpreterContext context, CancellationToken ct = default)
    {
        Asked.Add(input);
        return Task.FromResult<LinkedArtNet.Dates.Interpretation.InterpreterResponse?>(new(canonical, confidence, rationale));
    }
}

// Each test gets its own fixture (and store directory) so queue counts and review order are deterministic.
public class TrainerTests
{
    private static FormUrlEncodedContent Form(params (string, string)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2)));

    private static async Task<HttpClient> SignedInClient(TrainerFixture factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetAsync("/dates/train/login");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/dates/train", login.Headers.Location!.ToString());
        return client;
    }

    [Fact]
    public async Task Anonymous_is_sent_to_login_and_the_realise_endpoint_is_protected()
    {
        using var factory = new TrainerFixture();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/dates/train");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("http://localhost/dates/train/login", response.Headers.Location!.ToString());

        var realise = await client.GetAsync("/dates/train/realise?e=1984");
        Assert.Equal(HttpStatusCode.Redirect, realise.StatusCode);

        var unavailable = await client.GetAsync("/dates/train/unavailable");
        Assert.Equal(HttpStatusCode.OK, unavailable.StatusCode);
    }

    [Fact]
    public void Reviewer_allow_list_is_case_insensitive_and_local_needs_opting_in()
    {
        var options = new TrainerOptions { Reviewers = ["tomcrane"] };
        Assert.True(options.IsReviewer("TomCrane"));
        Assert.False(options.IsReviewer("someone"));
        Assert.False(options.IsReviewer("local"));
        Assert.False(options.IsReviewer(null));
        options.AllowLocalReviewer = true;
        Assert.True(options.IsReviewer("local"));
    }

    [Fact]
    public async Task Realise_endpoint_answers_for_a_signed_in_reviewer()
    {
        using var factory = new TrainerFixture();
        var client = await SignedInClient(factory);
        var ok = JsonNode.Parse(await client.GetStringAsync("/dates/train/realise?e=late%20195X&profile=default"))!;
        Assert.True(ok["ok"]!.GetValue<bool>());
        Assert.Equal("1957/1959", ok["edtf"]!.GetValue<string>());
        Assert.Equal("1957-01-01T00:00:00Z", ok["timespan"]!["begin_of_the_begin"]!.GetValue<string>());

        var bad = JsonNode.Parse(await client.GetStringAsync("/dates/train/realise?e=not%20canonical%20at%20all"))!;
        Assert.False(bad["ok"]!.GetValue<bool>());
        Assert.NotNull(bad["error"]);
    }

    [Fact]
    public async Task Review_round_trip_from_queue_to_pull_request()
    {
        using var factory = new TrainerFixture();
        var client = await SignedInClient(factory);
        var home = await client.GetStringAsync("/dates/train");
        Assert.Contains("Corpus trainer", home);
        Assert.Contains("Signed in as", home);

        // Queue three inputs: one the parser reads well, one it reads wrongly for our purposes, one it cannot read.
        var add = await client.PostAsync("/dates/train?handler=Add", Form(("inputs", "circa 1850\nspring 1920\nno date here"), ("profile", "default"), ("intent", "auto")));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        home = await client.GetStringAsync("/dates/train");
        Assert.Contains("3 inputs added", home);
        Assert.Contains("<strong>3</strong><span>waiting for review</span>", home);

        // Review 1: accept the parser's answer.
        var review = await client.GetStringAsync("/dates/train/review");
        Assert.Contains("circa 1850", review);
        Assert.Contains("1850~", review);
        var id1 = IdIn(review);
        var accept = await client.PostAsync($"/dates/train/review/{id1}?handler=Accept", Form(("id", id1)));
        Assert.Equal(HttpStatusCode.Redirect, accept.StatusCode);

        // Review 2: preview a correction, then save it with a different profile and tags.
        review = await client.GetStringAsync("/dates/train/review");
        Assert.Contains("spring 1920", review);
        var id2 = IdIn(review);
        var preview = await client.PostAsync($"/dates/train/review/{id2}?handler=Preview",
            Form(("id", id2), ("canonical", "1920-21"), ("intent", "single"), ("profile", "default"), ("expectedStatus", "parsed")));
        var previewHtml = await preview.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Contains("Preview:", previewHtml);
        Assert.Contains("1920-21", previewHtml);
        var badSave = await client.PostAsync($"/dates/train/review/{id2}?handler=Save",
            Form(("id", id2), ("canonical", "garbage!!"), ("intent", "single"), ("profile", "default"), ("expectedStatus", "parsed")));
        Assert.Contains("Cannot save", await badSave.Content.ReadAsStringAsync());
        var save = await client.PostAsync($"/dates/train/review/{id2}?handler=Save",
            Form(("id", id2), ("canonical", "1920-21"), ("intent", "single"), ("profile", "library"), ("expectedStatus", "parsed"), ("tags", "season, Month"), ("notes", "treated as the quarter")));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        // Review 3: mark as unparsed.
        review = await client.GetStringAsync("/dates/train/review");
        Assert.Contains("no date here", review);
        var id3 = IdIn(review);
        await client.PostAsync($"/dates/train/review/{id3}?handler=Save", Form(("id", id3), ("expectedStatus", "unparsed"), ("intent", "auto"), ("profile", "default")));
        review = await client.GetStringAsync("/dates/train/review");
        Assert.Contains("Nothing to review", review);

        // Download: a corpus file that validates against the schema and passes the evaluator.
        var download = await client.PostAsync("/dates/train?handler=Download", Form());
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/json", download.Content.Headers.ContentType!.MediaType);
        var json = await download.Content.ReadAsStringAsync();
        var path = Path.Combine(factory.DataDirectory, "downloaded.json");
        await File.WriteAllTextAsync(path, json);
        Assert.Empty(CorpusFile.SchemaErrors(path, Path.Combine(SiteFixture.RepoRoot, "corpus", "schema", "corpus-entry.schema.json")));
        var file = CorpusFile.Load(path);
        Assert.Equal(3, file.Entries.Count);
        Assert.All(file.Entries, e => Assert.Equal(EntryStatus.Proposed, e.Status));
        Assert.All(file.Entries, e => Assert.Contains("trainer", e.Tags));
        Assert.All(file.Entries, e => Assert.Matches("^tr-[0-9]{8}-[0-9]{3}$", e.Id));
        var corrected = file.Entries.Single(e => e.Input == "spring 1920");
        Assert.Equal("library", corrected.Profile);
        Assert.Equal("single", corrected.Intent);
        Assert.Equal("1920-21", corrected.Expect.Canonical);
        Assert.Equal("1920-21", corrected.Expect.Edtf);
        Assert.Contains("month", corrected.Tags);
        Assert.Contains("treated as the quarter", corrected.Notes);
        Assert.Equal("unparsed", file.Entries.Single(e => e.Input == "no date here").Expect.Status);
        var profiles = ProfileCatalog.Load(Path.Combine(SiteFixture.RepoRoot, "profiles"));
        var results = new CorpusEvaluator(profiles.Get).Evaluate(file.Entries);
        Assert.All(results, r => Assert.True(r.Passed, r.Entry.Input + ": " + string.Join("; ", r.Failures)));

        // Pull request: branch from the base, add the file, open the PR; the entries are then marked exported.
        var pr = await client.PostAsync("/dates/train?handler=PullRequest", Form());
        Assert.Equal(HttpStatusCode.Redirect, pr.StatusCode);
        home = await client.GetStringAsync("/dates/train");
        Assert.Contains("https://github.com/tomcrane/linked-art-net/pull/1", home);
        Assert.Contains("<strong>0</strong><span>entries not yet exported</span>", home);
        Assert.Equal(["GET", "POST", "PUT", "POST"], factory.GitHub.Calls.Select(c => c.Method));
        Assert.EndsWith("/repos/tomcrane/linked-art-net/git/ref/heads/de-pmc", factory.GitHub.Calls[0].Path);
        Assert.StartsWith("/repos/tomcrane/linked-art-net/contents/corpus/proposed/trainer-", factory.GitHub.Calls[2].Path);
        Assert.Contains("\"base\":\"de-pmc\"", factory.GitHub.Calls[3].Body);
        var put = JsonNode.Parse(factory.GitHub.Calls[2].Body!)!;
        var committed = Encoding.UTF8.GetString(Convert.FromBase64String(put["content"]!.GetValue<string>()));
        Assert.Equal(json, committed);
    }

    [Fact]
    public async Task Interpreter_proposals_prefill_the_review_and_are_recorded_in_the_entry()
    {
        var claude = new ScriptedInterpreter("1850~", 0.9, "The scrawl means about 1850.");
        using var factory = new TrainerFixture { Interpreter = claude };
        var client = await SignedInClient(factory);
        var add = await client.PostAsync("/dates/train?handler=Add", Form(("inputs", "ye olde scrawle abt eighteen-fiftie\ncirca 1850"), ("profile", "default"), ("intent", "auto")));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);

        // Only the unreadable input is offered for bulk proposals; the confident one is not.
        var home = await client.GetStringAsync("/dates/train");
        Assert.Contains("1 waiting input the parser cannot read confidently has no proposed reading yet", home);
        Assert.Contains("Ask scripted-model for readings", home);
        var bulk = await client.PostAsync("/dates/train?handler=Propose", Form());
        Assert.Equal(HttpStatusCode.Redirect, bulk.StatusCode);
        home = await client.GetStringAsync("/dates/train");
        Assert.Contains("scripted-model proposed 1 reading for 1 input", home);
        Assert.Equal(["ye olde scrawle abt eighteen-fiftie"], claude.Asked);

        // The review page shows the proposal and pre-fills the correction form with it.
        var review = await client.GetStringAsync("/dates/train/review");
        Assert.Contains("ye olde scrawle", review);
        Assert.Contains("What scripted-model proposes", review);
        Assert.Contains("The scrawl means about 1850.", review);
        Assert.Contains("confidence 0.60", review); // capped at the interpreter's default 0.6, not its own 0.9
        Assert.Contains("value=\"1850~\"", review);
        Assert.Contains("value=\"llm-proposed\"", review);
        Assert.Contains("Reading proposed by scripted-model, confirmed by the reviewer.", review);
        var id1 = IdIn(review);
        var save = await client.PostAsync($"/dates/train/review/{id1}?handler=Save",
            Form(("id", id1), ("canonical", "1850~"), ("intent", "auto"), ("profile", "default"), ("expectedStatus", "parsed"), ("tags", "llm-proposed"), ("notes", "Reading proposed by scripted-model, confirmed by the reviewer.")));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);

        // A confident parse can still be asked about, one item at a time.
        review = await client.GetStringAsync("/dates/train/review");
        Assert.Contains("circa 1850", review);
        Assert.Contains("Ask scripted-model for a reading", review);
        var id2 = IdIn(review);
        var ask = await client.PostAsync($"/dates/train/review/{id2}?handler=Propose", Form(("id", id2)));
        Assert.Equal(HttpStatusCode.Redirect, ask.StatusCode);
        review = await client.GetStringAsync($"/dates/train/review/{id2}");
        Assert.Contains("What scripted-model proposes", review);
        Assert.DoesNotContain("Ask scripted-model for a reading", review);
        Assert.Equal(2, claude.Asked.Count);

        // The saved entry carries the provenance.
        var download = await client.PostAsync("/dates/train?handler=Download", Form());
        var file = CorpusFile.Load(await WriteDownload(factory, download));
        var entry = Assert.Single(file.Entries);
        Assert.Equal("1850~", entry.Expect.Canonical);
        Assert.Contains("llm-proposed", entry.Tags);
        Assert.Contains("scripted-model", entry.Notes);
    }

    private static async Task<string> WriteDownload(TrainerFixture factory, HttpResponseMessage download)
    {
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        var path = Path.Combine(factory.DataDirectory, "downloaded.json");
        await File.WriteAllTextAsync(path, await download.Content.ReadAsStringAsync());
        return path;
    }

    [Fact]
    public async Task Capture_import_reads_the_service_jsonl()
    {
        using var factory = new TrainerFixture();
        Directory.CreateDirectory(factory.DataDirectory);
        await File.WriteAllTextAsync(factory.CapturePath,
            """{"at":"2026-10-14T10:00:00Z","input":"sometime in the 1600s or so","profile":"default","status":"partial","confidence":0.4}""" + "\n" +
            """{"at":"2026-10-14T10:00:01Z","input":"Hälfte 15. Jh.","profile":"multilingual","status":"unparsed","confidence":0}""" + "\n" +
            "not json\n");
        var client = await SignedInClient(factory);
        var home = await client.GetStringAsync("/dates/train");
        Assert.Contains("Import captured inputs", home);
        await client.PostAsync("/dates/train?handler=ImportCapture", Form());
        home = await client.GetStringAsync("/dates/train");
        Assert.Contains("2 captured inputs added", home);
        var again = await client.PostAsync("/dates/train?handler=ImportCapture", Form());
        Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
        Assert.Contains("0 captured inputs added", await client.GetStringAsync("/dates/train"));
        var store = factory.Services.GetRequiredService<TrainerStore>();
        Assert.Contains(store.Queue, q => q.Input == "Hälfte 15. Jh." && q.Profile == "multilingual" && q.Source.StartsWith("capture:"));
    }

    private static string IdIn(string reviewHtml)
    {
        const string marker = "name=\"id\" value=\"";
        var i = reviewHtml.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(i >= 0, "no queue id on the review page");
        var start = i + marker.Length;
        return reviewHtml[start..reviewHtml.IndexOf('"', start)];
    }
}
