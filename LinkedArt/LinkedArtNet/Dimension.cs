
using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class Dimension : LinkedArtObject
{

    public Dimension() { Type = nameof(Dimension); }

    [JsonPropertyName("value")]
    [JsonPropertyOrder(31)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    [JsonPropertyName("unit")]
    [JsonPropertyOrder(32)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MeasurementUnit? Unit { get; set; }

}
