using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class Place : LinkedArtObject
{
    public Place() { Type = nameof(Place); }

    // Place only
    [JsonPropertyName("defined_by")]
    [JsonPropertyOrder(800)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual string? DefinedBy { get; set; }
}
