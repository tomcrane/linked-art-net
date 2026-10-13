using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E12 Production of a physical object.</summary>
public class Production : EmbeddedActivity
{
    public Production() : base(nameof(Production))
    {
    }

    /// <summary>P9 consists of: sub-productions, e.g. one per artist or technique.</summary>
    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Production>? Part { get; set; }

    /// <summary>P140i was attributed by: uncertain attributions of the production. Used by the site's examples although the 1.0 schema omits it.</summary>
    [JsonPropertyName("attributed_by")]
    [JsonPropertyOrder(810)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AttributeAssignment>? AttributedBy { get; set; }
}
