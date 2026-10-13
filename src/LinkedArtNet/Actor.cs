using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E39 Actor: a <see cref="Person"/> or <see cref="Group"/>.</summary>
[JsonDerivedType(typeof(Person))]
[JsonDerivedType(typeof(Group))]
public abstract class Actor : LinkedArtEntity
{
    protected Actor(string type) : base(type)
    {
    }

    /// <summary>P76 has contact point: addresses, email addresses, telephone numbers.</summary>
    [JsonPropertyName("contact_point")]
    [JsonPropertyOrder(120)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Identifier>? ContactPoint { get; set; }

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

    /// <summary>P107i is current or former member of: the groups this actor belongs to.</summary>
    [JsonPropertyName("member_of")]
    [JsonPropertyOrder(140)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Group>? MemberOf { get; set; }
}
