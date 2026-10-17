using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Canonical;
using LinkedArtNet.Dates.Corpus;
using LinkedArtNet.Dates.Periods;
using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.Agents;
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
    /// <summary>Where agent corpus files from the trainer go; under corpus/agents so the agent loader finds them.</summary>
    public string AgentDirectory { get; set; } = "corpus/agents/proposed";
    public bool Configured => !string.IsNullOrWhiteSpace(Repo) && !string.IsNullOrWhiteSpace(Token);
}

public enum QueueState { Pending, Done, Skipped }

/// <summary>What kind of string a queue item is. The profile means a dates profile for <c>date</c> and an agent profile for <c>agent</c>.</summary>
public static class ItemKind
{
    public const string Date = "date";
    public const string Agent = "agent";
    public static string Normalise(string? kind) => string.Equals(kind, Agent, StringComparison.OrdinalIgnoreCase) ? Agent : Date;
}

/// <summary>
/// An input waiting for review, with the parser's answer at the time it was queued and, when asked for, the
/// interpreter's proposal. <see cref="Kind"/> is <see cref="ItemKind.Date"/> or <see cref="ItemKind.Agent"/>; items
/// queued before kinds existed read as dates.
/// </summary>
public sealed record QueueItem(string Id, string Input, string Profile, string Intent, string Source, DateTimeOffset AddedAt,
    QueueState State = QueueState.Pending, string? ReviewedBy = null, DateTimeOffset? ReviewedAt = null, Proposal? Proposal = null, string Kind = ItemKind.Date)
{
    public bool IsAgent => Kind == ItemKind.Agent;
}

/// <summary>
/// What the interpreter (Claude, by default) proposed for a queued input: a canonical expression, its confidence as the
/// parser caps it, the model's one-line rationale, and whether this parser can realise the expression. A proposal
/// pre-fills the review form; the reviewer's save is what enters the corpus.
/// </summary>
public sealed record Proposal(string? Canonical, double Confidence, string? Rationale, string Model, DateTimeOffset At, string? Intent, bool Realisable);

/// <summary>
/// A reviewed item, kept until it has been exported into a corpus file. A date review has an <see cref="Entry"/>;
/// an agent review has an <see cref="AgentEntry"/> and, when its date part was confirmed or corrected and the dates
/// corpus did not already hold that substring, an <see cref="Entry"/> for the substring as well.
/// </summary>
public sealed record Submission(string Id, string Reviewer, DateTimeOffset At, string? QueueId, CorpusEntry? Entry, string? ExportedTo = null,
    LinkedArtNet.Agents.AgentCorpusEntry? AgentEntry = null)
{
    public string Input => AgentEntry?.Input ?? Entry?.Input ?? "";
}

/// <summary>File-backed store for the queue and the submissions. One process, one lock; the data directory is not in git.</summary>
public sealed class TrainerStore
{
    private static readonly JsonSerializerOptions Json = new(CorpusFile.JsonOptions);
    private static readonly JsonSerializerOptions AgentJson = new(LinkedArtNet.Agents.AgentCorpusFile.JsonOptions);
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

    /// <summary>Queues inputs not already queued (same kind, text and profile); returns how many were added.</summary>
    public int Add(IEnumerable<string> inputs, string profile, string intent, string source, string kind = ItemKind.Date)
    {
        kind = ItemKind.Normalise(kind);
        lock (_lock)
        {
            var known = _queue.Select(q => (q.Kind, q.Input, q.Profile)).ToHashSet();
            var added = 0;
            foreach (var raw in inputs)
            {
                var input = raw.Trim();
                if (input.Length == 0 || input.Length > 500 || !known.Add((kind, input, profile))) continue;
                _queue.Add(new QueueItem("q-" + Guid.NewGuid().ToString("N")[..10], input, profile, intent, source, DateTimeOffset.UtcNow, Kind: kind));
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

    public int Count(QueueState state, string kind) { lock (_lock) return _queue.Count(q => q.State == state && q.Kind == kind); }

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

    /// <summary>The next dates corpus entry id: <c>tr-yyyymmdd-nnn</c>, unique across this store.</summary>
    public string NextEntryId() => NextId("tr");

    /// <summary>The next agent corpus entry id: <c>ta-yyyymmdd-nnn</c>, unique across this store.</summary>
    public string NextAgentEntryId() => NextId("ta");

    private string NextId(string kind)
    {
        lock (_lock)
        {
            var prefix = $"{kind}-{DateTime.UtcNow:yyyyMMdd}-";
            var ids = _submissions.SelectMany(s => new[] { s.Entry?.Id, s.AgentEntry?.Id }).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var n = ids.Count(id => id.StartsWith(prefix, StringComparison.Ordinal)) + 1;
            while (ids.Contains($"{prefix}{n:000}")) n++;
            return $"{prefix}{n:000}";
        }
    }

    /// <summary>Whether a dates entry for this input and profile is already waiting in this store.</summary>
    public bool HasDatesSubmission(string input, string profile)
    {
        lock (_lock) return _submissions.Any(s => s.Entry is { } e && string.Equals(e.Input, input, StringComparison.OrdinalIgnoreCase) && string.Equals(e.Profile, profile, StringComparison.OrdinalIgnoreCase));
    }

    public Submission AddSubmission(string reviewer, string? queueId, CorpusEntry? entry, LinkedArtNet.Agents.AgentCorpusEntry? agentEntry = null)
    {
        if (entry is null && agentEntry is null) throw new ArgumentException("A submission needs a dates entry, an agent entry, or both.");
        lock (_lock)
        {
            var submission = new Submission("s-" + Guid.NewGuid().ToString("N")[..10], reviewer, DateTimeOffset.UtcNow, queueId, entry, AgentEntry: agentEntry);
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
    private readonly Dictionary<string, AgentStringParser> _agentParsers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lazy<HashSet<(string Input, string Profile)>> _corpusDates;
    private readonly string _repoRoot;

    public Trainer(ExampleIndex index)
    {
        _repoRoot = index.RepoRoot;
        var dir = Path.Combine(_repoRoot, "profiles");
        _catalog = Directory.Exists(dir) ? ProfileCatalog.Load(dir) : new ProfileCatalog();
        _periods = LinkedArtNet.Dates.Periods.PeriodOSource.ForRepository(_repoRoot);
        _corpusDates = new(() => CorpusFile.LoadAll(Path.Combine(_repoRoot, "corpus")).SelectMany(f => f.Entries)
            .Select(e => (e.Input.Trim().ToLowerInvariant(), e.Profile.ToLowerInvariant())).ToHashSet());
    }

    public IReadOnlyList<string> AgentProfileNames => AgentProfileCatalog.BuiltIn.Profiles.Keys.OrderBy(n => n == "default" ? "" : n, StringComparer.Ordinal).ToList();

    public AgentProfile AgentProfile(string? name) => AgentProfileCatalog.BuiltIn.TryGet(name, out var p) ? p : LinkedArtNet.Agents.AgentProfile.Default;

    /// <summary>The agent-string parser's reading under an agent profile, its date part read by this trainer's dates profiles.</summary>
    public ParsedAgent ParseAgent(string input, string? profile)
    {
        var p = AgentProfile(profile);
        lock (_agentParsers)
        {
            if (!_agentParsers.TryGetValue(p.Name, out var parser)) _agentParsers[p.Name] = parser = new AgentStringParser(p, _catalog);
            return parser.Parse(input);
        }
    }

    /// <summary>Whether the dates corpus in the repository already has an entry for this input under this profile.</summary>
    public bool KnownInDatesCorpus(string input, string profile) => _corpusDates.Value.Contains((input.Trim().ToLowerInvariant(), profile.ToLowerInvariant()));

    /// <summary>The text the dates parser is asked about for a queue item, with the dates profile and intent that apply: the whole input for a date item, the date substring for an agent item.</summary>
    public (string? Text, string Profile, string Intent) DatePart(QueueItem item)
    {
        if (!item.IsAgent) return (item.Input, item.Profile, item.Intent);
        var parsed = ParseAgent(item.Input, item.Profile);
        return (parsed.DateText, AgentProfile(item.Profile).DatesProfile, "pair");
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

    /// <summary>Whether the deterministic parser is weak enough on this item's date part for an interpreter's proposal to be worth asking for. An agent item with no date part never is.</summary>
    public bool NeedsInterpreter(QueueItem item)
    {
        var (text, profile, intent) = DatePart(item);
        return text is not null && TimeSpanParser.NeedsInterpreter(Parse(text, profile, intent), Profile(profile));
    }

    /// <summary>
    /// Asks the interpreter about one queued input and returns its proposal, checked against this parser: the
    /// canonical expression is realised under the item's profile, so the reviewer sees whether it is usable before
    /// accepting it. Never throws for a bad answer; a proposal with a null canonical records that none was offered.
    /// </summary>
    public async Task<Proposal> ProposeAsync(QueueItem item, InterpreterHost host, CancellationToken ct = default)
    {
        var interpreter = host.Interpreter ?? throw new InvalidOperationException("No interpreter is configured.");
        var model = host.Model ?? interpreter.Name;
        var (text, profileName, intentName) = DatePart(item);
        if (text is null) return new Proposal(null, 0, "No date part to interpret: the agent parser found no dates in this string.", model, DateTimeOffset.UtcNow, null, false);
        var profile = Profile(profileName);
        var deterministic = Parse(text, profileName, intentName);
        LinkedArtNet.Dates.Interpretation.InterpreterResponse? response;
        try
        {
            response = await interpreter.InterpretAsync(text, new LinkedArtNet.Dates.Interpretation.InterpreterContext(profile, ParseIntent(intentName), deterministic), ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return new Proposal(null, 0, "The interpreter could not be reached: " + e.Message, model, DateTimeOffset.UtcNow, null, false);
        }
        if (response is null) return new Proposal(null, 0, "No reading offered: the model did not take the text for a date.", model, DateTimeOffset.UtcNow, null, false);
        var canonical = response.Canonical.Trim();
        var intent = response.Intent?.ToString().ToLowerInvariant();
        var (realised, error) = Realise(canonical, profileName, intent ?? intentName);
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
        var expect = BuildExpectation(profile, intent, expectedStatus, canonical);
        return new CorpusEntry
        {
            Id = id, Input = input, Intent = intent.ToLowerInvariant(), Profile = profile, Tags = CleanTags(tags), Expect = expect,
            Status = EntryStatus.Proposed, Notes = ReviewNote(notes, reviewer), SourceExpectation = "trainer",
        };
    }

    /// <summary>The dates expectation for a reviewed reading; throws <see cref="ArgumentException"/> when a parsed reading's expression cannot be realised.</summary>
    public Expectation BuildExpectation(string profile, string intent, string expectedStatus, string? canonical)
    {
        var status = expectedStatus.ToLowerInvariant() is "undated" or "unparsed" ? expectedStatus.ToLowerInvariant() : "parsed";
        if (status != "parsed") return new Expectation { Status = status };
        var (realised, error) = Realise(canonical ?? "", profile, intent);
        if (realised is null) throw new ArgumentException(error, nameof(canonical));
        return new Expectation
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

    /// <summary>
    /// A proposed agent corpus entry from a review. Every part the reviewer stated is pinned; the date part nests a
    /// dates expectation when the reviewer gave the substring a status, and <c>noDates</c> when they said there is none.
    /// </summary>
    public AgentCorpusEntry BuildAgentEntry(string id, string input, string profile, AgentReview review, IEnumerable<string> tags, string? notes, string reviewer)
    {
        var dateText = Blank(review.DateText);
        Expectation? dates = null;
        if (dateText is not null && review.DatesStatus is { } status) dates = BuildExpectation(review.DatesProfile, "pair", status, review.DatesCanonical);
        return new AgentCorpusEntry
        {
            Id = id, Input = input, Profile = profile, Tags = CleanTags(tags), Status = EntryStatus.Proposed, Notes = ReviewNote(notes, reviewer),
            Expect = new AgentExpectation
            {
                Kind = review.Kind.ToLowerInvariant(),
                Name = Blank(review.Name),
                FamilyName = Blank(review.FamilyName),
                GivenNames = Blank(review.GivenNames),
                Honorifics = review.Honorifics.Count > 0 ? review.Honorifics : null,
                RoleLabels = review.RoleLabels.Count > 0 ? review.RoleLabels : null,
                Nationalities = review.Nationalities.Count > 0 ? review.Nationalities : null,
                Qualifiers = review.Qualifiers.Count > 0 ? review.Qualifiers : null,
                DateText = dateText,
                Dates = dates,
                NoDates = dateText is null ? true : null,
                NoUnconsumed = review.NoUnconsumed ? true : null,
            },
        };
    }

    /// <summary>
    /// The dates corpus entry an agent review yields for its date substring, or null when the review gave the part no
    /// status or the dates corpus already has that input under that profile. The entry is tagged <c>agent-derived</c>
    /// and its note names the agent entry it came from.
    /// </summary>
    public CorpusEntry? DerivedDatesEntry(string id, AgentReview review, string agentInput, string agentEntryId, IEnumerable<string> tags, string reviewer)
    {
        var dateText = Blank(review.DateText);
        if (dateText is null || review.DatesStatus is null || KnownInDatesCorpus(dateText, review.DatesProfile)) return null;
        var note = $"Date part of the agent string \"{agentInput}\" (agent entry {agentEntryId}).";
        return BuildEntry(id, dateText, review.DatesProfile, "pair", review.DatesStatus, review.DatesCanonical, tags.Append("agent-derived"), note, reviewer);
    }

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static List<string> CleanTags(IEnumerable<string> tags) => tags.Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().Order(StringComparer.Ordinal).ToList();

    private static string ReviewNote(string? notes, string reviewer)
    {
        var note = $"Reviewed by {reviewer} in the trainer on {DateTime.UtcNow:yyyy-MM-dd}.";
        return string.IsNullOrWhiteSpace(notes) ? note : notes.Trim() + " " + note;
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
            Entries = submissions.Select(s => s.Entry).OfType<CorpusEntry>().ToList(),
        };
    }

    public static string Serialise(CorpusFile file) => JsonSerializer.Serialize(file, CorpusFile.JsonOptions) + "\n";

    /// <summary>An agent corpus file holding the given submissions' agent entries, in the shape of corpus/agents/*.json.</summary>
    public AgentCorpusFile BuildAgentFile(IReadOnlyList<Submission> submissions)
    {
        var reviewers = submissions.Select(s => s.Reviewer).Distinct().Order(StringComparer.Ordinal);
        return new AgentCorpusFile
        {
            Source = new CorpusSource("Trainer submissions (agent strings)", "CC0-1.0",
                Notes: $"Reviewed in the examples-site trainer by {string.Join(", ", reviewers)}. Proposed until a maintainer verifies them; the date parts also went to the dates corpus as agent-derived entries unless it already had them."),
            Defaults = new AgentCorpusDefaults(Status: EntryStatus.Proposed, Tags: ["trainer"]),
            Entries = submissions.Select(s => s.AgentEntry).OfType<AgentCorpusEntry>().ToList(),
        };
    }

    public static string Serialise(AgentCorpusFile file) => JsonSerializer.Serialize(file, AgentCorpusFile.JsonOptions) + "\n";

    /// <summary>Errors for a serialised agent corpus file: it must load back under the strict reader and every entry must have an id, an input and a known profile.</summary>
    public IReadOnlyList<string> ValidateAgents(string json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<AgentCorpusFile>(json, AgentCorpusFile.JsonOptions) ?? throw new JsonException("empty file");
            var errors = new List<string>();
            foreach (var e in file.Entries)
            {
                if (string.IsNullOrWhiteSpace(e.Id)) errors.Add("an entry has no id");
                if (string.IsNullOrWhiteSpace(e.Input)) errors.Add($"{e.Id}: no input");
                if (!AgentProfileCatalog.BuiltIn.TryGet(e.Profile, out _)) errors.Add($"{e.Id}: unknown agent profile '{e.Profile}'");
            }
            var dup = file.Entries.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dup.Count > 0) errors.Add("duplicate ids: " + string.Join(", ", dup));
            return errors;
        }
        catch (JsonException e) { return [e.Message]; }
    }

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

/// <summary>
/// What a reviewer says an agent string holds. Lists are the labels the corpus checks as sets; <see cref="DateText"/>
/// null or blank means there is no date part; <see cref="DatesStatus"/> null means the date part is pinned as text only.
/// </summary>
public sealed record AgentReview(string Kind, string? Name, string? FamilyName, string? GivenNames,
    IReadOnlyList<string> Honorifics, IReadOnlyList<string> RoleLabels, IReadOnlyList<string> Nationalities, IReadOnlyList<string> Qualifiers,
    string? DateText, string? DatesStatus, string? DatesCanonical, string DatesProfile, bool NoUnconsumed)
{
    /// <summary>The parser's reading taken as right. The date part gets a nested expectation only when the dates parser read it fully.</summary>
    public static AgentReview FromParsed(ParsedAgent a, string datesProfile)
    {
        string? status = a.Dates?.Status switch { ParseStatus.Parsed => "parsed", ParseStatus.Undated => "undated", ParseStatus.Unparsed => "unparsed", _ => null };
        return new AgentReview(a.Kind.ToString().ToLowerInvariant(), a.Name, a.FamilyName, a.GivenNames,
            a.Honorifics, a.Roles.Select(r => r.Label).ToList(), a.Nationalities.Select(n => n.Label).ToList(), a.Qualifiers.Select(Camel).Order(StringComparer.Ordinal).ToList(),
            a.DateText, status, a.Dates?.Canonical, datesProfile, a.Unconsumed is null);
    }

    public static string Camel(AgentQualifier q) => char.ToLowerInvariant(q.ToString()[0]) + q.ToString()[1..];

    public static IReadOnlyList<string> Split(string? list) => (list ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToList();
}

/// <summary>Opens a pull request adding corpus files, through the GitHub REST API.</summary>
public sealed class GitHubSubmitter(HttpClient http, IOptions<TrainerOptions> options)
{
    public bool Configured => options.Value.Submission.Configured;

    public string BaseBranch => options.Value.Submission.BaseBranch;

    /// <summary>One file under the dates proposed directory.</summary>
    public Task<string> CreatePullRequestAsync(string fileName, string content, string title, string body, CancellationToken ct = default) =>
        CreatePullRequestAsync([($"{options.Value.Submission.Directory.TrimEnd('/')}/{fileName}", content)], title, body, ct);

    /// <param name="files">Repository-relative paths with their contents; all go on one branch in one pull request.</param>
    /// <returns>The pull request's HTML URL.</returns>
    public async Task<string> CreatePullRequestAsync(IReadOnlyList<(string Path, string Content)> files, string title, string body, CancellationToken ct = default)
    {
        var s = options.Value.Submission;
        if (!s.Configured) throw new InvalidOperationException("Pull requests are not configured (Trainer:Submission:Repo and :Token).");
        if (files.Count == 0) throw new ArgumentException("No files to submit.", nameof(files));
        var baseRef = await SendAsync(HttpMethod.Get, $"repos/{s.Repo}/git/ref/heads/{s.BaseBranch}", null, ct);
        var sha = baseRef["object"]!["sha"]!.GetValue<string>();
        var branch = $"trainer/{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        await SendAsync(HttpMethod.Post, $"repos/{s.Repo}/git/refs", new { @ref = $"refs/heads/{branch}", sha }, ct);
        foreach (var (path, content) in files)
            await SendAsync(HttpMethod.Put, $"repos/{s.Repo}/contents/{path.TrimStart('/')}", new { message = title, content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)), branch }, ct);
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
