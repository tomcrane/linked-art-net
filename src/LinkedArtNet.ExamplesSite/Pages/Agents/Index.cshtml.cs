using LinkedArtNet.Agents;
using LinkedArtNet.Dates.Realisation;
using LinkedArtNet.ExamplesSite.Services;
using LinkedArtNet.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Agents;

/// <summary>The agent-string playground: one string in, the parts and the Linked Art Person or Group out. Parses in-process.</summary>
public class IndexModel(ExampleIndex index) : PageModel
{
    private static readonly Dictionary<string, AgentStringParser> Parsers = new(StringComparer.OrdinalIgnoreCase);

    [BindProperty(SupportsGet = true)] public string? S { get; set; }
    [BindProperty(SupportsGet = true)] public string Profile { get; set; } = "default";

    public ParsedAgent? Result { get; private set; }
    public string? AgentJson { get; private set; }
    public IReadOnlyList<AgentProfile> Profiles => AgentProfileCatalog.BuiltIn.Profiles.Values.OrderBy(p => p.Name == "default" ? "" : p.Name, StringComparer.Ordinal).ToList();

    public static readonly IReadOnlyList<(string Text, string Profile)> Samples =
    [
        ("Grierson, Herbert John Clifford [Sir, 1866-1960] (preface by)", "dscribe"),
        ("Crane, Thomas [1971-] (developer)", "dscribe"),
        ("Royal Academy of Arts [1768-] (publisher)", "dscribe"),
        ("Leconte de Lisle, Charles Marie, 1818-1894", "marc"),
        ("Smith, John, active 1640-1660, ill.", "marc"),
        ("Dutch painter, 1606-1669", "ulan"),
        ("French illuminator, active mid-15th century", "ulan"),
        ("Smith; John (1900-1980); artist", "ead"),
        ("attributed to Rembrandt", "default"),
        ("Rembrandt and others", "default"),
    ];

    public void OnGet()
    {
        if (!AgentProfileCatalog.BuiltIn.TryGet(Profile, out var profile)) { Profile = "default"; profile = AgentProfile.Default; }
        if (string.IsNullOrWhiteSpace(S)) return;
        if (S.Length > 500) S = S[..500];
        AgentStringParser parser;
        lock (Parsers)
        {
            if (!Parsers.TryGetValue(profile.Name, out parser!))
            {
                var profilesDir = Path.Combine(index.RepoRoot, "profiles");
                Parsers[profile.Name] = parser = new AgentStringParser(profile, Directory.Exists(profilesDir) ? ProfileCatalog.Load(profilesDir) : null);
            }
        }
        Result = parser.Parse(S);
        if (Result.HasName || Result.HasDates || Result.Nationalities.Count > 0) AgentJson = LinkedArtJson.Serialize(Result.ToActor());
    }

    public string CSharp()
    {
        var profile = Profile == "default" ? "" : $"AgentProfileCatalog.BuiltIn.Get(\"{Profile}\")";
        return $"var parsed = new AgentStringParser({profile}).Parse({Quote(S ?? "")});\n" +
               (Result is { Kind: AgentKind.Group } ? "var group = parsed.ToGroup(\"https://example.org/group/1\");" : "var person = parsed.ToPerson(\"https://example.org/person/1\");");
    }

    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
