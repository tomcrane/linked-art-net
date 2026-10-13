using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace LinkedArtNet.Tests;

/// <summary>
/// Compares the C# model with the Linked Art 1.0 schema manifest produced by tools/SchemaInspector
/// (schemas/linked-art/1.0/manifest.json). The comparison is a ratchet: every gap must be listed in
/// tests/LinkedArtNet.Tests/Contract/model-gap-baseline.json, and every baseline entry must still be a gap,
/// so the baseline only ever shrinks. Set LINKEDARTNET_WRITE_BASELINE=1 to regenerate the baseline
/// (and docs/model-gap-report.md) after a deliberate model change.
/// </summary>
public class SchemaContractTests(ITestOutputHelper output)
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ManifestPath = Path.Combine(RepoRoot, "schemas", "linked-art", "1.0", "manifest.json");
    private static readonly string BaselinePath = Path.Combine(RepoRoot, "tests", "LinkedArtNet.Tests", "Contract", "model-gap-baseline.json");
    private static readonly string ReportPath = Path.Combine(RepoRoot, "docs", "model-gap-report.md");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Model_gaps_are_all_listed_in_the_baseline()
    {
        var gaps = ComputeGaps();
        if (Environment.GetEnvironmentVariable("LINKEDARTNET_WRITE_BASELINE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BaselinePath)!);
            File.WriteAllText(BaselinePath, JsonSerializer.Serialize(gaps, JsonOptions) + "\n");
            File.WriteAllText(ReportPath, gaps.ToMarkdown());
            output.WriteLine($"Baseline written to {BaselinePath}");
            return;
        }

        var baseline = LoadBaseline();
        var regressions = gaps.Except(baseline);
        output.WriteLine(gaps.Summary());
        Assert.True(regressions.IsEmpty,
            "New gaps between the model and the schema (add a class/property, or add these to the baseline deliberately):\n" + regressions.ToText());
    }

    [Fact]
    public void Baseline_contains_no_resolved_gaps()
    {
        if (Environment.GetEnvironmentVariable("LINKEDARTNET_WRITE_BASELINE") == "1") return;
        var gaps = ComputeGaps();
        var baseline = LoadBaseline();
        var resolved = baseline.Except(gaps);
        Assert.True(resolved.IsEmpty,
            "These baseline entries are no longer gaps; remove them from the baseline (or rerun with LINKEDARTNET_WRITE_BASELINE=1):\n" + resolved.ToText());
    }

    [Fact]
    public void Manifest_covers_the_expected_linked_art_classes()
    {
        var manifest = LoadManifest();
        var fullClasses = manifest.Classes.Where(c => !c.Value.Reference).Select(c => c.Key).ToHashSet();
        string[] expected =
        [
            "HumanMadeObject", "DigitalObject", "DigitalService", "VisualItem", "LinguisticObject", "PropositionalObject",
            "Set", "Person", "Group", "Place", "Type", "Material", "Language", "Currency", "MeasurementUnit",
            "Name", "Identifier", "Dimension", "MonetaryAmount", "Right", "TimeSpan",
            "Period", "Event", "Activity", "Production", "Creation", "Destruction", "Encounter", "Birth", "Death",
            "Formation", "Dissolution", "Modification", "PartRemoval", "Acquisition", "Payment", "TransferOfCustody",
            "Move", "RightAcquisition", "AttributeAssignment",
        ];
        Assert.Empty(expected.Except(fullClasses));
        Assert.Empty(fullClasses.Except(expected));
    }

    // ---- comparison --------------------------------------------------------------------------------

    private static GapSet ComputeGaps()
    {
        var manifest = LoadManifest();
        var assembly = typeof(LinkedArtObject).Assembly;
        var modelTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(LinkedArtObject).IsAssignableFrom(t))
            .ToList();

        var gaps = new GapSet();
        foreach (var (className, cls) in manifest.Classes.Where(c => !c.Value.Reference).OrderBy(c => c.Key))
        {
            var csType = MapToCSharpType(className, modelTypes);
            if (csType is null)
            {
                gaps.UnmappedClasses.Add(className);
                continue;
            }

            // @context is a serialisation concern and id is permitted on any JSON-LD node, so neither is compared.
            var schemaProps = cls.Properties.Keys.Where(p => p is not ("@context" or "id")).ToHashSet();
            var modelProps = JsonPropertyNames(csType).Where(p => p is not ("@context" or "id")).ToHashSet();

            foreach (var missing in schemaProps.Except(modelProps).Order())
                gaps.Missing.Add($"{className}.{missing}");
            foreach (var extra in modelProps.Except(schemaProps).Order())
                gaps.Extra.Add($"{className}.{extra}");
        }
        return gaps;
    }

    /// <summary>Finds the C# class that serialises a given Linked Art class: same name, or LinkedArt+name (TimeSpan).</summary>
    private static System.Type? MapToCSharpType(string className, List<System.Type> modelTypes) =>
        modelTypes.FirstOrDefault(t => t.Name == className)
        ?? modelTypes.FirstOrDefault(t => t.Name == "LinkedArt" + className);

    private static IEnumerable<string> JsonPropertyNames(System.Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always)
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name)
            .Distinct();

    // ---- files ----------------------------------------------------------------------------------------

    private static Manifest LoadManifest()
    {
        Assert.True(File.Exists(ManifestPath), $"Manifest not found at {ManifestPath}; run tools/SchemaInspector.");
        return JsonSerializer.Deserialize<Manifest>(File.ReadAllText(ManifestPath), JsonOptions)!;
    }

    private static GapSet LoadBaseline()
    {
        Assert.True(File.Exists(BaselinePath), $"Baseline not found at {BaselinePath}; run the tests once with LINKEDARTNET_WRITE_BASELINE=1.");
        return JsonSerializer.Deserialize<GapSet>(File.ReadAllText(BaselinePath), JsonOptions)!;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (linked-art-net.slnx).");
    }

    // ---- types ----------------------------------------------------------------------------------------

    public sealed record Manifest(string Version, string Source, Dictionary<string, ManifestClass> Classes);

    public sealed record ManifestClass(
        string File,
        string DefinitionName,
        bool TopLevel,
        bool Reference,
        List<string>? SharesDefinitionWith,
        Dictionary<string, ManifestProperty> Properties);

    public sealed record ManifestProperty(bool Array, string? Scalar, List<string> Range);

    public sealed class GapSet
    {
        /// <summary>Schema properties with no C# property on the mapped class.</summary>
        public SortedSet<string> Missing { get; init; } = [];
        /// <summary>C# properties the schema does not allow on that class.</summary>
        public SortedSet<string> Extra { get; init; } = [];
        /// <summary>Schema classes with no C# type at all.</summary>
        public SortedSet<string> UnmappedClasses { get; init; } = [];

        public bool IsEmpty => Missing.Count == 0 && Extra.Count == 0 && UnmappedClasses.Count == 0;

        public GapSet Except(GapSet other) => new()
        {
            Missing = new SortedSet<string>(Missing.Except(other.Missing)),
            Extra = new SortedSet<string>(Extra.Except(other.Extra)),
            UnmappedClasses = new SortedSet<string>(UnmappedClasses.Except(other.UnmappedClasses)),
        };

        public string Summary() =>
            $"Model vs schema: {Missing.Count} missing properties, {Extra.Count} extra properties, {UnmappedClasses.Count} unmapped classes.";

        public string ToText()
        {
            var lines = new List<string>();
            if (UnmappedClasses.Count > 0) lines.Add("Unmapped classes: " + string.Join(", ", UnmappedClasses));
            lines.AddRange(Missing.Select(m => "missing: " + m));
            lines.AddRange(Extra.Select(e => "extra:   " + e));
            return string.Join('\n', lines);
        }

        public string ToMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Model vs Linked Art 1.0 schema: gap report");
            sb.AppendLine();
            sb.AppendLine("Generated by `SchemaContractTests` with `LINKEDARTNET_WRITE_BASELINE=1`. Do not edit by hand.");
            sb.AppendLine();
            sb.AppendLine(Summary());
            sb.AppendLine();
            sb.AppendLine("## Schema classes with no C# class");
            sb.AppendLine();
            foreach (var c in UnmappedClasses) sb.AppendLine($"- {c}");
            sb.AppendLine();
            sb.AppendLine("## Schema properties missing from the model");
            sb.AppendLine();
            foreach (var m in Missing) sb.AppendLine($"- {m}");
            sb.AppendLine();
            sb.AppendLine("## Model properties the schema does not allow on that class");
            sb.AppendLine();
            foreach (var e in Extra) sb.AppendLine($"- {e}");
            return sb.ToString();
        }
    }
}
