
using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class Activity : LinkedArtObject
{
    public Activity() : base("Activity")
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

    // QUESTION - is TimeSpan an array? LA site example is not.
    [JsonPropertyName("timespan")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtTimeSpan>? TimeSpan { get; set; }


    [JsonPropertyName("took_place_at")]
    [JsonPropertyOrder(103)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Place>? TookPlaceAt { get; set; }


    [JsonPropertyName("used_specific_object")]
    [JsonPropertyOrder(104)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? UsedSpecificObject { get; set; }


    [JsonPropertyName("transferred_title_of")]
    [JsonPropertyOrder(109)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? TransferredTitleOf { get; set; }

    [JsonPropertyName("transferred_title_to")]
    [JsonPropertyOrder(109)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? TransferredTitleTo { get; set; }



    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public new List<Activity>? Part { get; set; }


    [JsonPropertyName("technique")]
    [JsonPropertyOrder(150)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Technique { get; set; }


    [JsonPropertyName("influenced_by")]
    [JsonPropertyOrder(160)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? InfluencedBy { get; set; }
}
