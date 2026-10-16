using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace LinkedArtNet.Examples.Tests;

/// <summary>
/// The idiom catalogue stays tied to the code: every idiom is used by at least one example (else the note is
/// stale), and every hand-written fluent helper is either an idiom or declared plain (else a new helper slipped in
/// without a decision about whether it deserves a note).
/// </summary>
public class IdiomTests
{
    [Fact]
    public void Every_idiom_is_used_by_at_least_one_example()
    {
        var path = Path.Combine(TestPaths.RepoRoot, "examples", "source.json");
        var used = JsonNode.Parse(File.ReadAllText(path))!.AsArray()
            .SelectMany(e => e!["idioms"]?.AsArray().Select(k => k!.GetValue<string>()) ?? [])
            .ToHashSet(StringComparer.Ordinal);
        var unused = Idioms.All.Select(i => i.Key).Where(k => !used.Contains(k)).ToList();
        Assert.True(unused.Count == 0, "No example uses: " + string.Join(", ", unused) + " (remove the idiom, or rerun tools/ExampleSourceExtractor).");
        var unknown = used.Where(k => Idioms.Find(k) is null).ToList();
        Assert.True(unknown.Count == 0, "source.json names idioms not in the catalogue: " + string.Join(", ", unknown));
    }

    [Fact]
    public void Every_hand_written_helper_is_an_idiom_or_declared_plain()
    {
        // The generated file shares the LinkedArtExtensions class, so the hand-written helpers are read from their two source files.
        var helpers = new[] { "LinkedArtExtensions.cs", "DimensionExtensions.cs" }
            .Select(f => File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "src", "LinkedArtNet", f)))
            .SelectMany(text => Regex.Matches(text, @"^\s*public static \S+ (\w+)(?:<[^>]*>)?\(", RegexOptions.Multiline).Select(m => m.Groups[1].Value))
            .Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.True(helpers.Count > 10, "Expected to find the hand-written helpers; found " + helpers.Count);
        var covered = Idioms.All.SelectMany(i => i.Methods).Select(m => m.Contains('.') ? m[(m.IndexOf('.') + 1)..] : m)
            .Concat(Idioms.Plain).ToHashSet(StringComparer.Ordinal);
        var undecided = helpers.Where(h => !covered.Contains(h)).ToList();
        Assert.True(undecided.Count == 0, "Add to an idiom or to Idioms.Plain: " + string.Join(", ", undecided));
    }

    [Theory]
    [InlineData("WithClassifiedAs", null, 1, false, false, false, null)]
    [InlineData("WithClassifiedAs", null, 2, false, false, false, "metatype")]
    [InlineData("WithClassifiedAs", null, 2, true, false, false, null)]      // (id, label): two strings, no metatype
    [InlineData("WithClassifiedAs", null, 3, true, false, false, "metatype")]
    [InlineData("Term", "Aat", 2, true, false, false, "terms")]
    [InlineData("WithProducedBy", null, 1, false, false, true, "configure")]
    [InlineData("WithDimension", null, 1, false, false, false, null)]
    [InlineData("WithDimension", null, 3, false, false, false, "physical")]
    public void Detection_follows_the_call_shape(string name, string? qualifier, int arguments, bool firstIsString, bool firstIsNumber, bool lambda, string? expected)
    {
        var detected = Idioms.Detect([new Invocation(name, qualifier, arguments, firstIsString, firstIsNumber, lambda)]);
        if (expected is null) Assert.Empty(detected);
        else Assert.Equal([expected], detected);
    }
}
