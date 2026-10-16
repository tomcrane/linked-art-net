using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LinkedArtNet.Serialization;

namespace LinkedArtNet.Dates;

/// <summary>
/// One parsed input as the dates service reports it over HTTP, and as the examples site reports it when it parses
/// in-process, so a reader sees one shape wherever the result came from. TimeSpans are Linked Art JSON exactly as
/// the library writes it.
/// </summary>
public sealed record TimeSpanResultDto(
    string Input,
    string? Ref,
    string Status,
    string Kind,
    double Confidence,
    string? Canonical,
    string? Edtf,
    IReadOnlyList<string> Flags,
    string Strategy,
    [property: JsonPropertyName("timespan")] JsonNode? TimeSpan,
    JsonNode? Begin,
    JsonNode? End,
    IReadOnlyList<AlternativeDto> Alternatives,
    IReadOnlyList<string> Notes,
    string? Unconsumed,
    string Profile,
    string ParserVersion)
{
    public static TimeSpanResultDto From(TimeSpanParseResult r, string? reference) => new(
        r.Input, reference, Lower(r.Status.ToString()), Camel(r.Kind.ToString()), Math.Round(r.Confidence, 3), r.Canonical, r.Edtf,
        r.Flags.Select(f => f.ToString()).Order(StringComparer.Ordinal).ToList(), r.Strategy,
        Json(r.TimeSpan), Json(r.Begin), Json(r.End),
        r.Alternatives.Select(a => new AlternativeDto(a.Canonical, Math.Round(a.Confidence, 3), a.Note, Json(a.TimeSpan))).ToList(),
        r.Notes, r.UnconsumedText, r.Profile, r.ParserVersion);

    /// <summary><c>shape=timespan</c>: the TimeSpan alone, or the spans a pair produced.</summary>
    public JsonNode? SpansOnly()
    {
        if (Begin is null && End is null) return TimeSpan;
        var o = new JsonObject();
        if (TimeSpan is not null) o["timespan"] = TimeSpan.DeepClone();
        if (Begin is not null) o["begin"] = Begin.DeepClone();
        if (End is not null) o["end"] = End.DeepClone();
        return o;
    }

    public static JsonNode? Json(LinkedArtTimeSpan? ts) => ts is null ? null : JsonNode.Parse(LinkedArtJson.Serialize(ts));

    public static string Lower(string s) => s.ToLowerInvariant();

    public static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

public sealed record AlternativeDto(string Canonical, double Confidence, string Note, [property: JsonPropertyName("timespan")] JsonNode? TimeSpan);
