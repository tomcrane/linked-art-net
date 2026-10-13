using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E9 Move of objects from one place to another.</summary>
public class Move : ActivityBase
{
    public Move() : base(nameof(Move))
    {
    }

    [JsonPropertyName("moved")]
    [JsonPropertyOrder(108)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? Moved { get; set; }

    [JsonPropertyName("moved_from")]
    [JsonPropertyOrder(109)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Place? MovedFrom { get; set; }

    [JsonPropertyName("moved_to")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Place? MovedTo { get; set; }
}
