using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class HumanMadeObject : LinkedArtObject
{
    public HumanMadeObject() { Type = nameof(HumanMadeObject); }



    [JsonPropertyName("produced_by")]
    [JsonPropertyOrder(23)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Activity? ProducedBy { get; set; }


    [JsonPropertyName("shows")]
    [JsonPropertyOrder(30)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Work>? Shows { get; set; }

    [JsonPropertyName("carries")]
    [JsonPropertyOrder(30)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? Carries{ get; set; }


    [JsonPropertyName("made_of")]
    [JsonPropertyOrder(31)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? MadeOf { get; set; }


    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual new List<HumanMadeObject>? Part { get; set; }


    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual new List<HumanMadeObject>? PartOf { get; set; }


    [JsonPropertyName("current_owner")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? CurrentOwner { get; set; }

    [JsonPropertyName("current_permanent_custodian")]
    [JsonPropertyOrder(211)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual LinkedArtObject? CurrentPermanentCustodian { get; set; }


    [JsonPropertyName("current_custodian")]
    [JsonPropertyOrder(212)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<LinkedArtObject>? CurrentCustodian { get; set; }

    [JsonPropertyName("current_permanent_location")]
    [JsonPropertyOrder(221)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual Place? CurrentPermanentLocation { get; set; }

    [JsonPropertyName("current_location")]
    [JsonPropertyOrder(222)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual Place? CurrentLocation { get; set; }


    [JsonPropertyName("changed_ownership_through")]
    [JsonPropertyOrder(251)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public virtual List<Activity>? ChangedOwnershipThrough { get; set; }



}
