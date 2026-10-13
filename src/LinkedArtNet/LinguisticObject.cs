using LinkedArtNet.Vocabulary;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E33 Linguistic Object. Text: a statement, a description, a book's content, a web page.</summary>
[JsonDerivedType(typeof(IIIFManifest))]
public class LinguisticObject : PropositionalObject, IHasContent, IHasLanguage
{
    public LinguisticObject() : base(nameof(LinguisticObject))
    {
    }

    protected LinguisticObject(string type) : base(type)
    {
    }

    [JsonPropertyName("content")]
    [JsonPropertyOrder(10)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonPropertyName("language")]
    [JsonPropertyOrder(80)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Language>? Language { get; set; }

    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<PropositionalObject>? PartOf { get; set; }

    [JsonPropertyName("used_for")]
    [JsonPropertyOrder(220)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? UsedFor { get; set; }

    /// <summary>dc:format: the media type of the text itself (e.g. text/html).</summary>
    [JsonPropertyName("format")]
    [JsonPropertyOrder(403)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    [JsonPropertyName("digitally_carried_by")]
    [JsonPropertyOrder(710)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalObject>? DigitallyCarriedBy { get; set; }
}
