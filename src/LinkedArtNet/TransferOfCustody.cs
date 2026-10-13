using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E10 Transfer of Custody: physical possession changed hands (a loan, a deposit).</summary>
public partial class TransferOfCustody : ActivityBase
{
    public TransferOfCustody() : base(nameof(TransferOfCustody))
    {
    }

    [JsonPropertyName("transferred_custody_of")]
    [JsonPropertyOrder(108)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? TransferredCustodyOf { get; set; }

    [JsonPropertyName("transferred_custody_from")]
    [JsonPropertyOrder(109)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? TransferredCustodyFrom { get; set; }

    [JsonPropertyName("transferred_custody_to")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? TransferredCustodyTo { get; set; }
}
