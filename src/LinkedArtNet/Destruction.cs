using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E6 Destruction of an object.</summary>
public partial class Destruction : TemporalEntity
{
    public Destruction() : base(nameof(Destruction))
    {
    }

    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Event>? PartOf { get; set; }
}
