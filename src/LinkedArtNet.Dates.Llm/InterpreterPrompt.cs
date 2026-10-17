using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using LinkedArtNet.Dates.Interpretation;

namespace LinkedArtNet.Dates.Llm;

/// <summary>
/// The words the model is given. The system prompt is fixed for a parser version (so it caches); the user turn
/// carries the text, the profile and what the deterministic strategies made of it. Kept apart from the client so
/// the prompt can be read and tested without a network.
/// </summary>
public static class InterpreterPrompt
{
    private static readonly Lazy<string> Grammar = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LinkedArtNet.Dates.Llm.canonical-expression.md")
            ?? throw new InvalidOperationException("The canonical expression document is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>The JSON the model must answer with. Realisation is never delegated: only a canonical expression comes back.</summary>
    public static readonly string ResponseSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["canonical", "confidence", "rationale", "intent"],
          "properties": {
            "canonical": { "type": "string", "description": "A canonical date expression from the grammar, or 'undated' when the text says there is no date. Empty string when the text is not a date at all." },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1, "description": "How sure you are that the expression is what the cataloguer meant." },
            "rationale": { "type": "string", "description": "One sentence for the reviewer: what in the text led to the reading." },
            "intent": { "type": "string", "enum": ["single", "pair", "auto"], "description": "'pair' when the text gives two life or activity dates (born/died, formed/dissolved), 'single' for one span, 'auto' when unsure." }
          }
        }
        """;

    public static JsonElement ResponseSchema => JsonDocument.Parse(ResponseSchemaJson).RootElement.Clone();

    /// <summary>Fixed text: the task, the rules the parser enforces anyway, and the grammar document.</summary>
    public static string System(string parserVersion) => $"""
        You read dates as written in museum, library and archive records and express each one in a small canonical
        language. You are consulted only when a deterministic parser could not read the text, or read it weakly; your
        answer is a proposal that a human reviewer confirms before it is used, so be exact and do not over-claim.

        Rules
        - Answer with the canonical expression only, never with a realised date range: the grammar below is the whole
          vocabulary. 'century 19' means the nineteenth century and the caller's profile decides whether that is
          1800-1899 or 1801-1900; never decide that yourself.
        - Keep the meaning the cataloguer wrote. 'c.' is '~', '?' is '?', 'before' is 'before', a decade is '195X'.
          Do not sharpen a vague date into a precise one and do not widen a precise one.
        - A period name you recognise but that is not in the examples is written 'period:<name in lower case with
          hyphens>'; the caller resolves or rejects it.
        - Edition, printing, reprint and reproduction clauses date the object; when the text gives an original date
          and a later edition date, the first date is the one wanted unless the text says the edition is what is described.
        - 'n.d.', 'undated', 'no date', 's.d.' and the like are 'undated'. Text that carries no date at all gets an
          empty canonical and confidence 0.
        - When two life or activity dates appear (born/died, b./d., formed/dissolved, active from/to), say intent 'pair'
          and write the bounds with their markers: 'born 1606/died 1669', 'fl 1480/1510'.
        - Confidence 0.9 and above only when the reading is the plain sense of the words; 0.5 to 0.8 when you resolved
          an ambiguity; below 0.5 when you guessed.

        The parser is version {parserVersion}. The canonical language follows.

        {Grammar.Value}
        """;

    /// <summary>The variable part: the text and everything the parser already knows about it.</summary>
    public static string User(string input, InterpreterContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Text, exactly as written between the markers:");
        sb.AppendLine("<<<");
        sb.AppendLine(input);
        sb.AppendLine(">>>");
        sb.AppendLine();
        sb.Append("Profile: ").Append(context.Profile.Name);
        if (!string.IsNullOrWhiteSpace(context.Profile.Description)) sb.Append(" (").Append(context.Profile.Description.Trim()).Append(')');
        sb.AppendLine();
        sb.Append("Languages the records are known to use: ").AppendLine(string.Join(", ", context.Languages));
        sb.Append("Intent requested by the caller: ").AppendLine(context.Intent.ToString().ToLowerInvariant());
        var d = context.Deterministic;
        sb.Append("Deterministic parser: status ").Append(d.Status.ToString().ToLowerInvariant());
        if (d.Canonical is not null) sb.Append(", read it as '").Append(d.Canonical).Append("' with confidence ").Append(d.Confidence.ToString("0.00", CultureInfo.InvariantCulture));
        sb.AppendLine(".");
        if (d.UnconsumedText is not null) sb.Append("Text it could not use: '").Append(d.UnconsumedText).AppendLine("'.");
        foreach (var note in d.Notes.Take(6)) sb.Append("Parser note: ").AppendLine(note);
        return sb.ToString();
    }
}
