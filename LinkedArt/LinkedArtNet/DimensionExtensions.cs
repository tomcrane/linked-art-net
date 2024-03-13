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
}
