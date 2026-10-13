using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LinkedArtNet.Serialization;

/// <summary>
/// Reads any <see cref="LinkedArtObject"/> (or subclass) by dispatching on the JSON <c>type</c> property to the
/// concrete model class, then populating that class's properties. Writing is delegated to the ordinary
/// serializer (the model's attributes already describe the output), so adding this converter to options changes
/// reading only.
/// </summary>
public sealed class LinkedArtObjectConverterFactory(bool strict = true) : JsonConverterFactory
{
    /// <summary>When true, unknown properties and unknown or incompatible <c>type</c> values throw; when false they are ignored.</summary>
    public bool Strict { get; } = strict;

    public override bool CanConvert(System.Type typeToConvert) => typeof(LinkedArtObject).IsAssignableFrom(typeToConvert);

    public override JsonConverter CreateConverter(System.Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert), this)!;

    // Options used to write, and to read property metadata: the same options minus this factory, so the
    // ordinary object (de)serialiser is used for the concrete class itself while nested Linked Art values
    // still come back through the factory.
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> PlainOptions = new();

    internal static JsonSerializerOptions Plain(JsonSerializerOptions options) =>
        PlainOptions.GetValue(options, static o =>
        {
            var plain = new JsonSerializerOptions(o);
            for (var i = plain.Converters.Count - 1; i >= 0; i--)
            {
                if (plain.Converters[i] is LinkedArtObjectConverterFactory) plain.Converters.RemoveAt(i);
            }
            return plain;
        });

    private sealed class Converter<T>(LinkedArtObjectConverterFactory factory) : JsonConverter<T> where T : LinkedArtObject
    {
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, value.GetType(), Plain(options));

        public override T? Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"Expected an object for {typeToConvert.Name} but found {reader.TokenType}.");

            var element = JsonElement.ParseValue(ref reader);
            var declaredTypeName = element.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String
                ? typeProp.GetString()
                : null;

            var concrete = ChooseClass(typeToConvert, declaredTypeName);
            var instance = (LinkedArtObject)Activator.CreateInstance(concrete)!;
            var typeInfo = Plain(options).GetTypeInfo(concrete);
            var properties = typeInfo.Properties.ToDictionary(p => p.Name, StringComparer.Ordinal);

            foreach (var property in element.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "type":
                        continue; // fixed by the class
                    case "@context":
                        instance.Context = ReadContext(property.Value);
                        continue;
                }

                if (!properties.TryGetValue(property.Name, out var info) || info.Set is null)
                {
                    if (factory.Strict)
                        throw new JsonException($"Unknown property '{property.Name}' on {concrete.Name} (JSON type '{declaredTypeName ?? "none"}').");
                    continue;
                }

                try
                {
                    info.Set(instance, property.Value.Deserialize(info.PropertyType, options));
                }
                catch (JsonException ex)
                {
                    throw new JsonException($"{concrete.Name}.{property.Name}: {ex.Message}", ex);
                }
            }

            return (T)instance;
        }

        private System.Type ChooseClass(System.Type declared, string? typeName)
        {
            if (typeName is not null && LinkedArtTypes.TryResolve(typeName, out var fromName))
            {
                if (declared.IsAssignableFrom(fromName)) return fromName;
                if (factory.Strict)
                    throw new JsonException($"JSON type '{typeName}' is not a {declared.Name}.");
            }
            else if (typeName is not null && factory.Strict)
            {
                throw new JsonException($"Unknown Linked Art type '{typeName}'.");
            }

            if (!declared.IsAbstract) return declared;
            throw new JsonException(typeName is null
                ? $"Cannot read a {declared.Name} without a 'type' property."
                : $"Cannot read JSON type '{typeName}' as the abstract class {declared.Name}.");
        }

        private static string[]? ReadContext(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => [value.GetString()!],
            JsonValueKind.Array => value.EnumerateArray().Select(e => e.GetString()!).ToArray(),
            JsonValueKind.Null => null,
            _ => throw new JsonException("@context must be a string or an array of strings."),
        };
    }
}
