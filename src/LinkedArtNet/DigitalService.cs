using LinkedArtNet.Vocabulary;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// la:DigitalService. A service (such as a IIIF Image API endpoint) through which a <see cref="DigitalObject"/>
/// is available. In the 1.0 schema it is an embedded structure, not a <see cref="DigitalObject"/>.
/// </summary>
[JsonDerivedType(typeof(IIIFImageService))]
public class DigitalService : LinkedArtObject, IHasStatements
{
    public DigitalService() : base(nameof(DigitalService))
    {
    }

    [JsonPropertyName("referred_to_by")]
    [JsonPropertyOrder(105)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? ReferredToBy { get; set; }

    [JsonPropertyName("access_point")]
    [JsonPropertyOrder(401)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DigitalObject>? AccessPoint { get; set; }

    [JsonPropertyName("conforms_to")]
    [JsonPropertyOrder(402)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<InformationObject>? ConformsTo { get; set; }

    /// <summary>dc:format. Used by the site's IIIF examples although the 1.0 schema omits it on DigitalService.</summary>
    [JsonPropertyName("format")]
    [JsonPropertyOrder(403)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Format { get; set; }
}
