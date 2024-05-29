
using System.Text.Json.Serialization;

namespace LinkedArtNet;

[JsonDerivedType(typeof(Payment))]
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

    [JsonPropertyName("timespan")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedArtTimeSpan? TimeSpan { get; set; }


    [JsonPropertyName("took_place_at")]
    [JsonPropertyOrder(103)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Place>? TookPlaceAt { get; set; }


    [JsonPropertyName("used_specific_object")]
    [JsonPropertyOrder(104)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? UsedSpecificObject { get; set; }
    //public List<HumanMadeObject>? UsedSpecificObject { get; set; } // need to allow a Set


    [JsonPropertyName("transferred_title_of")]
    [JsonPropertyOrder(108)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? TransferredTitleOf { get; set; }

    [JsonPropertyName("transferred_title_from")]
    [JsonPropertyOrder(109)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? TransferredTitleFrom { get; set; }

    [JsonPropertyName("transferred_title_to")]
    [JsonPropertyOrder(110)]
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
    public List<LinkedArtObject>? InfluencedBy { get; set; }


    [JsonPropertyName("diminished")]
    [JsonPropertyOrder(200)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HumanMadeObject? Diminished { get; set; }


    [JsonPropertyName("caused_by")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? CausedBy { get; set; }

    

    [JsonPropertyName("starts_after_the_end_of")]
    [JsonPropertyOrder(310)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? StartsAfterTheEndOf { get; set; }

    [JsonPropertyName("ends_before_the_start_of")]
    [JsonPropertyOrder(320)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? EndsBeforeTheStartOf { get; set; }


    [JsonPropertyName("motivated_by")]
    [JsonPropertyOrder(410)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? MotivatedBy { get; set; }

    // move to specific AttributeAssignment class?
    [JsonPropertyName("assigned_property")]
    [JsonPropertyOrder(510)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssignedProperty { get; set; }


    // move to specific AttributeAssignment class?
    [JsonPropertyName("assigned")]
    [JsonPropertyOrder(511)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Assigned { get; set; }
}
