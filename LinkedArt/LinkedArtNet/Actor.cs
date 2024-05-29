using System.Text.Json.Serialization;

namespace LinkedArtNet
{
    [JsonDerivedType(typeof(Person))]
    [JsonDerivedType(typeof(Group))]
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



        [JsonPropertyName("carried_out")]
        [JsonPropertyOrder(131)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Activity>? CarriedOut { get; set; }


        [JsonPropertyName("participated_in")]
        [JsonPropertyOrder(132)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Activity>? ParticipatedIn { get; set; }
    }
}
