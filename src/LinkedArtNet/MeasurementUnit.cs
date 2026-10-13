using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

public class MeasurementUnit : LinkedArtObject
{

    public MeasurementUnit() { Type = nameof(MeasurementUnit); }

    // Utility instances
    public static MeasurementUnit Centimeters => _centimeters;

    private static MeasurementUnit _centimeters = new MeasurementUnit
    {
        Id = $"{Getty.Aat}300379098",
        Label = "centimeters"
    };


    public static MeasurementUnit Millimeters => _millimeters;

    private static MeasurementUnit _millimeters = new MeasurementUnit
    {
        Id = $"{Getty.Aat}300379099", // check
        Label = "millimeters"
    };


    public static MeasurementUnit Kilobytes => _kilobytes;

    private static MeasurementUnit _kilobytes = new MeasurementUnit
    {
        Id = $"{Getty.Aat}300265870",
        Label = "kilobytes"
    };

}
