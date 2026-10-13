
using System.Text.Json.Serialization;

namespace LinkedArtNet
{
    public class Payment : Activity
    {
        // feels like this should be its own class to isolate these properties:
        public Payment() { Type = nameof(Payment); }

        [JsonPropertyName("paid_amount")]
        [JsonPropertyOrder(31)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MonetaryAmount? PaidAmount{ get; set; }

        [JsonPropertyName("paid_from")]
        [JsonPropertyOrder(32)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Actor>? PaidFrom { get; set; }

        [JsonPropertyName("paid_to")]
        [JsonPropertyOrder(33)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Actor>? PaidTo { get; set; }

    }
}
