using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LinkedArtNet.Serialization;

/// <summary>
/// The one place that knows how Linked Art JSON-LD is written and read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Writing</b> works with any <see cref="JsonSerializerOptions"/>, because the model carries its own
/// attributes (property names and order, null omission, polymorphism). <see cref="Options"/> adds what
/// attributes cannot: empty collections are omitted, non-ASCII and quotes are not escaped, output is indented.
/// </para>
/// <para>
/// <b>Reading</b> needs <see cref="Options"/> (or <see cref="Tolerant"/>): the <see cref="LinkedArtObjectConverterFactory"/>
/// dispatches on the JSON <c>type</c> property to the right C# class, wherever a <see cref="LinkedArtObject"/>
/// (or a subclass) is expected. <see cref="Options"/> is strict and throws on unknown properties or types;
/// <see cref="Tolerant"/> ignores unknown properties and falls back to the declared class for unknown types,
/// for reading data from other producers.
/// </para>
/// </remarks>
public static class LinkedArtJson
{
    /// <summary>Strict, indented options for reading and writing Linked Art.</summary>
    public static JsonSerializerOptions Options { get; } = Create(strict: true, indented: true);

    /// <summary>Strict, compact (single line) options.</summary>
    public static JsonSerializerOptions Compact { get; } = Create(strict: true, indented: false);

    /// <summary>Indented options that ignore unknown properties and types when reading.</summary>
    public static JsonSerializerOptions Tolerant { get; } = Create(strict: false, indented: true);

    /// <summary>Builds a fresh options instance configured for Linked Art, for callers who need to customise further.</summary>
    public static JsonSerializerOptions Create(bool strict = true, bool indented = true)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitEmptyCollections } },
        };
        options.Converters.Add(new LinkedArtObjectConverterFactory(strict));
        return options;
    }

    public static string Serialize(LinkedArtObject value, bool indented = true)
        => JsonSerializer.Serialize(value, value.GetType(), indented ? Options : Compact);

    public static void Serialize(Stream stream, LinkedArtObject value, bool indented = true)
        => JsonSerializer.Serialize(stream, value, value.GetType(), indented ? Options : Compact);

    /// <summary>Reads a Linked Art document as <typeparamref name="T"/>, dispatching on <c>type</c> to the concrete class.</summary>
    public static T Deserialize<T>(string json, bool strict = true) where T : LinkedArtObject
        => JsonSerializer.Deserialize<T>(json, strict ? Options : Tolerant)
           ?? throw new JsonException("The document was JSON null.");

    public static T Deserialize<T>(Stream json, bool strict = true) where T : LinkedArtObject
        => JsonSerializer.Deserialize<T>(json, strict ? Options : Tolerant)
           ?? throw new JsonException("The document was JSON null.");

    /// <summary>Reads any Linked Art document; the result's runtime class is chosen from its <c>type</c>.</summary>
    public static LinkedArtObject Parse(string json, bool strict = true) => Deserialize<LinkedArtObject>(json, strict);

    /// <summary>A canonical compact form (no indentation, same property order as the model) suitable for hashing or diffing.</summary>
    public static string Canonical(LinkedArtObject value) => Serialize(value, indented: false);

    private static void OmitEmptyCollections(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
        foreach (var property in typeInfo.Properties)
        {
            if (property.PropertyType != typeof(string) && typeof(ICollection).IsAssignableFrom(property.PropertyType))
            {
                property.ShouldSerialize = static (_, value) => value is ICollection { Count: > 0 };
            }
        }
    }
}
