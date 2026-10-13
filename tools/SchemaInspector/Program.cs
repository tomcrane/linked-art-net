// Reads the Linked Art 1.0 JSON schemas and writes a manifest of classes, their properties and the
// classes each property may point at. The manifest drives the model contract test
// (tests/LinkedArtNet.Tests/SchemaContractTests.cs). See docs/02-model-library.md §2.
//
// Usage: SchemaInspector [schemaDir] [manifestOut]

using System.Text.Json;
using System.Text.Json.Nodes;
using SchemaInspector;

if (args.Length > 0 && args[0] == "--analyze")
{
    Analysis.Run(args.Length > 1 ? args[1] : "schemas/linked-art/1.0");
    return;
}

var schemaDir = args.Length > 0 ? args[0] : "schemas/linked-art/1.0";
var outPath = args.Length > 1 ? args[1] : Path.Combine(schemaDir, "manifest.json");

var inspector = new Inspector(schemaDir);
var manifest = inspector.Build();

var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
});
File.WriteAllText(outPath, json);

Console.WriteLine($"{manifest.Classes.Count} classes written to {outPath}");
foreach (var cls in manifest.Classes.OrderBy(c => c.Key))
{
    var c = cls.Value;
    Console.WriteLine($"  {cls.Key,-28} {(c.TopLevel ? "top " : "    ")}{(c.Reference ? "ref " : "    ")}{c.Properties.Count,3} props  ({c.File})");
}

namespace SchemaInspector
{
    /// <summary>Prints the property sets of the full (non-reference) classes and their subset relationships.</summary>
    public static class Analysis
    {
        public static void Run(string schemaDir)
        {
            var manifest = new Inspector(schemaDir).Build();
            var classes = manifest.Classes.Where(c => !c.Value.Reference)
                .ToDictionary(c => c.Key, c => c.Value.Properties.Keys.Where(p => p != "@context").ToHashSet());

            var common = classes.Values.Aggregate((a, b) => a.Intersect(b).ToHashSet());
            Console.WriteLine("COMMON TO ALL: " + string.Join(" ", common.Order()));
            Console.WriteLine();

            foreach (var (name, props) in classes.OrderBy(c => c.Key))
            {
                var own = props.Except(common).Order();
                Console.WriteLine($"{name} ({props.Count}): {string.Join(" ", own)}");
                var ranges = manifest.Classes[name].Properties
                    .Where(p => p.Value.Range.Count > 0 && !common.Contains(p.Key))
                    .Select(p => $"{p.Key}->{string.Join("|", p.Value.Range)}{(p.Value.Array ? "[]" : "")}");
                Console.WriteLine("    " + string.Join(" ", ranges));
            }
            Console.WriteLine();

            Console.WriteLine("SUBSET RELATIONS (A <= B means every property of A is allowed on B):");
            foreach (var (a, pa) in classes.OrderBy(c => c.Key))
            {
                var supers = classes.Where(b => b.Key != a && pa.IsSubsetOf(b.Value)).Select(b => b.Key).Order();
                var line = string.Join(", ", supers);
                if (line.Length > 0) Console.WriteLine($"  {a} <= {line}");
            }
        }
    }

    public sealed record Manifest(string Version, string Source, SortedDictionary<string, ClassInfo> Classes);

    public sealed class ClassInfo
    {
        public required string File { get; init; }
        public required string DefinitionName { get; init; }
        public bool TopLevel { get; init; }
        public bool Reference { get; init; }
        /// <summary>Other type names allowed by the same schema definition (e.g. Type/Material/Language/Currency/MeasurementUnit).</summary>
        public List<string>? SharesDefinitionWith { get; init; }
        public string? Description { get; init; }
        public List<string> Required { get; init; } = [];
        public bool? AdditionalProperties { get; init; }
        public SortedDictionary<string, PropertyInfo> Properties { get; init; } = [];
    }

    public sealed class PropertyInfo
    {
        public bool Array { get; set; }
        public string? Scalar { get; set; }
        public SortedSet<string> Range { get; init; } = [];
        public string? Description { get; set; }
    }

    public sealed class Inspector
    {
        private readonly Dictionary<string, JsonNode> _docs = new();
        private readonly Dictionary<string, (List<string> Names, string File)> _classDefs = new(); // "file#/$defs/X" -> allowed type names

        public Inspector(string schemaDir)
        {
            foreach (var f in Directory.GetFiles(schemaDir, "*.json").OrderBy(x => x))
            {
                var name = Path.GetFileName(f);
                if (name == "manifest.json") continue;
                using var doc = JsonDocument.Parse(File.ReadAllText(f));
                _docs[name] = ToNode(doc.RootElement, name, "")!;
            }
        }

        /// <summary>
        /// Converts a JsonElement to a JsonNode, keeping the first of any duplicated property names
        /// (the published schemas contain at least one duplicate) and reporting them.
        /// </summary>
        private static JsonNode? ToNode(JsonElement el, string file, string path)
        {
            switch (el.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new JsonObject();
                    foreach (var p in el.EnumerateObject())
                    {
                        if (!obj.TryAdd(p.Name, ToNode(p.Value, file, path + "/" + p.Name)))
                            Console.Error.WriteLine($"warning: duplicate property '{p.Name}' at {file}#{path}; keeping the first");
                    }
                    return obj;
                case JsonValueKind.Array:
                    var arr = new JsonArray();
                    var i = 0;
                    foreach (var item in el.EnumerateArray()) arr.Add(ToNode(item, file, $"{path}/{i++}"));
                    return arr;
                case JsonValueKind.Null:
                    return null;
                default:
                    return JsonValue.Create(el.Clone());
            }
        }

        public Manifest Build()
        {
            var classes = new SortedDictionary<string, ClassInfo>();

            // Pass 1: find every definition that is a class (an object whose `type` property has a const).
            foreach (var (file, root) in _docs)
            {
                if (TypeNames(root) is { } topNames)
                    _classDefs[file + "#"] = (topNames, file);

                if (root["$defs"] is JsonObject defs)
                {
                    foreach (var (defName, defNode) in defs)
                    {
                        if (defNode is JsonObject && TypeNames(defNode) is { } clsNames)
                            _classDefs[$"{file}#/$defs/{defName}"] = (clsNames, file);
                    }
                }
            }

            // Pass 2: describe each class. Top-level schemas first so they win when a class is also defined in core.json.
            foreach (var (key, (clsNames, file)) in _classDefs.OrderBy(kv => kv.Key.EndsWith('#') ? 0 : 1).ThenBy(kv => kv.Key))
            {
                var node = ResolvePointer(key);
                var isRef = key.EndsWith("Ref", StringComparison.Ordinal);
                var properties = DescribeProperties(node, file).ToList();

                foreach (var clsName in clsNames)
                {
                    var manifestName = isRef ? clsName + "Ref" : clsName;
                    if (classes.TryGetValue(manifestName, out var existing))
                    {
                        // Same class defined in two places (e.g. a top-level schema and a core def): merge properties.
                        foreach (var (pn, pi) in properties)
                            existing.Properties.TryAdd(pn, pi);
                        continue;
                    }

                    var info = new ClassInfo
                    {
                        File = file,
                        DefinitionName = key[(key.IndexOf('#') + 1)..].Replace("/$defs/", ""),
                        TopLevel = key.EndsWith("#", StringComparison.Ordinal),
                        Reference = isRef,
                        SharesDefinitionWith = clsNames.Count > 1 ? clsNames.Where(n => n != clsName).ToList() : null,
                        Description = FirstLine(node["description"]?.GetValue<string>()),
                        Required = node["required"] is JsonArray req ? req.Select(r => r!.GetValue<string>()).ToList() : [],
                        AdditionalProperties = node["additionalProperties"] is JsonValue ap && ap.TryGetValue<bool>(out var b) ? b : null,
                    };
                    foreach (var (pn, pi) in properties)
                        info.Properties[pn] = pi;
                    classes[manifestName] = info;
                }
            }

            return new Manifest("1.0", "https://linked.art/api/1.0/schema/", classes);
        }

        private IEnumerable<(string, PropertyInfo)> DescribeProperties(JsonNode classNode, string file)
        {
            if (classNode["properties"] is not JsonObject props) yield break;
            foreach (var (propName, propNode) in props)
            {
                if (propNode is null) continue;
                var info = new PropertyInfo { Description = FirstLine(propNode["description"]?.GetValue<string>()) };
                Walk(propNode, file, info, [], depth: 0);
                yield return (propName, info);
            }
        }

        private void Walk(JsonNode? node, string file, PropertyInfo info, HashSet<string> visited, int depth)
        {
            if (node is not JsonObject obj || depth > 12) return;

            if (obj["$ref"] is JsonValue refVal)
            {
                var key = Canonical(refVal.GetValue<string>(), file);
                if (_classDefs.TryGetValue(key, out var cls))
                {
                    var isRef = key.EndsWith("Ref", StringComparison.Ordinal);
                    foreach (var name in cls.Names)
                        info.Range.Add(isRef ? name + "Ref" : name);
                    return; // do not descend into a class: its properties belong to it, not to this property
                }
                if (!visited.Add(key)) return;
                Walk(ResolvePointer(key), FileOf(key), info, visited, depth + 1);
                return;
            }

            if (obj["type"] is JsonValue t && t.TryGetValue<string>(out var typeName))
            {
                if (typeName == "array")
                {
                    info.Array = true;
                    Walk(obj["items"], file, info, visited, depth + 1);
                }
                else if (typeName != "object")
                {
                    info.Scalar = obj["format"] is JsonValue fmt ? $"{typeName}:{fmt.GetValue<string>()}" : typeName;
                }
            }

            foreach (var combinator in new[] { "allOf", "oneOf", "anyOf" })
            {
                if (obj[combinator] is JsonArray arr)
                    foreach (var item in arr) Walk(item, file, info, visited, depth + 1);
            }

            if (obj["items"] is JsonNode items && obj["type"] is null)
            {
                info.Array = true;
                Walk(items, file, info, visited, depth + 1);
            }
        }

        /// <summary>The allowed values of a definition's `type` property (const or enum), or null if it is not a class.</summary>
        private static List<string>? TypeNames(JsonNode node)
        {
            if (node["properties"]?["type"] is not JsonNode typeProp) return null;
            var names = new List<string>();
            CollectTypeNames(typeProp, names);
            return names.Count == 0 ? null : names;
        }

        private static void CollectTypeNames(JsonNode? node, List<string> names)
        {
            if (node is not JsonObject obj) return;
            if (obj["const"] is JsonValue c && c.TryGetValue<string>(out var s)) names.Add(s);
            if (obj["enum"] is JsonArray en)
                foreach (var item in en)
                    if (item is JsonValue v && v.TryGetValue<string>(out var e)) names.Add(e);
            foreach (var combinator in new[] { "allOf", "oneOf", "anyOf" })
                if (obj[combinator] is JsonArray arr)
                    foreach (var item in arr) CollectTypeNames(item, names);
        }

        private static string Canonical(string reference, string currentFile)
        {
            var hash = reference.IndexOf('#');
            var file = hash < 0 ? reference : reference[..hash];
            if (file.Length == 0) file = currentFile;
            var pointer = hash < 0 ? "" : reference[hash..];
            if (pointer.Length == 0) pointer = "#";
            return file + pointer;
        }

        private static string FileOf(string key) => key[..key.IndexOf('#')];

        private JsonNode ResolvePointer(string key)
        {
            var hash = key.IndexOf('#');
            JsonNode node = _docs[key[..hash]];
            foreach (var seg in key[(hash + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries))
                node = node[seg] ?? throw new InvalidOperationException($"Cannot resolve {key}");
            return node;
        }

        private static string? FirstLine(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var line = s.Split('\n')[0].Trim();
            return line.Length > 200 ? line[..200] : line;
        }
    }
}
