using LinkedArtNet.Vocabulary;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// A referenceable thing with its own URI: physical and digital objects, works, sets, actors, places and
/// concepts. Adds the properties the 1.0 schemas give to all of them: <c>referred_to_by</c>, <c>equivalent</c>,
/// <c>representation</c>, <c>subject_of</c>, <c>attributed_by</c>. (<c>member_of</c> is declared per class because
/// its range differs: Sets for most things, Groups for actors.)
/// </summary>
[JsonDerivedType(typeof(HumanMadeObject))]
[JsonDerivedType(typeof(DigitalObject))]
[JsonDerivedType(typeof(PropositionalObject))]
[JsonDerivedType(typeof(LinguisticObject))]
[JsonDerivedType(typeof(IIIFManifest))]
[JsonDerivedType(typeof(VisualItem))]
[JsonDerivedType(typeof(Set))]
[JsonDerivedType(typeof(Person))]
[JsonDerivedType(typeof(Group))]
[JsonDerivedType(typeof(Place))]
[JsonDerivedType(typeof(Type))]
[JsonDerivedType(typeof(Material))]
[JsonDerivedType(typeof(Language))]
[JsonDerivedType(typeof(Currency))]
[JsonDerivedType(typeof(MeasurementUnit))]
public abstract partial class LinkedArtEntity : LinkedArtObject
{
    protected LinkedArtEntity(string type) : base(type)
    {
    }

    /// <summary>P67i is referred to by: statements, descriptions, notes.</summary>
    [JsonPropertyName("referred_to_by")]
    [JsonPropertyOrder(105)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? ReferredToBy { get; set; }

    /// <summary>la:equivalent: the same entity in another dataset.</summary>
    [JsonPropertyName("equivalent")]
    [JsonPropertyOrder(150)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Equivalent { get; set; }

    /// <summary>P138i has representation: images of this entity.</summary>
    [JsonPropertyName("representation")]
    [JsonPropertyOrder(510)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VisualItem>? Representation { get; set; }

    /// <summary>P129i is subject of: texts about this entity (web pages, articles).</summary>
    [JsonPropertyName("subject_of")]
    [JsonPropertyOrder(610)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? SubjectOf { get; set; }

    /// <summary>P140i was attributed by: assertions about this entity.</summary>
    [JsonPropertyName("attributed_by")]
    [JsonPropertyOrder(810)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AttributeAssignment>? AttributedBy { get; set; }
}
