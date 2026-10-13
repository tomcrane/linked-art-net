using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using LinkedArtNet.Dates.Canonical;

namespace LinkedArtNet.Dates.Periods;

/// <summary>
/// Resolves period names ("Edo period", "Victorian", "Kangxi") and regnal years ("Meiji 27") to
/// <see cref="NamedPeriod"/> expressions with their bounds filled in. Also resolves canonical keys (<c>period:edo</c>).
/// </summary>
public interface INamedPeriodSource
{
    /// <summary>Resolve free text as a whole; the text is already normalised and may be any case.</summary>
    bool TryResolve(string text, out NamedPeriod period);

    /// <summary>Resolve a canonical key such as <c>edo</c> or <c>meiji-27</c>.</summary>
    bool TryResolveKey(string key, out NamedPeriod period);
}

/// <summary>One period in a JSON table.</summary>
public sealed record PeriodDefinition
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    /// <summary>Names and spellings that denote the period; matched case-insensitively as whole strings.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
    /// <summary>Canonical expression for the start (a year, or a day where the change of era is dated).</summary>
    public required string Start { get; init; }
    /// <summary>Canonical expression for the end; <c>..</c> for a period still running.</summary>
    public required string End { get; init; }
    /// <summary>True when years are counted within the period: "Meiji 27" is the 27th year from <see cref="Start"/>.</summary>
    public bool Regnal { get; init; }
    public string? Uri { get; init; }
    public string? Notes { get; init; }

    public long StartYear => CanonicalReader.Parse(Start) is YearPoint y ? y.Year : ((DayPoint)CanonicalReader.Parse(Start)).Year;

    public DateExpression Resolved => new Interval(CanonicalReader.Parse(Start), End == ".." ? null : CanonicalReader.Parse(End));
}

/// <summary>A table of periods as JSON; see the files under <c>Periods/</c> for the format.</summary>
public sealed record PeriodTable(string Name, string Licence, string? Notes, IReadOnlyList<PeriodDefinition> Periods);

/// <summary>
/// Periods from one or more JSON tables. The built-in tables (<see cref="BuiltIn"/>) cover Japanese eras and periods,
/// Chinese dynasties and reign periods, British periods and reigns, and a few archaeological periods whose bounds are
/// conventional. Art-historical styles ("Baroque") are deliberately absent: a profile can add its own table.
/// </summary>
public sealed partial class JsonNamedPeriodSource : INamedPeriodSource
{
    private readonly Dictionary<string, PeriodDefinition> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PeriodDefinition> _byKey = new(StringComparer.Ordinal);
    private readonly List<PeriodTable> _tables = [];

    public IReadOnlyList<PeriodTable> Tables => _tables;

    public static readonly string[] BuiltInNames = ["japanese-eras", "chinese-dynasties", "british-periods", "archaeological"];

    private static readonly Lazy<JsonNamedPeriodSource> _builtIn = new(() =>
    {
        var source = new JsonNamedPeriodSource();
        foreach (var name in BuiltInNames) source.AddEmbedded(name);
        return source;
    });

    public static JsonNamedPeriodSource BuiltIn => _builtIn.Value;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
    };

    public JsonNamedPeriodSource AddEmbedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"LinkedArtNet.Dates.Periods.{name}.json")
            ?? throw new FileNotFoundException($"No built-in period table '{name}'.");
        using var reader = new StreamReader(stream);
        return AddJson(reader.ReadToEnd());
    }

    public JsonNamedPeriodSource AddFile(string path) => AddJson(File.ReadAllText(path));

    public JsonNamedPeriodSource AddJson(string json)
    {
        var table = JsonSerializer.Deserialize<PeriodTable>(json, JsonOptions) ?? throw new InvalidDataException("Empty period table.");
        return Add(table);
    }

    public JsonNamedPeriodSource Add(PeriodTable table)
    {
        foreach (var p in table.Periods)
        {
            if (!_byKey.TryAdd(p.Key, p)) throw new InvalidDataException($"Period key '{p.Key}' is defined twice.");
            foreach (var name in p.Names.Append(p.Label)) _byName.TryAdd(Normalise(name), p);
            _ = p.Resolved; // validate the bounds now rather than at first use
        }
        _tables.Add(table);
        return this;
    }

    public bool TryResolve(string text, out NamedPeriod period)
    {
        period = null!;
        var key = Normalise(text);
        if (key.Length == 0) return false;
        if (_byName.TryGetValue(key, out var p)) { period = Make(p); return true; }

        // "Meiji 27", "Kangxi 12th year": a regnal year within an era.
        var m = RegnalPattern().Match(key);
        if (m.Success && _byName.TryGetValue(m.Groups["era"].Value.Trim(), out var era) && era.Regnal)
        {
            var n = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
            period = Regnal(era, n);
            return true;
        }
        return false;
    }

    public bool TryResolveKey(string key, out NamedPeriod period)
    {
        period = null!;
        if (_byKey.TryGetValue(key, out var p)) { period = Make(p); return true; }
        var dash = key.LastIndexOf('-');
        if (dash > 0 && int.TryParse(key[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && _byKey.TryGetValue(key[..dash], out var era) && era.Regnal)
        {
            period = Regnal(era, n);
            return true;
        }
        return false;
    }

    private static NamedPeriod Make(PeriodDefinition p) => new(p.Key, p.Label, p.Resolved);

    private static NamedPeriod Regnal(PeriodDefinition era, int n)
    {
        if (n < 1) throw new ArgumentOutOfRangeException(nameof(n));
        var year = era.StartYear + n - 1;
        return new NamedPeriod($"{era.Key}-{n}", $"{era.Label} {n}", new YearPoint(year));
    }

    /// <summary>Lower-case, accents and punctuation removed, spaces collapsed: "Shōwa" and "Showa" meet here.</summary>
    public static string Normalise(string text)
    {
        var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return Spaces().Replace(sb.ToString(), " ").Trim();
    }

    [GeneratedRegex(@"^(?<era>.+?)\s+(?<n>\d{1,3})(?:\s*(?:st|nd|rd|th))?(?:\s+year)?$")]
    private static partial Regex RegnalPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

/// <summary>Several sources asked in order.</summary>
public sealed class CompositePeriodSource(params INamedPeriodSource[] sources) : INamedPeriodSource
{
    public bool TryResolve(string text, out NamedPeriod period)
    {
        foreach (var s in sources) if (s.TryResolve(text, out period)) return true;
        period = null!;
        return false;
    }

    public bool TryResolveKey(string key, out NamedPeriod period)
    {
        foreach (var s in sources) if (s.TryResolveKey(key, out period)) return true;
        period = null!;
        return false;
    }
}

/// <summary>Strategy 3: the whole string names a period or a regnal year.</summary>
public sealed class NamedPeriodStrategy(INamedPeriodSource source) : IParseStrategy
{
    public string Name => "named-period";

    public Candidate? TryParse(string text, Realisation.ParseProfile profile) =>
        source.TryResolve(text, out var period)
            ? new Candidate(period, 0.85, Name, Notes: [$"'{text}' is the period {period.Label} ({period.Resolved!.Canonical})."], Flags: new HashSet<DateFlag> { DateFlag.NamedPeriod })
            : null;
}
