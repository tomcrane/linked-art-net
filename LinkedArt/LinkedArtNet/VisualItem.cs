using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class VisualItem : LinkedArtObject
{
    public VisualItem() { Type = nameof(VisualItem); }

    [JsonPropertyName("represents")]
    [JsonPropertyOrder(101)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Represents { get; set; }


    [JsonPropertyName("represents_instance_of_type")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? RepresentsInstanceOfType { get; set; }



    [JsonPropertyName("digitally_shown_by")]
    [JsonPropertyOrder(202)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalObject>? DigitallyShownBy { get; set; }
}
