using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.Serialization;

namespace LinkedArtNet.Dates.Service;

/// <summary>One parsed input as the service reports it. TimeSpans are Linked Art JSON exactly as the library writes it.</summary>
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

    internal static JsonNode? Json(LinkedArtTimeSpan? ts) => ts is null ? null : JsonNode.Parse(LinkedArtJson.Serialize(ts));

    internal static string Lower(string s) => s.ToLowerInvariant();

    internal static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

public sealed record AlternativeDto(string Canonical, double Confidence, string Note, [property: JsonPropertyName("timespan")] JsonNode? TimeSpan);

public sealed record BatchResponse(IReadOnlyList<TimeSpanResultDto> Results);

/// <summary>Request-level options; <c>llm</c> is accepted and ignored until an interpreter is configured.</summary>
public sealed record BatchOptions(bool? Llm = null, bool? EmitInnerBounds = null, string? LabelPolicy = null);

/// <summary>POST body. <see cref="Inputs"/> items are strings or objects <c>{ s, intent?, profile?, ref? }</c>.</summary>
public sealed record BatchRequest(string? Profile, string? Intent, BatchOptions? Options, IReadOnlyList<JsonElement> Inputs);

public sealed record ProfileSummary(string Name, string? Description, IReadOnlyList<string> Languages, string CenturyConvention);

public sealed record VersionInfo(string Service, string Parser, string Model, string? Git, IReadOnlyList<string> Profiles, int PeriodTables);

public sealed record ErrorResponse(string Error, string? Detail = null);

internal static class Parsing
{
    public static bool TryIntent(string? text, out Intent intent)
    {
        intent = Intent.Auto;
        if (string.IsNullOrEmpty(text)) return true;
        return Enum.TryParse(text, true, out intent);
    }

    public static bool TryLabelPolicy(string? text, out LabelPolicy? policy)
    {
        policy = null;
        if (string.IsNullOrEmpty(text)) return true;
        if (!Enum.TryParse<LabelPolicy>(text, true, out var p)) return false;
        policy = p;
        return true;
    }
}
