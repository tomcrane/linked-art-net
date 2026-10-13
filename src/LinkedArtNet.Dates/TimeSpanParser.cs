using LinkedArtNet.Dates.Canonical;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates;

/// <summary>Per-call options; the profile and intent are the usual ones to set.</summary>
public sealed record ParseOptions
{
    public Intent Intent { get; init; } = Intent.Auto;
    public ParseProfile? Profile { get; init; }
    public int MaxAlternatives { get; init; } = 5;
}

/// <summary>A reading of a normalised string produced by one strategy.</summary>
public sealed record Candidate(DateExpression Expression, double Confidence, string Strategy, string? UnconsumedText = null, IReadOnlyList<string>? Notes = null);

/// <summary>One link of the strategy chain: cheap and deterministic, returns null when it has nothing to say.</summary>
public interface IParseStrategy
{
    string Name { get; }
    Candidate? TryParse(string text, ParseProfile profile);
}

/// <summary>Strategy 1: strict ISO 8601 / EDTF and our canonical extensions, confidence 1.</summary>
public sealed class CanonicalStrategy : IParseStrategy
{
    public string Name => "canonical";

    public Candidate? TryParse(string text, ParseProfile profile) =>
        CanonicalReader.TryParse(text, out var expression) ? new Candidate(expression!, 1.0, Name) : null;
}

/// <summary>
/// Free text in, Linked Art TimeSpans out. Thread-safe; keep one instance. The strategy chain is tried in order and
/// the first candidate at or above the profile's accept threshold wins; otherwise the best candidate is used and the
/// rest are reported as alternatives. Nothing here throws for bad input.
/// </summary>
public sealed class TimeSpanParser
{
    public const string Version = "0.1.0";

    private readonly IReadOnlyList<IParseStrategy> _strategies;
    private readonly ParseProfile _profile;
    private readonly Func<long>? _currentYear;

    public TimeSpanParser(ParseProfile? profile = null, IEnumerable<IParseStrategy>? strategies = null, Func<long>? currentYear = null)
    {
        _profile = profile ?? ParseProfile.Default;
        _strategies = strategies?.ToList() ?? [new CanonicalStrategy()];
        _currentYear = currentYear;
    }

    public static TimeSpanParser Default { get; } = new();

    public ParseProfile Profile => _profile;

    public TimeSpanParseResult Parse(string input, Intent intent = Intent.Auto) => Parse(input, new ParseOptions { Intent = intent });

    public TimeSpanParseResult Parse(string input, ParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var profile = options.Profile ?? _profile;
        var original = input ?? "";
        var text = Normalise(original);
        if (text.Length == 0) return Unparsed(original, profile, "Empty input.");

        var candidates = new List<Candidate>();
        foreach (var strategy in _strategies)
        {
            var candidate = strategy.TryParse(text, profile);
            if (candidate is null) continue;
            candidates.Add(candidate);
            if (candidate.Confidence >= profile.AcceptThreshold) break;
        }
        if (candidates.Count == 0) return Unparsed(original, profile, "No strategy could read the text.");

        var best = candidates.OrderByDescending(c => c.Confidence).First();
        var realiser = new TimeSpanRealiser(profile, _currentYear);
        var result = realiser.Realise(best.Expression, original, options.Intent, best.Confidence, best.Strategy);

        var alternatives = result.Alternatives.ToList();
        foreach (var other in candidates.Where(c => c != best).Take(options.MaxAlternatives))
        {
            var span = realiser.Realise(other.Expression, original, options.Intent, other.Confidence, other.Strategy);
            alternatives.Add(new Alternative(other.Expression.Canonical, other.Confidence, $"Read by the {other.Strategy} strategy", span.TimeSpan));
        }
        var notes = result.Notes.ToList();
        if (best.Notes is { Count: > 0 } n) notes.InsertRange(0, n);
        var flags = new HashSet<DateFlag>(result.Flags);
        if (best.UnconsumedText is not null) flags.Add(DateFlag.Unconsumed);

        return result with { Alternatives = alternatives, Notes = notes, Flags = flags, UnconsumedText = best.UnconsumedText };
    }

    // The full normaliser (Unicode, dashes, brackets, MARC noise) arrives with the grammar strategy; until then: trim.
    private static string Normalise(string input) => input.Length > 500 ? input[..500].Trim() : input.Trim();

    private static TimeSpanParseResult Unparsed(string input, ParseProfile profile, string note) => new()
    {
        Input = input, Status = ParseStatus.Unparsed, Kind = ResultKind.None, Confidence = 0,
        Notes = [note], Profile = profile.Name,
    };
}
