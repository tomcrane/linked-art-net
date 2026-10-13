using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>la:Payment of money, usually as part of a provenance activity.</summary>
public class Payment : ActivityBase
{
    public Payment() : base(nameof(Payment))
    {
    }

    [JsonPropertyName("paid_amount")]
    [JsonPropertyOrder(31)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MonetaryAmount? PaidAmount { get; set; }

    [JsonPropertyName("paid_from")]
    [JsonPropertyOrder(32)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? PaidFrom { get; set; }

    [JsonPropertyName("paid_to")]
    [JsonPropertyOrder(33)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? PaidTo { get; set; }
}
