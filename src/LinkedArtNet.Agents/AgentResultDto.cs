using System.Text.Json.Nodes;
using LinkedArtNet.Dates;
using LinkedArtNet.Serialization;

namespace LinkedArtNet.Agents;

public sealed record RoleDto(string Text, string Label, string? Aat, bool Occupation);
public sealed record NationalityDto(string Text, string Label, string? Aat);

/// <summary>One parsed agent string as the service reports it from <c>GET /v1/agent</c> and the site shows it; the same shape wherever it came from.</summary>
public sealed record AgentResultDto(
    string Input,
    string Profile,
    string Kind,
    string? Name,
    string? FamilyName,
    string? GivenNames,
    string? SortName,
    string? DisplayName,
    IReadOnlyList<string> Honorifics,
    string? DateText,
    TimeSpanResultDto? Dates,
    IReadOnlyList<RoleDto> Roles,
    IReadOnlyList<NationalityDto> Nationalities,
    IReadOnlyList<string> Qualifiers,
    string? Unconsumed,
    double Confidence,
    IReadOnlyList<string> Notes,
    string ParserVersion,
    JsonNode? Agent)
{
    /// <param name="withAgent">Include the Linked Art Person or Group JSON built from the parse.</param>
    public static AgentResultDto From(ParsedAgent a, bool withAgent = true) => new(
        a.Input, a.Profile, Camel(a.Kind.ToString()), a.Name, a.FamilyName, a.GivenNames, a.SortName, a.DisplayName, a.Honorifics, a.DateText,
        a.Dates is null ? null : TimeSpanResultDto.From(a.Dates, null),
        a.Roles.Select(r => new RoleDto(r.Text, r.Label, r.AatUri, r.IsOccupation)).ToList(),
        a.Nationalities.Select(n => new NationalityDto(n.Text, n.Label, n.AatUri)).ToList(),
        a.Qualifiers.Select(q => Camel(q.ToString())).Order(StringComparer.Ordinal).ToList(),
        a.Unconsumed, a.Confidence, a.Notes, AgentStringParser.Version,
        withAgent && (a.HasName || a.HasDates || a.Nationalities.Count > 0) ? JsonNode.Parse(LinkedArtJson.Serialize(a.ToActor())) : null);

    private static string Camel(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
