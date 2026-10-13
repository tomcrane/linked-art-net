using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E36 Visual Item. The image content shown by an object or a digital file.</summary>
public class VisualItem : PropositionalObject
{
    public VisualItem() : base(nameof(VisualItem))
    {
    }

    [JsonPropertyName("represents")]
    [JsonPropertyOrder(101)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Represents { get; set; }

    [JsonPropertyName("represents_instance_of_type")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Type>? RepresentsInstanceOfType { get; set; }

    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PropositionalObject>? PartOf { get; set; }

    [JsonPropertyName("digitally_shown_by")]
    [JsonPropertyOrder(202)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalObject>? DigitallyShownBy { get; set; }

    [JsonPropertyName("used_for")]
    [JsonPropertyOrder(220)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? UsedFor { get; set; }
}
