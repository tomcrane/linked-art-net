using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E13 Attribute Assignment: an assertion about an entity, with its source and context.</summary>
public partial class AttributeAssignment : EmbeddedActivity
{
    public AttributeAssignment() : base(nameof(AttributeAssignment))
    {
    }

    /// <summary>P177 assigned property of type: the property name being asserted (e.g. "classified_as").</summary>
    [JsonPropertyName("assigned_property")]
    [JsonPropertyOrder(510)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssignedProperty { get; set; }

    /// <summary>P141 assigned: the value being asserted.</summary>
    [JsonPropertyName("assigned")]
    [JsonPropertyOrder(511)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Assigned { get; set; }
}
