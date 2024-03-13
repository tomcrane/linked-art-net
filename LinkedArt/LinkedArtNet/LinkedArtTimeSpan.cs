using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class LinkedArtTimeSpan : LinkedArtObject
{
    public LinkedArtTimeSpan() { Type = "TimeSpan"; }

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


    public static LinkedArtTimeSpan FromYear(int year, string? id = null)
    {
        var ts = new LinkedArtTimeSpan()
            .WithId(id)
            .WithLabel($"{year}");
        ts.BeginOfTheBegin = new LinkedArtDate(year, 1, 1);
        if (ts.BeginOfTheBegin.Date.HasValue)
        {
            ts.EndOfTheEnd = new LinkedArtDate(ts.BeginOfTheBegin.Date.Value.AddYears(1).AddSeconds(-1));
        }
        else
        {
            ts.EndOfTheEnd = new LinkedArtDate(year, 12, 31);
        }
        return ts;
    }


    public static LinkedArtTimeSpan FromDay(int year, int month, int day, string? id = null)
    {
        var ts = new LinkedArtTimeSpan().WithId(id);
        ts.BeginOfTheBegin = new LinkedArtDate(year, month, day);
        if (ts.BeginOfTheBegin.Date.HasValue)
        {
            ts.EndOfTheEnd = new LinkedArtDate(ts.BeginOfTheBegin.Date.Value.AddDays(1)); //.AddSeconds(-1));
        }
        else
        {
            // This is actually impossible without a more complex calendar implementation, so we're going to cheat
            var pseudoEnd = new DateTime(2000, month, day).AddDays(1);
            int newYear = pseudoEnd.Year == 2000 ? year : year + 1;
            ts.EndOfTheEnd = new LinkedArtDate(newYear, pseudoEnd.Month, pseudoEnd.Day);
        }
        ts.WithLabel(LinkedArtDateConverter.DayFormat(year, month, day));
        return ts;
    }
}
