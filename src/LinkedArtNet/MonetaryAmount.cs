namespace LinkedArtNet;

/// <summary>
/// crm:E97 Monetary Amount: a <see cref="Dimension"/> whose unit is a <see cref="Currency"/>. The site's
/// provenance examples also list a monetary amount among a Set's dimensions, which the 1.0 schema does not allow.
/// </summary>
public partial class MonetaryAmount : Dimension
{
    public MonetaryAmount() : base(nameof(MonetaryAmount))
    {
    }

    [System.Text.Json.Serialization.JsonPropertyName("currency")]
    [System.Text.Json.Serialization.JsonPropertyOrder(33)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Currency? Currency { get; set; }
}
