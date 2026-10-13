using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet;

/// <summary>
/// Reads and writes <see cref="LinkedArtDate"/> as the <c>xsd:dateTime</c> strings Linked Art uses.
/// Reading is tolerant (see <see cref="LinkedArtDate.TryParse(ReadOnlySpan{char}, out LinkedArtDate)"/>);
/// writing always produces <c>yyyy-MM-ddTHH:mm:ssZ</c>.
/// </summary>
public sealed class LinkedArtDateConverter : JsonConverter<LinkedArtDate>
{
    public override LinkedArtDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string for a Linked Art dateTime but found {reader.TokenType}.");

        var text = reader.GetString();
        if (!LinkedArtDate.TryParse(text, null, out var date))
            throw new JsonException($"'{text}' is not a valid Linked Art dateTime.");
        return date;
    }

    public override void Write(Utf8JsonWriter writer, LinkedArtDate value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
