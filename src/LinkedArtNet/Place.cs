using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E53 Place.</summary>
public partial class Place : LinkedArtEntity
{
    public Place() : base(nameof(Place))
    {
    }

    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Place>? PartOf { get; set; }

    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Set>? MemberOf { get; set; }

    /// <summary>P168 is defined by: a WKT geometry string.</summary>
    [JsonPropertyName("defined_by")]
    [JsonPropertyOrder(800)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefinedBy { get; set; }
}
