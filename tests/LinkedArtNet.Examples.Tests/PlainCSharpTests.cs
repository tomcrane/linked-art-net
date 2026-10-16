using System.Reflection;
using System.Text;
using LinkedArtNet.Serialization;
using LinkedArtNet.Vocabulary;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit.Abstractions;

namespace LinkedArtNet.Examples.Tests;

/// <summary>
/// <see cref="LinkedArtCSharp"/> writes every example as plain C#, that C# compiles, and the object it builds has the
/// same JSON as the example method's. One compilation for all examples, so the whole suite costs a few seconds.
/// </summary>
public class PlainCSharpTests(ITestOutputHelper output)
{
    [Fact]
    public void Every_example_can_be_written_as_plain_csharp_that_rebuilds_the_same_json()
    {
        var examples = ExampleCatalog.All;
        var source = new StringBuilder("using LinkedArtNet;\nusing LinkedArtNet.Vocabulary;\npublic static class Rebuilt\n{\n");
        var expected = new Dictionary<string, string>();
        for (var i = 0; i < examples.Count; i++)
        {
            var built = examples[i].Build();
            expected[examples[i].Key] = LinkedArtJson.Serialize(built);
            source.Append($"    public static LinkedArtObject E{i}() => {LinkedArtCSharp.Expression(built)};\n");
        }
        source.Append("}\n");

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).Cast<MetadataReference>()
            .Append(MetadataReference.CreateFromFile(typeof(LinkedArtObject).Assembly.Location));
        var compilation = CSharpCompilation.Create("Rebuilt", [CSharpSyntaxTree.ParseText(source.ToString())], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        foreach (var error in errors.Take(20))
        {
            var line = error.Location.GetLineSpan().StartLinePosition.Line;
            output.WriteLine($"{error.Id} {error.GetMessage()} at line {line + 1}: {source.ToString().Split('\n')[line][..Math.Min(200, source.ToString().Split('\n')[line].Length)]}");
        }
        Assert.True(result.Success, $"{errors.Count} compile error(s) in the plain C#; see output.");

        var rebuilt = Assembly.Load(stream.ToArray()).GetType("Rebuilt")!;
        var mismatches = new List<string>();
        for (var i = 0; i < examples.Count; i++)
        {
            var obj = (LinkedArtObject)rebuilt.GetMethod($"E{i}")!.Invoke(null, null)!;
            var actual = LinkedArtJson.Serialize(obj);
            if (actual != expected[examples[i].Key])
            {
                mismatches.Add(examples[i].Key);
                output.WriteLine($"-- {examples[i].Key}: first difference at {JsonEquivalence.FirstDifference(expected[examples[i].Key], actual)}");
            }
        }
        Assert.True(mismatches.Count == 0, "Plain C# built different JSON for: " + string.Join(", ", mismatches));
    }

    [Fact]
    public void Registry_terms_and_getty_instances_are_written_as_constants()
    {
        var obj = new HumanMadeObject().WithId("https://example.org/1").WithLabel("A painting").WithClassifiedAs(Aat.Painting);
        obj.ProducedBy = new Production { TookPlaceAt = [Tgn.Place("7008038", "Paris")], CarriedOutBy = [Ulan.Person("500010879", "Manet")] };
        var code = LinkedArtCSharp.Write(obj, "painting");
        Assert.StartsWith("var painting = new HumanMadeObject", code);
        Assert.Contains("ClassifiedAs = [Aat.Painting]", code);
        Assert.Contains("Tgn.Place(\"7008038\", \"Paris\")", code);
        Assert.Contains("Ulan.Person(\"500010879\", \"Manet\")", code);
    }

    [Fact]
    public void A_term_with_more_than_id_and_label_is_written_in_full()
    {
        var code = LinkedArtCSharp.Expression(Aat.Painting.WithClassifiedAs(Aat.TypeOfWork));
        Assert.StartsWith("new Type(\"http://vocab.getty.edu/aat/300033618\", \"Painting\")", code);
        Assert.Contains("ClassifiedAs = [Aat.TypeOfWork]", code);
    }
}
