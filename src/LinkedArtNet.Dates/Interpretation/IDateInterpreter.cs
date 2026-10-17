using System.Text.Json;
using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.Dates.Text;

namespace LinkedArtNet.Dates.Interpretation;

/// <summary>
/// Strategy 5 of the parser: something that can propose a <em>canonical expression</em> for text the deterministic
/// strategies could not read, or read only weakly. An LLM is the obvious implementation (<c>LinkedArtNet.Dates.Llm</c>);
/// a table of reviewed answers is another (<see cref="LookupInterpreter"/>). The interpreter never produces dates:
/// its answer is read by the canonical reader and realised under the profile exactly as typed text would be, so a
/// wrong answer can only be a wrong reading, never a malformed TimeSpan. See docs/03-timespan-parser.md section 4.
/// </summary>
public interface IDateInterpreter
{
    /// <summary>Reported as the result's strategy, e.g. "claude" or "lookup".</summary>
    string Name { get; }

    /// <summary>
    /// The highest confidence a reading from this interpreter may carry; the parser caps the interpreter's own
    /// estimate at this. 0.6 by default, so an interpreted reading never outranks a grammar reading and stays
    /// below the default accept threshold: it is a proposal for review, not a verdict.
    /// </summary>
    double MaxConfidence => 0.6;

    /// <summary>A canonical expression for the text, or null when the interpreter has nothing to offer.</summary>
    Task<InterpreterResponse?> InterpretAsync(string input, InterpreterContext context, CancellationToken ct = default);
}

/// <param name="Canonical">A canonical expression (docs/canonical-expression.md); anything else is discarded by the parser.</param>
/// <param name="SelfConfidence">The interpreter's own estimate, 0 to 1; capped by <see cref="IDateInterpreter.MaxConfidence"/>.</param>
/// <param name="Rationale">One sentence for the reviewer; recorded in the result's notes.</param>
/// <param name="Intent">Set when the interpreter can tell a single span from a pair and the caller left it to auto.</param>
public sealed record InterpreterResponse(string Canonical, double SelfConfidence, string? Rationale = null, Intent? Intent = null);

/// <summary>What the interpreter is told besides the text: the profile (its conventions and languages), the intent, and what the deterministic strategies made of it.</summary>
public sealed record InterpreterContext(ParseProfile Profile, Intent Intent, TimeSpanParseResult Deterministic)
{
    public IReadOnlyList<string> Languages => Profile.Languages;
}

/// <summary>
/// A fixed table of readings, text to canonical expression: reviewed answers for one collection kept as a file, or
/// scripted answers in tests. Matching is on the trimmed, case-folded text. Because the readings were made by a
/// person, <see cref="MaxConfidence"/> is high (0.95 by default) rather than the interpreter default.
/// </summary>
public sealed class LookupInterpreter(IReadOnlyDictionary<string, string> readings, string name = "lookup", double maxConfidence = 0.95) : IDateInterpreter
{
    private readonly Dictionary<string, string> _readings = readings.ToDictionary(kv => Key(kv.Key), kv => kv.Value, StringComparer.Ordinal);

    public string Name => name;
    public double MaxConfidence => maxConfidence;
    public int Count => _readings.Count;

    /// <summary>Loads a JSON object of <c>{ "text as written": "canonical expression" }</c>.</summary>
    public static LookupInterpreter Load(string path, string name = "lookup", double maxConfidence = 0.95)
    {
        var readings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                       ?? throw new InvalidDataException($"{path}: expected a JSON object of text to canonical expression.");
        return new LookupInterpreter(readings, name, maxConfidence);
    }

    public Task<InterpreterResponse?> InterpretAsync(string input, InterpreterContext context, CancellationToken ct = default) =>
        Task.FromResult(_readings.TryGetValue(Key(input), out var canonical)
            ? new InterpreterResponse(canonical, 1.0, "From the lookup table.")
            : null);

    private static string Key(string text) => Normaliser.Normalise(text).Text.ToLowerInvariant();
}
