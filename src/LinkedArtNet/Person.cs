using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E21 Person.</summary>
public class Person : Actor
{
    public Person() : base(nameof(Person))
    {
    }

    [JsonPropertyName("born")]
    [JsonPropertyOrder(122)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Birth? Born { get; set; }

    [JsonPropertyName("died")]
    [JsonPropertyOrder(123)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Death? Died { get; set; }
}
