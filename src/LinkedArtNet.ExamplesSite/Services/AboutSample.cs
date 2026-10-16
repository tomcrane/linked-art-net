using System.Reflection;
using LinkedArtNet.Dates;
using LinkedArtNet.Serialization;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.ExamplesSite.Services;

/// <summary>
/// The "Using the library" sample on the About page. The page shows the lines between the markers below, read from
/// this file (embedded in the assembly), and the JSON-LD of the object those lines build, so the code a visitor reads
/// is the code that ran.
/// </summary>
public static class AboutSample
{
    public static HumanMadeObject Build()
    {
        // sample-start
        var rembrandt = Ulan.Person("500011051", "Rembrandt van Rijn");

        var painting = new HumanMadeObject()
            .WithId("https://example.org/object/nightwatch")
            .WithLabel("The Night Watch")
            .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
            .WithName("The Night Watch", n => n.AsPrimaryName().WithLanguage(Aat.English))
            .WithName("De Nachtwacht", n => n.WithLanguage(Aat.Dutch))
            .WithIdentifier("SK-C-5", i => i.AsAccessionNumber())
            .WithProducedBy(p => p
                .WithCarriedOutBy(rembrandt)
                .WithTookPlaceAt(Tgn.Place("7006952", "Amsterdam"))
                .WithTimeSpan("1642"))
            .WithHeightDimension(379.5, Aat.Centimeters)
            .WithWidthDimension(453.5, Aat.Centimeters)
            .WithStatement(Aat.MaterialsStatement, "oil on canvas");
        // sample-end
        return painting;
    }

    public static string Json { get; } = LinkedArtJson.Serialize(Build());

    /// <summary>The sample as written above, dedented.</summary>
    public static string Source { get; } = ReadSource();

    private static string ReadSource()
    {
        var assembly = typeof(AboutSample).Assembly;
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("AboutSample.cs", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("AboutSample.cs is not embedded; see the EmbeddedResource item in the project file.");
        using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
        var lines = reader.ReadToEnd().Replace("\r\n", "\n").Split('\n');
        var start = Array.FindIndex(lines, l => l.Contains("// sample-start", StringComparison.Ordinal)) + 1;
        var end = Array.FindIndex(lines, l => l.Contains("// sample-end", StringComparison.Ordinal));
        var body = lines[start..end];
        var indent = body.Where(l => l.Trim().Length > 0).Min(l => l.Length - l.TrimStart().Length);
        return string.Join("\n", body.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
    }
}
