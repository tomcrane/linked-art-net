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
}
