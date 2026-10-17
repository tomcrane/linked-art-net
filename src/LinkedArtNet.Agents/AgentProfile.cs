using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet.Agents;

/// <summary>What a bracketed or parenthesised group may hold in a source's convention.</summary>
public enum GroupMeaning { Dates, Honorific, Role, Qualifier, Nationality }

/// <summary>
/// The conventions of one source's agent strings: what square brackets, parentheses and braces carry, whether
/// comma-separated trailing segments (MARC headings) and semicolon forms (EAD authority names) are read, whether a
/// name is required at all (ULAN display strings have none), whether "Surname, Forenames" inversion applies, and
/// which dates profile reads the date part. Profiles are JSON files embedded under <c>Profiles/</c>; a file may say
/// <c>"inherits": "default"</c> and state only what it changes.
/// </summary>
public sealed record AgentProfile
{
    public string Name { get; init; } = "default";
    public string? Description { get; init; }
    public string? Inherits { get; init; }
    public IReadOnlyList<GroupMeaning> Brackets { get; init; } = [GroupMeaning.Dates, GroupMeaning.Honorific, GroupMeaning.Qualifier, GroupMeaning.Role];
    public IReadOnlyList<GroupMeaning> Parentheses { get; init; } = [GroupMeaning.Role, GroupMeaning.Qualifier, GroupMeaning.Dates, GroupMeaning.Honorific];
    public IReadOnlyList<GroupMeaning> Braces { get; init; } = [GroupMeaning.Dates];
    /// <summary>Read "Surname, Forenames, 1818-1894, artist": trailing comma segments that classify as dates, honorifics, roles or nationality phrases are taken off the name.</summary>
    public bool TrailingSegments { get; init; } = true;
    /// <summary>Read "Smith; John (1900-1980); artist": surname; forenames; epithets.</summary>
    public bool SemicolonForm { get; init; }
    /// <summary>False for strings that describe without naming ("Dutch painter, 1606-1669").</summary>
    public bool NameRequired { get; init; } = true;
    /// <summary>"Surname, Forenames" is inverted order; a single comma splits family and given names.</summary>
    public bool InvertedNames { get; init; } = true;
    /// <summary>The dates profile (profiles/*.json of the dates parser) for the date part.</summary>
    public string DatesProfile { get; init; } = "default";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static readonly AgentProfile Default = new();
}

/// <summary>The built-in profiles, and any added from files; <c>inherits</c> is resolved on load.</summary>
public sealed class AgentProfileCatalog
{
    private readonly Dictionary<string, AgentProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public static readonly string[] BuiltInNames = ["default", "dscribe", "marc", "ulan", "ead", "wellcome"];

    private static readonly Lazy<AgentProfileCatalog> _builtIn = new(() =>
    {
        var catalog = new AgentProfileCatalog();
        var raw = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in BuiltInNames)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LinkedArtNet.Agents.Profiles.{name}.json")
                ?? throw new FileNotFoundException($"No built-in agent profile '{name}'.");
            raw[name] = JsonDocument.Parse(stream).RootElement.Clone();
        }
        foreach (var name in BuiltInNames) catalog.Add(Resolve(name, raw, []));
        return catalog;
    });

    public static AgentProfileCatalog BuiltIn => _builtIn.Value;

    public IReadOnlyDictionary<string, AgentProfile> Profiles => _profiles;

    public AgentProfileCatalog Add(AgentProfile profile) { _profiles[profile.Name] = profile; return this; }

    public bool TryGet(string? name, out AgentProfile profile)
    {
        if (string.IsNullOrEmpty(name)) { profile = _profiles.GetValueOrDefault("default") ?? AgentProfile.Default; return true; }
        return _profiles.TryGetValue(name, out profile!);
    }

    public AgentProfile Get(string? name) => TryGet(name, out var p) ? p : throw new KeyNotFoundException($"Unknown agent profile '{name}'.");

    /// <summary>A child profile is its parent's JSON with the child's properties written over it.</summary>
    private static AgentProfile Resolve(string name, Dictionary<string, JsonElement> raw, HashSet<string> chain)
    {
        if (!chain.Add(name)) throw new InvalidDataException($"Agent profile inheritance loops at '{name}'.");
        var element = raw[name];
        var merged = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (element.TryGetProperty("inherits", out var parent) && parent.ValueKind == JsonValueKind.String)
        {
            var parentJson = JsonSerializer.SerializeToElement(Resolve(parent.GetString()!, raw, chain), AgentProfile.JsonOptions);
            foreach (var p in parentJson.EnumerateObject()) merged[p.Name] = p.Value;
        }
        foreach (var p in element.EnumerateObject()) merged[p.Name] = p.Value;
        merged.Remove("inherits");
        var profile = JsonSerializer.Deserialize<AgentProfile>(JsonSerializer.Serialize(merged), AgentProfile.JsonOptions)!;
        if (!string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Agent profile file '{name}' says its name is '{profile.Name}'.");
        return profile;
    }
}
