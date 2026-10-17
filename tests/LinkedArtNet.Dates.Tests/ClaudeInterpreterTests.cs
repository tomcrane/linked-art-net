using LinkedArtNet.Dates.Interpretation;
using LinkedArtNet.Dates.Llm;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Tests;

/// <summary>The Claude interpreter without a network: the prompt it builds, the answers it accepts, and how it sits in the parser.</summary>
public class ClaudeInterpreterTests
{
    /// <summary>Answers from a script instead of the API, and keeps what it was asked.</summary>
    private sealed class ScriptedClaude(Func<string, string?> answer, ClaudeInterpreterOptions? options = null) : ClaudeDateInterpreter(options)
    {
        public List<(string System, string User)> Requests { get; } = [];
        protected override Task<RawAnswer?> SendAsync(string system, string user, CancellationToken ct)
        {
            Requests.Add((system, user));
            var json = answer(user);
            return Task.FromResult<RawAnswer?>(new RawAnswer(json, 1200, 40, 1100, 0, json is null ? "refusal" : "end_turn"));
        }
    }

    private static InterpreterContext Context(string input) =>
        new(ParseProfile.Default, Intent.Auto, TimeSpanParser.Default.Parse(input));

    [Fact]
    public void The_system_prompt_carries_the_grammar_and_the_rules()
    {
        var system = InterpreterPrompt.System("0.2.0");
        Assert.Contains("# Canonical date expressions", system);
        Assert.Contains("'period:' KEY", system);
        Assert.Contains("| late 1950s | `late 195X` |", system);
        Assert.Contains("never with a realised date range", system);
        Assert.Contains("version 0.2.0", system);
        Assert.DoesNotContain("1800-1899 or 1801-1900; never decide that yourself.\n\n\n", system);
    }

    [Fact]
    public void The_user_turn_carries_the_text_the_profile_and_the_parser_s_view()
    {
        var context = new InterpreterContext(ParseProfile.Default with { Name = "library", Description = "MARC records", Languages = ["en", "fr"] }, Intent.Pair,
            TimeSpanParser.Default.Parse("ye olde scrawle 1850ish"));
        var user = InterpreterPrompt.User("ye olde scrawle 1850ish", context);
        Assert.Contains("<<<\nye olde scrawle 1850ish\n>>>", user.Replace("\r\n", "\n"));
        Assert.Contains("Profile: library (MARC records)", user);
        Assert.Contains("Languages the records are known to use: en, fr", user);
        Assert.Contains("Intent requested by the caller: pair", user);
        Assert.Contains("Deterministic parser: status", user);
    }

    [Theory]
    [InlineData("""{"canonical":"1850~","confidence":0.8,"rationale":"about 1850","intent":"single"}""", "1850~", 0.8, "about 1850")]
    [InlineData("""{"canonical":" born 1606/died 1669 ","confidence":1.4,"rationale":"","intent":"pair"}""", "born 1606/died 1669", 1.0, "")]
    [InlineData("""{"canonical":"undated","confidence":0.95,"rationale":"n.d.","intent":"auto"}""", "undated", 0.95, "n.d.")]
    public void Answers_are_read_from_the_json(string json, string canonical, double confidence, string rationale)
    {
        var response = ClaudeDateInterpreter.ParseAnswer(json)!;
        Assert.Equal(canonical, response.Canonical);
        Assert.Equal(confidence, response.SelfConfidence);
        Assert.Equal(rationale, response.Rationale);
    }

    [Theory]
    [InlineData("""{"canonical":"","confidence":0,"rationale":"not a date","intent":"auto"}""")]
    [InlineData("""{"confidence":0.5}""")]
    [InlineData("not json at all")]
    [InlineData("""["1850"]""")]
    public void Empty_or_unusable_answers_are_null(string json) => Assert.Null(ClaudeDateInterpreter.ParseAnswer(json));

    [Fact]
    public void Pair_intent_comes_through()
    {
        var response = ClaudeDateInterpreter.ParseAnswer("""{"canonical":"born 1606/died 1669","confidence":0.9,"rationale":"b. and d.","intent":"pair"}""")!;
        Assert.Equal(Intent.Pair, response.Intent);
        Assert.Null(ClaudeDateInterpreter.ParseAnswer("""{"canonical":"1850","confidence":0.9,"rationale":"","intent":"auto"}""")!.Intent);
    }

    [Fact]
    public async Task In_the_parser_the_answer_is_realised_capped_and_cached()
    {
        var claude = new ScriptedClaude(_ => """{"canonical":"1850~","confidence":0.95,"rationale":"The scrawl says about 1850.","intent":"single"}""");
        var parser = new TimeSpanParser();

        var result = await parser.ParseAsync("ye olde scrawle, abt eighteen-fiftie", claude);
        Assert.Equal(ParseStatus.Parsed, result.Status);
        Assert.Equal("1850~", result.Canonical);
        Assert.Equal("claude", result.Strategy);
        Assert.Equal(0.6, result.Confidence);
        Assert.Contains(DateFlag.Interpreted, result.Flags);
        Assert.Contains(result.Notes, n => n.Contains("claude: The scrawl says about 1850."));

        await parser.ParseAsync("ye olde scrawle, abt eighteen-fiftie", claude); // same text, same profile: answered from memory
        Assert.Single(claude.Requests);
        Assert.Contains("<<<", claude.Requests[0].User);
        Assert.Equal(1, claude.Usage.Calls);
        Assert.Equal(1200, claude.Usage.InputTokens);
        Assert.Equal(1100, claude.Usage.CacheReadTokens);

        Assert.Equal(ParseStatus.Parsed, parser.Parse("late 1950s").Status); // and the deterministic path is untouched
    }

    [Fact]
    public async Task A_refusal_or_a_non_date_leaves_the_deterministic_result()
    {
        var refused = new ScriptedClaude(_ => null);
        var result = await new TimeSpanParser().ParseAsync("ye olde scrawle", refused);
        Assert.Equal(ParseStatus.Unparsed, result.Status);
        Assert.Contains(result.Notes, n => n.Contains("had no reading"));

        var notADate = new ScriptedClaude(_ => """{"canonical":"","confidence":0,"rationale":"a shelf mark","intent":"auto"}""");
        var still = await new TimeSpanParser().ParseAsync("ye olde shelf mark", notADate);
        Assert.Equal(ParseStatus.Unparsed, still.Status);
    }

    [Fact]
    public void Options_set_the_cap_and_the_model()
    {
        var claude = new ScriptedClaude(_ => null, new ClaudeInterpreterOptions { Model = "claude-sonnet-5-5", MaxConfidence = 0.5 });
        Assert.Equal("claude-sonnet-5-5", claude.Model);
        Assert.Equal(0.5, claude.MaxConfidence);
        Assert.Equal("claude-opus-5-5", new ClaudeInterpreterOptions().Model);
    }
}
