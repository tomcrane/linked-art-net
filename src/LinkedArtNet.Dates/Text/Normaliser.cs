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

        // "1750âs", "1750â€™s": a UTF-8 apostrophe read in another encoding. The source data is corrupt, not the date, so repair it.
        var repaired = MojibakeApostrophe().Replace(text, "'");
        repaired = MojibakeDash().Replace(repaired, "-");
        if (repaired != text) { text = repaired; notes.Add("Mojibake (a UTF-8 apostrophe or dash read in another encoding) repaired."); }

        text = text.Normalize(NormalizationForm.FormKC);
        text = Dashes().Replace(text, "-");
        text = text.Replace('‘', '\'').Replace('’', '\'').Replace('“', '"').Replace('”', '"');
        text = Spaces().Replace(text, " ").Trim();
        text = JesusChrist().Replace(text, "$1.$2.");   // "av. J.-C." -> "av. J.C.", so the era phrase survives tokenizing

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

        // "18690000", "18690500", "18690512": a date column's YYYYMMDD value, with zeros where the month or day is unknown.
        if (DigitBlock().Match(text) is { Success: true } block)
        {
            var month = int.Parse(block.Groups["m"].Value); var day = int.Parse(block.Groups["d"].Value);
            string? iso = (month, day) switch
            {
                (0, 0) => block.Groups["y"].Value,
                (>= 1 and <= 12, 0) => $"{block.Groups["y"].Value}-{block.Groups["m"].Value}",
                (>= 1 and <= 12, >= 1 and <= 31) => $"{block.Groups["y"].Value}-{block.Groups["m"].Value}-{block.Groups["d"].Value}",
                _ => null,
            };
            if (iso is not null) { text = iso; notes.Add("Eight digits read as a YYYYMMDD database value; a zero month or day means unspecified."); }
        }

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

    [GeneratedRegex(@"\b([Jj])\.-([Cc])\.")]
    private static partial Regex JesusChrist();

    // U+2018/U+2019 in UTF-8 are E2 80 98/99. Read as cp1252 they show as "â€˜"/"â€™"; read as Latin-1 as "â" followed by the
    // C1 controls U+0080 and U+0098/U+0099 (yearrange's data); a source that dropped the unprintable bytes shows a bare "â" before "s".
    [GeneratedRegex(@"â(?:\u0080|€)(?:\u0098|\u0099|˜|™)|(?<=\d)â(?=s\b)")]
    private static partial Regex MojibakeApostrophe();

    // U+2013/U+2014 (en and em dash) are E2 80 93/94: "â€“"/"â€”" in cp1252, "â" + U+0080 + U+0093/U+0094 in Latin-1.
    [GeneratedRegex(@"â(?:\u0080|€)(?:\u0093|\u0094|“|”)")]
    private static partial Regex MojibakeDash();

    [GeneratedRegex(@"^(?<y>\d{4})(?<m>\d{2})(?<d>\d{2})$")]
    private static partial Regex DigitBlock();
}
