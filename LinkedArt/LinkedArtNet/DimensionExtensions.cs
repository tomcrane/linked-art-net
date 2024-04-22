using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

public static class DimensionExtensions
{
    public static HumanMadeObject WithHeightDimension(this HumanMadeObject hmObj, 
        string id, double value, MeasurementUnit unit)
    {
        var height = new Dimension().WithId(id).WithClassifiedAs("300055644", "height");
        height.Value = value;
        height.Unit = unit;
        hmObj.Dimension ??= [];
        hmObj.Dimension.Add(height);
        return hmObj;
    }


    public static HumanMadeObject WithWidthDimension(this HumanMadeObject hmObj,
        string id, double value, MeasurementUnit unit)
    {
        var width = new Dimension().WithId(id).WithClassifiedAs("300055647", "width");
        width.Value = value;
        width.Unit = unit;
        hmObj.Dimension ??= [];
        hmObj.Dimension.Add(width);
        return hmObj;
    }

    public static LinkedArtTimeSpan WithDaysDimension(this LinkedArtTimeSpan ts, double value, string? id = null)
    {
        var days = new Dimension().WithId(id);
        days.Value = value;
        days.Unit = new MeasurementUnit().WithId($"{Getty.Aat}300379242").WithLabel("days");
        ts.Duration = days;
        return ts;
    }
}
