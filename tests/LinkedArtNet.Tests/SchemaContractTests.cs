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
            var csType = MapToCSharpType(className, cls, modelTypes);
            if (csType is null)
            {
                gaps.UnmappedClasses.Add(className);
                continue;
            }

            var schemaProps = cls.Properties.Keys.Where(p => p != "@context").ToHashSet();
            var modelProps = JsonPropertyNames(csType).Where(p => p != "@context").ToHashSet();

            foreach (var missing in schemaProps.Except(modelProps).Order())
                gaps.Missing.Add($"{className}.{missing}  [{csType.Name}]");
            foreach (var extra in modelProps.Except(schemaProps).Order())
                gaps.Extra.Add($"{className}.{extra}  [{csType.Name}]");
        }
        return gaps;
    }

    /// <summary>
    /// Finds the C# type that serialises a given Linked Art class today. Prefers a class of the same name
    /// (or LinkedArt+name); otherwise, while the model still uses generic classes, falls back to
    /// <see cref="Activity"/> for event-like classes and <see cref="LinkedArtObject"/> for concept-like ones
    /// that the <see cref="Types"/> enum can name.
    /// </summary>
    private static Type? MapToCSharpType(string className, ManifestClass cls, List<Type> modelTypes)
    {
        var direct = modelTypes.FirstOrDefault(t => t.Name == className)
                     ?? modelTypes.FirstOrDefault(t => t.Name == "LinkedArt" + className);
        if (direct is not null) return direct;

        if (Enum.TryParse<Types>(className, out _))
        {
            return cls.Properties.ContainsKey("timespan") ? typeof(Activity) : typeof(LinkedArtObject);
        }
        return null;
    }

    private static IEnumerable<string> JsonPropertyNames(Type type) =>
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
            sb.AppendLine("## Schema properties missing from the model (class.property [C# type checked])");
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
