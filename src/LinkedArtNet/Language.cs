using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E56 Language.</summary>
public partial class Language : Type
{
    public Language() : base(nameof(Language))
    {
    }

    public Language(string id, string? label, string? notation = null) : this()
    {
        Id = id;
        Label = label;
        Notation = notation;
    }

    /// <summary>skos:notation: the language code (e.g. "en"). Used by the site examples although the 1.0 schema omits it.</summary>
    [JsonPropertyName("notation")]
    [JsonPropertyOrder(100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notation { get; set; }
}
