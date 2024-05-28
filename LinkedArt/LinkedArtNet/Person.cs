using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class Person : Actor
{
    public Person() { Type = nameof(Person); }


    [JsonPropertyName("born")]
    [JsonPropertyOrder(120)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Activity? Born { get; set; }


    [JsonPropertyName("died")]
    [JsonPropertyOrder(121)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Activity? Died { get; set; }
}
