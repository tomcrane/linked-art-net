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

    [JsonPropertyName("about")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? About { get; set; }
}
