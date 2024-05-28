using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class Work : LinkedArtObject
{
    // There is no Work class in Linked Art...
    public Work()
    {
    }

    public Work(string type) : base(type)
    {
    }

    public Work(Types type) : base(type)
    {
    }

    [JsonPropertyName("represents")]
    [JsonPropertyOrder(101)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Represents { get; set; }


    [JsonPropertyName("represents_instance_of_type")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? RepresentsInstanceOfType { get; set; }

    [JsonPropertyName("about")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? About { get; set; }


    [JsonPropertyName("digitally_shown_by")]
    [JsonPropertyOrder(202)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalObject>? DigitallyShownBy { get; set; }
}
