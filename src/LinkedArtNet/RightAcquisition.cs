using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>la:RightAcquisition: rights were established or invalidated.</summary>
public partial class RightAcquisition : ActivityBase
{
    public RightAcquisition() : base(nameof(RightAcquisition))
    {
    }

    [JsonPropertyName("establishes")]
    [JsonPropertyOrder(108)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Right>? Establishes { get; set; }

    [JsonPropertyName("invalidates")]
    [JsonPropertyOrder(109)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Right>? Invalidates { get; set; }
}
