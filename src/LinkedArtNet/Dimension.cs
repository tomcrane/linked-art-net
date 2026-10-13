using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E54 Dimension. A measured value with a unit (height, width, file size, colour, count, duration).</summary>
public class Dimension : LinkedArtObject
{
    public Dimension() : base(nameof(Dimension))
    {
    }

    [JsonPropertyName("value")]
    [JsonPropertyOrder(31)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Value { get; set; }

    [JsonPropertyName("unit")]
    [JsonPropertyOrder(32)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MeasurementUnit? Unit { get; set; }

    [JsonPropertyName("lower_value_limit")]
    [JsonPropertyOrder(33)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? LowerValueLimit { get; set; }

    [JsonPropertyName("upper_value_limit")]
    [JsonPropertyOrder(34)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? UpperValueLimit { get; set; }

    /// <summary>P141i was assigned by: who measured, and how.</summary>
    [JsonPropertyName("assigned_by")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AttributeAssignment>? AssignedBy { get; set; }
}
