using System.Collections.Frozen;
using System.Reflection;

namespace LinkedArtNet.Serialization;

/// <summary>Maps Linked Art <c>type</c> names to the C# classes that serialise them, and back.</summary>
public static class LinkedArtTypes
{
    private static readonly FrozenDictionary<string, System.Type> ByName = Build();

    /// <summary>All concrete model classes, keyed by the <c>type</c> string they write.</summary>
    public static IReadOnlyDictionary<string, System.Type> All => ByName;

    /// <summary>The class for a <c>type</c> name (e.g. "HumanMadeObject", "TimeSpan"), or null if unknown.</summary>
    public static System.Type? Resolve(string typeName) => ByName.GetValueOrDefault(typeName);

    public static bool TryResolve(string typeName, out System.Type type)
    {
        var found = ByName.GetValueOrDefault(typeName);
        type = found!;
        return found is not null;
    }

    /// <summary>The <c>type</c> string a class writes, e.g. "TimeSpan" for <see cref="LinkedArtTimeSpan"/>.</summary>
    public static string TypeNameOf(System.Type modelClass)
    {
        if (!typeof(LinkedArtObject).IsAssignableFrom(modelClass) || modelClass.IsAbstract)
            throw new ArgumentException($"{modelClass.Name} is not a concrete Linked Art model class.", nameof(modelClass));
        var instance = (LinkedArtObject?)Activator.CreateInstance(modelClass, nonPublic: true)
            ?? throw new ArgumentException($"{modelClass.Name} has no parameterless constructor.", nameof(modelClass));
        return instance.Type;
    }

    private static FrozenDictionary<string, System.Type> Build()
    {
        var map = new Dictionary<string, System.Type>(StringComparer.Ordinal);
        var concrete = typeof(LinkedArtObject).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(LinkedArtObject).IsAssignableFrom(t)
                        && t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, System.Type.EmptyTypes) is not null);

        foreach (var type in concrete)
        {
            var name = ((LinkedArtObject)Activator.CreateInstance(type)!).Type;
            // Helpers such as IIIFManifest write their base class's type; the class named like the type wins.
            if (!map.TryGetValue(name, out var existing) || type.Name == name || type.Name == "LinkedArt" + name)
            {
                if (existing is null || existing.Name != name && existing.Name != "LinkedArt" + name)
                    map[name] = type;
            }
        }
        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
