using System.Text.Json;
using System.Text.Json.Nodes;

namespace LinkedArtNet.Dates.Realisation;

/// <summary>
/// The profiles in a directory (<c>profiles/*.json</c>), by name. A file may say <c>"inherits": "default"</c>: the
/// base profile's JSON is loaded first and the file's properties are laid over it, so a profile states only what it
/// changes. <see cref="ParseProfile.Default"/> is always available as "default".
/// </summary>
public sealed class ProfileCatalog
{
    private readonly Dictionary<string, ParseProfile> _profiles = new(StringComparer.OrdinalIgnoreCase) { ["default"] = ParseProfile.Default };

    public IReadOnlyDictionary<string, ParseProfile> Profiles => _profiles;

    public ParseProfile this[string name] => Get(name);

    public ParseProfile Get(string name) =>
        _profiles.TryGetValue(name, out var p) ? p : throw new KeyNotFoundException($"No profile named '{name}'. Known: {string.Join(", ", _profiles.Keys.Order())}.");

    public bool TryGet(string name, out ParseProfile profile) => _profiles.TryGetValue(name, out profile!);

    public ProfileCatalog Add(ParseProfile profile)
    {
        _profiles[profile.Name] = profile;
        return this;
    }

    /// <summary>Loads every <c>*.json</c> in a directory, resolving <c>inherits</c> in dependency order.</summary>
    public static ProfileCatalog Load(string directory)
    {
        var catalog = new ProfileCatalog();
        var pending = Directory.GetFiles(directory, "*.json").ToDictionary(f => Path.GetFileNameWithoutExtension(f), f => JsonNode.Parse(File.ReadAllText(f))!.AsObject(), StringComparer.OrdinalIgnoreCase);
        var resolved = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        JsonObject Resolve(string key, int depth)
        {
            if (resolved.TryGetValue(key, out var done)) return done;
            if (depth > 10) throw new InvalidDataException($"Profile '{key}': inheritance chain too deep (cycle?).");
            if (!pending.TryGetValue(key, out var node))
            {
                if (key.Equals("default", StringComparison.OrdinalIgnoreCase)) return resolved[key] = JsonNode.Parse(ParseProfile.Default.ToJson())!.AsObject();
                throw new InvalidDataException($"Profile inherits from unknown profile '{key}'.");
            }
            var merged = new JsonObject();
            if (node["inherits"] is { } inherits)
            {
                foreach (var (k, v) in Resolve(inherits.GetValue<string>(), depth + 1)) merged[k] = v?.DeepClone();
            }
            foreach (var (k, v) in node)
            {
                if (k == "inherits") continue;
                merged[k] = v?.DeepClone();
            }
            return resolved[key] = merged;
        }

        foreach (var key in pending.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            var json = Resolve(key, 0);
            var profile = ParseProfile.FromJson(json.ToJsonString(ParseProfile.JsonOptions));
            if (!string.Equals(profile.Name, key, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Profile file '{key}.json' is named '{profile.Name}'; the file name and the name must agree.");
            catalog._profiles[profile.Name] = profile;
        }
        return catalog;
    }
}
