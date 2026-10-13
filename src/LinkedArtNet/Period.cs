using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// crm:E4 Period: a referenceable span of happening (a historical period, an exhibition, a provenance
/// activity). In the 1.0 schema <see cref="Period"/>, <see cref="Event"/> and <see cref="Activity"/> share
/// one definition, so this class carries the referenceable-entity properties too.
/// </summary>
[JsonDerivedType(typeof(Event))]
[JsonDerivedType(typeof(Activity))]
public partial class Period : ActivityBase
{
    public Period() : base(nameof(Period))
    {
    }

    protected Period(string type) : base(type)
    {
    }

    /// <summary>P11 had participant: actors involved without having carried the event out.</summary>
    [JsonPropertyName("participant")]
    [JsonPropertyOrder(106)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? Participant { get; set; }

    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Period>? PartOf { get; set; }

    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Set>? MemberOf { get; set; }

    [JsonPropertyName("equivalent")]
    [JsonPropertyOrder(150)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Equivalent { get; set; }

    [JsonPropertyName("representation")]
    [JsonPropertyOrder(510)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VisualItem>? Representation { get; set; }

    [JsonPropertyName("subject_of")]
    [JsonPropertyOrder(610)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? SubjectOf { get; set; }

    [JsonPropertyName("attributed_by")]
    [JsonPropertyOrder(810)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AttributeAssignment>? AttributedBy { get; set; }
}
