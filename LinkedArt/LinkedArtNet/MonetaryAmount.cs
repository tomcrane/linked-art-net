
using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class MonetaryAmount : LinkedArtObject
{
    public MonetaryAmount() { Type = nameof(MonetaryAmount); }

    [JsonPropertyName("value")]
    [JsonPropertyOrder(31)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Value { get; set; }

    [JsonPropertyName("currency")]
    [JsonPropertyOrder(33)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedArtObject? Currency{ get; set; }

}
