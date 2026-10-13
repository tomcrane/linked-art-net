using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E74 Group: an organisation, family, workshop or other collective actor.</summary>
public partial class Group : Actor
{
    public Group() : base(nameof(Group))
    {
    }

    [JsonPropertyName("formed_by")]
    [JsonPropertyOrder(220)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Formation? FormedBy { get; set; }

    [JsonPropertyName("dissolved_by")]
    [JsonPropertyOrder(221)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dissolution? DissolvedBy { get; set; }

    /// <summary>Prototypical members of a group that cannot be enumerated (e.g. 14th-century Italian painters). Used by the site; not in the 1.0 schema.</summary>
    [JsonPropertyName("members_exemplified_by")]
    [JsonPropertyOrder(222)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? MembersExemplifiedBy { get; set; }
}
