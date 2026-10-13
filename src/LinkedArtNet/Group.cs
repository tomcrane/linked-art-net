using System.Text.Json.Serialization;

namespace LinkedArtNet
{
    public class Group : Actor
    {
        public Group() { Type = nameof(Group); }


        [JsonPropertyName("formed_by")]
        [JsonPropertyOrder(220)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Activity? FormedBy { get; set; }



        [JsonPropertyName("dissolved_by")]
        [JsonPropertyOrder(221)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Activity? DissolvedBy { get; set; }
    }
}
