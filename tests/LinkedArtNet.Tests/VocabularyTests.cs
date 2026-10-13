using System.Text.Json;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Tests;

/// <summary>
/// The vocabulary registries (vocabulary/*.json) and the generated constants (src/LinkedArtNet/Vocabulary/*.g.cs)
/// must agree, and the registries must be internally consistent.
/// </summary>
public class VocabularyTests
{
    public static IEnumerable<object[]> Registries() =>
    [
        ["aat.json", Aat.Terms],
        ["tgn.json", Tgn.Terms],
        ["ulan.json", Ulan.Terms],
    ];

    [Theory]
    [MemberData(nameof(Registries))]
    public void Generated_constants_match_the_json_registry(string file, IReadOnlyList<VocabularyTerm> generated)
    {
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "vocabulary", file)));
        var expected = json.RootElement.GetProperty("terms").EnumerateArray().Select(t => new VocabularyTerm(
            t.GetProperty("name").GetString()!,
            t.GetProperty("id").GetString()!,
            t.GetProperty("label").GetString()!,
            t.TryGetProperty("kind", out var k) ? Enum.Parse<VocabularyKind>(k.GetString()!) : VocabularyKind.Type,
            t.TryGetProperty("notation", out var n) ? n.GetString() : null,
            t.TryGetProperty("aliasOf", out var a) ? a.GetString() : null)).ToList();

        Assert.Equal(expected, generated); // same terms, same order: otherwise rerun tools/VocabGen
    }

    [Theory]
    [MemberData(nameof(Registries))]
    public void Registry_is_consistent(string file, IReadOnlyList<VocabularyTerm> terms)
    {
        Assert.Equal(terms.Count, terms.Select(t => t.Name).Distinct().Count());

        var canonical = terms.Where(t => t.AliasOf is null).ToList();
        var duplicateIds = canonical.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => $"{g.Key}: {string.Join(", ", g.Select(t => t.Name))}").ToList();
        Assert.True(duplicateIds.Count == 0, $"{file}: ids used by more than one non-alias term: {string.Join("; ", duplicateIds)}");

        foreach (var alias in terms.Where(t => t.AliasOf is not null))
        {
            var target = Assert.Single(terms, t => t.Name == alias.AliasOf);
            Assert.Equal(target.Id, alias.Id);
        }

        Assert.All(terms, t => Assert.Matches("^[0-9]+$", t.Id));
        Assert.All(terms, t => Assert.Matches("^[A-Z][A-Za-z0-9]*$", t.Name));
    }

    [Fact]
    public void Aat_properties_return_fresh_correctly_typed_instances()
    {
        var a = Aat.Painting;
        var b = Aat.Painting;
        Assert.NotSame(a, b);
        Assert.Equal("http://vocab.getty.edu/aat/300033618", a.Id);
        Assert.Equal("Painting", a.Label);
        Assert.Equal("Type", a.Type);

        Assert.IsType<Material>(Aat.Oil);
        Assert.IsType<MeasurementUnit>(Aat.Centimeters);
        Assert.IsType<Currency>(Aat.FrenchFrancs);
        var english = Assert.IsType<Language>(Aat.English);
        Assert.Equal("en", english.Notation);
        Assert.IsType<Place>(Tgn.Amsterdam);
        Assert.IsType<Person>(Ulan.Rembrandt);
        Assert.IsType<Group>(Ulan.Rijksmuseum);
    }

    [Fact]
    public void Lookups_by_id_use_registry_labels()
    {
        Assert.Equal("Painting", Aat.TryGet("300033618")!.Label);
        Assert.Equal("Painting", Aat.TryGet("http://vocab.getty.edu/aat/300033618")!.Label);
        Assert.Null(Aat.TryGet("300000000"));

        Assert.Equal("Painting", Aat.Of("300033618").Label);
        Assert.Equal("paintings (visual works)", Aat.Of("300033618", "paintings (visual works)").Label);
        Assert.Null(Aat.Of("300000000").Label);
        Assert.Equal("http://vocab.getty.edu/aat/300000000", Aat.Of("300000000").Id);

        Assert.Equal("nl", Aat.OfLanguage("300388256").Notation);
        Assert.Equal("Place", Tgn.Of("7008038").Type);
        Assert.Equal("http://vocab.getty.edu/ulan/500010363", Ulan.PersonOf("500010363").Id);
    }

    [Fact]
    public void Alias_terms_share_an_id_but_not_a_label()
    {
        Assert.Equal(Aat.Count.Id, Aat.CountOf.Id);
        Assert.NotEqual(Aat.Count.Label, Aat.CountOf.Label);
        Assert.Equal(Aat.SystemAssignedNumber.Id, Aat.RecordIdentifiers.Id);
        Assert.Equal(Aat.PartType.Id, Aat.Components.Id);
        Assert.IsType<MeasurementUnit>(Aat.Components);
    }
}
