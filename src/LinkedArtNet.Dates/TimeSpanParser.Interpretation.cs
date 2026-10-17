using LinkedArtNet.Dates.Canonical;
using LinkedArtNet.Dates.Interpretation;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates;

public sealed partial class TimeSpanParser
{
    /// <summary>
    /// <see cref="Parse(string, ParseOptions)"/>, then strategy 5: when the deterministic result is unparsed, partial
    /// or below the profile's accept threshold, the interpreter is asked for a canonical expression, which is read and
    /// realised exactly as typed text would be. Undated text is left alone (it says there is no date). The
    /// interpreter's reading is capped at its <see cref="IDateInterpreter.MaxConfidence"/>, flagged
    /// <see cref="DateFlag.Interpreted"/>, carries the rationale as a note, and keeps the deterministic reading, if
    /// there was one, as an alternative. An answer that is not a canonical expression, no answer, or a failure leaves
    /// the deterministic result as it was, with a note saying so. <see cref="Parse(string, ParseOptions)"/> itself
    /// never calls an interpreter and never does I/O.
    /// </summary>
    public async Task<TimeSpanParseResult> ParseAsync(string input, IDateInterpreter? interpreter, ParseOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ParseOptions();
        var result = Parse(input, options);
        var profile = options.Profile ?? _profile;
        var wanted = options.AlwaysInterpret ? result.Status != ParseStatus.Undated : NeedsInterpreter(result, profile);
        if (interpreter is null || !options.AllowInterpreter || !wanted) return result;

        InterpreterResponse? response;
        try
        {
            response = await interpreter.InterpretAsync(input, new InterpreterContext(profile, options.Intent, result), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return WithNote(result, $"The {interpreter.Name} interpreter failed: {ex.Message}");
        }
        if (response is null) return WithNote(result, $"The {interpreter.Name} interpreter had no reading.");

        var canonical = response.Canonical.Trim();
        if (!CanonicalReader.TryParse(canonical, out var expression, out var error, _periods))
            return WithNote(result, $"The {interpreter.Name} interpreter proposed \"{canonical}\", which is not a canonical expression ({error}); discarded.");

        var confidence = Math.Clamp(Math.Min(response.SelfConfidence, interpreter.MaxConfidence), 0, 1);
        var intent = response.Intent is { } suggested && options.Intent == Intent.Auto ? suggested : options.Intent;
        var realised = new TimeSpanRealiser(profile, _currentYear).Realise(expression!, input, intent, confidence, interpreter.Name);

        var flags = new HashSet<DateFlag>(realised.Flags) { DateFlag.Interpreted };
        var notes = new List<string>(realised.Notes);
        if (!string.IsNullOrWhiteSpace(response.Rationale)) notes.Add($"{interpreter.Name}: {response.Rationale.Trim()}");
        var alternatives = realised.Alternatives.ToList();
        if (result.Canonical is not null && result.Canonical != realised.Canonical)
            alternatives.Add(new Alternative(result.Canonical, result.Confidence, $"Read by the {result.Strategy} strategy", result.TimeSpan));
        return realised with { Flags = flags, Notes = notes, Alternatives = alternatives };
    }

    /// <summary>Whether a deterministic result is weak enough to ask an interpreter about: unparsed, partial, or confident below the profile's threshold. Undated text is never escalated.</summary>
    public static bool NeedsInterpreter(TimeSpanParseResult result, ParseProfile profile) => result.Status switch
    {
        ParseStatus.Undated => false,
        ParseStatus.Unparsed or ParseStatus.Partial => true,
        _ => result.Confidence < profile.AcceptThreshold,
    };

    private static TimeSpanParseResult WithNote(TimeSpanParseResult result, string note) => result with { Notes = [.. result.Notes, note] };
}
