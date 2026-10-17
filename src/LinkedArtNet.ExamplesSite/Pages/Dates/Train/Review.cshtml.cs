using LinkedArtNet.Agents;
using LinkedArtNet.Dates;
using LinkedArtNet.ExamplesSite.Services;
using LinkedArtNet.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Dates.Train;

public class ReviewModel(TrainerStore store, Trainer trainer, InterpreterHost interpreters) : PageModel
{
    /// <summary>An interpreter is configured and this item has not been asked about yet.</summary>
    public bool CanPropose => interpreters.Available && Item is { Proposal: null };
    public string? InterpreterModel => interpreters.Model;

    public QueueItem? Item { get; private set; }
    /// <summary>The dates parser's reading: of the whole input for a date item, of the date substring for an agent item (null when it has none).</summary>
    public TimeSpanParseResult? Parsed { get; private set; }
    /// <summary>The agent parser's reading, for agent items.</summary>
    public ParsedAgent? Agent { get; private set; }
    public int Pending { get; private set; }
    public IReadOnlyList<string> Profiles => trainer.ProfileNames;
    public string? Error { get; private set; }

    // Agent review fields; the date part uses Canonical, ExpectedStatus and DatesProfile (intent is always pair).
    [BindProperty] public string AgentKind { get; set; } = "person";
    [BindProperty] public string? Name { get; set; }
    [BindProperty] public string? FamilyName { get; set; }
    [BindProperty] public string? GivenNames { get; set; }
    [BindProperty] public string? Honorifics { get; set; }
    [BindProperty] public string? RoleLabels { get; set; }
    [BindProperty] public string? Nationalities { get; set; }
    [BindProperty] public string? Qualifiers { get; set; }
    [BindProperty] public string? DateText { get; set; }
    [BindProperty] public string DatesProfile { get; set; } = "default";
    [BindProperty] public bool NoUnconsumed { get; set; } = true;
    public static readonly string[] QualifierNames = Enum.GetValues<AgentQualifier>().Select(AgentReview.Camel).ToArray();

    [BindProperty] public string? Canonical { get; set; }
    [BindProperty] public string Intent { get; set; } = "auto";
    [BindProperty] public string Profile { get; set; } = "default";
    [BindProperty] public string ExpectedStatus { get; set; } = "parsed";
    [BindProperty] public string? Tags { get; set; }
    [BindProperty] public string? Notes { get; set; }

    public TimeSpanParseResult? Preview { get; private set; }
    public string? PreviewError { get; private set; }

    public IActionResult OnGet(string? id)
    {
        if (!Load(id)) return Page();
        Intent = Item!.Intent;
        Profile = Item.Profile;
        if (Agent is { } a)
        {
            var review = AgentReview.FromParsed(a, trainer.AgentProfile(Item.Profile).DatesProfile);
            AgentKind = review.Kind; Name = review.Name; FamilyName = review.FamilyName; GivenNames = review.GivenNames;
            Honorifics = string.Join(", ", review.Honorifics); RoleLabels = string.Join(", ", review.RoleLabels);
            Nationalities = string.Join(", ", review.Nationalities); Qualifiers = string.Join(", ", review.Qualifiers);
            DateText = review.DateText; DatesProfile = review.DatesProfile; NoUnconsumed = review.NoUnconsumed;
            Intent = "pair";
        }
        Canonical = Parsed?.Canonical;
        ExpectedStatus = Parsed is { Status: ParseStatus.Undated } ? "undated" : "parsed";
        if (Item.Proposal is { Canonical: not null, Realisable: true } proposal)
        {
            // The proposal pre-fills the correction form; what the reviewer saves is what enters the corpus.
            ExpectedStatus = proposal.Canonical == "undated" ? "undated" : "parsed";
            Canonical = ExpectedStatus == "undated" ? null : proposal.Canonical;
            if (proposal.Intent is not null) Intent = proposal.Intent;
            Tags = "llm-proposed";
            Notes = $"Reading proposed by {proposal.Model}, confirmed by the reviewer.";
        }
        return Page();
    }

    public async Task<IActionResult> OnPostProposeAsync(string id, CancellationToken ct)
    {
        if (!Load(id)) return Page();
        if (!interpreters.Available) { Error = "No interpreter is configured on this server."; return Page(); }
        store.SetProposal(id, await trainer.ProposeAsync(Item!, interpreters, ct));
        return RedirectToPage("/Dates/Train/Review", new { id });
    }

    public IActionResult OnPostPreview(string id)
    {
        if (!Load(id)) return Page();
        var (profile, intent) = Agent is null ? (Profile, Intent) : (DatesProfile, "pair");
        (Preview, PreviewError) = ExpectedStatus == "parsed" ? trainer.Realise(Canonical ?? "", profile, intent) : (null, $"Expected status '{ExpectedStatus}' needs no expression.");
        return Page();
    }

    public IActionResult OnPostAccept(string id)
    {
        if (!Load(id)) return Page();
        if (Agent is { } a) return SubmitAgent(id, AgentReview.FromParsed(a, trainer.AgentProfile(Item!.Profile).DatesProfile));
        var status = Parsed!.Status switch { ParseStatus.Undated => "undated", ParseStatus.Unparsed => "unparsed", _ => "parsed" };
        return Submit(id, status, Parsed.Canonical, Item!.Profile, Item.Intent);
    }

    public IActionResult OnPostSave(string id)
    {
        if (!Load(id)) return Page();
        if (Agent is not null)
        {
            var dateText = string.IsNullOrWhiteSpace(DateText) ? null : DateText.Trim();
            var review = new AgentReview(AgentKind is "group" or "unknown" ? AgentKind : "person", Name, FamilyName, GivenNames,
                AgentReview.Split(Honorifics), AgentReview.Split(RoleLabels), AgentReview.Split(Nationalities), AgentReview.Split(Qualifiers),
                dateText, dateText is null ? null : ExpectedStatus, Canonical, DatesProfile, NoUnconsumed);
            return SubmitAgent(id, review);
        }
        return Submit(id, ExpectedStatus, Canonical, Profile, Intent);
    }

    /// <summary>Saves an agent review: the agent entry, and the dates entry for its date part unless the corpus or this store already has that substring.</summary>
    private IActionResult SubmitAgent(string id, AgentReview review)
    {
        var reviewer = User.Identity?.Name ?? "?";
        var tags = (Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        AgentCorpusEntry agentEntry;
        LinkedArtNet.Dates.Corpus.CorpusEntry? datesEntry;
        try
        {
            var agentId = store.NextAgentEntryId();
            agentEntry = trainer.BuildAgentEntry(agentId, Item!.Input, Item.Profile, review, tags, Notes, reviewer);
            datesEntry = review.DateText is { } t && store.HasDatesSubmission(t, review.DatesProfile) ? null
                : trainer.DerivedDatesEntry(store.NextEntryId(), review, Item.Input, agentId, tags, reviewer);
        }
        catch (ArgumentException e)
        {
            Error = "Cannot save: " + e.Message;
            return Page();
        }
        store.AddSubmission(reviewer, id, datesEntry, agentEntry);
        store.SetState(id, QueueState.Done, reviewer);
        return RedirectToPage("/Dates/Train/Review", new { id = (string?)null });
    }

    public IActionResult OnPostSkip(string id)
    {
        store.SetState(id, QueueState.Skipped, User.Identity?.Name ?? "?");
        return RedirectToPage("/Dates/Train/Review", new { id = (string?)null });
    }

    private IActionResult Submit(string id, string status, string? canonical, string profile, string intent)
    {
        var reviewer = User.Identity?.Name ?? "?";
        CorpusEntryOrError entry;
        try
        {
            var tags = (Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            entry = new(trainer.BuildEntry(store.NextEntryId(), Item!.Input, profile, intent, status, canonical, tags, Notes, reviewer), null);
        }
        catch (ArgumentException e)
        {
            entry = new(null, e.Message);
        }
        if (entry.Entry is null)
        {
            Error = "Cannot save: " + entry.Error;
            return Page();
        }
        store.AddSubmission(reviewer, id, entry.Entry);
        store.SetState(id, QueueState.Done, reviewer);
        return RedirectToPage("/Dates/Train/Review", new { id = (string?)null });
    }

    private bool Load(string? id)
    {
        Item = id is null ? store.Next() : store.Get(id);
        Pending = store.Count(QueueState.Pending);
        if (Item is null) return false;
        if (Item.IsAgent)
        {
            Agent = trainer.ParseAgent(Item.Input, Item.Profile);
            Parsed = Agent.Dates;
        }
        else Parsed = trainer.Parse(Item.Input, Item.Profile, Item.Intent);
        return true;
    }

    public static string Kind(TimeSpanParseResult r) => char.ToLowerInvariant(r.Kind.ToString()[0]) + r.Kind.ToString()[1..];

    public static IEnumerable<(string Label, string Json)> Spans(TimeSpanParseResult r)
    {
        if (r.TimeSpan is not null) yield return ("TimeSpan", LinkedArtJson.Serialize(r.TimeSpan));
        if (r.Begin is not null) yield return ("Begin", LinkedArtJson.Serialize(r.Begin));
        if (r.End is not null) yield return ("End", LinkedArtJson.Serialize(r.End));
    }

    private sealed record CorpusEntryOrError(LinkedArtNet.Dates.Corpus.CorpusEntry? Entry, string? Error);
}
