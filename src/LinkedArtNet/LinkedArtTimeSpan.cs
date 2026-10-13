using System.Globalization;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E52 Time-Span. See <see cref="LinkedArtDate"/> for the date conventions.</summary>
public class LinkedArtTimeSpan : LinkedArtObject
{
    public LinkedArtTimeSpan() : base("TimeSpan")
    {
    }

    [JsonPropertyName("begin_of_the_begin")]
    [JsonPropertyOrder(121)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedArtDate? BeginOfTheBegin { get; set; }

    [JsonPropertyName("end_of_the_begin")]
    [JsonPropertyOrder(122)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedArtDate? EndOfTheBegin { get; set; }

    [JsonPropertyName("begin_of_the_end")]
    [JsonPropertyOrder(123)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedArtDate? BeginOfTheEnd { get; set; }

    [JsonPropertyName("end_of_the_end")]
    [JsonPropertyOrder(124)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LinkedArtDate? EndOfTheEnd { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(160)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dimension? Duration { get; set; }

    /// <summary>A span covering a whole year, labelled with the year.</summary>
    public static LinkedArtTimeSpan FromYear(long year, string? id = null)
    {
        var ts = new LinkedArtTimeSpan()
            .WithId(id)
            .WithLabel(year.ToString(CultureInfo.InvariantCulture));
        ts.BeginOfTheBegin = LinkedArtDate.StartOfYear(year);
        ts.EndOfTheEnd = LinkedArtDate.EndOfYear(year);
        return ts;
    }

    /// <summary>A span covering a whole day.</summary>
    public static LinkedArtTimeSpan FromDay(long year, int month, int day, string? id = null)
    {
        var begin = new LinkedArtDate(year, month, day);
        // TODO (plan: Phase 1 step 6): factories should not set labels by default; this keeps the
        // legacy label form until the examples are migrated.
        var ts = new LinkedArtTimeSpan()
            .WithId(id)
            .WithLabel(string.Create(CultureInfo.InvariantCulture, $"{begin.Year:D4}-{begin.Month:D2}-{begin.Day:D2}T00:00:00"));
        ts.BeginOfTheBegin = begin;
        ts.EndOfTheEnd = begin.LastSecondOfDay();
        return ts;
    }
}
