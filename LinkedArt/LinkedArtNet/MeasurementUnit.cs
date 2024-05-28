using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

public class MeasurementUnit : LinkedArtObject
{

    public MeasurementUnit() { Type = nameof(MeasurementUnit); }

    // Utility instances
    public static MeasurementUnit Centimetres => _centimetres;

    private static MeasurementUnit _centimetres = new MeasurementUnit
    {
        Id = $"{Getty.Aat}300379098",
        Label = "centimeters"
    };


    public static MeasurementUnit Millimetres => _millimetres;

    private static MeasurementUnit _millimetres = new MeasurementUnit
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
