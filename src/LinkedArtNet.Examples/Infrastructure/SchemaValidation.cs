using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;

namespace LinkedArtNet.Examples;

/// <summary>
/// Validates records against the vendored linked.art 1.0 JSON schemas (<c>schemas/linked-art/1.0</c>). Every schema
/// file is registered under its <c>$id</c> so the cross-file <c>$ref</c>s resolve offline. The record's <c>type</c>
/// picks the top-level schema (an <c>Activity</c> classified as a provenance activity uses <c>provenance.json</c>,
/// any other <c>Activity</c>, <c>Event</c> or <c>Period</c> uses <c>event.json</c>), so the errors come from one
/// schema instead of every branch of the <c>linked_art.json</c> union.
/// </summary>
public sealed class SchemaValidation
{
    public const string ProvenanceActivityId = "http://vocab.getty.edu/aat/300055863";

    /// <summary>One validation error: where in the instance, where in the schema, and the message.</summary>
    public sealed record Error(string InstanceLocation, string SchemaLocation, string Message)
    {
        /// <summary>The instance path with array indexes removed, for grouping like errors across records.</summary>
        public string Shape => Regex.Replace(InstanceLocation, "/[0-9]+(?=/|$)", "/[]");

        public override string ToString() => $"{InstanceLocation}: {Message}";
    }

    private static readonly Dictionary<string, string> SchemaByType = new(StringComparer.Ordinal)
    {
        ["Type"] = "concept.json", ["Material"] = "concept.json", ["Language"] = "concept.json",
        ["Currency"] = "concept.json", ["MeasurementUnit"] = "concept.json",
        ["DigitalObject"] = "digital.json",
        ["Event"] = "event.json", ["Period"] = "event.json", ["Activity"] = "event.json",
        ["Group"] = "group.json",
        ["VisualItem"] = "image.json",
        ["HumanMadeObject"] = "object.json",
        ["Person"] = "person.json",
        ["Place"] = "place.json",
        ["Set"] = "set.json",
        ["LinguisticObject"] = "text.json",
        ["PropositionalObject"] = "abstract.json",
    };

    private readonly Dictionary<string, JsonSchema> _schemas = new(StringComparer.Ordinal);

    private SchemaValidation() { }

    public static SchemaValidation Load(string schemaDirectory)
    {
        var v = new SchemaValidation();
        foreach (var file in Directory.GetFiles(schemaDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            if (name == "manifest.json") continue; // SchemaInspector output, not a schema
            var schema = JsonSchema.FromFile(file);
            SchemaRegistry.Global.Register(schema);
            v._schemas[name] = schema;
        }
        return v;
    }

    /// <summary>The schema file a record validates against, from its <c>type</c> and classification.</summary>
    public static string SchemaFileFor(JsonElement record)
    {
        var type = record.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type is null || !SchemaByType.TryGetValue(type, out var file))
            throw new InvalidDataException($"No top-level schema for type '{type ?? "(none)"}'.");
        if (type == "Activity" && record.TryGetProperty("classified_as", out var classes) && classes.ValueKind == JsonValueKind.Array
            && classes.EnumerateArray().Any(c => c.TryGetProperty("id", out var id) && id.GetString() == ProvenanceActivityId))
            return "provenance.json";
        return file;
    }

    /// <summary>
    /// The errors that explain a failure. Branches of an <c>anyOf</c>/<c>oneOf</c> are pruned: a branch that failed
    /// while a sibling passed is not an error at all, and where every branch failed only the branch that got furthest
    /// into the instance is reported (the others fail with "additional property" noise about the properties that
    /// belong to the intended type).
    /// </summary>
    public IReadOnlyList<Error> Errors(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Errors(document.RootElement);
    }

    public IReadOnlyList<Error> Errors(JsonElement record)
    {
        var schema = _schemas[SchemaFileFor(record)];
        var results = schema.Evaluate(record, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (results.IsValid) return [];

        var details = (results.Details ?? []).Where(d => !d.IsValid).ToList();
        var byParent = details.GroupBy(d => d.Parent).ToDictionary(g => g.Key ?? results, g => g.ToList());
        var pruned = new HashSet<EvaluationResults>(ReferenceEqualityComparer.Instance);

        foreach (var (parent, children) in byParent)
        {
            if (parent.IsValid) { foreach (var c in children) pruned.Add(c); continue; }
            foreach (var group in children.GroupBy(c => BranchGroup(parent, c)).Where(g => g.Key is not null))
            {
                var branches = group.ToList();
                if (branches.Count < 2) continue;
                var best = branches.Max(b => Depth(b, details));
                foreach (var b in branches.Where(b => Depth(b, details) < best)) pruned.Add(b);
            }
        }

        return details
            .Where(d => d.Errors is { Count: > 0 } && !Ancestors(d).Any(pruned.Contains))
            .SelectMany(d => d.Errors!.Select(e => new Error(d.InstanceLocation.ToString(), d.EvaluationPath.ToString(), e.Value)))
            .Distinct()
            .ToList();
    }

    // "<parent path>/anyOf" when the child is a branch of the parent's anyOf/oneOf, else null.
    private static string? BranchGroup(EvaluationResults parent, EvaluationResults child)
    {
        var path = child.EvaluationPath.ToString();
        var m = Regex.Match(path, "^(.*/(?:anyOf|oneOf))/[0-9]+$");
        return m.Success && m.Groups[1].Value.StartsWith(parent.EvaluationPath.ToString(), StringComparison.Ordinal) ? m.Groups[1].Value : null;
    }

    // How far into the instance a branch's subtree got: the deepest instance location among its invalid descendants.
    private static int Depth(EvaluationResults branch, List<EvaluationResults> details) =>
        details.Where(d => Ancestors(d).Contains(branch) || ReferenceEquals(d, branch))
               .Max(d => d.InstanceLocation.ToString().Count(ch => ch == '/'));

    private static IEnumerable<EvaluationResults> Ancestors(EvaluationResults d)
    {
        for (var node = d; node is not null; node = node.Parent) yield return node;
    }

    /// <summary>
    /// Leaf errors only: the specific complaints, without the "some items do not match" summaries that every
    /// enclosing array and anyOf adds above them.
    /// </summary>
    public IReadOnlyList<Error> LeafErrors(string json)
    {
        var all = Errors(json);
        return all.Where(e => !all.Any(other => other != e && other.SchemaLocation.StartsWith(e.SchemaLocation + "/", StringComparison.Ordinal)
                                               && other.InstanceLocation.StartsWith(e.InstanceLocation, StringComparison.Ordinal)))
                  .ToList();
    }

    /// <summary>Distinct error shapes (paths without array indexes, plus message), for comparing two records' errors.</summary>
    public IReadOnlySet<string> ErrorShapes(string json) =>
        LeafErrors(json).Select(e => $"{e.Shape}: {e.Message}").ToHashSet(StringComparer.Ordinal);
}
