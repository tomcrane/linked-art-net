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


    public static LinkedArtTimeSpan FromYear(string id, int year)
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
}
