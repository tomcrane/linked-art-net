using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// crm:E55 Type: a concept, usually a Getty AAT term. Base of <see cref="Material"/>, <see cref="Language"/>,
/// <see cref="Currency"/> and <see cref="MeasurementUnit"/>, which share its properties in the 1.0 schema.
/// </summary>
/// <remarks>Shadows <see cref="System.Type"/> inside this namespace; use <c>System.Type</c> explicitly there.</remarks>
[JsonDerivedType(typeof(Material))]
[JsonDerivedType(typeof(Language))]
[JsonDerivedType(typeof(Currency))]
[JsonDerivedType(typeof(MeasurementUnit))]
public partial class Type : LinkedArtEntity
{
    public Type() : base(nameof(Type))
    {
    }

    /// <summary>A concept reference by URI and label.</summary>
    public Type(string id, string? label) : this()
    {
        Id = id;
        Label = label;
    }

    protected Type(string type) : base(type)
    {
    }

    /// <summary>skos:broader: the parent concept.</summary>
    [JsonPropertyName("broader")]
    [JsonPropertyOrder(120)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Type>? Broader { get; set; }

    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Set>? MemberOf { get; set; }

    [JsonPropertyName("created_by")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Creation? CreatedBy { get; set; }
}
