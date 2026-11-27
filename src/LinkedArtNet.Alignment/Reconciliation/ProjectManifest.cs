using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinkedArtNet.Alignment.Reconciliation;

/// <summary>
/// What a project tells the reconciler (design doc 12, section 4; doc 10, section 5): the context rules, the
/// thresholds, the single-source rule. Stored as the project's manifest JSON; the pipeline runner (doc 10, step 4)
/// adds the sources and adapters to it.
/// </summary>
public sealed record ProjectManifest
{
    public Dictionary<string, ContextRuleJson> Contexts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public double AcceptScore { get; init; } = 0.9;
    public double ReviewScore { get; init; } = 0.6;
    public int MinIndependentSources { get; init; } = 2;
    public bool AcceptSingleSourceWithDates { get; init; } = true;
    /// <summary>Accept an undated string's single exact match in one source when it is the only candidate at any score (policy; see ReconcilerOptions).</summary>
    public bool AcceptUniqueExactSingleSource { get; init; } = true;
    public bool MintWhenNothingFound { get; init; } = true;
    /// <summary>Where the dataset's places mostly are, for a qualifier TGN knows in several countries (finding 71).</summary>
    public string? HomePlace { get; init; }
    /// <summary>
    /// The trades of the catalogue, as a record's biography names them ("auctioneer", "dealer"): a name with an honorific ("Mr Farebrother")
    /// that one tied record bears whole is carried when the record is a body, or a person of one of these trades (finding 109); a person of
    /// no listed trade is shown and left to the desk. Project knowledge, not the toolkit's: a sale-catalogue library's list is not a museum's.
    /// Empty, persons are never carried by it.
    /// </summary>
    public List<string> Trades { get; init; } = [];
    /// <summary>
    /// What a cataloguer writes when the item does not say: "s.n." (sine nomine, no publisher named), "S.l." (sine loco, no place),
    /// RDA's "publisher not identified". A string that is one of these, whatever its case, spacing and brackets, is decided as not a
    /// name (<see cref="DecisionMethod.NotAName"/>) and no entity is made of it (the owner, 2026-11-22). The forms are a catalogue's
    /// conventions, so they are the project's to list. Empty, nothing is so decided.
    /// </summary>
    public List<string> NotNames { get; init; } = [];
    /// <summary>The local indexes to consult, in order.</summary>
    public List<string> Sources { get; init; } = ["ulan", "aat", "tgn", "loc", "lcsh"];
    /// <summary>
    /// The source systems the project's records come from, by the key a collect is given (design doc 12 section 14.1): a library
    /// catalogue, an archive catalogue, a museum's. A record is named by its system and its identifier there together.
    /// </summary>
    public Dictionary<string, SourceSystemJson> Systems { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A source system named to a person: the manifest's name for it, or its key.</summary>
    public string SystemName(string system) => Systems.TryGetValue(system, out var s) && !string.IsNullOrWhiteSpace(s.Name) ? s.Name : system;

    /// <summary>The page of a record in its own system, when the manifest has a template for the system (<c>{id}</c> is the record's identifier); null otherwise.</summary>
    public string? RecordUrl(string system, string recordId) =>
        Systems.TryGetValue(system, out var s) && !string.IsNullOrWhiteSpace(s.RecordUrl) ? s.RecordUrl.Replace("{id}", Uri.EscapeDataString(recordId), StringComparison.Ordinal) : null;

    /// <summary>The field a string's records are grouped by at the desk unless the reviewer chooses another: the context's own setting, else the publisher for a place and the place for anything else.</summary>
    public string GroupBy(string context) =>
        Contexts.TryGetValue(context, out var c) && !string.IsNullOrWhiteSpace(c.GroupBy) ? c.GroupBy : context.StartsWith("place", StringComparison.OrdinalIgnoreCase) ? "publisher" : "place";

    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>A context named to a reviewer: the manifest's label for it ("place of publication"), or the context's own name.</summary>
    public string FieldName(string? context) => context is not null && Contexts.TryGetValue(context, out var c) && !string.IsNullOrWhiteSpace(c.Label) ? c.Label : context ?? "";

    /// <summary>What the field holds, in a sentence, when the manifest says.</summary>
    public string? FieldAbout(string? context) => context is not null && Contexts.TryGetValue(context, out var c) && !string.IsNullOrWhiteSpace(c.About) ? c.About : null;

    public static ProjectManifest FromJson(string? json) => string.IsNullOrWhiteSpace(json) ? new ProjectManifest() : JsonSerializer.Deserialize<ProjectManifest>(json, JsonOptions) ?? new ProjectManifest();

    public ReconcilerOptions ToOptions() => new()
    {
        AcceptScore = AcceptScore,
        ReviewScore = ReviewScore,
        MinIndependentSources = MinIndependentSources,
        AcceptSingleSourceWithDates = AcceptSingleSourceWithDates,
        AcceptUniqueExactSingleSource = AcceptUniqueExactSingleSource,
        MintWhenNothingFound = MintWhenNothingFound,
        HomePlace = HomePlace,
        Trades = Trades,
        NotNames = NotNames,
        Contexts = Contexts.ToDictionary(kv => kv.Key, kv => kv.Value.ToRule(), StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>How the PMC library's fields are read: the old source_string_map sources. Sale catalogues name auctioneers and dealers in corpauthor and publisher, some of them people.</summary>
    public static ProjectManifest PmcDefault => new()
    {
        HomePlace = "United Kingdom", // a British art library's places (finding 71)
        // the Heritage library catalogue; its record identifier is the standard number (an ISBN where the book has one, else a lettered
        // number by kind of material: profiled 2026-11-15). No template: the assistant does not know of a page per record to link to
        Systems = new Dictionary<string, SourceSystemJson>(StringComparer.OrdinalIgnoreCase) { ["library"] = new("Library catalogue", "standard number") },
        // AACR2's forms and RDA's, as the PMC records write them (counted 2026-11-22: some 1,280 records); the list was put to the owner first
        NotNames = ["s.n.", "s.l.", "publisher not identified", "place of publication not identified"],
        Trades = ["auction", "dealer", "bookseller", "book seller", "printseller", "print seller", "publisher", "stationer"], // the sale catalogues' own trades (finding 109)
        Contexts = new Dictionary<string, ContextRuleJson>(StringComparer.OrdinalIgnoreCase)
        {
            // the labels and sentences are the assistant's reading of the PMC library export's field names and of the records seen in
            // them (2026-11-13), not the library's own documentation: the cataloguers' to correct
            ["persauthorfull"] = new([EntityClass.Person], "default", NamesMakers: true, Label: "personal author", About: "A person named on the record as author, editor or other contributor, with the role when given."),
            ["corpauthor"] = new([EntityClass.Group, EntityClass.Person], "default", Parts: new(PartsShape.List, ";"), Label: "corporate author", About: "A body named as the record's author: a gallery, a society, and on a sale catalogue the auction house or dealer."),
            ["publisher"] = new([EntityClass.Group, EntityClass.Person], "default", Parts: new(PartsShape.List, ";"), Label: "publisher", About: "Who published the item, as the record gives it; on a sale catalogue often the auctioneer."),
            ["place"] = new([EntityClass.Place], Qualified: true, Parts: new(PartsShape.List, ";"), Label: "place of publication", About: "Where the item was published, as the record gives it; several places are separated by semicolons."),
            // a precoordinated LCSH heading: looked up whole, else decided as its parts; the head may be a dated name heading
            // a heading without "--" is decided whole, and a dated name heading ("Atken, J, fl 1783", "Reuss, Albert, 1889-1975") reads
            // through the MARC profile as its head would (finding 46, 2026-10-22); the manifest needs refreshing for this
            ["keywords"] = new([EntityClass.Type, EntityClass.Person, EntityClass.Group, EntityClass.Place], "marc", Home: "lcsh", ParseDatedOnly: true, Parts: new(PartsShape.Compound, "--", Context: "keywords:sub", HeadContext: "keywords:head"), Collections: ["LCSHAuthorizedHeadings"], Label: "subject heading", About: "What the item is about: a subject heading as the cataloguer wrote it, whole."),
            ["keywords:head"] = new([EntityClass.Type, EntityClass.Person, EntityClass.Group, EntityClass.Place], "marc", Home: "lcsh", ParseDatedOnly: true, Collections: ["LCSHAuthorizedHeadings"], Label: "subject, main heading", About: "The first part of a subject heading that has subdivisions: the person, body, place or topic the item is about."),
            // a library's subdivisions are more often forms ("--Catalogs", "--Exhibitions") than topics where LCSH has both
            ["keywords:sub"] = new([EntityClass.Type, EntityClass.Place, EntityClass.Period], Home: "lcsh", Collections: ["GenreFormSubdivisions", "Subdivisions"], Label: "subject subdivision", About: "A later part of a subject heading: a form (\"Exhibitions\"), a place, a period or a narrower topic."),
        },
    };

    /// <summary>
    /// What differs from another manifest, in words for a page or a console (contexts present on one side only or read
    /// differently, the sources, the thresholds and policies), naming the two sides as the caller says.
    /// </summary>
    public IReadOnlyList<string> Differences(ProjectManifest other, string thisName = "this manifest", string otherName = "the other")
    {
        var d = new List<string>();
        var cmp = StringComparer.OrdinalIgnoreCase;
        foreach (var k in Contexts.Keys.Except(other.Contexts.Keys, cmp).OrderBy(x => x, cmp)) d.Add($"context {k} is in {thisName} only");
        foreach (var k in other.Contexts.Keys.Except(Contexts.Keys, cmp).OrderBy(x => x, cmp)) d.Add($"context {k} is in {otherName} only");
        foreach (var k in Contexts.Keys.Intersect(other.Contexts.Keys, cmp).OrderBy(x => x, cmp))
        {
            // a label or its sentence is how a field is named to a reviewer, not how it is read
            if (JsonSerializer.Serialize(Contexts[k] with { Label = null, About = null }, JsonOptions) != JsonSerializer.Serialize(other.Contexts[k] with { Label = null, About = null }, JsonOptions)) d.Add($"context {k} is read differently");
            else if (Contexts[k].Label != other.Contexts[k].Label || Contexts[k].About != other.Contexts[k].About) d.Add($"context {k} is named or described differently");
        }
        if (!Sources.SequenceEqual(other.Sources, cmp)) d.Add($"the sources ({string.Join(", ", Sources)} against {string.Join(", ", other.Sources)})");
        if (AcceptScore != other.AcceptScore || ReviewScore != other.ReviewScore || MinIndependentSources != other.MinIndependentSources
            || AcceptSingleSourceWithDates != other.AcceptSingleSourceWithDates || AcceptUniqueExactSingleSource != other.AcceptUniqueExactSingleSource || MintWhenNothingFound != other.MintWhenNothingFound)
            d.Add("the thresholds or policies");
        if (!string.Equals(HomePlace, other.HomePlace, StringComparison.Ordinal)) d.Add($"the home place ({HomePlace ?? "none"} against {other.HomePlace ?? "none"})");
        if (!Trades.SequenceEqual(other.Trades, cmp)) d.Add($"the trades ({(Trades.Count == 0 ? "none" : string.Join(", ", Trades))} against {(other.Trades.Count == 0 ? "none" : string.Join(", ", other.Trades))})");
        if (!NotNames.SequenceEqual(other.NotNames, cmp)) d.Add($"the strings that are not names ({(NotNames.Count == 0 ? "none" : string.Join(", ", NotNames))} against {(other.NotNames.Count == 0 ? "none" : string.Join(", ", other.NotNames))})");
        return d;
    }
}

/// <summary>A <see cref="ContextRule"/> as the manifest writes it.</summary>
/// <param name="Label">The field's name in plain words, for a reviewer ("place of publication" for <c>place</c>); the context's own name stands when there is none (the owner, 2026-11-13).</param>
/// <param name="About">A sentence on what the field holds in this catalogue, for the desk's and the entity page's help.</param>
/// <param name="NamesMakers">The field names who made the item (author, editor), so a person named in it was alive at the record's date (see <see cref="ContextRule"/>).</param>
/// <param name="GroupBy">The record field a string's records are grouped by at the desk (design doc 12 section 14.6): the field that most often tells two things of one name apart.</param>
public sealed record ContextRuleJson(List<EntityClass> Classes, string? AgentProfile = null, bool? Qualified = null, PartsRuleJson? Parts = null, string? Home = null, bool? ParseDatedOnly = null, List<string>? Collections = null, string? Label = null, string? About = null, string? GroupBy = null, bool? NamesMakers = null)
{
    public ContextRule ToRule() => new(Classes, AgentProfile, Qualified ?? false, Parts?.ToRule(), Home, ParseDatedOnly ?? false, Collections, NamesMakers ?? false);
}

/// <summary>A source system as the manifest describes it (design doc 12 section 14.1).</summary>
/// <param name="Name">What people call it ("Library catalogue").</param>
/// <param name="Identifier">What its record identifier is, in the cataloguer's words ("standard number", "reference code").</param>
/// <param name="RecordUrl">A template for a record's own page in the system, <c>{id}</c> standing for the identifier; none when the system has no pages to link to.</param>
public sealed record SourceSystemJson(string? Name = null, string? Identifier = null, string? RecordUrl = null);

/// <summary>A <see cref="PartsRule"/> as the manifest writes it: <c>{ "shape": "list", "separator": ";" }</c> or <c>{ "shape": "compound", "separator": "--", "context": "keywords:sub", "headContext": "keywords:head" }</c>.</summary>
public sealed record PartsRuleJson(PartsShape Shape, string Separator, string? Context = null, string? HeadContext = null)
{
    public PartsRule ToRule() => new(Shape, Separator, Context, HeadContext);
}
