using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E11 Modification of an object (conservation, alteration).</summary>
public partial class Modification : EmbeddedActivity
{
    public Modification() : base(nameof(Modification))
    {
    }

    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Production>? Part { get; set; }
}
