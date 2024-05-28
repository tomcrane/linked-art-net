using LinkedArtNet.Vocabulary;
using System.Drawing;
using System.Reflection.Emit;

namespace LinkedArtNet;

public static class DimensionExtensions
{
    public static LinkedArtObject WithFileSize(this LinkedArtObject laObj, double value, MeasurementUnit unit, string? label = null)
    {
        return laObj.WithFileSize(null, value, unit, label);
    }

    public static LinkedArtObject WithFileSize(this LinkedArtObject laObj, string? id, double value, MeasurementUnit unit, string? label = null)
    {
        var fileSize = new Dimension().WithId(id).WithClassifiedAs(Getty.AatType("File Size", "300265863"));
        fileSize.Value = value;
        fileSize.Unit = unit;
        laObj.Dimension ??= [];
        laObj.Dimension.Add(fileSize);

        // Example doesn't do this...
        //if (!string.IsNullOrWhiteSpace(displayTitle))
        //{
        //    fileSize.IdentifiedBy = [
        //        new Name(displayTitle).WithClassifiedAs(Getty.DisplayTitle)
        //    ];
        //}

        // ...but does this instead:
        if (!string.IsNullOrWhiteSpace(label))
        {
            fileSize.Label = label;
        }
        return laObj;

    }

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
                new Name(displayTitle).WithClassifiedAs(Getty.DisplayTitle)
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
                new Name(displayTitle).WithClassifiedAs(Getty.DisplayTitle)
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

    public static HumanMadeObject WithRgbColor(this HumanMadeObject hmObj, string rgbColor, 
        LinkedArtObject aatColor, string? label = null)
    {
        var rgbInt = Convert.ToInt32(rgbColor.Replace("#", ""), 16);
        hmObj.WithRgbColor(rgbInt, aatColor, label);
        hmObj.Dimension.Last().IdentifiedBy = [ new LinkedArtObject(Types.Identifier).WithContent(rgbColor) ];
        return hmObj;
    }


    public static HumanMadeObject WithRgbColor(this HumanMadeObject hmObj, int rgbColor, 
        LinkedArtObject aatColor, string? label = null)
    {
        label ??= aatColor.Label;
        var color = new Dimension().WithLabel(label!);
        color.ClassifiedAs = [
            Getty.AatType("Color", "300080438"),
            aatColor
        ];
        color.Value = rgbColor;
        color.Unit = new MeasurementUnit().WithId($"{Getty.Aat}300266239").WithLabel("rgb");

        hmObj.Dimension ??= [];
        hmObj.Dimension.Add(color);

        return hmObj;
    }

    public static HumanMadeObject WithShape(this HumanMadeObject hmObj, LinkedArtObject aatShape)
    {
        return hmObj.WithClassifiedAs(aatShape, Getty.Shape);
    }

    public static HumanMadeObject WithCount(this HumanMadeObject hmObj, int value)
    {
        var count = new Dimension()
            .WithClassifiedAs(Getty.AatType("Count", "300404433"));
        count.Value = value;
        count.Unit = new MeasurementUnit().WithId($"{Getty.Aat}300241583").WithLabel("Components");

        hmObj.Dimension ??= [];
        hmObj.Dimension.Add(count);

        return hmObj;
    }
}
