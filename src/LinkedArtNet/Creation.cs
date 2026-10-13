using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E65 Creation of a conceptual work (text, image content, set).</summary>
public class Creation : EmbeddedActivity
{
    public Creation() : base(nameof(Creation))
    {
    }

    /// <summary>P9 consists of: sub-creations, e.g. one per role (director, producer). Used by the site's examples although the 1.0 schema omits it.</summary>
    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Creation>? Part { get; set; }
}
