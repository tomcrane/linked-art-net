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

    [JsonPropertyName("language")]
    [JsonPropertyOrder(80)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? Language { get; set; }

    // P2 has type - https://www.cidoc-crm.org/Property/p2-has-type/version-6.2
    [JsonPropertyName("classified_as")]
    [JsonPropertyOrder(100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? ClassifiedAs { get; set; }

    [JsonPropertyName("assigned_by")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<Activity>? AssignedBy { get; set; }


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


}
