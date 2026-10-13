using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E30 Right. A legal right over a work or object (copyright, licence).</summary>
public class Right : LinkedArtObject, IHasStatements
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
}
