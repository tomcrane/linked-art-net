using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E8 Acquisition: a transfer of ownership.</summary>
public partial class Acquisition : EmbeddedActivity
{
    public Acquisition() : base(nameof(Acquisition))
    {
    }

    [JsonPropertyName("transferred_title_of")]
    [JsonPropertyOrder(108)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? TransferredTitleOf { get; set; }

    [JsonPropertyName("transferred_title_from")]
    [JsonPropertyOrder(109)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? TransferredTitleFrom { get; set; }

    [JsonPropertyName("transferred_title_to")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? TransferredTitleTo { get; set; }

    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ActivityBase>? Part { get; set; }
}
