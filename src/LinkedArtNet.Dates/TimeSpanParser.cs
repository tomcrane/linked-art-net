using LinkedArtNet.Dates.Canonical;
using LinkedArtNet.Dates.Grammar;
using LinkedArtNet.Dates.Periods;
using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.Dates.Text;

namespace LinkedArtNet.Dates;

/// <summary>Per-call options; the profile and intent are the usual ones to set.</summary>
public sealed record ParseOptions
{
    public Intent Intent { get; init; } = Intent.Auto;
    public ParseProfile? Profile { get; init; }
    public int MaxAlternatives { get; init; } = 5;
}

/// <summary>A reading of a normalised string produced by one strategy.</summary>
public sealed record Candidate(DateExpression Expression, double Confidence, string Strategy, string? UnconsumedText = null,
    IReadOnlyList<string>? Notes = null, IReadOnlySet<DateFlag>? Flags = null);

/// <summary>One link of the strategy chain: cheap and deterministic, returns null when it has nothing to say.</summary>
public interface IParseStrategy
{
    string Name { get; }
    /// <summary>True to receive the trimmed original text instead of the normalised one (EDTF sets keep their brackets).</summary>
    bool UsesRawText => false;
    Candidate? TryParse(string text, ParseProfile profile);
}

/// <summary>Strategy 1: strict ISO 8601 / EDTF and our canonical extensions, confidence 1. <c>period:key</c> resolves through the source.</summary>
public sealed class CanonicalStrategy(INamedPeriodSource? periods = null) : IParseStrategy
{
    public string Name => "canonical";

    public bool UsesRawText => true;

    public Candidate? TryParse(string text, ParseProfile profile) =>
        CanonicalReader.TryParse(text, out var expression, out _, periods) ? new Candidate(expression!, 1.0, Name) : null;
}

/// <summary>
/// Free text in, Linked Art TimeSpans out. Thread-safe; keep one instance. The text is normalised, then the strategy
/// chain is tried in order (canonical, grammar, salvage by default) and the first candidate at or above the profile's
/// accept threshold wins; otherwise the best candidate is used and the rest are reported as alternatives.
/// Nothing here throws for bad input.
/// </summary>
public sealed partial class TimeSpanParser
{
    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*\d{4}(?:-\d{2}(?:-\d{2})?)?\s*$")]
    private static partial System.Text.RegularExpressions.Regex IsoShape();

    // "-1850" is a catalogue's open start; a zero-padded "-0499" is EDTF's BCE year and is left alone.
    [System.Text.RegularExpressions.GeneratedRegex(@"^\s*-\s*[1-9]\d{2,3}\s*$")]
    private static partial System.Text.RegularExpressions.Regex LeadingDash();

    public const string Version = "0.2.0";

    private readonly IReadOnlyList<IParseStrategy> _strategies;
    private readonly ParseProfile _profile;
    private readonly INamedPeriodSource _periods;
    private readonly Func<long>? _currentYear;

    /// <param name="periods">Named-period source; the built-in tables when null. Pass an empty composite to disable.</param>
    public TimeSpanParser(ParseProfile? profile = null, IEnumerable<IParseStrategy>? strategies = null, Func<long>? currentYear = null, INamedPeriodSource? periods = null)
    {
        _profile = profile ?? ParseProfile.Default;
        _periods = periods ?? JsonNamedPeriodSource.BuiltIn;
        _strategies = strategies?.ToList() ?? DefaultStrategies(_profile, _periods);
        _currentYear = currentYear;
    }

    public static TimeSpanParser Default { get; } = new();

    public ParseProfile Profile => _profile;

    public INamedPeriodSource Periods => _periods;

    /// <summary>Canonical, grammar (with the profile's languages and the periods), named period, salvage.</summary>
    public static IReadOnlyList<IParseStrategy> DefaultStrategies(ParseProfile profile, INamedPeriodSource? periods = null)
    {
        var lexicon = Lexicon.Merge(profile.Languages.Select(Lexicon.Load));
        periods ??= JsonNamedPeriodSource.BuiltIn;
        return [new CanonicalStrategy(periods), new GrammarStrategy(new Tokenizer(lexicon), periods), new NamedPeriodStrategy(periods), new SalvageStrategy()];
    }

    public TimeSpanParseResult Parse(string input, Intent intent = Intent.Auto) => Parse(input, new ParseOptions { Intent = intent });

    public TimeSpanParseResult Parse(string input, ParseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var profile = options.Profile ?? _profile;
        var original = input ?? "";

        // Fast path: a bare ISO year, month or day ("1984", "2024-04-27") needs no normaliser and no strategy chain.
        if (original.Length is >= 4 and <= 12 && IsoShape().IsMatch(original) && CanonicalReader.TryParse(original.Trim(), out var iso))
            return new TimeSpanRealiser(profile, _currentYear).Realise(iso!, original, options.Intent, 1.0, "canonical");
        // EDTF sets ("[1667,1668,1670..1672]", "{..1984}") keep their brackets, which the normaliser would otherwise read as inferred-date marks.
        var trimmed = original.Trim();
        if (trimmed.Length >= 3 && trimmed[0] is (char)91 or (char)123 && trimmed[^1] is (char)93 or (char)125 && (trimmed.Contains((char)44) || trimmed.Contains("..", StringComparison.Ordinal))
            && CanonicalReader.TryParse(trimmed, out var set, out _, _periods))
            return new TimeSpanRealiser(profile, _currentYear).Realise(set!, original, options.Intent, 1.0, "canonical");

        var normalised = Normaliser.Normalise(original);
        if (normalised.Text.Length == 0) return Unparsed(original, profile, "Empty input.", normalised);

        var candidates = new List<Candidate>();
        foreach (var strategy in _strategies)
        {
            Candidate? candidate;
            try { candidate = strategy.TryParse(strategy.UsesRawText ? original.Trim() : normalised.Text, profile); }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or OverflowException)
            {
                candidate = null; // a strategy bug on odd input must never surface; the next strategy gets its turn
            }
            if (candidate is null) continue;
            if (strategy.UsesRawText && profile.LeadingDashIsOpenStart && LeadingDash().IsMatch(original))
            {
                // "-1850": the EDTF reading (a BCE year) is kept as an alternative; the catalogue reading wins.
                candidates.Add(candidate with { Confidence = 0.4, Notes = ["EDTF would read a leading minus as a BCE year."] });
                continue;
            }
            candidates.Add(candidate);
            if (candidate.Confidence >= profile.AcceptThreshold) break;
        }
        if (candidates.Count == 0) return Unparsed(original, profile, "No strategy could read the text.", normalised);

        var best = candidates.OrderByDescending(c => c.Confidence).First();
        var realiser = new TimeSpanRealiser(profile, _currentYear);
        var result = realiser.Realise(best.Expression, original, options.Intent, best.Confidence, best.Strategy);

        var alternatives = result.Alternatives.ToList();
        foreach (var other in candidates.Where(c => c != best).Take(options.MaxAlternatives))
        {
            var span = realiser.Realise(other.Expression, original, options.Intent, other.Confidence, other.Strategy);
            alternatives.Add(new Alternative(other.Expression.Canonical, other.Confidence, $"Read by the {other.Strategy} strategy", span.TimeSpan));
        }
        var notes = normalised.Notes.Concat(best.Notes ?? []).Concat(result.Notes).ToList();
        var flags = new HashSet<DateFlag>(result.Flags);
        flags.UnionWith(normalised.Flags);
        if (best.Flags is not null) flags.UnionWith(best.Flags);
        if (best.Strategy == "salvage") flags.Add(DateFlag.Salvaged);
        if (best.UnconsumedText is not null) flags.Add(DateFlag.Unconsumed);

        return result with { Alternatives = alternatives, Notes = notes, Flags = flags, UnconsumedText = best.UnconsumedText };
    }

    private static TimeSpanParseResult Unparsed(string input, ParseProfile profile, string note, NormalisedText normalised) => new()
    {
        Input = input, Status = ParseStatus.Unparsed, Kind = ResultKind.None, Confidence = 0,
        Notes = normalised.Notes.Append(note).ToList(), Flags = normalised.Flags, Profile = profile.Name,
    };
}
