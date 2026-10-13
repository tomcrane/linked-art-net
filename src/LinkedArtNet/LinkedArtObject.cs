using System.Text.Json.Serialization;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

/// <summary>
/// Base of every Linked Art node. Carries only what the 1.0 schemas allow on <em>every</em> class:
/// <c>id</c>, <c>type</c>, <c>_label</c>, <c>identified_by</c> and <c>classified_as</c> (plus <c>@context</c>
/// for top-level documents). Everything else lives on the class the schema puts it on.
/// </summary>
/// <remarks>
/// The hierarchy follows the 1.0 JSON schemas, verified by <c>SchemaContractTests</c>:
/// <list type="bullet">
/// <item><see cref="LinkedArtEntity"/>: things with their own URI (objects, works, actors, places, concepts, sets).</item>
/// <item><see cref="TemporalEntity"/>: events; <see cref="ActivityBase"/> adds agency; <see cref="EmbeddedActivity"/>
/// adds <c>part_of</c>; <see cref="Period"/>, <see cref="Event"/> and <see cref="Activity"/> are the referenceable forms.</item>
/// <item>Structures with no URI of their own: <see cref="Name"/>, <see cref="Identifier"/>, <see cref="Dimension"/>,
/// <see cref="MonetaryAmount"/>, <see cref="LinkedArtTimeSpan"/>, <see cref="Right"/>, <see cref="DigitalService"/>.</item>
/// </list>
/// </remarks>
[JsonDerivedType(typeof(HumanMadeObject))]
[JsonDerivedType(typeof(DigitalObject))]
[JsonDerivedType(typeof(DigitalService))]
[JsonDerivedType(typeof(IIIFImageService))]
[JsonDerivedType(typeof(InformationObject))]
[JsonDerivedType(typeof(PropositionalObject))]
[JsonDerivedType(typeof(LinguisticObject))]
[JsonDerivedType(typeof(IIIFManifest))]
[JsonDerivedType(typeof(VisualItem))]
[JsonDerivedType(typeof(Set))]
[JsonDerivedType(typeof(Person))]
[JsonDerivedType(typeof(Group))]
[JsonDerivedType(typeof(Place))]
[JsonDerivedType(typeof(Type))]
[JsonDerivedType(typeof(Material))]
[JsonDerivedType(typeof(Language))]
[JsonDerivedType(typeof(Currency))]
[JsonDerivedType(typeof(MeasurementUnit))]
[JsonDerivedType(typeof(Name))]
[JsonDerivedType(typeof(Identifier))]
[JsonDerivedType(typeof(Dimension))]
[JsonDerivedType(typeof(MonetaryAmount))]
[JsonDerivedType(typeof(Right))]
[JsonDerivedType(typeof(LinkedArtTimeSpan))]
[JsonDerivedType(typeof(Period))]
[JsonDerivedType(typeof(Event))]
[JsonDerivedType(typeof(Activity))]
[JsonDerivedType(typeof(Birth))]
[JsonDerivedType(typeof(Death))]
[JsonDerivedType(typeof(Destruction))]
[JsonDerivedType(typeof(Dissolution))]
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
public abstract partial class LinkedArtObject
{
    protected LinkedArtObject(string type)
    {
        Type = type;
    }

    [JsonPropertyName("@context")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(SingleStringJsonLDConverter))]
    public string[]? Context { get; set; }

    [JsonPropertyName("id")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    /// <summary>The Linked Art class name, fixed by the C# class.</summary>
    [JsonPropertyName("type")]
    [JsonPropertyOrder(3)]
    public string Type { get; protected set; }

    [JsonPropertyName("_label")]
    [JsonPropertyOrder(10)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; set; }

    /// <summary>P2 has type.</summary>
    [JsonPropertyName("classified_as")]
    [JsonPropertyOrder(100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Type>? ClassifiedAs { get; set; }

    /// <summary>P1 is identified by: <see cref="Name"/>s and <see cref="Identifier"/>s.</summary>
    [JsonPropertyName("identified_by")]
    [JsonPropertyOrder(101)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinkedArtObject>? IdentifiedBy { get; set; }

    /// <summary>
    /// The content of the <see cref="Name"/> classified as AAT Primary Name (directly or via <c>equivalent</c>),
    /// or null; optionally the <c>_label</c> when there is none.
    /// </summary>
    public string? GetPrimaryName(bool fallbackToLabel = false)
    {
        var primary = IdentifiedBy?.OfType<Name>().FirstOrDefault(n =>
            n.ClassifiedAs is { } cas && cas.Exists(ca =>
                ca.Id == Getty.PrimaryNameId ||
                (ca.Equivalent is { } eq && eq.Exists(e => e.Id == Getty.PrimaryNameId))));

        return primary?.Content ?? (fallbackToLabel ? Label : null);
    }

    public override string ToString() => $"{Type}{(Id is null ? "" : " " + Id)}{(Label is null ? "" : " \"" + Label + "\"")}";
}
