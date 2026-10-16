namespace LinkedArtNet.Examples;

/// <summary>
/// The shape of one method call in an example's source, as the source extractor sees it: enough to recognise an
/// idiom without a semantic model. <see cref="Qualifier"/> is the identifier before the dot when there is one
/// (<c>Aat</c> in <c>Aat.Term(...)</c>, or a variable name).
/// </summary>
public sealed record Invocation(string Name, string? Qualifier, int Arguments, bool FirstArgumentIsString, bool FirstArgumentIsNumber, bool HasLambda);

/// <summary>
/// A way of using the library worth a line of explanation when an example uses it. <see cref="Methods"/> names the
/// helpers it covers (simple names, or <c>Class.Method</c> for static factories), which the idiom tests check against
/// the hand-written extension methods so a new helper has to be placed somewhere. <see cref="When"/> narrows the match
/// to the call shape that is the idiom, since one method name can carry several overloads.
/// </summary>
public sealed record Idiom(string Key, string Title, string Note, IReadOnlyList<string> Methods, Func<Invocation, bool>? When = null, bool AnyMethod = false)
{
    public bool Matches(Invocation call)
    {
        if (!AnyMethod && !Methods.Contains(call.Name) && (call.Qualifier is null || !Methods.Contains(call.Qualifier + "." + call.Name))) return false;
        return When?.Invoke(call) ?? true;
    }
}

/// <summary>
/// The catalogue of idioms the examples site annotates. Kept small on purpose: a note is for something a reader
/// could misread or would not guess, not for every helper. Detection runs in tools/ExampleSourceExtractor, which
/// writes each example's idiom keys into examples/source.json; the site reads them from there.
/// </summary>
public static class Idioms
{
    public const string DocUrl = "https://github.com/tomcrane/linked-art-net/blob/de-pmc/docs/02-model-library.md#4-fluent-api";

    public static IReadOnlyList<Idiom> All { get; } =
    [
        new("metatype", "Metatype classification",
            "The second argument classifies the first, not the object: WithClassifiedAs(Aat.Description, Aat.Statement) says this is a Description, and Description is a kind of Statement. The Plain C# form shows the nesting.",
            ["WithClassifiedAs"],
            call => (call.Arguments == 2 && !call.FirstArgumentIsString) || call.Arguments >= 3),

        new("statement", "Statement in one call",
            "WithStatement creates the LinguisticObject, classifies it as the given type and as Brief Text, and adds it under referred_to_by.",
            ["WithStatement"]),

        new("roles", "Role shortcuts",
            "AsPrimaryName, AsAccessionNumber and AsSystemAssignedNumber each add a single classification, with the AAT terms Primary Name, Accession Number and System Number.",
            ["AsPrimaryName", "AsAccessionNumber", "AsSystemAssignedNumber"]),

        new("configure", "Configured in place",
            "A lambda configures the nested object as it is created, so a name, an identifier or a production is built where it is attached instead of in a separate variable.",
            ["WithName", "WithIdentifier"],
            call => call.HasLambda && call.Name.StartsWith("With", StringComparison.Ordinal), AnyMethod: true),

        new("physical", "Dimensions and physical description",
            "Each helper builds a Dimension with its AAT type, value and unit, or a classification with its metatype: height, width, colour, shape, page count, component count, file size, duration in days.",
            ["WithHeightDimension", "WithWidthDimension", "WithDimension", "WithRgbColor", "WithShape", "WithPageCount", "WithComponentCount", "WithFileSize", "WithDaysDimension"],
            call => call.Name != "WithDimension" || call.Arguments >= 3),

        new("terms", "Terms outside the registry",
            "The vocabulary registry holds only the terms linked.art itself publishes, so a dataset's own concepts, places and people are written with Aat.Term, Tgn.Place and Ulan.Person, giving the id and the label.",
            ["Aat.Term", "Aat.Material", "Aat.Unit", "Aat.Currency", "Aat.Language", "Aat.NationalityTerm", "Tgn.Place", "Ulan.Person", "Ulan.Group"]),
    ];

    /// <summary>Hand-written helpers with nothing to explain: they set one property, or read as what they do.</summary>
    public static IReadOnlyList<string> Plain { get; } =
    [
        "WithContext", "WithId", "WithLabel", "WithContent", "WithLanguage", "WithMadeOf", "WithTechnique", "AsReference",
        // WithTimeSpan(year), WithTimeSpan(y, m, d) and WithTimeSpan("free text") would deserve a note the day an example uses one.
        "WithTimeSpan",
    ];

    public static Idiom? Find(string key) => All.FirstOrDefault(i => i.Key == key);

    /// <summary>The idioms a method's calls use, in catalogue order.</summary>
    public static IReadOnlyList<string> Detect(IEnumerable<Invocation> calls)
    {
        var list = calls.ToList();
        return All.Where(i => list.Any(i.Matches)).Select(i => i.Key).ToList();
    }
}
