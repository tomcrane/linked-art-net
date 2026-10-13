using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// crm:E7 Activity: a referenceable activity with its own URI, such as a provenance activity, an exhibition
/// or a conservation project. Specific embedded activities (<see cref="Production"/>, <see cref="Acquisition"/>,
/// ...) are not subclasses of this; they derive from <see cref="EmbeddedActivity"/>.
/// </summary>
public partial class Activity : Event
{
    public Activity() : base(nameof(Activity))
    {
    }

    /// <summary>P9 consists of: the parts of this activity (acquisitions, payments, moves, ...).</summary>
    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ActivityBase>? Part { get; set; }
}
