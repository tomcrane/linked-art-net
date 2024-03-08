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
    public virtual string? Language { get; set; }

    // P2 has type - https://www.cidoc-crm.org/Property/p2-has-type/version-6.2
    [JsonPropertyName("classified_as")]
    [JsonPropertyOrder(20)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? ClassifiedAs { get; set; }



}
