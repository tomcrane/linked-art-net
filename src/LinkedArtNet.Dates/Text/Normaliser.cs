using System.Text;
using System.Text.RegularExpressions;

namespace LinkedArtNet.Dates.Text;

/// <summary>What the normaliser produced and what it stripped, so the result can explain itself.</summary>
public sealed record NormalisedText(string Text, IReadOnlySet<DateFlag> Flags, IReadOnlyList<string> Notes);

/// <summary>
/// Pure string clean-up before tokenizing: Unicode compatibility forms, dash and quote variants, whitespace,
/// wrapping brackets (the archival "inferred" convention) and parentheses, trailing punctuation and MARC noise.
/// Hedges (<c>c.</c>, <c>?</c>) are kept: the tokenizer needs them. The original string is never changed; it is the label.
/// </summary>
public static partial class Normaliser
{
    public const int MaxLength = 500;

    public static NormalisedText Normalise(string? input)
    {
        var flags = new HashSet<DateFlag>();
        var notes = new List<string>();
        var text = input ?? "";
        if (text.Length > MaxLength)
        {
            text = text[..MaxLength];
            notes.Add($"Input longer than {MaxLength} characters was truncated.");
        }

        text = text.Normalize(NormalizationForm.FormKC);
        text = Dashes().Replace(text, "-");
        text = text.Replace('‘', '\'').Replace('’', '\'').Replace('“', '"').Replace('”', '"');
        text = Spaces().Replace(text, " ").Trim();

        // "[1847-1852]" and "(1989?)": the wrapper says something about the whole and is not part of the date.
        var stripped = true;
        while (stripped && text.Length >= 2)
        {
            stripped = false;
            if (WrappedBy(text, '[', ']')) { text = text[1..^1].Trim(); flags.Add(DateFlag.Bracketed); stripped = true; }
            else if (WrappedBy(text, '(', ')')) { text = text[1..^1].Trim(); flags.Add(DateFlag.Parenthesised); stripped = true; }
        }
        if (flags.Contains(DateFlag.Bracketed)) notes.Add("Square brackets around the whole date mark it as inferred by the cataloguer.");

        var before = text;
        text = Sic().Replace(text, " ");
        text = Spaces().Replace(text, " ").Trim();
        text = TrailingPunctuation().Replace(text, "");
        text = LeadingPunctuation().Replace(text, "");
        text = text.Trim();
        if (text != before && before.Length - text.Length > 1) notes.Add("Trailing punctuation or '[sic]' removed.");

        return new NormalisedText(text, flags, notes);
    }

    private static bool WrappedBy(string text, char open, char close)
    {
        if (text[0] != open || text[^1] != close) return false;
        // The closing bracket must match the opening one, not an inner pair: "[1500] - [1520]" is not wrapped.
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == open) depth++;
            else if (text[i] == close) { depth--; if (depth == 0 && i < text.Length - 1) return false; }
        }
        return depth == 0;
    }

    [GeneratedRegex("[‐‑‒–—―−⁃﹘﹣－\u0096\u0097]")]
    private static partial Regex Dashes();

    [GeneratedRegex(@"[\s   ]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\[\s*sic\s*\]|\(\s*sic\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex Sic();

    // A full stop after a digit or bracket ends the field; after a letter it may be an abbreviation ("19th c."), so it stays.
    [GeneratedRegex(@"(?<=[\d\)\]])[.,;:]+$|[,;:]+$")]
    private static partial Regex TrailingPunctuation();

    [GeneratedRegex(@"^[,;:.]+")]
    private static partial Regex LeadingPunctuation();
}
