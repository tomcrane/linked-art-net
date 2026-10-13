using System.Reflection;
using System.Text.Json;
using LinkedArtNet.Serialization;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Tests;

public class FluentApiTests
{
    private static readonly HashSet<string> HandWritten = ["Context", "Id", "Type", "Label", "Content", "ClassifiedAs", "Language", "Technique", "MadeOf", "Dimension"];

    public static IEnumerable<object[]> EligibleProperties() =>
        typeof(LinkedArtObject).Assembly.GetTypes()
            .Where(t => t.IsClass && typeof(LinkedArtObject).IsAssignableFrom(t) && t.Namespace == "LinkedArtNet")
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.SetMethod is { IsPublic: true } && !HandWritten.Contains(p.Name))
                .Select(p => new object[] { t.Name, p.Name }));

    /// <summary>
    /// Every settable model property has a generated With… helper applicable to its declaring class, either
    /// constrained to the class or to a generated IHas… interface the class implements (else rerun tools/FluentGen).
    /// </summary>
    [Theory]
    [MemberData(nameof(EligibleProperties))]
    public void Every_property_has_a_generated_With_helper(string className, string propertyName)
    {
        var cls = typeof(LinkedArtObject).Assembly.GetType("LinkedArtNet." + className)!;
        var methods = typeof(LinkedArtExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "With" + propertyName && m.IsGenericMethodDefinition)
            .Where(m => m.GetGenericArguments()[0].GetGenericParameterConstraints().All(c => c.IsAssignableFrom(cls)))
            .ToList();
        Assert.True(methods.Count >= 1, $"No generated With{propertyName}<T>() applicable to {className}. Run: dotnet run --project tools/FluentGen");
    }

    [Fact]
    public void Shared_properties_are_reachable_through_generated_interfaces()
    {
        // referred_to_by is declared on six unrelated classes; one generic method serves them all.
        var method = typeof(LinkedArtExtensions).GetMethods().Single(m => m.Name == "WithReferredToBy" && m.GetParameters()[1].ParameterType.IsArray);
        var constraints = method.GetGenericArguments()[0].GetGenericParameterConstraints();
        Assert.Contains(constraints, c => c.IsInterface && c.Name == "IHasReferredToBy");
        Assert.All(new System.Type[] { typeof(HumanMadeObject), typeof(Production), typeof(Name), typeof(Right) },
            t => Assert.Contains(constraints.Single(c => c.IsInterface), t.GetInterfaces()));
    }

    [Fact]
    public void Chains_keep_their_static_type_and_build_nested_objects()
    {
        var nightWatch = new HumanMadeObject()
            .WithContext()
            .WithId("https://example.org/nightwatch")
            .WithLabel("Night Watch by Rembrandt")
            .WithClassifiedAs(Aat.Painting, metaType: Aat.TypeOfWork)
            .WithName("The Night Watch", n => n.AsPrimaryName().WithLanguage(Aat.English))
            .WithName("De Nachtwacht", n => n.WithLanguage(Aat.Dutch))
            .WithIdentifier("SK-C-5", i => i.AsAccessionNumber().WithAssignedBy(a => a.WithCarriedOutBy(Ulan.GroupOf("500246547", "Rijksmuseum"))))
            .WithProducedBy(p => p
                .WithCarriedOutBy(Ulan.PersonOf("500011051", "Rembrandt"))
                .WithTookPlaceAt(Tgn.Of("7006952", "Amsterdam"))
                .WithTimeSpan(1642))
            .WithHeightDimension(379.5, Aat.Centimeters, "379.5 cm high")
            .WithStatement(Aat.MaterialsStatement, "oil on canvas")
            .WithCurrentOwner(Ulan.GroupOf("500246547", "Rijksmuseum"))
            .WithShows(v => v.WithRepresents(Ulan.PersonOf("500011051", "Rembrandt").AsReference()));

        HumanMadeObject stillTyped = nightWatch; // the chain never degraded to LinkedArtObject

        Assert.Equal(3, stillTyped.IdentifiedBy!.Count);
        Assert.Equal("The Night Watch", stillTyped.GetPrimaryName());
        Assert.Equal("Rembrandt", stillTyped.ProducedBy!.CarriedOutBy![0].Label);
        Assert.Equal(LinkedArtDate.EndOfYear(1642), stillTyped.ProducedBy.TimeSpan!.EndOfTheEnd);
        Assert.Null(stillTyped.ProducedBy.TimeSpan.Label);
        Assert.Equal("Rijksmuseum", ((Identifier)stillTyped.IdentifiedBy[2]).AssignedBy![0].CarriedOutBy![0].Label);

        var json = LinkedArtJson.Serialize(stillTyped);
        var back = LinkedArtJson.Deserialize<HumanMadeObject>(json);
        Assert.Equal(json, LinkedArtJson.Serialize(back));
    }

    [Fact]
    public void Params_helpers_append_rather_than_replace()
    {
        var production = new Production()
            .WithCarriedOutBy(Ulan.PersonOf("500011051", "Rembrandt"))
            .WithCarriedOutBy(Ulan.PersonOf("500010363", "Manet"), Ulan.PersonOf("500015886", "Francis Harwood"));
        Assert.Equal(3, production.CarriedOutBy!.Count);
    }

    [Fact]
    public void Scalar_helpers_set_values()
    {
        var dimension = new Dimension().WithValue(74).WithUnit(Aat.Centimeters).WithLowerValueLimit(73.5).WithUpperValueLimit(74.5);
        Assert.Equal(74, dimension.Value);
        Assert.Equal(73.5, dimension.LowerValueLimit);

        var place = new Place().WithDefinedBy("POINT(4.9 52.37)");
        Assert.Equal("POINT(4.9 52.37)", place.DefinedBy);
    }

    [Fact]
    public void AsReference_keeps_only_id_type_and_label()
    {
        var full = new Person().WithId("https://example.org/p").WithLabel("Rembrandt").WithName("Rembrandt van Rijn");
        full.Born = new Birth().WithTimeSpan(1606, 7, 15);

        var reference = full.AsReference();
        Assert.IsType<Person>(reference);
        Assert.Equal(full.Id, reference.Id);
        Assert.Equal(full.Label, reference.Label);
        Assert.Null(reference.IdentifiedBy);
        Assert.Null(reference.Born);
        Assert.NotSame(full, reference);
    }

    [Fact]
    public void TimeSpan_factories_set_no_label()
    {
        Assert.Null(LinkedArtTimeSpan.FromYear(1881).Label);
        Assert.Null(LinkedArtTimeSpan.FromDay(1606, 7, 15).Label);
        Assert.Equal("1881", LinkedArtTimeSpan.FromYear(1881).WithLabel("1881").Label);

        var month = LinkedArtTimeSpan.FromMonth(1848, 2);
        Assert.Equal(new LinkedArtDate(1848, 2, 29, 23, 59, 59), month.EndOfTheEnd);

        var years = LinkedArtTimeSpan.FromYears(1881, 1883);
        Assert.Equal(new LinkedArtDate(1881, 1, 1), years.BeginOfTheBegin);
        Assert.Equal(LinkedArtDate.EndOfYear(1883), years.EndOfTheEnd);

        Assert.Throws<ArgumentException>(() => LinkedArtTimeSpan.Between(new LinkedArtDate(1900, 1, 1), new LinkedArtDate(1800, 1, 1)));
        Assert.True(years.IsOrdered());
        Assert.False(new LinkedArtTimeSpan { BeginOfTheBegin = new LinkedArtDate(1900, 1, 1), EndOfTheEnd = new LinkedArtDate(1800, 1, 1) }.IsOrdered());
    }

    [Fact]
    public void Generated_file_is_in_sync_with_the_model()
    {
        // Spot check that a recently added property is covered; the Theory above covers the rest.
        var method = typeof(LinkedArtExtensions).GetMethods().Single(m => m.Name == "WithTransferredCustodyOf" && m.GetParameters()[1].ParameterType.IsArray);
        Assert.Equal("TransferOfCustody", method.GetGenericArguments()[0].GetGenericParameterConstraints()[0].Name);
    }
}
