namespace LinkedArtNet;

public class MeasurementUnit : LinkedArtObject
{

    public MeasurementUnit() { Type = nameof(MeasurementUnit); }

    // Utility instances
    public static MeasurementUnit Centimetres => _centimetres;

    private static MeasurementUnit _centimetres = new MeasurementUnit
    {
        Id = "300379098",
        Label = "centimetres"
    };

}
