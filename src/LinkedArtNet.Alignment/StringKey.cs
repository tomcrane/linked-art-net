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

    /// <summary>
    /// A normalised key with its diacritics removed ("d\u00fcrer" becomes "durer") and a few letters without a plain
    /// decomposition spelled out (\u00df to ss, \u00f8 to o, \u0142 to l). The store indexes this as a second lookup key
    /// beside the normalised one, so accents present on one side and absent on the other do not decide a match.
    /// </summary>
    public static string Fold(string normalised)
    {
        if (normalised.Length == 0) return normalised;
        var decomposed = normalised.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            switch (c)
            {
                case '\u00df': sb.Append("ss"); break;
                case '\u00e6': sb.Append("ae"); break;
                case '\u0153': sb.Append("oe"); break;
                case '\u00f8': sb.Append('o'); break;
                case '\u0142': sb.Append('l'); break;
                case '\u0111': case '\u00f0': sb.Append('d'); break;
                case '\u00fe': sb.Append("th"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
