using System.Text.Json;
using System.Text.Json.Serialization;
using LinkedArtNet.Dates;
using LinkedArtNet.Dates.Corpus;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Agents;

/// <summary>What a parse should yield; every field is optional and only set fields are checked. Lists compare as sets.</summary>
public sealed record AgentExpectation
{
    public string? Kind { get; init; }
    public string? Name { get; init; }
    public string? FamilyName { get; init; }
    public string? GivenNames { get; init; }
    public string? SortName { get; init; }
    public string? DisplayName { get; init; }
    public IReadOnlyList<string>? Honorifics { get; init; }
    /// <summary>Role phrases as written.</summary>
    public IReadOnlyList<string>? Roles { get; init; }
    /// <summary>Role labels after the table ("writer of preface" for "preface by").</summary>
    public IReadOnlyList<string>? RoleLabels { get; init; }
    /// <summary>Nationality labels.</summary>
    public IReadOnlyList<string>? Nationalities { get; init; }
    public IReadOnlyList<string>? Qualifiers { get; init; }
    public string? DateText { get; init; }
    /// <summary>The dates corpus's own expectation shape, checked against the parsed date part; a failure here is the dates parser's.</summary>
    public Expectation? Dates { get; init; }
    /// <summary>True: there must be no date part at all.</summary>
    public bool? NoDates { get; init; }
    /// <summary>True: nothing may be left unplaced.</summary>
    public bool? NoUnconsumed { get; init; }
    public double? MinConfidence { get; init; }
    public double? MaxConfidence { get; init; }
}

public sealed record AgentCorpusEntry
{
    public required string Id { get; init; }
    public required string Input { get; init; }
    public string Profile { get; init; } = "default";
    public IReadOnlyList<string> Tags { get; init; } = [];
    public required AgentExpectation Expect { get; init; }
    /// <summary>Null in the file means the file's default, else proposed; always set after <see cref="AgentCorpusFile.Load"/>.</summary>
    public EntryStatus? Status { get; init; }
    public string? Notes { get; init; }
    [JsonIgnore] public string File { get; init; } = "";
}

public sealed record AgentCorpusDefaults(string? Profile = null, EntryStatus? Status = null, IReadOnlyList<string>? Tags = null);

/// <summary>A file of agent strings with expectations, in the same spirit as the dates corpus (docs/06).</summary>
public sealed record AgentCorpusFile
{
    [JsonPropertyName("$schema")] public string? Schema { get; init; }
    public required CorpusSource Source { get; init; }
    public AgentCorpusDefaults? Defaults { get; init; }
    public required IReadOnlyList<AgentCorpusEntry> Entries { get; init; }
    [JsonIgnore] public string Path { get; init; } = "";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static AgentCorpusFile Load(string path, string? root = null)
    {
        var file = JsonSerializer.Deserialize<AgentCorpusFile>(System.IO.File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException($"{path}: empty corpus file.");
        var relative = root is null ? System.IO.Path.GetFileName(path) : System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
        var defaults = file.Defaults;
        var entries = file.Entries.Select(e => e with
        {
            File = relative,
            Profile = e.Profile == "default" && defaults?.Profile is { } p ? p : e.Profile,
            Status = e.Status ?? defaults?.Status ?? EntryStatus.Proposed,
            Tags = defaults?.Tags is { } t ? t.Concat(e.Tags).Distinct(StringComparer.Ordinal).ToList() : e.Tags,
        }).ToList();
        return file with { Path = relative, Entries = entries };
    }

    /// <summary>Every agent corpus file under a root, in path order.</summary>
    public static IReadOnlyList<AgentCorpusFile> LoadAll(string root) =>
        Directory.Exists(root)
            ? Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).Where(p => !p.Replace('\\', '/').Contains("/schema/")).Order(StringComparer.Ordinal).Select(p => Load(p, root)).ToList()
            : [];
}

public sealed record AgentEntryResult(AgentCorpusEntry Entry, ParsedAgent Result, IReadOnlyList<string> Failures)
{
    public bool Passed => Failures.Count == 0;
}

/// <summary>Runs agent corpus entries through the parser and checks each set field of the expectation.</summary>
public sealed class AgentCorpusEvaluator(AgentProfileCatalog? profiles = null, ProfileCatalog? datesProfiles = null)
{
    private readonly AgentProfileCatalog _profiles = profiles ?? AgentProfileCatalog.BuiltIn;
    private readonly Dictionary<string, AgentStringParser> _parsers = new(StringComparer.OrdinalIgnoreCase);

    public AgentStringParser ParserFor(string profile)
    {
        lock (_parsers)
        {
            if (_parsers.TryGetValue(profile, out var parser)) return parser;
            return _parsers[profile] = new AgentStringParser(_profiles.Get(profile), datesProfiles);
        }
    }

    public AgentEntryResult Evaluate(AgentCorpusEntry entry)
    {
        var result = ParserFor(entry.Profile).Parse(entry.Input);
        return new AgentEntryResult(entry, result, Check(entry.Expect, result));
    }

    public IReadOnlyList<AgentEntryResult> Evaluate(IEnumerable<AgentCorpusEntry> entries) => entries.Select(Evaluate).ToList();

    public static IReadOnlyList<string> Check(AgentExpectation expect, ParsedAgent a)
    {
        var failures = new List<string>();
        void Eq(string field, string? expected, string? actual) { if (expected is not null && !string.Equals(expected, actual, StringComparison.Ordinal)) failures.Add($"{field}: expected '{expected}', got '{actual ?? "(none)"}'"); }
        void Set(string field, IReadOnlyList<string>? expected, IEnumerable<string> actual)
        {
            if (expected is null) return;
            var want = expected.Order(StringComparer.Ordinal).ToList(); var got = actual.Order(StringComparer.Ordinal).ToList();
            if (!want.SequenceEqual(got, StringComparer.Ordinal)) failures.Add($"{field}: expected [{string.Join(", ", want)}], got [{string.Join(", ", got)}]");
        }
        Eq("kind", expect.Kind, a.Kind.ToString().ToLowerInvariant());
        Eq("name", expect.Name, a.Name);
        Eq("familyName", expect.FamilyName, a.FamilyName);
        Eq("givenNames", expect.GivenNames, a.GivenNames);
        Eq("sortName", expect.SortName, a.SortName);
        Eq("displayName", expect.DisplayName, a.DisplayName);
        Set("honorifics", expect.Honorifics, a.Honorifics);
        Set("roles", expect.Roles, a.Roles.Select(r => r.Text));
        Set("roleLabels", expect.RoleLabels, a.Roles.Select(r => r.Label));
        Set("nationalities", expect.Nationalities, a.Nationalities.Select(n => n.Label));
        Set("qualifiers", expect.Qualifiers, a.Qualifiers.Select(q => char.ToLowerInvariant(q.ToString()[0]) + q.ToString()[1..]));
        Eq("dateText", expect.DateText, a.DateText);
        if (expect.NoDates == true && a.DateText is not null) failures.Add($"dates: expected none, got '{a.DateText}'");
        if (expect.Dates is not null)
        {
            if (a.Dates is null) failures.Add("dates: expected a date part, got none");
            else failures.AddRange(CorpusEvaluator.Check(expect.Dates, a.Dates).Select(f => "dates." + f));
        }
        if (expect.NoUnconsumed == true && a.Unconsumed is not null) failures.Add($"unconsumed: '{a.Unconsumed}'");
        if (expect.MinConfidence is { } min && a.Confidence < min) failures.Add($"confidence {a.Confidence:0.00} below {min:0.00}");
        if (expect.MaxConfidence is { } max && a.Confidence > max) failures.Add($"confidence {a.Confidence:0.00} above {max:0.00}");
        return failures;
    }
}
