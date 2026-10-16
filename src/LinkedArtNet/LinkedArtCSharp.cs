using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet;

/// <summary>
/// Writes a Linked Art object as the C# that builds it without the fluent helpers: object initialisers, collection
/// expressions, constructor arguments, and the vocabulary constants where a term is one the registry knows. The
/// counterpart of <see cref="Serialization.LinkedArtJson.Serialize(LinkedArtObject, bool)"/>: for the same object the
/// written code builds an object with the same JSON.
/// </summary>
public static class LinkedArtCSharp
{
    /// <summary>A statement assigning the object to <paramref name="variable"/>.</summary>
    public static string Write(LinkedArtObject value, string variable = "example") =>
        $"var {variable} = {Expression(value)};";

    /// <summary>The object as an expression.</summary>
    public static string Expression(LinkedArtObject value)
    {
        var sb = new StringBuilder();
        Append(sb, value, 0);
        return sb.ToString();
    }

    // ---- objects --------------------------------------------------------------------------------------------------

    private static void Append(StringBuilder sb, LinkedArtObject value, int indent)
    {
        // Convenience subclasses such as IIIFManifest only have constructors that build a shape; the plain form is the
        // nearest class a reader can write with an initialiser, which carries the same properties and JSON type.
        var type = value.GetType();
        while (type.GetConstructor(System.Type.EmptyTypes) is null && type.BaseType is { IsAbstract: false } baseType) type = baseType;
        var properties = SettableProperties(type).Where(p => p.GetValue(value) is not null).ToList();

        if (Vocabulary(value, properties) is { } constant) { sb.Append(constant); return; }

        // Constructor arguments where the class has a natural pair, so the rest of the initialiser stays short.
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        sb.Append("new ").Append(type.Name);
        if (value is Type term && term.Id is not null)
        {
            sb.Append('(').Append(Literal(term.Id)).Append(", ").Append(term.Label is null ? "null" : Literal(term.Label));
            if (value is Language { Notation: { } notation }) { sb.Append(", ").Append(Literal(notation)); consumed.Add(nameof(Language.Notation)); }
            sb.Append(')');
            consumed.Add(nameof(LinkedArtObject.Id)); consumed.Add(nameof(LinkedArtObject.Label));
        }
        else if (value is Name { Content: { } nameContent }) { sb.Append('(').Append(Literal(nameContent)).Append(')'); consumed.Add(nameof(Name.Content)); }
        else if (value is Identifier { Content: { } idContent }) { sb.Append('(').Append(Literal(idContent)).Append(')'); consumed.Add(nameof(Identifier.Content)); }

        var remaining = properties.Where(p => !consumed.Contains(p.Name)).ToList();
        if (remaining.Count == 0) { if (consumed.Count == 0) sb.Append("()"); return; }

        var pad = new string(' ', indent);
        sb.Append('\n').Append(pad).Append('{');
        foreach (var property in remaining)
        {
            var rendered = new StringBuilder();
            AppendValue(rendered, property.GetValue(value)!, indent + 4);
            // A value that opens on its own line (an initialiser or a long list) follows "=" directly, with no trailing space.
            sb.Append('\n').Append(pad).Append("    ").Append(property.Name).Append(rendered[0] == '\n' ? " =" : " = ").Append(rendered).Append(',');
        }
        sb.Append('\n').Append(pad).Append('}');
    }

    private static void AppendValue(StringBuilder sb, object value, int indent)
    {
        switch (value)
        {
            case string s: sb.Append(Literal(s)); break;
            case string[] strings: sb.Append('[').AppendJoin(", ", strings.Select(Literal)).Append(']'); break;
            case double d: sb.Append(Number(d)); break;
            case LinkedArtDate date: sb.Append(Date(date)); break;
            case LinkedArtObject obj: Append(sb, obj, indent); break;
            case System.Collections.IEnumerable list: AppendList(sb, list.Cast<object>().ToList(), indent); break;
            default: throw new NotSupportedException($"LinkedArtCSharp cannot write a value of type {value.GetType().Name}.");
        }
    }

    private static void AppendList(StringBuilder sb, List<object> items, int indent)
    {
        if (items.Count == 0) { sb.Append("[]"); return; }
        var pad = new string(' ', indent);

        // Short lists of constants and references stay on one line; anything with structure gets one item per line.
        var parts = items.Select(item => { var part = new StringBuilder(); AppendValue(part, item, indent + 4); return part.ToString(); }).ToList();
        if (parts.All(p => !p.Contains('\n')) && parts.Sum(p => p.Length) + indent < 100)
        {
            sb.Append('[').AppendJoin(", ", parts).Append(']');
            return;
        }
        sb.Append('\n').Append(pad).Append('[');
        foreach (var part in parts) sb.Append('\n').Append(pad).Append("    ").Append(part).Append(',');
        sb.Append('\n').Append(pad).Append(']');
    }

    // ---- vocabulary constants ---------------------------------------------------------------------------------------

    private static readonly Lazy<Dictionary<(System.Type, string, string?), string>> AatByIdAndLabel = new(() =>
    {
        var map = new Dictionary<(System.Type, string, string?), string>();
        foreach (var term in Aat.Terms)
        {
            var kind = term.Kind switch
            {
                VocabularyKind.Material => typeof(Material), VocabularyKind.Language => typeof(Language),
                VocabularyKind.Currency => typeof(Currency), VocabularyKind.MeasurementUnit => typeof(MeasurementUnit),
                _ => typeof(Type),
            };
            map.TryAdd((kind, Aat.Base + term.Id, term.Label), "Aat." + term.Name);
        }
        return map;
    });

    /// <summary>
    /// <c>Aat.Painting</c>, <c>Tgn.Place("7008038", "Paris")</c>, <c>Ulan.Person(...)</c> when the object is nothing
    /// but that reference: an id and a label (and a notation for a language). Anything with further properties is
    /// written out in full, so the reader sees every property that was set.
    /// </summary>
    private static string? Vocabulary(LinkedArtObject value, List<PropertyInfo> set)
    {
        if (value.Id is null) return null;
        var allowed = value is Language ? 3 : 2;
        if (set.Count > allowed || set.Any(p => p.Name is not (nameof(LinkedArtObject.Id) or nameof(LinkedArtObject.Label) or nameof(Language.Notation)))) return null;

        if (value.Id.StartsWith(Aat.Base, StringComparison.Ordinal))
        {
            if (AatByIdAndLabel.Value.TryGetValue((value.GetType(), value.Id, value.Label), out var name)) return name;
            if (value.Label is null) return null; // Aat.Term(id) would fill in the registry label, which this object does not carry
            var id = Literal(value.Id[Aat.Base.Length..]);
            var label = value.Label is null ? "" : ", " + Literal(value.Label);
            return value switch
            {
                Language l => $"Aat.Language({id}{label}{(l.Notation is null ? "" : ", " + Literal(l.Notation))})",
                Material => $"Aat.Material({id}{label})",
                Currency => $"Aat.Currency({id}{label})",
                MeasurementUnit => $"Aat.Unit({id}{label})",
                Type => $"Aat.Term({id}{label})",
                _ => null,
            };
        }
        if (value.Id.StartsWith(Tgn.Base, StringComparison.Ordinal) && value.GetType() == typeof(Place))
            return $"Tgn.Place({Literal(value.Id[Tgn.Base.Length..])}{(value.Label is null ? "" : ", " + Literal(value.Label))})";
        if (value.Id.StartsWith(Ulan.Base, StringComparison.Ordinal) && value.GetType() == typeof(Person))
            return $"Ulan.Person({Literal(value.Id[Ulan.Base.Length..])}{(value.Label is null ? "" : ", " + Literal(value.Label))})";
        if (value.Id.StartsWith(Ulan.Base, StringComparison.Ordinal) && value.GetType() == typeof(Group))
            return $"Ulan.Group({Literal(value.Id[Ulan.Base.Length..])}{(value.Label is null ? "" : ", " + Literal(value.Label))})";
        return null;
    }

    // ---- reflection -------------------------------------------------------------------------------------------------

    private static readonly Dictionary<System.Type, List<PropertyInfo>> PropertyCache = new();

    /// <summary>Public settable properties in JSON order, so the initialiser reads like the document.</summary>
    private static List<PropertyInfo> SettableProperties(System.Type type)
    {
        lock (PropertyCache)
        {
            if (PropertyCache.TryGetValue(type, out var cached)) return cached;
            var list = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
                .Select((p, i) => (Property: p, Order: p.GetCustomAttribute<JsonPropertyOrderAttribute>()?.Order ?? 0, Index: i))
                .OrderBy(x => x.Order).ThenBy(x => x.Index)
                .Select(x => x.Property).ToList();
            PropertyCache[type] = list;
            return list;
        }
    }

    // ---- literals ---------------------------------------------------------------------------------------------------

    private static string Literal(string s)
    {
        var sb = new StringBuilder(s.Length + 2).Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                default:
                    if (char.IsControl(c)) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }

    private static string Number(double d) => d.ToString("R", CultureInfo.InvariantCulture);

    private static string Date(LinkedArtDate d) => d is { Hour: 0, Minute: 0, Second: 0 }
        ? $"new LinkedArtDate({d.Year}, {d.Month}, {d.Day})"
        : $"new LinkedArtDate({d.Year}, {d.Month}, {d.Day}, {d.Hour}, {d.Minute}, {d.Second})";
}
