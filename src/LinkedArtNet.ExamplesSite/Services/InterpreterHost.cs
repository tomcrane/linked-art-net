using LinkedArtNet.Dates.Interpretation;
using LinkedArtNet.Dates.Llm;
using Microsoft.Extensions.Options;

namespace LinkedArtNet.ExamplesSite.Services;

/// <summary>Configuration section <c>Interpreter</c>. The trainer offers Claude's proposals only when a key is present.</summary>
public sealed class InterpreterOptions
{
    /// <summary>An Anthropic API key from the Console organisation that holds the credits; the ANTHROPIC_API_KEY environment variable is used when empty.</summary>
    public string? ApiKey { get; set; }
    public string Model { get; set; } = new ClaudeInterpreterOptions().Model;
    public string Effort { get; set; } = "low";
    /// <summary>Confidence cap for interpreted readings; they are proposals for a reviewer, so it stays below the accept threshold.</summary>
    public double MaxConfidence { get; set; } = 0.6;
    /// <summary>Items one bulk request on the trainer's index page sends, so a click answers within a request timeout.</summary>
    public int BatchSize { get; set; } = 25;
}

/// <summary>
/// The site's one interpreter, if any. The public playground and the dates service never call it: by the owner's
/// decision the LLM is a corpus-growing assistant, so it is reached only from the trainer, where a signed-in
/// reviewer asks for a proposal and confirms or corrects it before anything enters the corpus.
/// </summary>
public sealed class InterpreterHost
{
    public InterpreterHost(IOptions<InterpreterOptions> options)
    {
        var o = options.Value;
        var key = string.IsNullOrWhiteSpace(o.ApiKey) ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") : o.ApiKey;
        BatchSize = Math.Clamp(o.BatchSize, 1, 200);
        if (string.IsNullOrWhiteSpace(key)) return;
        Interpreter = new ClaudeDateInterpreter(new ClaudeInterpreterOptions { ApiKey = key, Model = o.Model, Effort = o.Effort, MaxConfidence = o.MaxConfidence });
        Model = o.Model;
    }

    /// <summary>For tests and other hosts: any interpreter, under any name.</summary>
    public InterpreterHost(IDateInterpreter interpreter, string model, int batchSize = 25)
    {
        Interpreter = interpreter;
        Model = model;
        BatchSize = batchSize;
    }

    public IDateInterpreter? Interpreter { get; }
    public string? Model { get; }
    public int BatchSize { get; }
    public bool Available => Interpreter is not null;
}
