using System.Globalization;
using System.Text.RegularExpressions;

namespace LinkedArtNet.Dates.Text;

public enum TokenKind
{
    Number, Ordinal, Roman, Word, Month, Season, CenturyWord, MillenniumWord, Band, Hedge, Era, Relation, OpenEnd,
    RangeWord, Between, Or, Floruit, Lifecycle, Undated, Magnitude, Ago, Bp, Correction, Noise, Unspecified, DecadeSuffix,
    Dash, Slash, Comma, Question, OpenParen, CloseParen, OpenBracket, CloseBracket, Other,
}

/// <summary>
/// One token of the normalised text. <see cref="Value"/> holds the typed meaning: a <see cref="long"/> for numbers
/// and roman numerals (years, ordinals), an <see cref="int"/> month, a <see cref="Season"/>, a <see cref="Band"/>,
/// a <see cref="Qualification"/>, "Bce"/"Ce"/"Bp" for eras, a <see cref="Relation"/>, a <see cref="LifecycleMarker"/>,
/// a <see cref="Magnitude"/>, or the X-pattern of an unspecified year.
/// </summary>
public sealed record Token(TokenKind Kind, string Text, int Start, object? Value = null)
{
    /// <summary>The <see cref="Value"/> of a <see cref="TokenKind.Noise"/> token that names an edition, printing or reproduction.</summary>
    public const string EditionMarker = "edition";

    public int End => Start + Text.Length;
    public long Number => Value is long n ? n : throw new InvalidOperationException($"{Kind} '{Text}' is not a number.");
    /// <summary>Digits in a numeric token, for the "is this a year" rules.</summary>
    public int Digits => Text.Count(char.IsAsciiDigit);
    public bool HasDecimal => Kind == TokenKind.Number && Text.Contains('.');
    public override string ToString() => $"{Kind}({Text})";
}

/// <summary>
/// Splits normalised text into typed tokens using a <see cref="Lexicon"/>. Numbers keep their ordinal and decade
/// suffixes as separate tokens; abbreviation dots are folded into the word for lookup; multi-word phrases from the
/// lexicon are joined longest-first.
/// </summary>
public sealed partial class Tokenizer(Lexicon lexicon)
{
    public Lexicon Lexicon { get; } = lexicon;

    public static Tokenizer English { get; } = new(Lexicon.English);

    public IReadOnlyList<Token> Tokenize(string text)
    {
        var raw = Scan(text);
        var tokens = Classify(raw);
        // "12 de marzo de 1850", "le 12 mars 1850": a filler word between the parts of a date carries nothing.
        for (var i = tokens.Count - 2; i >= 1; i--)
        {
            if (tokens[i].Kind == TokenKind.Noise && tokens[i - 1].Kind is TokenKind.Number or TokenKind.Month && tokens[i + 1].Kind is TokenKind.Number or TokenKind.Month
                && (tokens[i - 1].Kind == TokenKind.Month || tokens[i + 1].Kind == TokenKind.Month))
                tokens.RemoveAt(i);
        }
        // "1500 s", "1720 s late": a lone "s" after a number is a decade suffix that lost its apostrophe or its spacing.
        for (var i = 1; i < tokens.Count; i++)
        {
            if (tokens[i] is { Kind: TokenKind.Word } s && s.Text.TrimEnd('.').Equals("s", StringComparison.OrdinalIgnoreCase)
                && tokens[i - 1] is { Kind: TokenKind.Number } n && n.Digits >= 3 && !n.HasDecimal)
                tokens[i] = new Token(TokenKind.DecadeSuffix, s.Text, s.Start);
        }
        // "twenty-first": a hyphenated ordinal word the lexicon knows as one key.
        for (var i = tokens.Count - 3; i >= 0; i--)
        {
            if (tokens[i].Kind == TokenKind.Word && tokens[i + 1].Kind == TokenKind.Dash && tokens[i + 2].Kind is TokenKind.Word or TokenKind.Ordinal
                && Lexicon.OrdinalWords.TryGetValue(Lexicon.Normalise(tokens[i].Text) + "-" + Lexicon.Normalise(tokens[i + 2].Text), out var ordinal))
            {
                tokens[i] = new Token(TokenKind.Ordinal, text[tokens[i].Start..tokens[i + 2].End], tokens[i].Start, (long)ordinal);
                tokens.RemoveRange(i + 1, 2);
            }
        }
        return tokens;
    }

    // ---- pass 1: lexical shapes -------------------------------------------------------------------------------

    private static List<Token> Scan(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            var m = NumberPattern().Match(text, i);
            if (m.Success && m.Index == i && m.Groups["num"].Value.Count(char.IsAsciiDigit) > 15)
            {
                tokens.Add(new Token(TokenKind.Other, m.Value, i));
                i += m.Length;
                continue;
            }
            if (m.Success && m.Index == i)
            {
                var digits = m.Groups["num"].Value.Replace(",", "");
                if (m.Groups["unspec"].Success)
                {
                    var pattern = digits + new string('X', m.Groups["unspec"].Length);
                    tokens.Add(new Token(TokenKind.Unspecified, m.Value, i, pattern));
                }
                else
                {
                    var value = digits.Contains('.') ? (object)decimal.Parse(digits, CultureInfo.InvariantCulture) : long.Parse(digits, CultureInfo.InvariantCulture);
                    tokens.Add(new Token(m.Groups["ord"].Success ? TokenKind.Ordinal : TokenKind.Number, m.Groups["num"].Value + m.Groups["ord"].Value, i, value is decimal d ? (object)d : (long)value));
                    if (m.Groups["decade"].Success)
                        tokens.Add(new Token(TokenKind.DecadeSuffix, m.Groups["decade"].Value, i + m.Groups["decade"].Index - m.Index));
                }
                i += m.Length;
                continue;
            }

            m = WordPattern().Match(text, i);
            if (m.Success && m.Index == i)
            {
                tokens.Add(new Token(TokenKind.Word, m.Value, i));
                i += m.Length;
                continue;
            }

            var kind = c switch
            {
                '-' or '・' or '·' => TokenKind.Dash, '/' => TokenKind.Slash, ',' => TokenKind.Comma, '?' => TokenKind.Question,
                '(' => TokenKind.OpenParen, ')' => TokenKind.CloseParen, '[' => TokenKind.OpenBracket, ']' => TokenKind.CloseBracket,
                _ => TokenKind.Other,
            };
            tokens.Add(new Token(kind, c.ToString(), i));
            i++;
        }
        return tokens;
    }

    // ---- pass 2: lexicon lookup, multi-word phrases ------------------------------------------------------------

    private List<Token> Classify(List<Token> raw)
    {
        var result = new List<Token>();
        var i = 0;
        while (i < raw.Count)
        {
            var t = raw[i];
            if (t.Kind is not (TokenKind.Word or TokenKind.Other or TokenKind.Ordinal or TokenKind.Number))
            {
                result.Add(t);
                i++;
                continue;
            }

            // "12. März", "19. Jahrhundert", "2. Hälfte": the ordinal dot after a number adds nothing once the word follows.
            if (t.Kind == TokenKind.Number && i + 2 < raw.Count && raw[i + 1] is { Kind: TokenKind.Other, Text: "." } && raw[i + 2].Kind == TokenKind.Word)
            {
                raw.RemoveAt(i + 1);
            }

            // "c.late", "n.d.1850": a dotted word the lexicon does not know is several words.
            if (t.Kind == TokenKind.Word && t.Text.TrimEnd('.').Contains('.') && Lookup(Lexicon.Normalise(t.Text), t.Start, t.Text, t) is null)
            {
                var parts = new List<Token>();
                var offset = 0;
                foreach (var part in t.Text.Split('.', StringSplitOptions.RemoveEmptyEntries))
                {
                    var at = t.Text.IndexOf(part, offset, StringComparison.Ordinal);
                    var withDot = at + part.Length < t.Text.Length && t.Text[at + part.Length] == '.' ? part + "." : part;
                    parts.Add(new Token(TokenKind.Word, withDot, t.Start + at));
                    offset = at + part.Length;
                }
                raw.RemoveAt(i);
                raw.InsertRange(i, parts);
                continue;
            }

            // Longest phrase first: "no later than", "second half of", "years ago".
            var matched = false;
            for (var len = Math.Min(Lexicon.MaxPhraseWords, raw.Count - i); len >= 1; len--)
            {
                if (raw.Skip(i).Take(len).Any(x => x.Kind is TokenKind.Dash or TokenKind.Slash or TokenKind.Comma or TokenKind.OpenParen or TokenKind.CloseParen or TokenKind.OpenBracket or TokenKind.CloseBracket or TokenKind.Question or TokenKind.DecadeSuffix or TokenKind.Unspecified)) continue;
                var phrase = string.Join(" ", raw.Skip(i).Take(len).Select(x => Lexicon.Normalise(x.Text)));
                if (phrase.Length == 0) continue;
                var classified = Lookup(phrase, raw[i].Start, string.Join(" ", raw.Skip(i).Take(len).Select(x => x.Text)), len == 1 ? raw[i] : null);
                if (classified is null) continue;
                result.Add(classified);
                i += len;
                matched = true;
                break;
            }
            if (matched) continue;

            if (t.Kind == TokenKind.Word)
            {
                var word = t.Text.TrimEnd('.');
                if (TryRoman(word, out var roman, out var ordinal))
                    result.Add(new Token(ordinal ? TokenKind.Ordinal : TokenKind.Roman, t.Text, t.Start, roman));
                else if (t.Text.Length == 1 && t.Text[0] is '*' or '+')
                    result.Add(t with { Kind = TokenKind.Lifecycle, Value = LifecycleMarker.Born });
                else if (IsCenturyTypo(word))
                    result.Add(new Token(TokenKind.CenturyWord, t.Text, t.Start));
                else if (OtherLanguageMonth(word) is { } month)
                    result.Add(new Token(TokenKind.Month, t.Text, t.Start, month));
                else
                    result.Add(t);
            }
            else result.Add(t);
            i++;
        }
        return result;
    }

    private Token? Lookup(string key, int start, string text, Token? single)
    {
        // Numeric tokens take part only in phrases such as "1st half"; alone they keep their shape.
        if (single is { Kind: TokenKind.Number or TokenKind.Ordinal }) return null;
        if (Lexicon.Months.TryGetValue(key, out var month)) return new Token(TokenKind.Month, text, start, month);
        if (Lexicon.Seasons.TryGetValue(key, out var season)) return new Token(TokenKind.Season, text, start, season);
        if (Lexicon.Bands.TryGetValue(key, out var band)) return new Token(TokenKind.Band, text, start, band);
        if (Lexicon.Lifecycle.TryGetValue(key, out var marker) && key != "bp") return new Token(TokenKind.Lifecycle, text, start, marker);
        if (Lexicon.Floruit.Contains(key)) return new Token(TokenKind.Floruit, text, start);
        if (Lexicon.Undated.Contains(key)) return new Token(TokenKind.Undated, text, start);
        if (Lexicon.CenturyWords.Contains(key) && key is not ("c" or "s")) return new Token(TokenKind.CenturyWord, text, start);
        if (Lexicon.MillenniumWords.Contains(key)) return new Token(TokenKind.MillenniumWord, text, start);
        if (Lexicon.Hedges.TryGetValue(key, out var hedge)) return new Token(TokenKind.Hedge, text, start, hedge);
        if (Lexicon.Eras.TryGetValue(key, out var era)) return era == "Bp" ? new Token(TokenKind.Bp, text, start) : new Token(TokenKind.Era, text, start, era == "Bce" ? Era.Bce : Era.Ce);
        if (Lexicon.Between.Contains(key)) return new Token(TokenKind.Between, text, start);
        if (Lexicon.Or.Contains(key)) return new Token(TokenKind.Or, text, start);
        if (Lexicon.Ago.Contains(key)) return new Token(TokenKind.Ago, text, start);
        if (Lexicon.Bp.Contains(key)) return new Token(TokenKind.Bp, text, start);
        if (Lexicon.Magnitudes.TryGetValue(key, out var magnitude)) return new Token(TokenKind.Magnitude, text, start, magnitude);
        if (Lexicon.OpenEnd.Contains(key)) return new Token(TokenKind.OpenEnd, text, start);
        if (Lexicon.RangeWords.Contains(key)) return new Token(TokenKind.RangeWord, text, start);
        if (Lexicon.Relations.TryGetValue(key, out var relation)) return new Token(TokenKind.Relation, text, start, relation);
        if (Lexicon.Corrections.Contains(key)) return new Token(TokenKind.Correction, text, start);
        if (Lexicon.OrdinalWords.TryGetValue(key, out var ordinalWord)) return new Token(TokenKind.Ordinal, text, start, (long)ordinalWord);
        if (Lexicon.Edition.Contains(key)) return new Token(TokenKind.Noise, text, start, Token.EditionMarker);
        if (Lexicon.Noise.Contains(key)) return new Token(TokenKind.Noise, text, start);
        return null;
    }

    private static readonly Lazy<IReadOnlyDictionary<string, int>> OtherMonths = new(() =>
    {
        var months = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var code in new[] { "fr", "de", "it", "es", "nl", "la" })
            foreach (var (key, value) in Lexicon.Load(code).Months) months.TryAdd(key, value);
        return months;
    });

    /// <summary>"août 4 1776" under an English profile: a month name from another language the active lexicon does not know still reads as a month.</summary>
    private static int? OtherLanguageMonth(string word) => OtherMonths.Value.TryGetValue(Lexicon.Normalise(word), out var month) ? month : null;

    /// <summary>"centurry", "centruy", "centuly": one letter added, dropped, changed or swapped in "century" still means the century.</summary>
    private static bool IsCenturyTypo(string word)
    {
        var w = word.ToLowerInvariant();
        if (w.Length is < 6 or > 8 || w == "century") return false;
        const string target = "century";
        if (w.Length == target.Length)
        {
            var diffs = new List<int>();
            for (var i = 0; i < w.Length; i++) if (w[i] != target[i]) diffs.Add(i);
            return diffs.Count == 1 || (diffs.Count == 2 && diffs[1] == diffs[0] + 1 && w[diffs[0]] == target[diffs[1]] && w[diffs[1]] == target[diffs[0]]);
        }
        var (longer, shorter) = w.Length > target.Length ? (w, target) : (target, w);
        for (var i = 0; i < longer.Length; i++)
            if (string.Concat(longer.AsSpan(0, i), longer.AsSpan(i + 1)) == shorter) return true;
        return false;
    }

    /// <summary>"MDCCLXXV" -> 1775; "XIXth" / "XIXe" -> 19 as an ordinal. Single letters other than C, V and X are not numerals.</summary>
    public static bool TryRoman(string word, out long value, out bool ordinal)
    {
        value = 0; ordinal = false;
        var m = RomanPattern().Match(word);
        if (!m.Success || m.Groups["r"].Length == 0) return false;
        var r = m.Groups["r"].Value.ToUpperInvariant();
        if (r.Length == 1 && r is not ("C" or "V" or "X")) return false;
        if (word.Length == 1 && word == "c") return false; // "c." is circa, handled by the lexicon
        ordinal = m.Groups["suffix"].Length > 0;
        var total = 0L; var prev = 0L;
        foreach (var ch in r.Reverse())
        {
            var v = ch switch { 'I' => 1, 'V' => 5, 'X' => 10, 'L' => 50, 'C' => 100, 'D' => 500, 'M' => 1000, _ => 0 };
            if (v == 0) return false;
            total += v < prev ? -v : v;
            prev = v;
        }
        value = total;
        return total > 0;
    }

    // 10,000 | 4.5 | 1950 | 14th | 1950s / 1950's | 17_ / 18-- / 19XX / 19?? (unspecified digits)
    // "19thC" keeps its ordinal before a century letter; "187-" at the end of a MARC field is the decade 187X.
    [GeneratedRegex(@"(?<num>\d{1,3}(?:,\d{3})+|\d+(?:\.\d+)?)(?:(?<ord>st|nd|rd|th|e|º|°)(?![A-Za-z])|(?<ord>st|nd|rd|th)(?=[Cc](?![A-Za-z]))|(?<decade>'?s)(?![A-Za-z])|(?<unspec>_{1,3}|--|xx|XX|[xX](?![A-Za-z0-9])|\?\?|(?<=(?<!\d)\d{3})-(?=\s*[\]\)]*\s*$)))?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"[\p{L}][\p{L}'’]*(?:\.[\p{L}]+)*\.?|[*†©~+&]", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"^(?<r>M{0,4}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3}))(?<suffix>th|st|nd|rd|e|è|ème|º|°)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RomanPattern();
}
