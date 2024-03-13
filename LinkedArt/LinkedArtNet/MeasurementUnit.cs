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
        Label = "centimetres"
    };


    public static MeasurementUnit Millimetres => _millimetres;

    private static MeasurementUnit _millimetres = new MeasurementUnit
    {
        Id = $"{Getty.Aat}300379099", // check
        Label = "millimetres"
    };

}
