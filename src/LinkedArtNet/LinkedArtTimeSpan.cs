using System.Globalization;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E52 Time-Span. See <see cref="LinkedArtDate"/> for the date conventions.</summary>
public partial class LinkedArtTimeSpan : LinkedArtObject
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

    /// <summary>A span covering a whole year. No <c>_label</c> is set; add one with <c>WithLabel</c> if wanted.</summary>
    public static LinkedArtTimeSpan FromYear(long year, string? id = null) => new LinkedArtTimeSpan
    {
        BeginOfTheBegin = LinkedArtDate.StartOfYear(year),
        EndOfTheEnd = LinkedArtDate.EndOfYear(year),
    }.WithId(id);

    /// <summary>A span covering a whole month.</summary>
    public static LinkedArtTimeSpan FromMonth(long year, int month, string? id = null) => new LinkedArtTimeSpan
    {
        BeginOfTheBegin = LinkedArtDate.StartOfMonth(year, month),
        EndOfTheEnd = LinkedArtDate.EndOfMonth(year, month),
    }.WithId(id);

    /// <summary>A span covering a whole day.</summary>
    public static LinkedArtTimeSpan FromDay(long year, int month, int day, string? id = null)
    {
        var begin = new LinkedArtDate(year, month, day);
        return new LinkedArtTimeSpan { BeginOfTheBegin = begin, EndOfTheEnd = begin.LastSecondOfDay() }.WithId(id);
    }

    /// <summary>A span from the start of one year to the end of another (inclusive).</summary>
    public static LinkedArtTimeSpan FromYears(long firstYear, long lastYear, string? id = null) => new LinkedArtTimeSpan
    {
        BeginOfTheBegin = LinkedArtDate.StartOfYear(firstYear),
        EndOfTheEnd = LinkedArtDate.EndOfYear(lastYear),
    }.WithId(id);

    /// <summary>A span between two instants; <paramref name="end"/> is inclusive, so pass the last second wanted.</summary>
    public static LinkedArtTimeSpan Between(LinkedArtDate begin, LinkedArtDate end, string? id = null)
    {
        if (end < begin) throw new ArgumentException("The end of a time span cannot be before its beginning.", nameof(end));
        return new LinkedArtTimeSpan { BeginOfTheBegin = begin, EndOfTheEnd = end }.WithId(id);
    }

    /// <summary>True when every date present is in order: begin_of_the_begin &lt;= end_of_the_begin &lt;= begin_of_the_end &lt;= end_of_the_end.</summary>
    public bool IsOrdered()
    {
        LinkedArtDate? previous = null;
        foreach (var date in new[] { BeginOfTheBegin, EndOfTheBegin, BeginOfTheEnd, EndOfTheEnd })
        {
            if (date is null) continue;
            if (previous is not null && date < previous) return false;
            previous = date;
        }
        return true;
    }
}
