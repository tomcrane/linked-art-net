using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class LinkedArtObject
{
    public LinkedArtObject() { }
    public LinkedArtObject(string type) 
    {
        Type = type;
    }
    public LinkedArtObject(Types type) 
    {
        Type = type.ToString();
    }


    [JsonPropertyName("@context")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(SingleStringJsonLDConverter))]
    public virtual string[]? Context { get; set; }


    [JsonPropertyName("id")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual string? Id { get; set; }


    [JsonPropertyName("type")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual string? Type { get; set; }

    [JsonPropertyName("_label")]
    [JsonPropertyOrder(10)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual string? Label { get; set; }

    [JsonPropertyName("content")]
    [JsonPropertyOrder(10)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual string? Content { get; set; }


    [JsonPropertyName("dimension")]
    [JsonPropertyOrder(24)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dimension>? Dimension { get; set; }

    [JsonPropertyName("language")]
    [JsonPropertyOrder(80)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? Language { get; set; }

    // P2 has type - https://www.cidoc-crm.org/Property/p2-has-type/version-6.2
    [JsonPropertyName("classified_as")]
    [JsonPropertyOrder(100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? ClassifiedAs { get; set; }


    [JsonPropertyName("referred_to_by")]
    [JsonPropertyOrder(105)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? ReferredToBy { get; set; }

    [JsonPropertyName("assigned_by")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<Activity>? AssignedBy { get; set; }

    [JsonPropertyName("identified_by")]
    [JsonPropertyOrder(120)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? IdentifiedBy { get; set; }


    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? Part { get; set; }


    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? PartOf { get; set; }


    [JsonPropertyName("member")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? Member { get; set; }


    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? MemberOf { get; set; }


    [JsonPropertyName("equivalent")]
    [JsonPropertyOrder(150)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? Equivalent { get; set; }


    [JsonPropertyName("created_by")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Activity? CreatedBy { get; set; }


    [JsonPropertyName("destroyed_by")]
    [JsonPropertyOrder(225)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Activity? DestroyedBy { get; set; }


    [JsonPropertyName("removed_by")]
    [JsonPropertyOrder(230)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? RemovedBy { get; set; }


    [JsonPropertyName("subject_to")]
    [JsonPropertyOrder(310)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Right>? SubjectTo { get; set; }


    [JsonPropertyName("about")]
    [JsonPropertyOrder(505)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? About { get; set; }


    [JsonPropertyName("representation")]
    [JsonPropertyOrder(510)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Work>? Representation { get; set; }

    [JsonPropertyName("subject_of")]
    [JsonPropertyOrder(610)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? SubjectOf { get; set; }


    [JsonPropertyName("digitally_carried_by")]
    [JsonPropertyOrder(710)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalObject>? DigitallyCarriedBy { get; set; }


    [JsonPropertyName("digitally_available_via")]
    [JsonPropertyOrder(720)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalService>? DigitallyAvailableVia { get; set; }


    [JsonPropertyName("attributed_by")]
    [JsonPropertyOrder(810)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<Activity>? AttributedBy { get; set; }


    [JsonPropertyName("modified_by")]
    [JsonPropertyOrder(820)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<Activity>? ModifiedBy { get; set; }
}
