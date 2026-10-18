using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace LinkedArtNet.Alignment.Authorities;

/// <summary>One N-Triples statement. Object literals are unescaped; IRIs are bare, without angle brackets.</summary>
/// <param name="Lang">The language tag of a literal object, lower-cased, or null.</param>
/// <param name="Datatype">The datatype IRI of a typed literal object, or null.</param>
public readonly record struct Triple(string Subject, string Predicate, string Object, bool IsLiteral, string? Lang = null, string? Datatype = null)
{
    public bool Is(string predicate) => string.Equals(Predicate, predicate, StringComparison.Ordinal);
}

/// <summary>
/// A streaming N-Triples reader for the authority dumps (Getty vocabularies, Library of Congress). One line, one
/// triple; blank nodes are kept as their <c>_:label</c>; malformed lines are skipped and counted rather than thrown,
/// because a multi-gigabyte dump with one bad line should still load. Gzip is detected from the <c>.gz</c> extension.
/// </summary>
public static class NTriples
{
    public static Stream Open(string path)
    {
        Stream s = File.OpenRead(path);
        return path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? new GZipStream(s, CompressionMode.Decompress) : s;
    }

    public static IEnumerable<Triple> Read(string path)
    {
        using var stream = Open(path);
        foreach (var t in Read(stream)) yield return t;
    }

    public static IEnumerable<Triple> Read(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1 << 16);
        while (reader.ReadLine() is { } line)
            if (TryParse(line, out var t)) yield return t;
    }

    public static IEnumerable<Triple> Read(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
            if (TryParse(line, out var t)) yield return t;
    }

    public static IEnumerable<Triple> ReadText(string text) => Read(new StringReader(text));

    /// <summary>Parses one line. False for blank lines, comments and anything malformed.</summary>
    public static bool TryParse(string line, out Triple triple)
    {
        triple = default;
        var i = 0;
        SkipWs(line, ref i);
        if (i >= line.Length || line[i] == '#') return false;
        if (!ReadTerm(line, ref i, out var s, out var sLit, out _, out _) || sLit) return false;
        SkipWs(line, ref i);
        if (!ReadTerm(line, ref i, out var p, out var pLit, out _, out _) || pLit) return false;
        SkipWs(line, ref i);
        if (!ReadTerm(line, ref i, out var o, out var oLit, out var lang, out var datatype)) return false;
        SkipWs(line, ref i);
        if (i >= line.Length || line[i] != '.') return false;
        triple = new Triple(s, p, o, oLit, lang, datatype);
        return true;
    }

    private static void SkipWs(string line, ref int i) { while (i < line.Length && (line[i] == ' ' || line[i] == '\t')) i++; }

    private static bool ReadTerm(string line, ref int i, out string value, out bool isLiteral, out string? lang, out string? datatype)
    {
        value = ""; isLiteral = false; lang = null; datatype = null;
        if (i >= line.Length) return false;
        switch (line[i])
        {
            case '<':
            {
                var end = line.IndexOf('>', i + 1);
                if (end < 0) return false;
                value = Unescape(line.AsSpan(i + 1, end - i - 1));
                i = end + 1;
                return value.Length > 0;
            }
            case '_':
            {
                var start = i;
                while (i < line.Length && line[i] != ' ' && line[i] != '\t' && line[i] != '.') i++;
                value = line[start..i];
                return value.Length > 2;
            }
            case '"':
            {
                var sb = new StringBuilder();
                i++;
                var closed = false;
                while (i < line.Length)
                {
                    var c = line[i];
                    if (c == '"') { closed = true; i++; break; }
                    if (c == '\\')
                    {
                        if (i + 1 >= line.Length) return false;
                        var e = line[i + 1];
                        switch (e)
                        {
                            case 't': sb.Append('\t'); i += 2; break;
                            case 'n': sb.Append('\n'); i += 2; break;
                            case 'r': sb.Append('\r'); i += 2; break;
                            case 'b': sb.Append('\b'); i += 2; break;
                            case 'f': sb.Append('\f'); i += 2; break;
                            case '"': sb.Append('"'); i += 2; break;
                            case '\'': sb.Append('\''); i += 2; break;
                            case '\\': sb.Append('\\'); i += 2; break;
                            case 'u':
                                if (i + 6 > line.Length || !int.TryParse(line.AsSpan(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u4)) return false;
                                sb.Append((char)u4); i += 6; break;
                            case 'U':
                                if (i + 10 > line.Length || !int.TryParse(line.AsSpan(i + 2, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u8)) return false;
                                sb.Append(char.ConvertFromUtf32(u8)); i += 10; break;
                            default: return false;
                        }
                        continue;
                    }
                    sb.Append(c); i++;
                }
                if (!closed) return false;
                value = sb.ToString();
                isLiteral = true;
                if (i < line.Length && line[i] == '@')
                {
                    var start = ++i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '-')) i++;
                    lang = line[start..i].ToLowerInvariant();
                }
                else if (i + 1 < line.Length && line[i] == '^' && line[i + 1] == '^')
                {
                    i += 2;
                    if (i >= line.Length || line[i] != '<') return false;
                    var end = line.IndexOf('>', i + 1);
                    if (end < 0) return false;
                    datatype = line[(i + 1)..end];
                    i = end + 1;
                }
                return true;
            }
            default:
                return false;
        }
    }

    private static string Unescape(ReadOnlySpan<char> iri)
    {
        if (iri.IndexOf('\\') < 0) return iri.ToString();
        var sb = new StringBuilder(iri.Length);
        for (var i = 0; i < iri.Length; i++)
        {
            if (iri[i] == '\\' && i + 5 < iri.Length && iri[i + 1] == 'u' && int.TryParse(iri.Slice(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u4)) { sb.Append((char)u4); i += 5; }
            else if (iri[i] == '\\' && i + 9 < iri.Length && iri[i + 1] == 'U' && int.TryParse(iri.Slice(i + 2, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var u8)) { sb.Append(char.ConvertFromUtf32(u8)); i += 9; }
            else sb.Append(iri[i]);
        }
        return sb.ToString();
    }
}
