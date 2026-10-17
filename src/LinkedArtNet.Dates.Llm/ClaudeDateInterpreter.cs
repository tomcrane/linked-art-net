using System.Collections.Concurrent;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using LinkedArtNet.Dates.Interpretation;

namespace LinkedArtNet.Dates.Llm;

public sealed class ClaudeInterpreterOptions
{
    /// <summary>Read from the ANTHROPIC_API_KEY environment variable when null.</summary>
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";
    /// <summary>Reasoning effort; a date string is a small task, so low is the default.</summary>
    public string Effort { get; set; } = "low";
    public int MaxTokens { get; set; } = 600;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(60);
    /// <summary>Highest confidence the parser lets an interpreted reading carry.</summary>
    public double MaxConfidence { get; set; } = 0.6;
    /// <summary>Remember answers for the life of the interpreter, keyed by model, profile, intent and text.</summary>
    public bool CacheResponses { get; set; } = true;
}

/// <summary>Token counts across every call this interpreter has made, for the cost line in a report.</summary>
public sealed record InterpreterUsage(int Calls, long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens);

/// <summary>What one call returned before parsing: the JSON text and the usage, or nothing when the model declined.</summary>
public sealed record RawAnswer(string? Json, long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens, string? StopReason);

/// <summary>
/// The reference <see cref="IDateInterpreter"/>: asks Claude for a canonical expression and nothing else. The
/// answer is constrained to a JSON schema (canonical, confidence, rationale, intent), the system prompt is the
/// canonical grammar and is marked for prompt caching, and the parser reads and realises the expression itself, so
/// the model never produces a date. Intended for growing the corpus under review, not for the public service.
/// </summary>
public class ClaudeDateInterpreter : IDateInterpreter
{
    private readonly ClaudeInterpreterOptions _options;
    private readonly Lazy<AnthropicClient> _client;
    private readonly ConcurrentDictionary<string, InterpreterResponse?> _memo = new(StringComparer.Ordinal);
    private long _calls, _input, _output, _cacheRead, _cacheWrite;

    public ClaudeDateInterpreter(ClaudeInterpreterOptions? options = null, AnthropicClient? client = null)
    {
        _options = options ?? new ClaudeInterpreterOptions();
        _client = new Lazy<AnthropicClient>(() => client ?? (_options.ApiKey is null ? new AnthropicClient() : new AnthropicClient { ApiKey = _options.ApiKey }));
    }

    public string Name => "claude";
    public double MaxConfidence => _options.MaxConfidence;
    public string Model => _options.Model;

    public InterpreterUsage Usage => new((int)Interlocked.Read(ref _calls), Interlocked.Read(ref _input), Interlocked.Read(ref _output), Interlocked.Read(ref _cacheRead), Interlocked.Read(ref _cacheWrite));

    public async Task<InterpreterResponse?> InterpretAsync(string input, InterpreterContext context, CancellationToken ct = default)
    {
        var key = $"{_options.Model}|{context.Profile.Name}|{context.Intent}|{input}";
        if (_options.CacheResponses && _memo.TryGetValue(key, out var cached)) return cached;

        var raw = await SendAsync(InterpreterPrompt.System(TimeSpanParser.Version), InterpreterPrompt.User(input, context), ct).ConfigureAwait(false);
        Interlocked.Increment(ref _calls);
        if (raw is not null)
        {
            Interlocked.Add(ref _input, raw.InputTokens); Interlocked.Add(ref _output, raw.OutputTokens);
            Interlocked.Add(ref _cacheRead, raw.CacheReadTokens); Interlocked.Add(ref _cacheWrite, raw.CacheWriteTokens);
        }
        var response = raw?.Json is null ? null : ParseAnswer(raw.Json);
        if (_options.CacheResponses) _memo[key] = response;
        return response;
    }

    /// <summary>The model's JSON to a response; null when the text was judged not to be a date or the JSON is unusable.</summary>
    internal static InterpreterResponse? ParseAnswer(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var canonical = root.TryGetProperty("canonical", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(canonical)) return null;
            var confidence = root.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.Number ? Math.Clamp(conf.GetDouble(), 0, 1) : 0.5;
            var rationale = root.TryGetProperty("rationale", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
            Intent? intent = root.TryGetProperty("intent", out var i) && i.ValueKind == JsonValueKind.String
                ? i.GetString()?.ToLowerInvariant() switch { "pair" => Intent.Pair, "single" => Intent.Single, _ => null }
                : null;
            return new InterpreterResponse(canonical, confidence, rationale, intent);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>One Messages API call. Overridden in tests to answer from a script.</summary>
    protected virtual async Task<RawAnswer?> SendAsync(string system, string user, CancellationToken ct)
    {
        var parameters = new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = new List<TextBlockParam> { new() { Text = system, CacheControl = new CacheControlEphemeral() } },
            Messages = [new() { Role = Role.User, Content = user }],
            OutputConfig = new OutputConfig
            {
                Effort = ParseEffort(_options.Effort),
                Format = new JsonOutputFormat { Schema = SchemaDictionary() },
            },
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.Timeout);
        var message = await _client.Value.Messages.Create(parameters, cancellationToken: timeout.Token).ConfigureAwait(false);
        var stop = message.StopReason?.ToString();
        var text = message.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        var usage = message.Usage;
        return new RawAnswer(stop is "refusal" ? null : text, usage.InputTokens, usage.OutputTokens, usage.CacheReadInputTokens ?? 0, usage.CacheCreationInputTokens ?? 0, stop);
    }

    private static Effort ParseEffort(string effort) => effort.ToLowerInvariant() switch
    {
        "low" => Effort.Low, "medium" => Effort.Medium, "high" => Effort.High, "max" => Effort.Max, _ => Effort.Low,
    };

    private static Dictionary<string, JsonElement> SchemaDictionary()
    {
        var dict = new Dictionary<string, JsonElement>();
        foreach (var property in InterpreterPrompt.ResponseSchema.EnumerateObject()) dict[property.Name] = property.Value.Clone();
        return dict;
    }
}
