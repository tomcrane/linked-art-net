
using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class Activity : LinkedArtObject
{
    public Activity()
    {
    }

    public Activity(string type) : base(type)
    {
    }

    public Activity(Types type) : base(type)
    {
    }

    [JsonPropertyName("carried_out_by")]
    [JsonPropertyOrder(101)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? CarriedOutBy { get; set; }


    [JsonPropertyName("timespan")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtTimeSpan>? TimeSpan { get; set; }


    [JsonPropertyName("took_place_at")]
    [JsonPropertyOrder(103)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Place>? TookPlaceAt { get; set; }
}
