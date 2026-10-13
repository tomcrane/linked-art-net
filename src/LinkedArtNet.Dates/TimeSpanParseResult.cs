namespace LinkedArtNet.Dates;

public enum Intent
{
    /// <summary>Pair kinds when the expression carries lifecycle or floruit markers, otherwise one span.</summary>
    Auto,
    /// <summary>One span covering everything the text denotes.</summary>
    Single,
    /// <summary>Two spans (birth and death, formation and dissolution) from an interval or lifecycle markers.</summary>
    Pair,
}

public enum ParseStatus { Parsed, Partial, Undated, Unparsed }

public enum ResultKind { None, Single, BirthDeath, FormationDissolution, Floruit }

public enum DateFlag
{
    Approximate, Uncertain, OpenStart, OpenEnd, Disjunction, Bracketed, Salvaged, Interpreted, Corrected,
    Floruit, Lifecycle, Bce, DeepTime, NamedPeriod, Season, Band, AmbiguousDayMonth, Unconsumed, Inverted, Padded,
    TwoDigitYear, SecondaryDate, Parenthesised, RomanNumeral,
}

/// <summary>Another reading of the input, or a member of a disjunction, with its own realisation.</summary>
public sealed record Alternative(string Canonical, double Confidence, string Note, LinkedArtTimeSpan? TimeSpan);

/// <summary>Everything the parser can say about one input string.</summary>
public sealed record TimeSpanParseResult
{
    public required string Input { get; init; }
    public required ParseStatus Status { get; init; }
    public ResultKind Kind { get; init; } = ResultKind.None;
    /// <summary>0..1; how sure the parser is of the <see cref="Expression"/> (realisation is deterministic).</summary>
    public double Confidence { get; init; }
    /// <summary>The span for <see cref="ResultKind.Single"/>, or the whole active period for <see cref="ResultKind.Floruit"/>.</summary>
    public LinkedArtTimeSpan? TimeSpan { get; init; }
    /// <summary>The first span of a pair (birth, formation, start of activity).</summary>
    public LinkedArtTimeSpan? Begin { get; init; }
    /// <summary>The second span of a pair (death, dissolution, end of activity).</summary>
    public LinkedArtTimeSpan? End { get; init; }
    public DateExpression? Expression { get; init; }
    /// <summary>The canonical text of <see cref="Expression"/>; what a reviewer corrects and what an interpreter returns.</summary>
    public string? Canonical { get; init; }
    /// <summary>Strict EDTF for the realised bounds, where EDTF can express them.</summary>
    public string? Edtf { get; init; }
    public IReadOnlySet<DateFlag> Flags { get; init; } = new HashSet<DateFlag>();
    public IReadOnlyList<Alternative> Alternatives { get; init; } = [];
    /// <summary>Human-readable explanation, one step per line.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];
    /// <summary>Which strategy produced the expression ("canonical", "grammar", "named-period", "salvage", "interpreter").</summary>
    public string Strategy { get; init; } = "";
    public string? UnconsumedText { get; init; }
    public string Profile { get; init; } = "default";
    public string ParserVersion { get; init; } = TimeSpanParser.Version;

    public bool Succeeded => Status is ParseStatus.Parsed or ParseStatus.Partial;

    /// <summary>All spans produced, in order: <see cref="TimeSpan"/>, <see cref="Begin"/>, <see cref="End"/>.</summary>
    public IEnumerable<LinkedArtTimeSpan> Spans()
    {
        if (TimeSpan is not null) yield return TimeSpan;
        if (Begin is not null) yield return Begin;
        if (End is not null) yield return End;
    }
}
