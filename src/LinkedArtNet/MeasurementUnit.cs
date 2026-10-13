using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

/// <summary>crm:E58 Measurement Unit.</summary>
public partial class MeasurementUnit : Type
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
    public static MeasurementUnit Centimeters => Aat.Centimeters;
    public static MeasurementUnit Millimeters => Aat.OfUnit("300379099", "millimeters");
    public static MeasurementUnit Kilobytes => Aat.Kilobytes;
    public static MeasurementUnit Days => Aat.Days;
}
