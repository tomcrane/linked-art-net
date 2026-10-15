using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet.Dates.Realisation;

/// <summary>Which years an ordinal century covers. BCE centuries always count strictly (5th century BCE = 500-401 BCE).</summary>
public enum CenturyConvention
{
    /// <summary>19th century = 1800-1899.</summary>
    Popular,
    /// <summary>19th century = 1801-1900.</summary>
    Strict,
}

public enum OpenEndMode
{
    /// <summary>"before 1850" emits only <c>end_of_the_end</c>; "after 1850" only <c>begin_of_the_begin</c>.</summary>
    OmitBound,
    /// <summary>The open bound is set to the current year (for a future-open end) or to <see cref="OpenEndPolicy.Year"/>.</summary>
    ClampToNow,
    /// <summary>The open bound is set to <see cref="OpenEndPolicy.Year"/>.</summary>
    ClampToYear,
}

public sealed record OpenEndPolicy(OpenEndMode Mode = OpenEndMode.OmitBound, long? Year = null)
{
    public static readonly OpenEndPolicy OmitBound = new();
}

public enum Hemisphere { North, South }

/// <summary>Which year "Winter 1776" belongs to: December 1776 to February 1777 (the meteorological season), or January to March 1776 as US archival practice has it.</summary>
public enum WinterPolicy { CrossesYearEnd, StartOfYear }

/// <summary>Where the seasons fall: whole months (spring = March to May), or the equinoxes and solstices as US archival practice (timetwister) has them (spring = 20 March to 21 June).</summary>
public enum SeasonDates { Months, Astronomical }

public enum DayMonthOrder { DayFirst, MonthFirst }

/// <summary>How the input string is recorded on the TimeSpan.</summary>
public enum LabelPolicy
{
    /// <summary><c>_label</c> = input.</summary>
    Label,
    /// <summary><c>_label</c> = input and an <c>identified_by</c> Name with the input as content.</summary>
    LabelAndName,
    /// <summary>Only the Name.</summary>
    Name,
}

/// <summary>A band as an inclusive range of positions within a span of <see cref="Length"/> units (years for a century, years for a decade).</summary>
public sealed record BandRange(int Start, int End);

/// <summary>Where "early", "mid", "late", "beginning of" and "end of" fall within a span; halves, quarters and thirds are exact.</summary>
public sealed record BandTable(int Length, BandRange Early, BandRange Mid, BandRange Late, BandRange BeginningOf, BandRange EndOf)
{
    public static readonly BandTable Century = new(100, new(0, 33), new(34, 66), new(67, 99), new(0, 9), new(90, 99));
    public static readonly BandTable Decade = new(10, new(0, 3), new(3, 6), new(7, 9), new(0, 0), new(9, 9));
    public static readonly BandTable Year = new(12, new(0, 3), new(4, 7), new(8, 11), new(0, 0), new(11, 11));
}

/// <summary>
/// Every interpretive rule the parser and realiser apply, as data. Profiles are JSON files (<c>profiles/*.json</c>)
/// and can be derived from one another with <c>with</c>; <see cref="Default"/> is the built-in one.
/// </summary>
public sealed record ParseProfile
{
    public string Name { get; init; } = "default";

    /// <summary>A sentence for the trainer UI and the service's profile list.</summary>
    public string? Description { get; init; }

    public CenturyConvention CenturyConvention { get; init; } = CenturyConvention.Popular;

    public BandTable CenturyBands { get; init; } = BandTable.Century;

    public BandTable DecadeBands { get; init; } = BandTable.Decade;

    public BandTable YearBands { get; init; } = BandTable.Year;

    /// <summary>Years added to each outer bound of an approximate (<c>~</c>) date. The hedge is always recorded as a flag.</summary>
    public int CircaPaddingYears { get; init; }

    /// <summary>Years added to each outer bound of an uncertain (<c>?</c>) date.</summary>
    public int UncertainPaddingYears { get; init; }

    public OpenEndPolicy OpenEnd { get; init; } = OpenEndPolicy.OmitBound;

    /// <summary>When true, padded dates also emit <c>end_of_the_begin</c> and <c>begin_of_the_end</c> (the unpadded bounds).</summary>
    public bool EmitInnerBounds { get; init; }

    public Hemisphere Hemisphere { get; init; } = Hemisphere.North;

    public WinterPolicy Winter { get; init; } = WinterPolicy.CrossesYearEnd;

    public SeasonDates SeasonDates { get; init; } = SeasonDates.Months;

    /// <summary>Era assumed for bare years when the text gives none.</summary>
    public Era DefaultEra { get; init; } = Era.Ce;

    /// <summary>A bare number with fewer digits is not a year unless an era word follows.</summary>
    public int MinYearDigits { get; init; } = 3;

    /// <summary>When set, a bare two-digit year is read within the hundred years starting here (1900: '08 = 1908).</summary>
    public int? TwoDigitYearWindow { get; init; }

    /// <summary>
    /// When true the window applies only to a two-digit year written with a month ("69-10" = October 1969), never to a bare
    /// two-digit number: for collections where regnal years, volume and edition numbers would otherwise read as years.
    /// </summary>
    public bool TwoDigitYearsNeedMonth { get; init; }

    public DayMonthOrder DayMonthOrder { get; init; } = DayMonthOrder.DayFirst;

    /// <summary>
    /// Catalogues write "-1850" for "until 1850"; EDTF reads it as 1851 BCE. When true (the default) free text of that shape is
    /// read as an open start and the EDTF reading is offered as an alternative; BCE years are written "-0499" in canonical input regardless.
    /// </summary>
    public bool LeadingDashIsOpenStart { get; init; } = true;

    public long PlausibleYearMin { get; init; } = -10_000;

    public long PlausibleYearMax { get; init; } = 2200;

    public LabelPolicy LabelPolicy { get; init; } = LabelPolicy.Label;

    /// <summary>Candidates below this confidence do not stop the strategy chain.</summary>
    public double AcceptThreshold { get; init; } = 0.75;

    /// <summary>Language codes whose lexicons the tokenizer loads, in priority order.</summary>
    public IReadOnlyList<string> Languages { get; init; } = ["en"];

    public static readonly ParseProfile Default = new();

    // Records compare collections by reference; profiles compare by content.
    public bool Equals(ParseProfile? other) =>
        other is not null && ToJson() == other.ToJson();

    public override int GetHashCode() => ToJson().GetHashCode(StringComparison.Ordinal);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static ParseProfile FromJson(string json) =>
        JsonSerializer.Deserialize<ParseProfile>(json, JsonOptions) ?? throw new InvalidDataException("Empty profile.");

    public static ParseProfile Load(string path) => FromJson(File.ReadAllText(path));

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
