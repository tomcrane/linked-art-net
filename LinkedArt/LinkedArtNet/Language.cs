using System.Text.Json.Serialization;

namespace LinkedArtNet;

public class Language : LinkedArtObject
{
    public Language() { Type = nameof(Language); }

    public Language(string label, string? notation = null)
    {
        Type = nameof(Language);
        Label = label;
        Notation = notation;
    }

    [JsonPropertyName("notation")]
    [JsonPropertyOrder(100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notation { get; set; }
}
