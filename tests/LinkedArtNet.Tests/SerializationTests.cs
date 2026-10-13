using System.Text.Json;
using LinkedArtNet.Serialization;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Tests;

public class SerializationTests
{
    [Fact]
    public void Reads_the_concrete_class_from_type()
    {
        var json = """{"id":"https://example.org/p","type":"Person","_label":"Rembrandt"}""";
        var obj = LinkedArtJson.Parse(json);
        var person = Assert.IsType<Person>(obj);
        Assert.Equal("Rembrandt", person.Label);
        Assert.Equal("Person", person.Type);
    }

    [Fact]
    public void Reads_mixed_subclasses_into_a_base_typed_list()
    {
        var json = """
            {"type":"Production","carried_out_by":[
                {"id":"https://example.org/p","type":"Person","_label":"Rembrandt"},
                {"id":"https://example.org/g","type":"Group","_label":"Workshop"}]}
            """;
        var production = LinkedArtJson.Deserialize<Production>(json);
        Assert.Collection(production.CarriedOutBy!,
            a => Assert.IsType<Person>(a),
            a => Assert.IsType<Group>(a));
    }

    [Fact]
    public void Reads_concept_subclasses_where_a_Type_is_expected()
    {
        var json = """
            {"type":"HumanMadeObject",
             "made_of":[{"id":"http://vocab.getty.edu/aat/300015050","type":"Material","_label":"oil"}],
             "classified_as":[{"id":"http://vocab.getty.edu/aat/300033618","type":"Type","_label":"Painting"}]}
            """;
        var hmo = LinkedArtJson.Deserialize<HumanMadeObject>(json);
        Assert.IsType<Material>(hmo.MadeOf![0]);
        Assert.IsType<Type>(hmo.ClassifiedAs![0]);
    }

    [Fact]
    public void Reads_context_as_string_or_array()
    {
        var single = LinkedArtJson.Parse("""{"@context":"https://linked.art/ns/v1/linked-art.json","type":"Place"}""");
        Assert.Equal(["https://linked.art/ns/v1/linked-art.json"], single.Context!);

        var multiple = LinkedArtJson.Parse("""{"@context":["https://a.example/","https://b.example/"],"type":"Place"}""");
        Assert.Equal(["https://a.example/", "https://b.example/"], multiple.Context!);
    }

    [Fact]
    public void Strict_mode_rejects_unknown_properties_types_and_mismatches()
    {
        var unknownProperty = Assert.Throws<JsonException>(() => LinkedArtJson.Parse("""{"type":"Place","colour":"blue"}"""));
        Assert.Contains("colour", unknownProperty.Message);

        var unknownType = Assert.Throws<JsonException>(() => LinkedArtJson.Parse("""{"type":"Spaceship"}"""));
        Assert.Contains("Spaceship", unknownType.Message);

        var mismatch = Assert.Throws<JsonException>(() => LinkedArtJson.Deserialize<Person>("""{"type":"Place"}"""));
        Assert.Contains("not a Person", mismatch.Message);

        var noType = Assert.Throws<JsonException>(() => LinkedArtJson.Parse("""{"id":"https://example.org/x"}"""));
        Assert.Contains("without a 'type'", noType.Message);

        var nested = Assert.Throws<JsonException>(() => LinkedArtJson.Deserialize<Production>("""{"type":"Production","carried_out_by":[{"type":"Place"}]}"""));
        Assert.Contains("carried_out_by", nested.Message);
    }

    [Fact]
    public void Tolerant_mode_ignores_unknown_properties_and_falls_back_to_the_declared_class()
    {
        var place = LinkedArtJson.Deserialize<Place>("""{"type":"Place","colour":"blue","_label":"Amsterdam"}""", strict: false);
        Assert.Equal("Amsterdam", place.Label);

        var unknownType = LinkedArtJson.Deserialize<Place>("""{"type":"Spaceship","_label":"x"}""", strict: false);
        Assert.IsType<Place>(unknownType);

        var noType = LinkedArtJson.Deserialize<Place>("""{"_label":"x"}""", strict: false);
        Assert.IsType<Place>(noType);

        // An abstract declared type still needs a usable type.
        Assert.Throws<JsonException>(() => LinkedArtJson.Deserialize<LinkedArtObject>("""{"type":"Spaceship"}""", strict: false));
    }

    [Fact]
    public void Writes_without_nulls_or_empty_collections_and_in_model_order()
    {
        var hmo = new HumanMadeObject()
            .WithContext()
            .WithId("https://example.org/1")
            .WithLabel("Thing");
        hmo.Shows = [];            // empty collection: omitted
        hmo.MadeOf = null;         // null: omitted
        hmo.ProducedBy = new Production { CarriedOutBy = [] };

        var json = LinkedArtJson.Serialize(hmo, indented: false);
        Assert.Equal("""{"@context":"https://linked.art/ns/v1/linked-art.json","id":"https://example.org/1","type":"HumanMadeObject","_label":"Thing","produced_by":{"type":"Production"}}""", json);
    }

    [Fact]
    public void Does_not_escape_quotes_or_non_ascii()
    {
        var name = new Name("L'Œuvre « Spring »");
        var json = LinkedArtJson.Serialize(name, indented: false);
        Assert.Contains("L'Œuvre « Spring »", json);
    }

    [Fact]
    public void Plain_serializer_options_still_produce_correct_polymorphic_output()
    {
        var set = new Set { Id = "https://example.org/s", About = [new Person().WithLabel("p"), new Material("https://example.org/m", "m")] };
        var json = JsonSerializer.Serialize(set);
        Assert.Contains("\"type\":\"Person\"", json);
        Assert.Contains("\"type\":\"Material\"", json);
    }

    [Fact]
    public void Round_trips_a_rich_object()
    {
        var spring = new HumanMadeObject()
            .WithContext()
            .WithId("https://example.org/spring")
            .WithLabel("Spring")
            .WithClassifiedAs(Aat.Painting, Aat.TypeOfWork)
            .WithHeightDimension(74, MeasurementUnit.Centimeters, "74 cm high")
            .WithMadeOf("oil", "300015050");
        spring.IdentifiedBy = [new Name("Spring").AsPrimaryName().WithLanguage("300388277", "English", "en")];
        spring.ProducedBy = new Production
        {
            TimeSpan = LinkedArtTimeSpan.FromYear(1881),
            CarriedOutBy = [Getty.UlanPerson("Manet", "500010363")],
            TookPlaceAt = [Getty.TgnPlace("Paris", "7008038")],
        };
        spring.CurrentOwner = [Getty.UlanGroup("Getty Museum", "500115988")];
        spring.ReferredToBy = [new LinguisticObject().WithContent("A painting of a woman.").WithClassifiedAs(Aat.Description, Aat.BriefText)];

        var json = LinkedArtJson.Serialize(spring);
        var back = LinkedArtJson.Deserialize<HumanMadeObject>(json);
        var again = LinkedArtJson.Serialize(back);

        Assert.Equal(json, again);
        Assert.IsType<Person>(back.ProducedBy!.CarriedOutBy![0]);
        Assert.IsType<Group>(back.CurrentOwner![0]);
        Assert.IsType<Language>(((Name)back.IdentifiedBy![0]).Language![0]);
        Assert.Equal(LinkedArtDate.EndOfYear(1881), back.ProducedBy.TimeSpan!.EndOfTheEnd);
    }

    [Fact]
    public void Type_registry_knows_every_class()
    {
        Assert.Equal(typeof(LinkedArtTimeSpan), LinkedArtTypes.Resolve("TimeSpan"));
        Assert.Equal(typeof(LinguisticObject), LinkedArtTypes.Resolve("LinguisticObject")); // not the IIIFManifest helper
        Assert.Equal(typeof(DigitalService), LinkedArtTypes.Resolve("DigitalService"));
        Assert.Null(LinkedArtTypes.Resolve("Spaceship"));
        Assert.Equal("TimeSpan", LinkedArtTypes.TypeNameOf(typeof(LinkedArtTimeSpan)));
        string[] expected =
        [
            "HumanMadeObject", "DigitalObject", "DigitalService", "VisualItem", "LinguisticObject", "PropositionalObject",
            "Set", "Person", "Group", "Place", "Type", "Material", "Language", "Currency", "MeasurementUnit",
            "Name", "Identifier", "Dimension", "MonetaryAmount", "Right", "TimeSpan",
            "Period", "Event", "Activity", "Production", "Creation", "Destruction", "Encounter", "Birth", "Death",
            "Formation", "Dissolution", "Modification", "PartRemoval", "Acquisition", "Payment", "TransferOfCustody",
            "Move", "RightAcquisition", "AttributeAssignment", "InformationObject",
        ];
        Assert.Equal(expected.Order(), LinkedArtTypes.All.Keys.Order());
    }

    // ---- every cached site example round-trips -----------------------------------------------------

    public static IEnumerable<object[]> CachedExampleFiles() =>
        Directory.EnumerateFiles(TestPaths.CachedExamples, "*.json", SearchOption.AllDirectories)
            .Select(f => new object[] { Path.GetRelativePath(TestPaths.CachedExamples, f).Replace('\\', '/') });

    [Theory]
    [MemberData(nameof(CachedExampleFiles))]
    public void Every_cached_site_example_reads_strictly_and_round_trips(string relativePath)
    {
        var siteJson = File.ReadAllText(Path.Combine(TestPaths.CachedExamples, relativePath));

        var obj = LinkedArtJson.Parse(siteJson);               // strict: unknown properties would throw
        var ours = LinkedArtJson.Serialize(obj);

        var difference = JsonEquivalence.FirstDifference(siteJson, ours);
        Assert.True(difference is null, $"{relativePath}: {difference}");
    }
}
