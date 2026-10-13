using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E30 Right. A legal right over a work or object (copyright, licence).</summary>
public partial class Right : LinkedArtObject, IHasDimensions
{
    public Right() : base(nameof(Right))
    {
    }

    [JsonPropertyName("referred_to_by")]
    [JsonPropertyOrder(105)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? ReferredToBy { get; set; }

    [JsonPropertyName("created_by")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Creation? CreatedBy { get; set; }

    /// <summary>P75i is possessed by: the rights holder.</summary>
    [JsonPropertyName("possessed_by")]
    [JsonPropertyOrder(211)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? PossessedBy { get; set; }

    /// <summary>Applies to: what the right is over. Used by the site's provenance examples; not in the 1.0 schema.</summary>
    [JsonPropertyName("applies_to")]
    [JsonPropertyOrder(212)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtEntity>? AppliesTo { get; set; }

    /// <summary>Share of the right, e.g. 40 percent. Used by the site's provenance examples; not in the 1.0 schema.</summary>
    [JsonPropertyName("dimension")]
    [JsonPropertyOrder(213)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dimension>? Dimension { get; set; }

    /// <summary>Partial rights making up this right. Used by the site's provenance examples; not in the 1.0 schema.</summary>
    [JsonPropertyName("part")]
    [JsonPropertyOrder(214)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Right>? Part { get; set; }
}
