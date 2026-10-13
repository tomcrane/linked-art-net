using System.Text.Json.Serialization;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

/// <summary>crm:E42 Identifier. An accession number, system number, sort value, contact point, ...</summary>
public class Identifier : LinkedArtObject, IHasContent, IHasStatements
{
    public Identifier() : base(nameof(Identifier))
    {
    }

    public Identifier(string content) : this()
    {
        Content = content;
    }

    [JsonPropertyName("content")]
    [JsonPropertyOrder(10)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonPropertyName("referred_to_by")]
    [JsonPropertyOrder(105)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LinguisticObject>? ReferredToBy { get; set; }

    /// <summary>P37i was assigned by: who assigned this identifier, and how.</summary>
    [JsonPropertyName("assigned_by")]
    [JsonPropertyOrder(110)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AttributeAssignment>? AssignedBy { get; set; }

    [JsonPropertyName("part")]
    [JsonPropertyOrder(130)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Identifier>? Part { get; set; }

    /// <summary>A sort value identifier, optionally recording what influenced the ordering.</summary>
    public static Identifier SortValue(string value, LinkedArtObject? influencedBy = null)
    {
        var identifier = new Identifier(value).WithClassifiedAs(Aat.SortValue);
        if (influencedBy != null)
        {
            identifier.AssignedBy = [new AttributeAssignment { InfluencedBy = [influencedBy] }];
        }
        return identifier;
    }
}
