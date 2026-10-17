using System.Text;
using System.Text.RegularExpressions;

namespace LinkedArtNet.Alignment;

/// <summary>
/// The normalised form that keys source strings, overrides and label matches: compatibility-decomposed Unicode,
/// one kind of apostrophe and dash, whitespace collapsed, case folded, outer brackets' noise and trailing
/// punctuation removed. Two strings with the same key are the same string for the store; nothing more is claimed.
/// </summary>
public static partial class StringKey
{
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"[‘’‚‛′´`]")] private static partial Regex Apostrophes();
    [GeneratedRegex(@"[‐-―−]")] private static partial Regex Dashes();
    [GeneratedRegex(@"[\s.,;:]+$")] private static partial Regex TrailingPunctuation();

    public static string Normalise(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Normalize(NormalizationForm.FormKC);
        s = Apostrophes().Replace(s, "'");
        s = Dashes().Replace(s, "-");
        s = Spaces().Replace(s, " ").Trim();
        s = TrailingPunctuation().Replace(s, "");
        return s.ToLowerInvariant();
    }
}
