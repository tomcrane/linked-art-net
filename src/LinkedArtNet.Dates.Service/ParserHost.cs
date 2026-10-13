using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using LinkedArtNet.Dates.Realisation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LinkedArtNet.Dates.Service;

/// <summary>Service configuration (section <c>Parser</c> in appsettings or environment variables <c>Parser__...</c>).</summary>
public sealed class ParserOptions
{
    /// <summary>Directory of profile JSON files; relative paths resolve against the content root. Missing: only "default".</summary>
    public string ProfilesDirectory { get; set; } = "profiles";
    public int MaxInputLength { get; set; } = 500;
    public int MaxInputsPerRequest { get; set; } = 500;
    public int MaxRequestBodyBytes { get; set; } = 256 * 1024;
    /// <summary>Memo cache entries kept in memory; results repeat heavily in real data.</summary>
    public int MemoSize { get; set; } = 50_000;
    public int CacheMaxAgeSeconds { get; set; } = 86_400;
    /// <summary>Requests per minute per client IP before 429.</summary>
    public int RequestsPerMinute { get; set; } = 600;
}

/// <summary>Opt-in capture of inputs the parser could not read well, for triage in the trainer. Off by default.</summary>
public sealed class CaptureOptions
{
    public bool Enabled { get; set; }
    /// <summary>JSONL file appended to; relative paths resolve against the content root.</summary>
    public string Path { get; set; } = "capture/unparsed.jsonl";
    /// <summary>Results at or above this confidence (and parsed) are not captured.</summary>
    public double MinConfidence { get; set; } = 0.75;
}

/// <summary>
/// One parser per effective profile (profile name plus any per-request overrides), a memo of results keyed by
/// parser version, profile, intent and input, and the profile catalog. Thread-safe; registered as a singleton.
/// </summary>
public sealed class ParserHost
{
    private readonly ProfileCatalog _catalog;
    private readonly ConcurrentDictionary<string, TimeSpanParser> _parsers = new(StringComparer.Ordinal);
    private readonly IMemoryCache _memo;
    private readonly ParserOptions _options;

    public ParserHost(IOptions<ParserOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        var dir = Path.IsPathRooted(_options.ProfilesDirectory) ? _options.ProfilesDirectory : Path.Combine(environment.ContentRootPath, _options.ProfilesDirectory);
        _catalog = Directory.Exists(dir) ? ProfileCatalog.Load(dir) : new ProfileCatalog();
        _memo = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.MemoSize });
    }

    public ProfileCatalog Catalog => _catalog;

    public ParserOptions Options => _options;

    public bool TryGetProfile(string? name, out ParseProfile profile) => _catalog.TryGet(name is null or "" ? "default" : name, out profile);

    /// <summary>The parser for a profile, with request-level overrides applied as a derived profile.</summary>
    public TimeSpanParser ParserFor(ParseProfile profile, bool? emitInnerBounds, LabelPolicy? labelPolicy)
    {
        var effective = profile;
        if (emitInnerBounds is { } eib && eib != profile.EmitInnerBounds) effective = effective with { EmitInnerBounds = eib };
        if (labelPolicy is { } lp && lp != profile.LabelPolicy) effective = effective with { LabelPolicy = lp };
        var key = effective.Name + "|" + effective.GetHashCode();
        return _parsers.GetOrAdd(key, _ => new TimeSpanParser(effective));
    }

    public TimeSpanParseResult Parse(TimeSpanParser parser, string input, Intent intent)
    {
        var key = MemoKey(parser.Profile, intent, input);
        return _memo.GetOrCreate(key, entry =>
        {
            entry.Size = 1;
            entry.SlidingExpiration = TimeSpan.FromHours(6);
            return parser.Parse(input, intent);
        })!;
    }

    private static string MemoKey(ParseProfile profile, Intent intent, string input) =>
        $"{TimeSpanParser.Version}|{profile.Name}|{profile.GetHashCode()}|{intent}|{input}";

    /// <summary>A weak ETag over everything that determines the response.</summary>
    public static string ETag(string profile, Intent intent, string input, string shape)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{TimeSpanParser.Version}|{profile}|{intent}|{shape}|{input}"));
        return "W/\"" + Convert.ToHexString(bytes.AsSpan(0, 12)).ToLowerInvariant() + "\"";
    }
}

/// <summary>Appends weak results to a JSONL file off the request path. Never records <c>ref</c> or headers.</summary>
public sealed class CaptureWriter : BackgroundService
{
    private readonly Channel<string> _lines = Channel.CreateBounded<string>(new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });
    private readonly CaptureOptions _options;
    private readonly string _path;
    private readonly ILogger<CaptureWriter> _logger;

    public CaptureWriter(IOptions<CaptureOptions> options, IHostEnvironment environment, ILogger<CaptureWriter> logger)
    {
        _options = options.Value;
        _path = Path.IsPathRooted(_options.Path) ? _options.Path : Path.Combine(environment.ContentRootPath, _options.Path);
        _logger = logger;
    }

    public bool Enabled => _options.Enabled;

    public void Consider(TimeSpanParseResult result)
    {
        if (!_options.Enabled) return;
        if (result.Status == ParseStatus.Parsed && result.Confidence >= _options.MinConfidence) return;
        var line = JsonSerializer.Serialize(new
        {
            at = DateTimeOffset.UtcNow,
            input = result.Input,
            profile = result.Profile,
            status = result.Status.ToString().ToLowerInvariant(),
            canonical = result.Canonical,
            confidence = result.Confidence,
            strategy = result.Strategy,
            parserVersion = result.ParserVersion,
        });
        _lines.Writer.TryWrite(line);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await foreach (var line in _lines.Reader.ReadAllAsync(stoppingToken))
        {
            try { await File.AppendAllTextAsync(_path, line + "\n", stoppingToken); }
            catch (IOException ex) { _logger.LogWarning(ex, "Could not append to the capture file."); }
        }
    }
}
