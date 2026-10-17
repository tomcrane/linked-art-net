using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet.Dates.Periods;

/// <summary>One PeriodO period definition, compacted: the fields the parser needs and nothing else.</summary>
/// <param name="Id">The PeriodO id (<c>p0f65r2qmh2</c>); the permanent URI is <see cref="Uri"/>.</param>
/// <param name="Authority">The id of the authority (the published source) that defines it.</param>
/// <param name="StartEarliest">Astronomical year: the start is no earlier than this.</param>
/// <param name="StartLatest">Astronomical year: the start is no later than this (equal to <see cref="StartEarliest"/> when the source gives one year).</param>
/// <param name="StopEarliest">Null when the source leaves the period open.</param>
/// <param name="Spatial">The source's own description of where the period applies ("Levant", "Britain").</param>
public sealed record PeriodOPeriod(string Id, string Authority, string Label, IReadOnlyList<string> AltLabels, string? Language,
    long StartEarliest, long StartLatest, long? StopEarliest, long? StopLatest, string? Spatial)
{
    public const string UriBase = "http://n2t.net/ark:/99152/";
    public string Uri => UriBase + Id;
}

public sealed record PeriodOAuthority(string Id, string? Title);

/// <summary>
/// The compact form of the PeriodO dataset that the repository keeps at <c>periods/periodo.json</c>, written by
/// <c>tools/Corpus periodo</c> from https://data.perio.do/dataset/ and read by <see cref="PeriodOSource"/>.
/// </summary>
public sealed record PeriodOData(string Version, string Modified, string Source, string Licence, string Attribution,
    IReadOnlyList<PeriodOAuthority> Authorities, IReadOnlyList<PeriodOPeriod> Periods)
{
    public const string DatasetUrl = "https://data.perio.do/dataset/";
    public const string LicenceName = "CC0-1.0";
    public const string AttributionText = "PeriodO: a gazetteer of period definitions for linking and visualizing data (https://perio.do), dedicated to the public domain under CC0 1.0 by its contributors; the definitions belong to the cited authorities.";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static PeriodOData Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<PeriodOData>(stream, JsonOptions) ?? throw new InvalidDataException($"{path}: empty PeriodO file.");
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }

    /// <summary>
    /// Compacts the full dataset (the JSON-LD bag of authorities at <see cref="DatasetUrl"/>). Each period keeps its
    /// id, authority, label, alternative labels, language, bounds and spatial description; a period without a
    /// label or a start is dropped. Years are PeriodO's astronomical years (year 0 is 1 BCE), which is the
    /// canonical form's convention too, so they pass through unchanged.
    /// </summary>
    public static PeriodOData FromDataset(Stream json, string version, string modified)
    {
        using var doc = JsonDocument.Parse(json);
        var authorities = new List<PeriodOAuthority>();
        var periods = new List<PeriodOPeriod>();
        foreach (var authority in doc.RootElement.GetProperty("authorities").EnumerateObject())
        {
            var authorityId = Str(authority.Value, "id") ?? authority.Name;
            authorities.Add(new PeriodOAuthority(authorityId, SourceTitle(authority.Value)));
            if (!authority.Value.TryGetProperty("periods", out var defs) || defs.ValueKind != JsonValueKind.Object) continue;
            foreach (var def in defs.EnumerateObject())
            {
                var e = def.Value;
                var label = Str(e, "label")?.Trim();
                if (string.IsNullOrEmpty(label)) continue;
                var (startEarliest, startLatest) = Bounds(e, "start");
                if (startEarliest is null && startLatest is null) continue;
                var (stopEarliest, stopLatest) = Bounds(e, "stop");
                var alts = new List<string>();
                if (e.TryGetProperty("localizedLabels", out var localized) && localized.ValueKind == JsonValueKind.Object)
                    foreach (var language in localized.EnumerateObject())
                        if (language.Value.ValueKind == JsonValueKind.Array)
                            foreach (var v in language.Value.EnumerateArray())
                                if (v.ValueKind == JsonValueKind.String && v.GetString()?.Trim() is { Length: > 0 } alt && alt != label && !alts.Contains(alt, StringComparer.Ordinal)) alts.Add(alt);
                periods.Add(new PeriodOPeriod(Str(e, "id") ?? def.Name, authorityId, label, alts, Str(e, "languageTag"),
                    startEarliest ?? startLatest!.Value, startLatest ?? startEarliest!.Value, stopEarliest ?? stopLatest, stopLatest ?? stopEarliest, Str(e, "spatialCoverageDescription")?.Trim()));
            }
        }
        authorities.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        periods.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        return new PeriodOData(version, modified, DatasetUrl, LicenceName, AttributionText, authorities, periods);
    }

    private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Astronomical years from <c>start.in</c> / <c>stop.in</c>: <c>year</c>, or <c>earliestYear</c> and <c>latestYear</c>; strings or numbers.</summary>
    private static (long? Earliest, long? Latest) Bounds(JsonElement period, string name)
    {
        if (!period.TryGetProperty(name, out var bound) || !bound.TryGetProperty("in", out var within) || within.ValueKind != JsonValueKind.Object) return (null, null);
        long? Year(string key)
        {
            if (!within.TryGetProperty(key, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n;
            return v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }
        var year = Year("year");
        if (year is not null) return (year, year);
        return (Year("earliestYear"), Year("latestYear"));
    }

    /// <summary>The authority's citation: <c>source.title</c>, else <c>source.citation</c>, else the title of what it is part of.</summary>
    private static string? SourceTitle(JsonElement authority)
    {
        if (!authority.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.Object) return null;
        var title = Str(source, "title") ?? Str(source, "citation");
        if (title is null && source.TryGetProperty("partOf", out var partOf) && partOf.ValueKind == JsonValueKind.Object) title = Str(partOf, "title") ?? Str(partOf, "citation");
        title = title?.Trim();
        return title is { Length: > 160 } ? title[..157].TrimEnd() + "..." : title;
    }
}

/// <summary>Narrows which PeriodO definitions a name may resolve to when several share it.</summary>
public sealed record PeriodOOptions
{
    /// <summary>Case-insensitive substring of the definition's spatial description ("Britain", "Levant").</summary>
    public string? Place { get; init; }
    /// <summary>PeriodO authority ids whose definitions are preferred.</summary>
    public IReadOnlyList<string> Authorities { get; init; } = [];
    /// <summary>Language tags ("en", "fr") whose definitions are preferred.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];
}

/// <summary>
/// Period names from PeriodO (https://perio.do), a public-domain gazetteer of 9,000-odd period definitions from
/// some 500 published sources. A name that one source defines resolves to that definition, keyed by its PeriodO id
/// so the canonical form (<c>period:p0f65r2qmh2</c>) is a stable, dereferenceable reference. A name that many
/// sources define ("Iron Age": forty-odd regions and bounds) resolves to the hull of them all, keyed
/// <c>periodo-iron-age</c>, with a note saying so; <see cref="PeriodOOptions"/> narrows by place, authority or
/// language. Combine with the built-in tables through <see cref="WithBuiltIn"/>, which asks them first.
/// </summary>
public sealed class PeriodOSource : INamedPeriodSource
{
    private readonly PeriodOData _data;
    private readonly PeriodOOptions _options;
    private readonly Dictionary<string, List<PeriodOPeriod>> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _slugToName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PeriodOPeriod> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _authorityTitles;

    private static readonly ConcurrentDictionary<string, PeriodOData> Loaded = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, PeriodOSource> Built = new(StringComparer.OrdinalIgnoreCase);

    public PeriodOSource(PeriodOData data, PeriodOOptions? options = null)
    {
        _data = data;
        _options = options ?? new PeriodOOptions();
        _authorityTitles = data.Authorities.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First().Title, StringComparer.Ordinal);
        foreach (var p in data.Periods)
        {
            _byId[p.Id] = p;
            foreach (var name in p.AltLabels.Prepend(p.Label))
            {
                var key = JsonNamedPeriodSource.Normalise(name);
                if (key.Length == 0) continue;
                if (!_byName.TryGetValue(key, out var list)) _byName[key] = list = [];
                if (!list.Contains(p)) list.Add(p);
                _slugToName.TryAdd(Slug(key), key);
            }
        }
    }

    public PeriodOData Data => _data;
    public int Count => _data.Periods.Count;

    /// <summary>Loads the compact file once per path and keeps it, with the index built once for the default options; the data is a couple of megabytes.</summary>
    public static PeriodOSource Load(string path, PeriodOOptions? options = null)
    {
        var full = Path.GetFullPath(path);
        return options is null
            ? Built.GetOrAdd(full, f => new PeriodOSource(Loaded.GetOrAdd(f, PeriodOData.Load)))
            : new PeriodOSource(Loaded.GetOrAdd(full, PeriodOData.Load), options);
    }

    public const string DefaultRelativePath = "periods/periodo.json";

    /// <summary>The built-in tables first, then PeriodO from <paramref name="path"/>; just the built-in tables when the file is absent.</summary>
    public static INamedPeriodSource WithBuiltIn(string? path, PeriodOOptions? options = null) =>
        path is not null && File.Exists(path) ? new CompositePeriodSource(JsonNamedPeriodSource.BuiltIn, Load(path, options)) : JsonNamedPeriodSource.BuiltIn;

    /// <summary>The sources for a checkout or image laid out like the repository: <c>periods/periodo.json</c> under the root.</summary>
    public static INamedPeriodSource ForRepository(string? repoRoot, PeriodOOptions? options = null) =>
        WithBuiltIn(repoRoot is null ? null : Path.Combine(repoRoot, "periods", "periodo.json"), options);

    public bool TryResolve(string text, out NamedPeriod period)
    {
        period = null!;
        var key = JsonNamedPeriodSource.Normalise(text);
        if (key.Length == 0) return false;
        var candidates = Lookup(key);
        if (candidates is null) return false;
        period = Resolve(key, candidates);
        return true;
    }

    /// <summary>Nothing: nine thousand names, some of them ordinary words ("Italian", "Classical"), must not be read inside longer text. <see cref="NamedPeriodStrategy"/> matches them as the whole string.</summary>
    public INamedPeriodSource ForGrammar() => EmptyPeriodSource.Instance;

    public bool TryResolveKey(string key, out NamedPeriod period)
    {
        period = null!;
        if (_byId.TryGetValue(key, out var p)) { period = Single(p); return true; }
        if (key.StartsWith("periodo-", StringComparison.Ordinal) && _slugToName.TryGetValue(key["periodo-".Length..], out var name) && _byName.TryGetValue(name, out var list))
        {
            period = Resolve(name, list);
            return true;
        }
        return false;
    }

    /// <summary>Every definition a name matches, before narrowing; for a reviewer choosing among them.</summary>
    public IReadOnlyList<PeriodOPeriod> Definitions(string text) => Lookup(JsonNamedPeriodSource.Normalise(text)) ?? [];

    public string? AuthorityTitle(string authorityId) => _authorityTitles.GetValueOrDefault(authorityId);

    private List<PeriodOPeriod>? Lookup(string key) => _byName.TryGetValue(key, out var list) ? list : null;

    private NamedPeriod Resolve(string key, List<PeriodOPeriod> candidates)
    {
        var narrowed = Narrow(candidates);
        return narrowed.Count == 1 ? Single(narrowed[0]) : Hull(key, narrowed);
    }

    /// <summary>Applies the options in turn, keeping a filter only when something survives it.</summary>
    private List<PeriodOPeriod> Narrow(List<PeriodOPeriod> candidates)
    {
        var list = candidates;
        if (_options.Authorities.Count > 0) list = Keep(list, p => _options.Authorities.Contains(p.Authority, StringComparer.OrdinalIgnoreCase));
        if (_options.Place is { Length: > 0 } place) list = Keep(list, p => p.Spatial?.Contains(place, StringComparison.OrdinalIgnoreCase) == true);
        if (_options.Languages.Count > 0) list = Keep(list, p => p.Language is not null && _options.Languages.Contains(p.Language, StringComparer.OrdinalIgnoreCase));
        return list;
        static List<PeriodOPeriod> Keep(List<PeriodOPeriod> from, Func<PeriodOPeriod, bool> predicate) { var kept = from.Where(predicate).ToList(); return kept.Count > 0 ? kept : from; }
    }

    private NamedPeriod Single(PeriodOPeriod p)
    {
        var where = p.Spatial is null ? "" : $" ({p.Spatial})";
        var source = $"PeriodO {p.Id}{where}, as defined in {AuthorityTitle(p.Authority) ?? p.Authority}.";
        return new NamedPeriod(p.Id, p.Label, Bounds(p.StartEarliest, p.StopLatest), p.Uri, source);
    }

    private NamedPeriod Hull(string key, List<PeriodOPeriod> list)
    {
        var label = list.GroupBy(p => p.Label, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
        var start = list.Min(p => p.StartEarliest);
        long? stop = list.Any(p => p.StopLatest is null) ? null : list.Max(p => p.StopLatest!.Value);
        var authorities = list.Select(p => p.Authority).Distinct().Count();
        var places = list.Select(p => p.Spatial).Where(s => !string.IsNullOrEmpty(s)).Distinct().Take(4).ToList();
        var source = $"PeriodO has {list.Count} definitions of \"{label}\" from {authorities} authorities" +
                     (places.Count > 0 ? $" ({string.Join(", ", places)}{(list.Count > places.Count ? ", ..." : "")})" : "") +
                     $"; their hull, {YearText(start)} to {(stop is null ? "the present" : YearText(stop.Value))}, is used. Narrow it with a place or authority (PeriodOOptions), or name one definition as period:<id>.";
        return new NamedPeriod("periodo-" + Slug(key), label, Bounds(start, stop), null, source);
    }

    private static Interval Bounds(long start, long? stop) => new(new YearPoint(start), stop is null ? null : new YearPoint(stop.Value));

    private static string Slug(string normalisedKey) => normalisedKey.Replace(' ', '-');

    private static string YearText(long year) => year switch
    {
        <= -1_000_000 => $"{-year / 1_000_000.0:0.#} million years ago",
        <= 0 => $"{1 - year} BCE",
        _ => year.ToString(CultureInfo.InvariantCulture),
    };
}
