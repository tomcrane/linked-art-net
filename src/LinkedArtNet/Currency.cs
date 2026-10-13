using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>crm:E98 Currency.</summary>
public partial class Currency : Type
{
    public Currency() : base(nameof(Currency))
    {
    }

    public Currency(string id, string? label, string? notation = null) : this()
    {
        Id = id;
        Label = label;
        Notation = notation;
    }

    /// <summary>skos:notation: the ISO 4217 code (e.g. "USD"). Used by the site examples although the 1.0 schema omits it.</summary>
    [JsonPropertyName("notation")]
    [JsonPropertyOrder(100)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notation { get; set; }
}
