using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E97 Monetary Amount.</summary>
public class MonetaryAmount : LinkedArtObject
{
    public MonetaryAmount() : base(nameof(MonetaryAmount))
    {
    }

    [JsonPropertyName("value")]
    [JsonPropertyOrder(31)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Value { get; set; }

    [JsonPropertyName("currency")]
    [JsonPropertyOrder(33)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Currency? Currency { get; set; }

    [JsonPropertyName("lower_value_limit")]
    [JsonPropertyOrder(34)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? LowerValueLimit { get; set; }

    [JsonPropertyName("upper_value_limit")]
    [JsonPropertyOrder(35)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? UpperValueLimit { get; set; }
}
