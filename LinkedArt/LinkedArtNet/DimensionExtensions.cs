using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

public static class DimensionExtensions
{
    public static HumanMadeObject WithHeightDimension(this HumanMadeObject hmObj,
        double value, MeasurementUnit unit, string? displayTitle = null)
    {
        return hmObj.WithHeightDimension(null, value, unit, displayTitle);
    }

    public static HumanMadeObject WithHeightDimension(this HumanMadeObject hmObj, 
        string? id, double value, MeasurementUnit unit, string? displayTitle = null)
    {
        var height = new Dimension().WithId(id).WithClassifiedAs(Getty.AatType("Height", "300055644"));
        height.Value = value;
        height.Unit = unit;
        hmObj.Dimension ??= [];
        hmObj.Dimension.Add(height);
        if (!string.IsNullOrWhiteSpace(displayTitle))
        {
            height.IdentifiedBy = [
                new LinkedArtObject(Types.Name)
                    .WithContent(displayTitle)
                    .WithClassifiedAs(Getty.DisplayTitle)
            ];
        }
        return hmObj;
    }

    public static HumanMadeObject WithWidthDimension(this HumanMadeObject hmObj,
        double value, MeasurementUnit unit, string? displayTitle = null)
    {
        return hmObj.WithWidthDimension(null, value, unit, displayTitle);
    }

    public static HumanMadeObject WithWidthDimension(this HumanMadeObject hmObj,
        string? id, double value, MeasurementUnit unit, string? displayTitle = null)
    {
        var width = new Dimension().WithId(id).WithClassifiedAs(Getty.AatType("Width", "300055647"));
        width.Value = value;
        width.Unit = unit;
        hmObj.Dimension ??= [];
        hmObj.Dimension.Add(width);
        if (!string.IsNullOrWhiteSpace(displayTitle))
        {
            width.IdentifiedBy = [
                new LinkedArtObject(Types.Name)
                    .WithContent(displayTitle)
                    .WithClassifiedAs(Getty.DisplayTitle)
            ];
        }
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
