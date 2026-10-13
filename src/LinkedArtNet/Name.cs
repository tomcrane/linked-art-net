using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>la:Name (crm:E33_E41 Linguistic Appellation). A name or title.</summary>
public partial class Name : LinkedArtObject, IHasContent, IHasLanguage
{
    public Name() : base(nameof(Name))
    {
    }

    public Name(string content) : this()
    {
        Content = content;
    }

    [JsonPropertyName("content")]
    [JsonPropertyOrder(10)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonPropertyName("language")]
    [JsonPropertyOrder(80)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Language>? Language { get; set; }

    [JsonPropertyName("referred_to_by")]
    [JsonPropertyOrder(105)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? ReferredToBy { get; set; }

    /// <summary>P141i was assigned by: the source of the name. Used by the site's examples although the 1.0 schema omits it on Name.</summary>
    [JsonPropertyName("assigned_by")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AttributeAssignment>? AssignedBy { get; set; }

    /// <summary>P106 is composed of: the parts of the name (given name, family name, ...).</summary>
    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Name>? Part { get; set; }
}
