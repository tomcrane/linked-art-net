using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// A <see cref="TemporalEntity"/> with agency: <c>carried_out_by</c>, <c>technique</c>, <c>influenced_by</c>,
/// <c>used_specific_object</c>. Base of every activity class, embedded (<see cref="EmbeddedActivity"/>,
/// <see cref="Payment"/>, <see cref="Move"/>, ...) or referenceable (<see cref="Period"/>, <see cref="Event"/>,
/// <see cref="Activity"/>).
/// </summary>
[JsonDerivedType(typeof(Move))]
[JsonDerivedType(typeof(Transfer))]
[JsonDerivedType(typeof(Payment))]
[JsonDerivedType(typeof(RightAcquisition))]
[JsonDerivedType(typeof(TransferOfCustody))]
[JsonDerivedType(typeof(Creation))]
[JsonDerivedType(typeof(Formation))]
[JsonDerivedType(typeof(Production))]
[JsonDerivedType(typeof(Modification))]
[JsonDerivedType(typeof(Acquisition))]
[JsonDerivedType(typeof(Encounter))]
[JsonDerivedType(typeof(PartRemoval))]
[JsonDerivedType(typeof(AttributeAssignment))]
[JsonDerivedType(typeof(Period))]
[JsonDerivedType(typeof(Event))]
[JsonDerivedType(typeof(Activity))]
public abstract partial class ActivityBase : TemporalEntity
{
    protected ActivityBase(string type) : base(type)
    {
    }

    [JsonPropertyName("carried_out_by")]
    [JsonPropertyOrder(101)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Actor>? CarriedOutBy { get; set; }

    /// <summary>P16 used specific object: an object or set used in the activity.</summary>
    [JsonPropertyName("used_specific_object")]
    [JsonPropertyOrder(105)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtEntity>? UsedSpecificObject { get; set; }

    [JsonPropertyName("technique")]
    [JsonPropertyOrder(150)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Type>? Technique { get; set; }

    [JsonPropertyName("influenced_by")]
    [JsonPropertyOrder(160)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? InfluencedBy { get; set; }
}
