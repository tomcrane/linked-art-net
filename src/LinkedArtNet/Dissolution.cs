using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E68 Dissolution of a <see cref="Group"/>.</summary>
public partial class Dissolution : TemporalEntity
{
    public Dissolution() : base(nameof(Dissolution))
    {
    }

    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Event>? PartOf { get; set; }

    [JsonPropertyName("influenced_by")]
    [JsonPropertyOrder(160)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? InfluencedBy { get; set; }
}
