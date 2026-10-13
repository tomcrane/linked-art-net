using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// la:Transfer: a change of ownership or custody where which of the two is unknown. Used by the site's provenance
/// examples (<c>provenance/transfer/1</c>) although the 1.0 schemas have no definition for it.
/// </summary>
public partial class Transfer : ActivityBase
{
    public Transfer() : base(nameof(Transfer))
    {
    }

    [JsonPropertyName("transferred")]
    [JsonPropertyOrder(108)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? Transferred { get; set; }

    [JsonPropertyName("transferred_from")]
    [JsonPropertyOrder(109)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? TransferredFrom { get; set; }

    [JsonPropertyName("transferred_to")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? TransferredTo { get; set; }
}
