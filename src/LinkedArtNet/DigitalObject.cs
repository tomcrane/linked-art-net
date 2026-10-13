using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>dig:D1 Digital Object. A file, web page, API endpoint or other digital resource.</summary>
public class DigitalObject : LinkedArtEntity, IHasDimensions
{
    public DigitalObject() : base(nameof(DigitalObject))
    {
    }

    [JsonPropertyName("dimension")]
    [JsonPropertyOrder(25)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dimension>? Dimension { get; set; }

    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalObject>? PartOf { get; set; }

    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Set>? MemberOf { get; set; }

    [JsonPropertyName("created_by")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Creation? CreatedBy { get; set; }

    [JsonPropertyName("used_for")]
    [JsonPropertyOrder(220)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? UsedFor { get; set; }

    [JsonPropertyName("access_point")]
    [JsonPropertyOrder(401)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalObject>? AccessPoint { get; set; }

    [JsonPropertyName("conforms_to")]
    [JsonPropertyOrder(402)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InformationObject>? ConformsTo { get; set; }

    /// <summary>dc:format: the media type.</summary>
    [JsonPropertyName("format")]
    [JsonPropertyOrder(403)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }

    [JsonPropertyName("digitally_carries")]
    [JsonPropertyOrder(404)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? DigitallyCarries { get; set; }

    [JsonPropertyName("digitally_shows")]
    [JsonPropertyOrder(405)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VisualItem>? DigitallyShows { get; set; }

    [JsonPropertyName("digitally_available_via")]
    [JsonPropertyOrder(720)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalService>? DigitallyAvailableVia { get; set; }
}
