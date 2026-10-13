using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

/// <summary>Fluent helpers for <c>dimension</c> and <c>duration</c>.</summary>
public static class DimensionExtensions
{
    public static T WithDimension<T>(this T laObj, Dimension dimension) where T : LinkedArtObject, IHasDimensions
    {
        laObj.Dimension ??= [];
        laObj.Dimension.Add(dimension);
        return laObj;
    }

    /// <summary>Adds a dimension classified as <paramref name="dimensionType"/>, optionally with an id and a display title Name.</summary>
    public static T WithDimension<T>(this T laObj, Type dimensionType, double value, MeasurementUnit unit,
        string? displayTitle = null, string? id = null) where T : LinkedArtObject, IHasDimensions
    {
        var dimension = new Dimension { Value = value, Unit = unit }.WithId(id).WithClassifiedAs(dimensionType);
        if (!string.IsNullOrWhiteSpace(displayTitle))
        {
            dimension.IdentifiedBy = [new Name(displayTitle).WithClassifiedAs(Getty.DisplayTitle)];
        }
        return laObj.WithDimension(dimension);
    }

    public static T WithFileSize<T>(this T laObj, double value, MeasurementUnit unit, string? label = null) where T : LinkedArtObject, IHasDimensions
        => laObj.WithFileSize(null, value, unit, label);

    public static T WithFileSize<T>(this T laObj, string? id, double value, MeasurementUnit unit, string? label = null) where T : LinkedArtObject, IHasDimensions
    {
        var fileSize = new Dimension { Value = value, Unit = unit }.WithId(id).WithClassifiedAs(Getty.FileSize);
        if (!string.IsNullOrWhiteSpace(label))
        {
            fileSize.Label = label;
        }
        return laObj.WithDimension(fileSize);
    }

    public static HumanMadeObject WithHeightDimension(this HumanMadeObject hmObj, double value, MeasurementUnit unit, string? displayTitle = null)
        => hmObj.WithDimension(Getty.Height, value, unit, displayTitle);

    public static HumanMadeObject WithHeightDimension(this HumanMadeObject hmObj, string? id, double value, MeasurementUnit unit, string? displayTitle = null)
        => hmObj.WithDimension(Getty.Height, value, unit, displayTitle, id);

    public static HumanMadeObject WithWidthDimension(this HumanMadeObject hmObj, double value, MeasurementUnit unit, string? displayTitle = null)
        => hmObj.WithDimension(Getty.Width, value, unit, displayTitle);

    public static HumanMadeObject WithWidthDimension(this HumanMadeObject hmObj, string? id, double value, MeasurementUnit unit, string? displayTitle = null)
        => hmObj.WithDimension(Getty.Width, value, unit, displayTitle, id);

    public static LinkedArtTimeSpan WithDaysDimension(this LinkedArtTimeSpan ts, double value, string? id = null)
    {
        ts.Duration = new Dimension { Value = value, Unit = MeasurementUnit.Days }.WithId(id);
        return ts;
    }

    /// <summary>Adds a colour dimension from a hex string such as "#FF0000", recording the hex as an Identifier.</summary>
    public static HumanMadeObject WithRgbColor(this HumanMadeObject hmObj, string rgbColor, Type aatColor, string? label = null)
    {
        var rgbInt = Convert.ToInt32(rgbColor.Replace("#", ""), 16);
        hmObj.WithRgbColor(rgbInt, aatColor, label);
        hmObj.Dimension![^1].IdentifiedBy = [new Identifier(rgbColor)];
        return hmObj;
    }

    public static HumanMadeObject WithRgbColor(this HumanMadeObject hmObj, int rgbColor, Type aatColor, string? label = null)
    {
        var color = new Dimension
        {
            Label = label ?? aatColor.Label,
            ClassifiedAs = [Getty.Color, aatColor],
            Value = rgbColor,
            Unit = Getty.AatUnit("rgb", "300266239"),
        };
        return hmObj.WithDimension(color);
    }

    public static HumanMadeObject WithShape(this HumanMadeObject hmObj, Type aatShape) => hmObj.WithClassifiedAs(aatShape, Getty.Shape);

    public static HumanMadeObject WithComponentCount(this HumanMadeObject hmObj, int value)
    {
        var count = new Dimension { Value = value, Unit = Getty.AatUnit("Components", "300241583") }.WithClassifiedAs(Getty.Count);
        return hmObj.WithDimension(count);
    }

    public static T WithPageCount<T>(this T laObj, int value, string? label = null) where T : LinkedArtObject, IHasDimensions
    {
        var count = new Dimension { Value = value, Unit = Getty.AatUnit("Pages", "300194222") }
            .WithLabel(string.IsNullOrWhiteSpace(label) ? value.ToString() : label)
            .WithClassifiedAs(Getty.AatType("Count Of", "300404433"));
        return laObj.WithDimension(count);
    }
}
