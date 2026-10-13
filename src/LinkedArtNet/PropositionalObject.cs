using LinkedArtNet.Vocabulary;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// crm:E89 Propositional Object. An abstract intellectual work (an exhibition concept, a story). Base of
/// <see cref="LinguisticObject"/> and <see cref="VisualItem"/>, as in CIDOC-CRM.
/// </summary>
[JsonDerivedType(typeof(LinguisticObject))]
[JsonDerivedType(typeof(IIIFManifest))]
[JsonDerivedType(typeof(VisualItem))]
public partial class PropositionalObject : LinkedArtEntity, IHasDimensions
{
    public PropositionalObject() : base(nameof(PropositionalObject))
    {
    }

    protected PropositionalObject(string type) : base(type)
    {
    }

    [JsonPropertyName("dimension")]
    [JsonPropertyOrder(25)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dimension>? Dimension { get; set; }

    [JsonPropertyName("conceptually_part_of")]
    [JsonPropertyOrder(131)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PropositionalObject>? ConceptuallyPartOf { get; set; }

    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Set>? MemberOf { get; set; }

    [JsonPropertyName("created_by")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Creation? CreatedBy { get; set; }

    [JsonPropertyName("subject_to")]
    [JsonPropertyOrder(310)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Right>? SubjectTo { get; set; }

    [JsonPropertyName("about")]
    [JsonPropertyOrder(505)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? About { get; set; }
}
