using System.Reflection;
using System.Text.Json;

namespace LinkedArtNet.Alignment;

/// <summary>
/// The forms that make two writings of one name compare equal (Tables/name-forms.json): conventional abbreviations
/// of given names, a plural courtesy prefix for firms, and legal-form suffixes. Used on normalised keys when indexing
/// and comparing, never on what is published.
/// </summary>
public static class NameForms
{
    private static readonly Lazy<(Dictionary<string, string> Abbreviations, HashSet<string> FirmPrefixes, List<string[]> LegalSuffixes, HashSet<string> Connectives)> Tables = new(Load);

    public static IReadOnlyDictionary<string, string> Abbreviations => Tables.Value.Abbreviations;
    public static IReadOnlySet<string> FirmPrefixes => Tables.Value.FirmPrefixes;

    /// <summary>Whether a normalised key ends with one of the legal-form suffixes ("inc", "ltd", "limited", "& co").</summary>
    public static bool HasLegalSuffix(string normalised)
    {
        var tokens = StringKey.Bare(StringKey.WithoutQualifiers(normalised)).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > 1 && Tables.Value.LegalSuffixes.Any(suffix => tokens.Length > suffix.Length && tokens.Skip(tokens.Length - suffix.Length).SequenceEqual(suffix, StringComparer.Ordinal));
    }

    /// <summary>
    /// The comparison form of a normalised key: parenthesised qualifiers ("(New York, N.Y.)", "(Firm)") and punctuation dropped, a leading firm prefix removed, abbreviated given
    /// names expanded, legal suffixes at the end removed, and the connective an ampersand stands for dropped so that
    /// "&amp;" and "and" compare equal. "thos. agnew &amp; sons ltd", "thomas agnew &amp; sons" and "thomas agnew and sons" all
    /// give "thomas agnew sons".
    /// </summary>
    public static string Canonical(string normalised, bool dropQualifiers = true, bool expandAbbreviations = true)
    {
        var tokens = StringKey.Bare(dropQualifiers ? StringKey.WithoutQualifiers(normalised) : normalised).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Count == 0) return "";
        var (abbreviations, prefixes, suffixes, connectives) = Tables.Value;
        if (tokens.Count > 1 && prefixes.Contains(tokens[0])) tokens.RemoveAt(0);
        if (tokens.Count > 1) tokens.RemoveAll(connectives.Contains);
        if (tokens.Count == 0) return "";
        var trimmed = true;
        while (trimmed && tokens.Count > 1)
        {
            trimmed = false;
            foreach (var suffix in suffixes)
                if (tokens.Count > suffix.Length && tokens.Skip(tokens.Count - suffix.Length).SequenceEqual(suffix, StringComparer.Ordinal))
                { tokens.RemoveRange(tokens.Count - suffix.Length, suffix.Length); trimmed = true; break; }
        }
        if (expandAbbreviations)
            for (var i = 0; i < tokens.Count; i++)
                if (abbreviations.TryGetValue(tokens[i], out var full)) tokens[i] = full;
        return string.Join(' ', tokens);
    }

    private static (Dictionary<string, string>, HashSet<string>, List<string[]>, HashSet<string>) Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LinkedArtNet.Alignment.Tables.name-forms.json") ?? throw new InvalidOperationException("name-forms.json is not embedded.");
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;
        var abbreviations = root.GetProperty("abbreviations").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
        var prefixes = root.GetProperty("firmPrefixes").EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        var suffixes = root.GetProperty("legalSuffixes").EnumerateArray().Select(e => e.GetString()!.Split(' ', StringSplitOptions.RemoveEmptyEntries)).OrderByDescending(s => s.Length).ToList();
        var connectives = root.GetProperty("connectives").EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        return (abbreviations, prefixes, suffixes, connectives);
    }
}
