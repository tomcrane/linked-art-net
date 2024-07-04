using System.Runtime.CompilerServices;
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

    public static LinkedArtTimeSpan FromYear(int year)
    {
        return FromYear(year, null, true);
    }
    public static LinkedArtTimeSpan FromYear(int year, bool serializeWithTimespan)
    {
        return FromYear(year, null, serializeWithTimespan);
    }

    public static LinkedArtTimeSpan FromYear(int year, string? id = null, bool serializeWithTimespan = true)
    {
        var ts = new LinkedArtTimeSpan()
            .WithId(id)
            .WithLabel($"{year}");
        ts.BeginOfTheBegin = new LinkedArtDate(year, 1, 1, serializeWithTimespan);
        if (ts.BeginOfTheBegin.DtOffset.HasValue)
        {
            ts.EndOfTheEnd = new LinkedArtDate(
                ts.BeginOfTheBegin.DtOffset.Value.AddYears(1).AddSeconds(-1), serializeWithTimespan);
        }
        else
        {
            ts.EndOfTheEnd = new LinkedArtDate(year, 12, 31, serializeWithTimespan);
        }
        return ts;
    }

    public static LinkedArtTimeSpan FromDay(int year, int month, int day)
    {
        return FromDay(year, month, day, null, true);
    }

    public static LinkedArtTimeSpan FromDay(int year, int month, int day, bool serializeWithTimespan = true)
    {
        return FromDay(year, month, day, null, serializeWithTimespan);
    }

    public static LinkedArtTimeSpan FromDay(int year, int month, int day, string? id = null, bool serializeWithTimespan = true)
    {
        var ts = new LinkedArtTimeSpan().WithId(id);
        ts.BeginOfTheBegin = new LinkedArtDate(year, month, day);
        if (ts.BeginOfTheBegin.DtOffset.HasValue)
        {
            ts.EndOfTheEnd = new LinkedArtDate(
                ts.BeginOfTheBegin.DtOffset.Value.AddDays(1).AddSeconds(-1), serializeWithTimespan);
        }
        else
        {
            // This is actually impossible without a more complex calendar implementation, so we're going to cheat
            var pseudoEnd = new DateTime(2000, month, day).AddDays(1);
            int newYear = pseudoEnd.Year == 2000 ? year : year + 1;
            ts.EndOfTheEnd = new LinkedArtDate(newYear, pseudoEnd.Month, pseudoEnd.Day, serializeWithTimespan);
        }
        ts.WithLabel(LinkedArtDateConverter.DayFormat(year, month, day));
        return ts;
    }

    public static void AsBornAndDied(LinkedArtTimeSpan tsRange, Person person)
    {
        // given a time span derived from a date range, 

    }

}
