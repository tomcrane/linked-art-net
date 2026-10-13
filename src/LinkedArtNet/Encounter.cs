using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>sci:S19 Encounter Event: finding or excavating an object.</summary>
public partial class Encounter : EmbeddedActivity
{
    public Encounter() : base(nameof(Encounter))
    {
    }

    [JsonPropertyName("encountered")]
    [JsonPropertyOrder(108)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? Encountered { get; set; }
}
