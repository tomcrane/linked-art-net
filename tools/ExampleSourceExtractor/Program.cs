// Writes examples/source.json: the C# of every [LinkedArtExample] method in src/LinkedArtNet.Examples, as text, for the
// examples site to show beside the JSON. Run after changing an example:  dotnet run --project tools/ExampleSourceExtractor
// CoverageTests checks the file is current.

using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var repoRoot = args.Length > 0 ? args[0] : FindRepoRoot();
var examplesDir = Path.Combine(repoRoot, "src", "LinkedArtNet.Examples");
var entries = new List<SourceEntry>();

foreach (var file in Directory.GetFiles(examplesDir, "*.cs").Order(StringComparer.Ordinal))
{
    var text = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(text, path: file);
    var root = tree.GetCompilationUnitRoot();
    foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
    {
        var attribute = method.AttributeLists.SelectMany(l => l.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "LinkedArtExample" or "LinkedArtExampleAttribute");
        if (attribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax idLiteral) continue;
        var id = idLiteral.Token.ValueText;
        var siteIssue = method.AttributeLists.SelectMany(l => l.Attributes).FirstOrDefault(a => a.Name.ToString() is "SiteIssue" or "SiteIssueAttribute");
        var topic = (method.Parent as ClassDeclarationSyntax)?.Identifier.Text ?? "";
        var line = tree.GetLineSpan(method.Identifier.Span).StartLinePosition.Line + 1; // the line with the method name, not its attributes

        // The method as written, without its attributes, dedented to the left margin.
        var withoutAttributes = method.WithAttributeLists([]);
        var source = Dedent(withoutAttributes.ToFullString().TrimEnd());

        entries.Add(new SourceEntry(id, $"{topic}.{method.Identifier.Text}", topic, method.Identifier.Text,
            Path.GetRelativePath(repoRoot, file).Replace('\\', '/'), line, source,
            siteIssue?.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax reason ? reason.Token.ValueText : null));
    }
}

var outPath = Path.Combine(repoRoot, "examples", "source.json");
var json = JsonSerializer.Serialize(entries.OrderBy(e => e.Key, StringComparer.Ordinal), new JsonSerializerOptions
{
    WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
});
File.WriteAllText(outPath, json + "\n", new UTF8Encoding(false));
Console.WriteLine($"{entries.Count} example methods -> {Path.GetRelativePath(repoRoot, outPath)}");

static string Dedent(string code)
{
    var lines = code.Replace("\r\n", "\n").Split('\n');
    var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
    return string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx"))) dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (linked-art-net.slnx).");
}

/// <summary>One example method's source. <c>Key</c> is <c>Topic.Method</c>, matching <c>Example.Key</c> in the catalog.</summary>
record SourceEntry(string Id, string Key, string Topic, string Method, string File, int Line, string Source, string? SiteIssue);
