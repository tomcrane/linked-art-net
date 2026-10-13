using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet.Tests;

/// <summary>
/// System.Text.Json serialises a derived type through a base-typed property only when the base declares it with
/// <see cref="JsonDerivedTypeAttribute"/>; otherwise it throws at runtime. These tests keep every base class's
/// attribute list complete so that cannot happen.
/// </summary>
public class PolymorphismTests
{
    private static readonly System.Type[] ModelTypes = typeof(LinkedArtObject).Assembly.GetTypes()
        .Where(t => t.IsClass && typeof(LinkedArtObject).IsAssignableFrom(t))
        .ToArray();

    public static IEnumerable<object[]> BaseClasses() =>
        ModelTypes.Where(t => ModelTypes.Any(d => d != t && d.IsSubclassOf(t))).Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(BaseClasses))]
    public void Every_base_class_declares_all_its_concrete_descendants(System.Type baseType)
    {
        var declared = baseType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
            .Select(a => a.DerivedType).ToHashSet();
        var concreteDescendants = ModelTypes.Where(d => !d.IsAbstract && d != baseType && d.IsSubclassOf(baseType)).ToHashSet();

        var missing = concreteDescendants.Except(declared).Select(t => t.Name).Order().ToList();
        var stale = declared.Except(concreteDescendants).Select(t => t.Name).Order().ToList();

        Assert.True(missing.Count == 0, $"{baseType.Name} lacks [JsonDerivedType] for: {string.Join(", ", missing)}");
        Assert.True(stale.Count == 0, $"{baseType.Name} declares non-descendants: {string.Join(", ", stale)}");
    }

    public static IEnumerable<object[]> ConcreteClasses() =>
        ModelTypes.Where(t => !t.IsAbstract && t.GetConstructor(System.Type.EmptyTypes) is not null).Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(ConcreteClasses))]
    public void Every_concrete_class_serialises_through_a_base_typed_property(System.Type concreteType)
    {
        var instance = (LinkedArtObject)Activator.CreateInstance(concreteType)!;
        instance.Id = "https://example.org/" + concreteType.Name;

        // A base-typed list is the common case: identified_by, equivalent, about, represents, ...
        var holder = new Set { Id = "https://example.org/set", About = [instance] };
        var json = JsonSerializer.Serialize(holder);

        Assert.Contains($"\"type\":\"{instance.Type}\"", json);
        Assert.Contains("https://example.org/" + concreteType.Name, json);
    }

    [Fact]
    public void Type_names_match_class_names_except_TimeSpan()
    {
        foreach (var t in ModelTypes.Where(t => !t.IsAbstract && t.GetConstructor(System.Type.EmptyTypes) is not null))
        {
            var instance = (LinkedArtObject)Activator.CreateInstance(t)!;
            var expected = t == typeof(LinkedArtTimeSpan) ? "TimeSpan"
                : t.Name.StartsWith("IIIF") ? instance.Type // IIIF helpers keep their Linked Art base type
                : t.Name;
            Assert.Equal(expected, instance.Type);
        }
    }

    [Fact]
    public void Embedded_activities_serialise_inside_an_activity_part_list()
    {
        var activity = new Activity
        {
            Id = "https://example.org/prov/1",
            Part =
            [
                new Acquisition { Label = "sale" },
                new Payment { Label = "money", PaidAmount = new MonetaryAmount { Value = 1, Currency = new Currency("https://example.org/gbp", "GBP") } },
                new Move { Label = "shipping" },
                new TransferOfCustody { Label = "loan" },
                new RightAcquisition { Label = "rights" },
            ],
        };
        var json = JsonSerializer.Serialize(activity);
        foreach (var name in new[] { "Acquisition", "Payment", "Move", "TransferOfCustody", "RightAcquisition", "MonetaryAmount", "Currency" })
            Assert.Contains($"\"type\":\"{name}\"", json);
    }
}
