using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>la:Set. A collection, archival grouping, exhibition object list or other set of things.</summary>
public class Set : LinkedArtEntity, IHasDimensions
{
    public Set() : base(nameof(Set))
    {
    }

    [JsonPropertyName("dimension")]
    [JsonPropertyOrder(25)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Dimension>? Dimension { get; set; }

    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Set>? MemberOf { get; set; }

    /// <summary>la:members_contained_by: the box, folder or other object that physically holds the members.</summary>
    [JsonPropertyName("members_contained_by")]
    [JsonPropertyOrder(144)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<HumanMadeObject>? MembersContainedBy { get; set; }

    [JsonPropertyName("created_by")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Creation? CreatedBy { get; set; }

    [JsonPropertyName("used_for")]
    [JsonPropertyOrder(220)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Activity>? UsedFor { get; set; }

    [JsonPropertyName("about")]
    [JsonPropertyOrder(505)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? About { get; set; }

    /// <summary>la:members_exemplified_by: a prototypical member describing what the members are like.</summary>
    [JsonPropertyName("members_exemplified_by")]
    [JsonPropertyOrder(920)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? MembersExemplifiedBy { get; set; }
}
