using System.Text.Json;
using System.Text.Json.Serialization;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Dates.Service;

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
