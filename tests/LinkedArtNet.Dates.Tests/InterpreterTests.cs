using LinkedArtNet.Dates.Interpretation;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Tests;

public class InterpreterTests
{
    /// <summary>Answers from a script and records what it was asked, so the tests can see when strategy 5 runs.</summary>
    private sealed class ScriptedInterpreter(Func<string, InterpreterContext, InterpreterResponse?> script, double maxConfidence = 0.6) : IDateInterpreter
    {
        public List<(string Input, InterpreterContext Context)> Calls { get; } = [];
        public string Name => "scripted";
        public double MaxConfidence => maxConfidence;
        public Task<InterpreterResponse?> InterpretAsync(string input, InterpreterContext context, CancellationToken ct = default)
        {
            Calls.Add((input, context));
            return Task.FromResult(script(input, context));
        }
    }

    private static readonly TimeSpanParser Parser = new();

    [Fact]
    public async Task A_confident_parse_never_reaches_the_interpreter()
    {
        var interpreter = new ScriptedInterpreter((_, _) => new InterpreterResponse("1066", 1));
        var result = await Parser.ParseAsync("late 1950s", interpreter);
        Assert.Equal("late 195X", result.Canonical);
        Assert.Equal("grammar", result.Strategy);
        Assert.Empty(interpreter.Calls);
        Assert.DoesNotContain(DateFlag.Interpreted, result.Flags);
    }

    [Fact]
    public async Task Undated_text_is_not_escalated()
    {
        var interpreter = new ScriptedInterpreter((_, _) => new InterpreterResponse("1066", 1));
        var result = await Parser.ParseAsync("n.d.", interpreter);
        Assert.Equal(ParseStatus.Undated, result.Status);
        Assert.Empty(interpreter.Calls);
    }

    [Fact]
    public async Task Unparsed_text_is_read_from_the_interpreter_s_canonical_expression()
    {
        var interpreter = new ScriptedInterpreter((input, ctx) =>
        {
            Assert.Equal(ParseStatus.Unparsed, ctx.Deterministic.Status);
            Assert.Equal("default", ctx.Profile.Name);
            return new InterpreterResponse("1850~", 0.9, "The scrawl reads as 'about 1850'.");
        });
        var result = await Parser.ParseAsync("ye olde scrawle, abt eighteen-fiftie", interpreter);

        Assert.Single(interpreter.Calls);
        Assert.Equal(ParseStatus.Parsed, result.Status);
        Assert.Equal("1850~", result.Canonical);
        Assert.Equal("scripted", result.Strategy);
        Assert.Equal(0.6, result.Confidence); // capped at the interpreter's MaxConfidence, not its own 0.9
        Assert.Contains(DateFlag.Interpreted, result.Flags);
        Assert.Contains(DateFlag.Approximate, result.Flags);
        Assert.Contains(result.Notes, n => n.Contains("scripted: The scrawl reads"));
        Assert.Equal("1850-01-01T00:00:00Z", result.TimeSpan!.BeginOfTheBegin!.Value.ToString());
        Assert.Equal("ye olde scrawle, abt eighteen-fiftie", result.TimeSpan.Label);
    }

    [Fact]
    public async Task An_answer_that_is_not_canonical_is_discarded_with_a_note()
    {
        var interpreter = new ScriptedInterpreter((_, _) => new InterpreterResponse("sometime in the 1800s", 0.9));
        var result = await Parser.ParseAsync("ye olde scrawle", interpreter);
        Assert.Equal(ParseStatus.Unparsed, result.Status);
        Assert.Null(result.Canonical);
        Assert.Contains(result.Notes, n => n.Contains("not a canonical expression") && n.Contains("sometime in the 1800s"));
        Assert.DoesNotContain(DateFlag.Interpreted, result.Flags);
    }

    [Fact]
    public async Task No_answer_and_a_failure_both_leave_the_deterministic_result()
    {
        var silent = new ScriptedInterpreter((_, _) => null);
        var quiet = await Parser.ParseAsync("ye olde scrawle", silent);
        Assert.Equal(ParseStatus.Unparsed, quiet.Status);
        Assert.Contains(quiet.Notes, n => n.Contains("had no reading"));

        var broken = new ScriptedInterpreter((_, _) => throw new HttpRequestException("503 from upstream"));
        var failed = await Parser.ParseAsync("ye olde scrawle", broken);
        Assert.Equal(ParseStatus.Unparsed, failed.Status);
        Assert.Contains(failed.Notes, n => n.Contains("failed") && n.Contains("503"));
    }

    [Fact]
    public async Task A_weak_reading_is_escalated_and_kept_as_an_alternative()
    {
        // A strict profile: nothing below 0.99 is accepted, so the grammar's 0.9 for "late 1950s" goes to the interpreter.
        var strict = new TimeSpanParser(ParseProfile.Default with { Name = "strict", AcceptThreshold = 0.99 });
        var interpreter = new ScriptedInterpreter((_, ctx) =>
        {
            Assert.Equal("late 195X", ctx.Deterministic.Canonical);
            return new InterpreterResponse("195X", 0.8, "The source means the whole decade.");
        }, maxConfidence: 0.7);
        var result = await strict.ParseAsync("late 1950s", interpreter);

        Assert.Equal("195X", result.Canonical);
        Assert.Equal(0.7, result.Confidence);
        Assert.Contains(result.Alternatives, a => a.Canonical == "late 195X" && a.Note.Contains("grammar"));
        Assert.Contains(DateFlag.Interpreted, result.Flags);
    }

    [Fact]
    public async Task The_interpreter_can_be_switched_off_per_call_and_can_set_the_intent()
    {
        var interpreter = new ScriptedInterpreter((_, _) => new InterpreterResponse("born 1606/died 1669", 0.9, Intent: Intent.Pair));
        var off = await Parser.ParseAsync("ye olde scrawle", interpreter, new ParseOptions { AllowInterpreter = false });
        Assert.Equal(ParseStatus.Unparsed, off.Status);
        Assert.Empty(interpreter.Calls);

        var on = await Parser.ParseAsync("ye olde scrawle", interpreter);
        Assert.Equal(ResultKind.BirthDeath, on.Kind);
        Assert.NotNull(on.Begin);
        Assert.NotNull(on.End);
    }

    [Fact]
    public async Task Lookup_interpreter_reads_a_table_of_reviewed_answers()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lookup-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "Ye Olde Scrawle": "1850~", "the usual date": "century 19" }""");
        try
        {
            var lookup = LookupInterpreter.Load(path);
            Assert.Equal(2, lookup.Count);
            var result = await Parser.ParseAsync("ye olde  scrawle", lookup); // case and spacing are normalised away
            Assert.Equal("1850~", result.Canonical);
            Assert.Equal("lookup", result.Strategy);
            Assert.Equal(0.95, result.Confidence);
            Assert.Contains(DateFlag.Interpreted, result.Flags);

            var miss = await Parser.ParseAsync("nothing known", lookup);
            Assert.Equal(ParseStatus.Unparsed, miss.Status);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task AlwaysInterpret_asks_even_about_a_confident_reading()
    {
        var interpreter = new ScriptedInterpreter((_, _) => new InterpreterResponse("195X", 0.9, "The whole decade."));
        var result = await Parser.ParseAsync("late 1950s", interpreter, new ParseOptions { AlwaysInterpret = true });
        Assert.Single(interpreter.Calls);
        Assert.Equal("195X", result.Canonical);
        Assert.Contains(result.Alternatives, a => a.Canonical == "late 195X");
        await Parser.ParseAsync("n.d.", interpreter, new ParseOptions { AlwaysInterpret = true });
        Assert.Single(interpreter.Calls); // undated text is still left alone
    }

    [Theory]
    [InlineData("1984", false)]
    [InlineData("n.d.", false)]
    [InlineData("ye olde scrawle", true)]
    public void NeedsInterpreter_follows_status_and_threshold(string input, bool expected)
    {
        Assert.Equal(expected, TimeSpanParser.NeedsInterpreter(Parser.Parse(input), ParseProfile.Default));
    }
}
