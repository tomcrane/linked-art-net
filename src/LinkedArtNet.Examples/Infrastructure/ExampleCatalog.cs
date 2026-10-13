using System.Reflection;

namespace LinkedArtNet.Examples;

/// <summary>One example method: its site id, how to build it, and whether a site problem blocks validation.</summary>
public sealed record Example(string Id, MethodInfo Method, SiteIssueAttribute? SiteIssue)
{
    public const string SiteRoot = "https://linked.art/example/";

    /// <summary>The URI the example object carries as its <c>id</c>, and the site's raw JSON-LD page.</summary>
    public string Uri => SiteRoot + Id;

    public string JsonUrl => Uri + ".json";

    /// <summary>The containing class, named after the model page the example comes from.</summary>
    public string Topic => Method.DeclaringType!.Name;

    public string Name => Method.Name;

    /// <summary>
    /// <c>Topic.Name</c>: unique even where two methods share an id because the site publishes two different
    /// examples under one URL (each such method carries a <see cref="SiteIssueAttribute"/>).
    /// </summary>
    public string Key => $"{Topic}.{Name}";

    public LinkedArtObject Build() => (LinkedArtObject)Method.Invoke(null, null)!;

    public override string ToString() => Key;
}

/// <summary>Every <see cref="LinkedArtExampleAttribute"/> method in this assembly, in source order within each topic.</summary>
public static class ExampleCatalog
{
    public static IReadOnlyList<Example> All { get; } = Discover();

    /// <summary>The method expected to match the site's JSON for an id: the one without a site issue, if any.</summary>
    public static Example? Find(string id) => ForId(id).FirstOrDefault();

    /// <summary>Every method for an id, the one without a site issue first.</summary>
    public static IReadOnlyList<Example> ForId(string id) =>
        All.Where(e => e.Id == id).OrderBy(e => e.SiteIssue is null ? 0 : 1).ToList();

    public static Example? FindByKey(string key) => All.FirstOrDefault(e => e.Key == key);

    public static IReadOnlyCollection<string> Ids { get; } = All.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);

    private static List<Example> Discover()
    {
        var examples = new List<Example>();
        foreach (var type in typeof(ExampleCatalog).Assembly.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.MetadataToken))
            {
                var attribute = method.GetCustomAttribute<LinkedArtExampleAttribute>();
                if (attribute is null) continue;
                if (method.GetParameters().Length != 0 || !typeof(LinkedArtObject).IsAssignableFrom(method.ReturnType))
                    throw new InvalidOperationException($"{type.Name}.{method.Name}: a [LinkedArtExample] method takes no parameters and returns a LinkedArtObject.");
                examples.Add(new Example(attribute.Id, method, method.GetCustomAttribute<SiteIssueAttribute>()));
            }
        }
        return examples;
    }
}
