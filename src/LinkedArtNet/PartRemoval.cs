using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E80 Part Removal: an object was removed from a larger one.</summary>
public class PartRemoval : EmbeddedActivity
{
    public PartRemoval() : base(nameof(PartRemoval))
    {
    }

    /// <summary>P112 diminished: the object the part was removed from.</summary>
    [JsonPropertyName("diminished")]
    [JsonPropertyOrder(200)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HumanMadeObject? Diminished { get; set; }
}
