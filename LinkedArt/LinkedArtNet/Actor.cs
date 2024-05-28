using System.Text.Json.Serialization;

namespace LinkedArtNet
{
    public abstract class Actor : LinkedArtObject
    {

        [JsonPropertyName("contact_point")]
        [JsonPropertyOrder(120)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<LinkedArtObject>? ContactPoint { get; set; }


        [JsonPropertyName("residence")]
        [JsonPropertyOrder(121)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Place>? Residence { get; set; }
    }
}
