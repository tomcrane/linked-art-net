using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>Writes a one-element <c>@context</c> as a plain string and longer ones as an array; reads either form.</summary>
internal sealed class SingleStringJsonLDConverter : JsonConverter<string[]?>
{
    public override string[]? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return [reader.GetString()!];
            case JsonTokenType.StartArray:
                var items = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType != JsonTokenType.String)
                        throw new JsonException("@context array items must be strings.");
                    items.Add(reader.GetString()!);
                }
                return items.ToArray();
            default:
                throw new JsonException($"@context must be a string or an array of strings, not {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string[]? value, JsonSerializerOptions options)
    {
        if (value is null || value.Length == 0)
        {
            writer.WriteNullValue();
            return;
        }
        if (value.Length == 1)
        {
            writer.WriteStringValue(value[0]);
            return;
        }
        writer.WriteStartArray();
        foreach (var s in value) writer.WriteStringValue(s);
        writer.WriteEndArray();
    }
}
