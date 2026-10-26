using LinkedArtNet.Dates;

namespace LinkedArtNet.Agents;

public enum AgentKind { Person, Group, Unknown }

/// <summary>Cataloguing qualifiers that change what the name asserts rather than who is named.</summary>
public enum AgentQualifier
{
    AttributedTo, After, CircleOf, WorkshopOf, FollowerOf, SchoolOf, StyleOf, Possibly, Formerly, AndOthers, Pseudonym, Anonymous, Unknown,
}

/// <summary>A role or occupation phrase as written, normalised, and the vocabulary term it maps to when the table has one.</summary>
/// <param name="IsOccupation">True for what the person is (painter), false for what they did for this record (preface by).</param>
public sealed record AgentRole(string Text, string Label, string? AatUri, bool IsOccupation);

/// <summary>A nationality adjective as written and the AAT nationality term the registry has for it, when it has one.</summary>
public sealed record AgentNationality(string Text, string Label, string? AatUri);

/// <summary>
/// What one agent string said. <see cref="Name"/> is the name as written once dates, honorifics, roles and qualifiers
/// are removed; <see cref="SortName"/> and <see cref="DisplayName"/> are the inverted and natural orders when the
/// profile allows the split; <see cref="Dates"/> is the dates parser's reading of <see cref="DateText"/> with
/// <c>Intent.Pair</c>, so a lifespan has <c>Begin</c> and <c>End</c> and active dates have a floruit span.
/// </summary>
public sealed record ParsedAgent(
    string Input,
    string Profile,
    AgentKind Kind,
    string? Name,
    string? FamilyName,
    string? GivenNames,
    string? SortName,
    string? DisplayName,
    IReadOnlyList<string> Honorifics,
    string? DateText,
    TimeSpanParseResult? Dates,
    IReadOnlyList<AgentRole> Roles,
    IReadOnlyList<AgentNationality> Nationalities,
    IReadOnlySet<AgentQualifier> Qualifiers,
    string? Unconsumed,
    double Confidence,
    IReadOnlyList<string> Notes)
{
    public bool HasName => !string.IsNullOrEmpty(Name);
    public bool HasDates => Dates is { Status: ParseStatus.Parsed or ParseStatus.Partial };
    /// <summary>The given names are a rank and its particle and nothing else ("Yarmouth, Earl of", "Hertford, Marquess of", "Earl of Yarmouth"): a title, which several people may have borne, not a personal name. The rank stays in the name.</summary>
    public bool TitleOnly { get; init; }
}
