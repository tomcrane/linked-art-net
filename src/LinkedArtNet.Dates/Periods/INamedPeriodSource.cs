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

    /// <summary>
    /// The part of this source the grammar may use inside longer text, where a period name stands wherever a date
    /// could ("early Edo period", "Meiji 27 to Taisho 3"). Curated tables return themselves. A gazetteer with
    /// thousands of names, some of them ordinary words ("Italian", "Classical"), returns an empty source so that
    /// its names are only matched as the whole string by <see cref="NamedPeriodStrategy"/>.
    /// </summary>
    INamedPeriodSource ForGrammar() => this;
}

/// <summary>Knows no periods; what a gazetteer offers the grammar.</summary>
public sealed class EmptyPeriodSource : INamedPeriodSource
{
    public static readonly EmptyPeriodSource Instance = new();
    public bool TryResolve(string text, out NamedPeriod period) { period = null!; return false; }
    public bool TryResolveKey(string key, out NamedPeriod period) { period = null!; return false; }
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

    public INamedPeriodSource ForGrammar()
    {
        var safe = sources.Select(s => s.ForGrammar()).Where(s => s is not EmptyPeriodSource).ToArray();
        return safe.Length == sources.Length ? this : safe.Length == 0 ? EmptyPeriodSource.Instance : new CompositePeriodSource(safe);
    }
}

/// <summary>
/// Strategy 3: the whole string names a period or a regnal year, allowing a leading "c."/"circa", a band word
/// (early, mid, late) and "the", and a trailing generic "period"/"era" the table may not list ("Late Iron Age",
/// "c. Edo era"). Sources are asked for the fuller text before the trimmed one, so a curated table keeps priority
/// over a gazetteer that would also know the name.
/// </summary>
public sealed partial class NamedPeriodStrategy(INamedPeriodSource source) : IParseStrategy
{
    public string Name => "named-period";

    public Candidate? TryParse(string text, Realisation.ParseProfile profile)
    {
        var rest = text.Trim();
        Qualification? qualification = null;
        Band? band = null;
        NamedPeriod period;
        var prefix = Prefix().Match(rest);
        var circa = prefix.Groups["circa"];
        // The whole text first ("Early Bronze II" is a period's name, not the early part of "Bronze II"), then without
        // the hedge ("c. Early Bronze II"), then without the band word too ("late Iron Age").
        if (Resolve(rest, out period)) { }
        else if (circa.Success && Resolve(rest[(circa.Index + circa.Length)..].Trim(), out period)) qualification = Qualification.Approximate;
        else if (prefix.Length > 0 && Resolve(rest[prefix.Length..].Trim(), out period))
        {
            if (circa.Success) qualification = Qualification.Approximate;
            if (prefix.Groups["band"].Success)
                band = prefix.Groups["band"].Value.ToLowerInvariant() switch { "early" => Band.Early, "mid" or "middle" => Band.Mid, _ => Band.Late };
            rest = rest[prefix.Length..].Trim();
        }
        else return null;

        DateExpression expression = period;
        var flags = new HashSet<DateFlag> { DateFlag.NamedPeriod };
        if (band is { } b) { expression = new Banded(expression, b); flags.Add(DateFlag.Band); }
        if (qualification is { } q) { expression = new Qualified(expression, q); flags.Add(DateFlag.Approximate); }
        var note = $"'{rest}' is the period {period.Label} ({period.Resolved!.Canonical})." + (period.Source is null ? "" : " " + period.Source);
        return new Candidate(expression, 0.85, Name, Notes: [note], Flags: flags);
    }

    private bool Resolve(string text, out NamedPeriod period)
    {
        if (source.TryResolve(text, out period)) return true;
        var m = GenericSuffix().Match(text);
        return m.Success && source.TryResolve(text[..m.Index].TrimEnd(), out period);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^(?:(?<circa>c\.?|ca\.?|circa)\s+)?(?:(?<band>early|mid|middle|late|later)\s+)?(?:the\s+)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Prefix();

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+(period|era|periode|periodo|age)\.?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex GenericSuffix();
}
