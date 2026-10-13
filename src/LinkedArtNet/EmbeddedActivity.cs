using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// An activity without its own URI that is embedded in the thing it is about and may be <c>part_of</c> a
/// larger event: <see cref="Production"/>, <see cref="Creation"/>, <see cref="Acquisition"/>, ... (The schema's
/// own term is "embedded", e.g. <c>EmbeddedAcquisition</c>.)
/// </summary>
[JsonDerivedType(typeof(Creation))]
[JsonDerivedType(typeof(Formation))]
[JsonDerivedType(typeof(Production))]
[JsonDerivedType(typeof(Modification))]
[JsonDerivedType(typeof(Acquisition))]
[JsonDerivedType(typeof(Encounter))]
[JsonDerivedType(typeof(PartRemoval))]
[JsonDerivedType(typeof(AttributeAssignment))]
public abstract class EmbeddedActivity : ActivityBase
{
    protected EmbeddedActivity(string type) : base(type)
    {
    }

    /// <summary>P9i forms part of: the larger event this is a part of (an exhibition, a provenance activity).</summary>
    [JsonPropertyName("part_of")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Event>? PartOf { get; set; }
}
