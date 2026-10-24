using System.Reflection;
using System.Text.Json;
using LinkedArtNet.Vocabulary;

namespace LinkedArtNet.Agents;

/// <summary>A role phrase's meaning: its label, the AAT URI when the registry has the term, and whether it names an occupation.</summary>
public sealed record RoleEntry(string Label, string? AatUri, bool IsOccupation);

/// <summary>A nationality adjective's meaning: the label and the AAT URI when the registry has the term.</summary>
public sealed record NationalityEntry(string Label, string? AatUri);

/// <summary>
/// The word lists the parser consults, loaded once from the embedded JSON tables under <c>Tables/</c>. Every lookup
/// key is normalised with <see cref="Key"/>: lower case, trimmed, trailing full stop removed, spaces collapsed.
/// AAT identifiers come only from the repository's registry (<see cref="Aat"/>), resolved by the registry's own
/// property names, so the tables never carry an identifier the registry has not verified.
/// </summary>
public sealed class AgentTables
{
    public static AgentTables Default { get; } = Load();

    public IReadOnlySet<string> Honorifics { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> Suffixes { get; private init; } = new HashSet<string>();
    /// <summary>The honorifics that follow a name (Bt, Esq, KBE, RA): the only ones read off the end of the given names (finding 94).</summary>
    public IReadOnlySet<string> Postnominals { get; private init; } = new HashSet<string>();
    /// <summary>Titles the data uses as forenames (Dean, Earl): kept when they lead the given names of an inverted heading (finding 94).</summary>
    public IReadOnlySet<string> ForenamesToo { get; private init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, AgentQualifier> Qualifiers { get; private init; } = new Dictionary<string, AgentQualifier>();
    public IReadOnlySet<string> CorporateWords { get; private init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, NationalityEntry> Nationalities { get; private init; } = new Dictionary<string, NationalityEntry>();
    public IReadOnlyDictionary<string, RoleEntry> Roles { get; private init; } = new Dictionary<string, RoleEntry>();
    /// <summary>Longest qualifier phrase, in words, so a leading-phrase scan knows how far to look.</summary>
    public int LongestQualifierWords { get; private init; }
    public int LongestNationalityWords { get; private init; }

    public static string Key(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        while (t.EndsWith('.') || t.EndsWith(',') || t.EndsWith(';')) t = t[..^1].TrimEnd();
        return string.Join(' ', t.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public bool TryRole(string text, out RoleEntry entry) => Roles.TryGetValue(Key(text), out entry!);
    public bool IsHonorific(string text) => Honorifics.Contains(Key(text));
    public bool IsSuffix(string text) => Suffixes.Contains(Key(text));
    public bool IsPostnominal(string text) => Postnominals.Contains(Key(text));
    public bool IsForenameToo(string text) => ForenamesToo.Contains(Key(text));
    public bool TryQualifier(string text, out AgentQualifier qualifier) => Qualifiers.TryGetValue(Key(text), out qualifier);
    public bool TryNationality(string text, out NationalityEntry entry) => Nationalities.TryGetValue(Key(text), out entry!);
    public bool IsCorporateWord(string word) => CorporateWords.Contains(Key(word).Trim('&'));

    private static AgentTables Load()
    {
        var honorifics = Read("honorifics");
        var qualifiers = Read("qualifiers");
        var corporate = Read("corporate-words");
        var nationalities = Read("nationalities");
        var roles = Read("roles");

        var qualifierMap = new Dictionary<string, AgentQualifier>(StringComparer.Ordinal);
        foreach (var property in qualifiers.GetProperty("qualifiers").EnumerateObject())
        {
            var qualifier = Enum.Parse<AgentQualifier>(property.Name, ignoreCase: true);
            foreach (var phrase in property.Value.EnumerateArray()) qualifierMap[Key(phrase.GetString()!)] = qualifier;
        }

        var nationalityMap = new Dictionary<string, NationalityEntry>(StringComparer.Ordinal);
        foreach (var entry in nationalities.GetProperty("nationalities").EnumerateArray())
        {
            var term = entry.TryGetProperty("term", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            var (label, uri) = Registry(term);
            foreach (var adjective in entry.GetProperty("adjectives").EnumerateArray())
            {
                var text = adjective.GetString()!;
                nationalityMap[Key(text)] = new NationalityEntry(label ?? text, uri);
            }
        }

        var roleMap = new Dictionary<string, RoleEntry>(StringComparer.Ordinal);
        foreach (var entry in roles.GetProperty("roles").EnumerateArray())
        {
            var term = entry.TryGetProperty("aat", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
            var (_, uri) = Registry(term);
            var role = new RoleEntry(entry.GetProperty("label").GetString()!, uri, entry.GetProperty("occupation").GetBoolean());
            foreach (var phrase in entry.GetProperty("phrases").EnumerateArray()) roleMap.TryAdd(Key(phrase.GetString()!), role);
            roleMap.TryAdd(Key(role.Label), role);
        }

        return new AgentTables
        {
            Honorifics = honorifics.GetProperty("honorifics").EnumerateArray().Select(h => Key(h.GetString()!)).ToHashSet(StringComparer.Ordinal),
            Suffixes = honorifics.GetProperty("suffixes").EnumerateArray().Select(h => Key(h.GetString()!)).ToHashSet(StringComparer.Ordinal),
            Postnominals = honorifics.GetProperty("postnominals").EnumerateArray().Select(h => Key(h.GetString()!)).ToHashSet(StringComparer.Ordinal),
            ForenamesToo = honorifics.GetProperty("forenamesToo").EnumerateArray().Select(h => Key(h.GetString()!)).ToHashSet(StringComparer.Ordinal),
            Qualifiers = qualifierMap,
            CorporateWords = corporate.GetProperty("words").EnumerateArray().Select(w => Key(w.GetString()!)).ToHashSet(StringComparer.Ordinal),
            Nationalities = nationalityMap,
            Roles = roleMap,
            LongestQualifierWords = qualifierMap.Keys.Max(k => k.Split(' ').Length),
            LongestNationalityWords = nationalityMap.Keys.Max(k => k.Split(' ').Length),
        };
    }

    /// <summary>The label and URI of a registry term named like the generated <see cref="Aat"/> property ("DutchNationality", "Author").</summary>
    private static (string? Label, string? Uri) Registry(string? name)
    {
        if (string.IsNullOrEmpty(name)) return (null, null);
        var property = typeof(Aat).GetProperty(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidDataException($"The agent tables name a registry term '{name}' that vocabulary/aat.json does not have.");
        var type = (LinkedArtObject)property.GetValue(null)!;
        return (type.Label, type.Id);
    }

    private static JsonElement Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LinkedArtNet.Agents.Tables.{name}.json")
            ?? throw new FileNotFoundException($"No embedded table '{name}'.");
        return JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement.Clone();
    }
}
