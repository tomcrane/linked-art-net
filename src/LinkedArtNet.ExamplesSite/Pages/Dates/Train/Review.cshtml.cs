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
    public TimeSpanParseResult Parsed { get; private set; } = null!;
    public int Pending { get; private set; }
    public IReadOnlyList<string> Profiles => trainer.ProfileNames;
    public string? Error { get; private set; }

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
        Canonical = Parsed.Canonical;
        Intent = Item!.Intent;
        Profile = Item.Profile;
        ExpectedStatus = Parsed.Status == ParseStatus.Parsed ? "parsed" : Parsed.Status == ParseStatus.Undated ? "undated" : "parsed";
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
        (Preview, PreviewError) = ExpectedStatus == "parsed" ? trainer.Realise(Canonical ?? "", Profile, Intent) : (null, $"Expected status '{ExpectedStatus}' needs no expression.");
        return Page();
    }

    public IActionResult OnPostAccept(string id)
    {
        if (!Load(id)) return Page();
        var status = Parsed.Status switch { ParseStatus.Undated => "undated", ParseStatus.Unparsed => "unparsed", _ => "parsed" };
        return Submit(id, status, Parsed.Canonical, Item!.Profile, Item.Intent);
    }

    public IActionResult OnPostSave(string id)
    {
        if (!Load(id)) return Page();
        return Submit(id, ExpectedStatus, Canonical, Profile, Intent);
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
        Parsed = trainer.Parse(Item.Input, Item.Profile, Item.Intent);
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
