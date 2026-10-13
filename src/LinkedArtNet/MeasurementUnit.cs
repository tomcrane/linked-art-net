using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

/// <summary>crm:E58 Measurement Unit.</summary>
public class MeasurementUnit : Type
{
    public MeasurementUnit() : base(nameof(MeasurementUnit))
    {
    }

    public MeasurementUnit(string id, string? label) : this()
    {
        Id = id;
        Label = label;
    }

    // Common units. Each access returns a new instance so callers cannot mutate a shared object.
    public static MeasurementUnit Centimeters => new($"{Getty.Aat}300379098", "centimeters");
    public static MeasurementUnit Millimeters => new($"{Getty.Aat}300379099", "millimeters");
    public static MeasurementUnit Kilobytes => new($"{Getty.Aat}300265870", "kilobytes");
    public static MeasurementUnit Days => new($"{Getty.Aat}300379242", "days");
}
