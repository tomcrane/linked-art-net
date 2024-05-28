using System.Text.Json.Serialization;

namespace LinkedArtNet
{
    public class DigitalObject : LinkedArtObject
    {
        public DigitalObject() { Type = nameof(DigitalObject); }


        [JsonPropertyName("access_point")]
        [JsonPropertyOrder(401)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public virtual List<DigitalObject>? AccessPoint{ get; set; }



        [JsonPropertyName("conforms_to")]
        [JsonPropertyOrder(402)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public virtual List<LinkedArtObject>? ConformsTo { get; set; }


        [JsonPropertyName("format")]
        [JsonPropertyOrder(403)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public virtual string? Format { get; set; }



        [JsonPropertyName("digitally_carries")]
        [JsonPropertyOrder(404)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public virtual List<LinkedArtObject>? DigitallyCarries { get; set; }


        [JsonPropertyName("digitally_shows")]
        [JsonPropertyOrder(405)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Work>? DigitallyShows { get; set; }
    }
}
