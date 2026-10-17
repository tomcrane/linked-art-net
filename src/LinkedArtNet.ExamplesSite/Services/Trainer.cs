using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Canonical;
using LinkedArtNet.Dates.Corpus;
using LinkedArtNet.Dates.Periods;
using LinkedArtNet.Dates.Realisation;
using Microsoft.Extensions.Options;

namespace LinkedArtNet.ExamplesSite.Services;

/// <summary>Configuration for the corpus trainer at /dates/train.</summary>
public sealed class TrainerOptions
{
    public const string LocalReviewer = "local";

    /// <summary>Where the queue and submissions live (JSON files); relative to the content root.</summary>
    public string DataDirectory { get; set; } = "trainer-data";

    /// <summary>The JSONL file the dates service writes when capture is enabled; imported on request.</summary>
    public string? CapturePath { get; set; }

    /// <summary>Lets anyone review as "local" without GitHub login. For development and tests only.</summary>
    public bool AllowLocalReviewer { get; set; }

    /// <summary>GitHub logins allowed to review (case-insensitive).</summary>
    public List<string> Reviewers { get; set; } = [];

    public GitHubOAuthOptions GitHub { get; set; } = new();
    public SubmissionOptions Submission { get; set; } = new();

    public bool GitHubLoginConfigured => !string.IsNullOrWhiteSpace(GitHub.ClientId) && !string.IsNullOrWhiteSpace(GitHub.ClientSecret);
    public bool Available => GitHubLoginConfigured || AllowLocalReviewer;

    public bool IsReviewer(string? login)
    {
        if (string.IsNullOrWhiteSpace(login)) return false;
        if (login == LocalReviewer) return AllowLocalReviewer;
        return Reviewers.Any(r => r.Equals(login, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class GitHubOAuthOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}

public sealed class SubmissionOptions
{
    /// <summary>owner/name of the repository that receives corpus pull requests.</summary>
    public string? Repo { get; set; }
    /// <summary>A token with contents and pull-request write permission on that repository.</summary>
    public string? Token { get; set; }
    public string BaseBranch { get; set; } = "de-pmc";
    public string Directory { get; set; } = "corpus/proposed";
    public bool Configured => !string.IsNullOrWhiteSpace(Repo) && !string.IsNullOrWhiteSpace(Token);
}

public enum QueueState { Pending, Done, Skipped }

/// <summary>An input waiting for review, with the parser's answer at the time it was queued and, when asked for, the interpreter's proposal.</summary>
public sealed record QueueItem(string Id, string Input, string Profile, string Intent, string Source, DateTimeOffset AddedAt,
    QueueState State = QueueState.Pending, string? ReviewedBy = null, DateTimeOffset? ReviewedAt = null, Proposal? Proposal = null);

/// <summary>
/// What the interpreter (Claude, by default) proposed for a queued input: a canonical expression, its confidence as the
/// parser caps it, the model's one-line rationale, and whether this parser can realise the expression. A proposal
/// pre-fills the review form; the reviewer's save is what enters the corpus.
/// </summary>
public sealed record Proposal(string? Canonical, double Confidence, string? Rationale, string Model, DateTimeOffset At, string? Intent, bool Realisable);

/// <summary>A reviewed entry, kept until it has been exported into a corpus file.</summary>
public sealed record Submission(string Id, string Reviewer, DateTimeOffset At, string? QueueId, CorpusEntry Entry, string? ExportedTo = null);

/// <summary>File-backed store for the queue and the submissions. One process, one lock; the data directory is not in git.</summary>
public sealed class TrainerStore
{
    private static readonly JsonSerializerOptions Json = new(CorpusFile.JsonOptions);
    private readonly object _lock = new();
    private readonly string _queuePath;
    private readonly string _submissionsPath;
    private List<QueueItem> _queue;
    private List<Submission> _submissions;

    public TrainerStore(IOptions<TrainerOptions> options, IHostEnvironment environment)
    {
        var dir = options.Value.DataDirectory;
        Directory = Path.GetFullPath(Path.IsPathRooted(dir) ? dir : Path.Combine(environment.ContentRootPath, dir));
        _queuePath = Path.Combine(Directory, "queue.json");
        _submissionsPath = Path.Combine(Directory, "submissions.json");
        _queue = Load<QueueItem>(_queuePath);
        _submissions = Load<Submission>(_submissionsPath);
    }

    public string Directory { get; }

    public IReadOnlyList<QueueItem> Queue { get { lock (_lock) return _queue.ToList(); } }
    public IReadOnlyList<Submission> Submissions { get { lock (_lock) return _submissions.ToList(); } }
    public IReadOnlyList<Submission> Unexported => Submissions.Where(s => s.ExportedTo is null).ToList();

    /// <summary>Queues inputs not already queued (same text and profile); returns how many were added.</summary>
    public int Add(IEnumerable<string> inputs, string profile, string intent, string source)
    {
        lock (_lock)
        {
            var known = _queue.Select(q => (q.Input, q.Profile)).ToHashSet();
            var added = 0;
            foreach (var raw in inputs)
            {
                var input = raw.Trim();
                if (input.Length == 0 || input.Length > 500 || !known.Add((input, profile))) continue;
                _queue.Add(new QueueItem("q-" + Guid.NewGuid().ToString("N")[..10], input, profile, intent, source, DateTimeOffset.UtcNow));
                added++;
            }
            if (added > 0) Save(_queuePath, _queue);
            return added;
        }
    }

    /// <summary>Reads the service's capture JSONL (one object per line with <c>input</c> and <c>profile</c>).</summary>
    public int ImportCapture(string path)
    {
        if (!File.Exists(path)) return 0;
        var byProfile = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonNode? node;
            try { node = JsonNode.Parse(line); } catch (JsonException) { continue; }
            if (node?["input"]?.GetValue<string>() is not { } input) continue;
            var profile = node["profile"]?.GetValue<string>() ?? "default";
            if (!byProfile.TryGetValue(profile, out var list)) byProfile[profile] = list = [];
            list.Add(input);
        }
        return byProfile.Sum(kv => Add(kv.Value, kv.Key, "auto", "capture:" + Path.GetFileName(path)));
    }

    public QueueItem? Next() { lock (_lock) return _queue.Where(q => q.State == QueueState.Pending).OrderBy(q => q.AddedAt).FirstOrDefault(); }

    public QueueItem? Get(string id) { lock (_lock) return _queue.FirstOrDefault(q => q.Id == id); }

    public int Count(QueueState state) { lock (_lock) return _queue.Count(q => q.State == state); }

    public void SetProposal(string id, Proposal proposal)
    {
        lock (_lock)
        {
            var i = _queue.FindIndex(q => q.Id == id);
            if (i < 0) return;
            _queue[i] = _queue[i] with { Proposal = proposal };
            Save(_queuePath, _queue);
        }
    }

    /// <summary>Pending items the parser cannot read confidently and nobody has asked the interpreter about yet.</summary>
    public IReadOnlyList<QueueItem> AwaitingProposal(Func<QueueItem, bool> needsInterpreter)
    {
        lock (_lock) return _queue.Where(q => q.State == QueueState.Pending && q.Proposal is null && needsInterpreter(q)).OrderBy(q => q.AddedAt).ToList();
    }

    public void SetState(string id, QueueState state, string reviewer)
    {
        lock (_lock)
        {
            var i = _queue.FindIndex(q => q.Id == id);
            if (i < 0) return;
            _queue[i] = _queue[i] with { State = state, ReviewedBy = reviewer, ReviewedAt = DateTimeOffset.UtcNow };
            Save(_queuePath, _queue);
        }
    }

    /// <summary>The next corpus entry id: <c>tr-yyyymmdd-nnn</c>, unique across this store.</summary>
    public string NextEntryId()
    {
        lock (_lock)
        {
            var prefix = $"tr-{DateTime.UtcNow:yyyyMMdd}-";
            var n = _submissions.Count(s => s.Entry.Id.StartsWith(prefix, StringComparison.Ordinal)) + 1;
            while (_submissions.Any(s => s.Entry.Id == $"{prefix}{n:000}")) n++;
            return $"{prefix}{n:000}";
        }
    }

    public Submission AddSubmission(string reviewer, string? queueId, CorpusEntry entry)
    {
        lock (_lock)
        {
            var submission = new Submission("s-" + Guid.NewGuid().ToString("N")[..10], reviewer, DateTimeOffset.UtcNow, queueId, entry);
            _submissions.Add(submission);
            Save(_submissionsPath, _submissions);
            return submission;
        }
    }

    public void MarkExported(IEnumerable<string> submissionIds, string target)
    {
        lock (_lock)
        {
            var ids = submissionIds.ToHashSet(StringComparer.Ordinal);
            for (var i = 0; i < _submissions.Count; i++)
                if (ids.Contains(_submissions[i].Id)) _submissions[i] = _submissions[i] with { ExportedTo = target };
            Save(_submissionsPath, _submissions);
        }
    }

    private static List<T> Load<T>(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Json) ?? [] : [];

    private void Save<T>(string path, List<T> items)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(items, Json), new UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}

/// <summary>Parses and realises for the reviewer, and turns a review into a corpus entry or file.</summary>
public sealed class Trainer
{
    private readonly ProfileCatalog _catalog;
    private readonly LinkedArtNet.Dates.Periods.INamedPeriodSource _periods;
    private readonly Dictionary<string, TimeSpanParser> _parsers = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _repoRoot;

    public Trainer(ExampleIndex index)
    {
        _repoRoot = index.RepoRoot;
        var dir = Path.Combine(_repoRoot, "profiles");
        _catalog = Directory.Exists(dir) ? ProfileCatalog.Load(dir) : new ProfileCatalog();
        _periods = LinkedArtNet.Dates.Periods.PeriodOSource.ForRepository(_repoRoot);
    }

    /// <summary>The built-in period tables plus PeriodO when the repository has periods/periodo.json.</summary>
    public LinkedArtNet.Dates.Periods.INamedPeriodSource Periods => _periods;

    public IReadOnlyList<string> ProfileNames => _catalog.Profiles.Keys.OrderBy(n => n == "default" ? "" : n, StringComparer.Ordinal).ToList();

    public ParseProfile Profile(string? name) => name is not null && _catalog.TryGet(name, out var p) ? p : ParseProfile.Default;

    public TimeSpanParseResult Parse(string input, string? profile, string? intent)
    {
        var p = Profile(profile);
        lock (_parsers)
        {
            if (!_parsers.TryGetValue(p.Name, out var parser)) _parsers[p.Name] = parser = new TimeSpanParser(p, periods: _periods);
            return parser.Parse(input, ParseIntent(intent));
        }
    }

    /// <summary>Whether the deterministic parser is weak enough on this item for an interpreter's proposal to be worth asking for.</summary>
    public bool NeedsInterpreter(QueueItem item) => TimeSpanParser.NeedsInterpreter(Parse(item.Input, item.Profile, item.Intent), Profile(item.Profile));

    /// <summary>
    /// Asks the interpreter about one queued input and returns its proposal, checked against this parser: the
    /// canonical expression is realised under the item's profile, so the reviewer sees whether it is usable before
    /// accepting it. Never throws for a bad answer; a proposal with a null canonical records that none was offered.
    /// </summary>
    public async Task<Proposal> ProposeAsync(QueueItem item, InterpreterHost host, CancellationToken ct = default)
    {
        var interpreter = host.Interpreter ?? throw new InvalidOperationException("No interpreter is configured.");
        var model = host.Model ?? interpreter.Name;
        var profile = Profile(item.Profile);
        var deterministic = Parse(item.Input, item.Profile, item.Intent);
        LinkedArtNet.Dates.Interpretation.InterpreterResponse? response;
        try
        {
            response = await interpreter.InterpretAsync(item.Input, new LinkedArtNet.Dates.Interpretation.InterpreterContext(profile, ParseIntent(item.Intent), deterministic), ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new Proposal(null, 0, "The interpreter could not be reached: " + e.Message, model, DateTimeOffset.UtcNow, null, false);
        }
        if (response is null) return new Proposal(null, 0, "No reading offered: the model did not take the text for a date.", model, DateTimeOffset.UtcNow, null, false);
        var canonical = response.Canonical.Trim();
        var intent = response.Intent?.ToString().ToLowerInvariant();
        var (realised, error) = Realise(canonical, item.Profile, intent ?? item.Intent);
        var confidence = Math.Clamp(Math.Min(response.SelfConfidence, interpreter.MaxConfidence), 0, 1);
        return new Proposal(canonical, confidence, string.IsNullOrWhiteSpace(response.Rationale) ? null : response.Rationale.Trim(), model, DateTimeOffset.UtcNow, intent, realised is not null && error is null);
    }

    /// <summary>Realises a canonical expression under a profile; the error is set when it is not canonical or cannot be realised.</summary>
    public (TimeSpanParseResult? Result, string? Error) Realise(string canonical, string? profile, string? intent)
    {
        if (string.IsNullOrWhiteSpace(canonical)) return (null, "Empty expression.");
        if (!CanonicalReader.TryParse(canonical.Trim(), out var expression, out var error, JsonNamedPeriodSource.BuiltIn)) return (null, error ?? "Not a canonical expression.");
        try { return (new TimeSpanRealiser(Profile(profile)).Realise(expression!, canonical.Trim(), ParseIntent(intent)), null); }
        catch (RealisationException e) { return (null, e.Message); }
    }

    public static Intent ParseIntent(string? intent) => Enum.TryParse<Intent>(intent, true, out var i) ? i : Intent.Auto;

    /// <summary>
    /// A proposed corpus entry. For <c>parsed</c> the expectation pins the canonical form and the realised bounds; for
    /// <c>undated</c> and <c>unparsed</c> it pins the status only.
    /// </summary>
    public CorpusEntry BuildEntry(string id, string input, string profile, string intent, string expectedStatus, string? canonical,
        IEnumerable<string> tags, string? notes, string reviewer)
    {
        var status = expectedStatus.ToLowerInvariant() is "undated" or "unparsed" ? expectedStatus.ToLowerInvariant() : "parsed";
        Expectation expect;
        if (status == "parsed")
        {
            var (realised, error) = Realise(canonical ?? "", profile, intent);
            if (realised is null) throw new ArgumentException(error, nameof(canonical));
            expect = new Expectation
            {
                Status = "parsed",
                Kind = Camel(realised.Kind.ToString()),
                Canonical = realised.Canonical,
                Edtf = realised.Edtf,
                TimeSpan = Expected(realised.TimeSpan),
                Begin = Expected(realised.Begin),
                End = Expected(realised.End),
            };
        }
        else
        {
            expect = new Expectation { Status = status };
        }
        var allTags = tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().Order(StringComparer.Ordinal).ToList();
        var note = $"Reviewed by {reviewer} in the trainer on {DateTime.UtcNow:yyyy-MM-dd}.";
        if (!string.IsNullOrWhiteSpace(notes)) note = notes.Trim() + " " + note;
        return new CorpusEntry
        {
            Id = id, Input = input, Intent = intent.ToLowerInvariant(), Profile = profile, Tags = allTags, Expect = expect,
            Status = EntryStatus.Proposed, Notes = note, SourceExpectation = "trainer",
        };
    }

    /// <summary>A corpus file holding the given submissions, in the shape of corpus/authored/*.json.</summary>
    public CorpusFile BuildFile(IReadOnlyList<Submission> submissions)
    {
        var reviewers = submissions.Select(s => s.Reviewer).Distinct().Order(StringComparer.Ordinal);
        return new CorpusFile
        {
            Schema = "../schema/corpus-entry.schema.json",
            Source = new CorpusSource("Trainer submissions", "CC0-1.0",
                Notes: $"Reviewed in the examples-site trainer by {string.Join(", ", reviewers)}. Proposed until a maintainer verifies them."),
            Defaults = new CorpusDefaults(Status: EntryStatus.Proposed, Tags: ["trainer"]),
            Entries = submissions.Select(s => s.Entry).ToList(),
        };
    }

    public static string Serialise(CorpusFile file) => JsonSerializer.Serialize(file, CorpusFile.JsonOptions) + "\n";

    /// <summary>Schema errors for a serialised file, by writing it beside the store and validating as the corpus tool does.</summary>
    public IReadOnlyList<string> Validate(string json, string scratchDirectory)
    {
        Directory.CreateDirectory(scratchDirectory);
        var path = Path.Combine(scratchDirectory, "validate-" + Guid.NewGuid().ToString("N")[..8] + ".json");
        File.WriteAllText(path, json, new UTF8Encoding(false));
        try { return CorpusFile.SchemaErrors(path, Path.Combine(_repoRoot, "corpus", "schema", "corpus-entry.schema.json")); }
        finally { File.Delete(path); }
    }

    private static ExpectedTimeSpan? Expected(LinkedArtTimeSpan? ts) => ts is null ? null : new ExpectedTimeSpan
    {
        BeginOfTheBegin = ts.BeginOfTheBegin?.ToString(), EndOfTheBegin = ts.EndOfTheBegin?.ToString(),
        BeginOfTheEnd = ts.BeginOfTheEnd?.ToString(), EndOfTheEnd = ts.EndOfTheEnd?.ToString(),
    };

    private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

/// <summary>Opens a pull request adding one corpus file, through the GitHub REST API.</summary>
public sealed class GitHubSubmitter(HttpClient http, IOptions<TrainerOptions> options)
{
    public bool Configured => options.Value.Submission.Configured;

    public string BaseBranch => options.Value.Submission.BaseBranch;

    /// <returns>The pull request's HTML URL.</returns>
    public async Task<string> CreatePullRequestAsync(string fileName, string content, string title, string body, CancellationToken ct = default)
    {
        var s = options.Value.Submission;
        if (!s.Configured) throw new InvalidOperationException("Pull requests are not configured (Trainer:Submission:Repo and :Token).");
        var baseRef = await SendAsync(HttpMethod.Get, $"repos/{s.Repo}/git/ref/heads/{s.BaseBranch}", null, ct);
        var sha = baseRef["object"]!["sha"]!.GetValue<string>();
        var branch = $"trainer/{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        await SendAsync(HttpMethod.Post, $"repos/{s.Repo}/git/refs", new { @ref = $"refs/heads/{branch}", sha }, ct);
        var path = $"{s.Directory.TrimEnd('/')}/{fileName}";
        await SendAsync(HttpMethod.Put, $"repos/{s.Repo}/contents/{path}", new { message = title, content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)), branch }, ct);
        var pr = await SendAsync(HttpMethod.Post, $"repos/{s.Repo}/pulls", new { title, head = branch, @base = s.BaseBranch, body }, ct);
        return pr["html_url"]!.GetValue<string>();
    }

    private async Task<JsonNode> SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.Submission.Token);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("linked-art-net-trainer");
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"GitHub {method} {url} returned {(int)response.StatusCode}: {text[..Math.Min(text.Length, 300)]}");
        return JsonNode.Parse(text) ?? new JsonObject();
    }
}
