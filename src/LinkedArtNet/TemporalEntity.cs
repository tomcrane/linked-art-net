using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// crm:E2 Temporal Entity: something that happens. The properties every event-like class has in the 1.0
/// schema: <c>timespan</c>, <c>took_place_at</c>, <c>during</c>, <c>caused_by</c>, <c>after</c>, <c>before</c>,
/// <c>referred_to_by</c>. <see cref="Birth"/> and <see cref="Death"/> are exactly this; activities add agency
/// via <see cref="ActivityBase"/>.
/// </summary>
[JsonDerivedType(typeof(Birth))]
[JsonDerivedType(typeof(Death))]
[JsonDerivedType(typeof(Destruction))]
[JsonDerivedType(typeof(Dissolution))]
[JsonDerivedType(typeof(Move))]
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
public abstract class TemporalEntity : LinkedArtObject, IHasStatements
{
    protected TemporalEntity(string type) : base(type)
    {
    }

    [JsonPropertyName("timespan")]
    [JsonPropertyOrder(102)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedArtTimeSpan? TimeSpan { get; set; }

    [JsonPropertyName("took_place_at")]
    [JsonPropertyOrder(103)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Place>? TookPlaceAt { get; set; }

    /// <summary>P10 falls within: the period this happened in.</summary>
    [JsonPropertyName("during")]
    [JsonPropertyOrder(104)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Period>? During { get; set; }

    [JsonPropertyName("referred_to_by")]
    [JsonPropertyOrder(105)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? ReferredToBy { get; set; }

    [JsonPropertyName("caused_by")]
    [JsonPropertyOrder(210)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Event>? CausedBy { get; set; }

    /// <summary>P183i starts after the end of.</summary>
    [JsonPropertyName("after")]
    [JsonPropertyOrder(310)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Period>? After { get; set; }

    /// <summary>P183 ends before the start of.</summary>
    [JsonPropertyName("before")]
    [JsonPropertyOrder(320)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Period>? Before { get; set; }
}
