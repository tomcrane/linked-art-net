using System.Text;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace LinkedArtNet.ExamplesSite.Pages.Dates.Train;

public class IndexModel(TrainerStore store, Trainer trainer, GitHubSubmitter github, IOptions<TrainerOptions> options, IHostEnvironment environment, InterpreterHost interpreters) : PageModel
{
    public bool InterpreterAvailable => interpreters.Available;
    public string? InterpreterModel => interpreters.Model;
    public int BatchSize => interpreters.BatchSize;
    /// <summary>Pending inputs the parser cannot read confidently and that have no proposal yet.</summary>
    public int Proposable { get; private set; }
    public int Pending { get; private set; }
    public int Done { get; private set; }
    public int Skipped { get; private set; }
    public IReadOnlyList<Submission> Unexported { get; private set; } = [];
    public IReadOnlyList<(string Target, int Count)> Exported { get; private set; } = [];
    public IReadOnlyList<string> Profiles => trainer.ProfileNames;
    public bool CaptureAvailable => CapturePath is { } p && System.IO.File.Exists(p);
    public bool PullRequestsConfigured => github.Configured;
    [TempData] public string? Message { get; set; }

    private string? CapturePath => options.Value.CapturePath is { Length: > 0 } p
        ? (Path.IsPathRooted(p) ? p : Path.Combine(environment.ContentRootPath, p))
        : null;

    public void OnGet() => Load();

    public async Task<IActionResult> OnPostAddAsync(string? inputs, IFormFile? file, string profile = "default", string intent = "auto")
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(inputs)) lines.AddRange(inputs.Split('\n'));
        if (file is { Length: > 0 and < 2_000_000 })
        {
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            while (await reader.ReadLineAsync() is { } line) lines.Add(line);
        }
        if (!trainer.ProfileNames.Contains(profile, StringComparer.OrdinalIgnoreCase)) profile = "default";
        if (intent is not ("auto" or "single" or "pair")) intent = "auto";
        var added = store.Add(lines, profile, intent, "manual:" + User.Identity?.Name);
        Message = $"{added} input{(added == 1 ? "" : "s")} added to the queue.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostProposeAsync(CancellationToken ct)
    {
        if (!interpreters.Available) return RedirectToPage();
        var items = store.AwaitingProposal(trainer.NeedsInterpreter).Take(interpreters.BatchSize).ToList();
        var offered = 0;
        foreach (var item in items)
        {
            var proposal = await trainer.ProposeAsync(item, interpreters, ct);
            store.SetProposal(item.Id, proposal);
            if (proposal.Canonical is not null) offered++;
        }
        Message = $"{interpreters.Model} proposed {offered} reading{(offered == 1 ? "" : "s")} for {items.Count} input{(items.Count == 1 ? "" : "s")}; each is pre-filled on its review page for you to confirm.";
        return RedirectToPage();
    }

    public IActionResult OnPostImportCapture()
    {
        var added = CapturePath is { } p ? store.ImportCapture(p) : 0;
        Message = $"{added} captured input{(added == 1 ? "" : "s")} added to the queue.";
        return RedirectToPage();
    }

    public IActionResult OnPostDownload()
    {
        var pending = store.Unexported;
        if (pending.Count == 0) return RedirectToPage();
        var (name, json, errors) = Build(pending);
        if (errors.Count > 0)
        {
            Message = "The file does not validate against the corpus schema: " + string.Join("; ", errors.Take(5));
            return RedirectToPage();
        }
        return File(Encoding.UTF8.GetBytes(json), "application/json", name);
    }

    public async Task<IActionResult> OnPostPullRequestAsync(CancellationToken ct)
    {
        var pending = store.Unexported;
        if (pending.Count == 0 || !github.Configured) return RedirectToPage();
        var (name, json, errors) = Build(pending);
        if (errors.Count > 0)
        {
            Message = "The file does not validate against the corpus schema: " + string.Join("; ", errors.Take(5));
            return RedirectToPage();
        }
        var reviewers = string.Join(", ", pending.Select(s => s.Reviewer).Distinct());
        var title = $"Corpus: {pending.Count} proposed entr{(pending.Count == 1 ? "y" : "ies")} from the trainer";
        var body = $"{pending.Count} date string{(pending.Count == 1 ? "" : "s")} reviewed in the examples-site trainer by {reviewers}.\n\n" +
                   $"Entries are `proposed`; a maintainer verifies them with `dotnet run --project tools/Corpus -- promote corpus/proposed/{name}` (`--dry-run` shows the evaluation first).\n\n" +
                   string.Join("\n", pending.Select(s => $"- `{s.Entry.Input}` → {s.Entry.Expect.Status}{(s.Entry.Expect.Canonical is null ? "" : $" `{s.Entry.Expect.Canonical}`")}"));
        try
        {
            var url = await github.CreatePullRequestAsync(name, json, title, body, ct);
            store.MarkExported(pending.Select(s => s.Id), url);
            Message = $"Pull request opened: <a href=\"{url}\">{url}</a>";
        }
        catch (HttpRequestException e)
        {
            Message = "GitHub refused the pull request: " + System.Net.WebUtility.HtmlEncode(e.Message);
        }
        return RedirectToPage();
    }

    private (string Name, string Json, IReadOnlyList<string> Errors) Build(IReadOnlyList<Submission> pending)
    {
        var json = Trainer.Serialise(trainer.BuildFile(pending));
        var name = $"trainer-{DateTime.UtcNow:yyyyMMdd-HHmm}.json";
        return (name, json, trainer.Validate(json, store.Directory));
    }

    private void Load()
    {
        Pending = store.Count(QueueState.Pending);
        Proposable = interpreters.Available ? store.AwaitingProposal(trainer.NeedsInterpreter).Count : 0;
        Done = store.Count(QueueState.Done);
        Skipped = store.Count(QueueState.Skipped);
        var all = store.Submissions;
        Unexported = all.Where(s => s.ExportedTo is null).OrderBy(s => s.At).ToList();
        Exported = all.Where(s => s.ExportedTo is not null).GroupBy(s => s.ExportedTo!).Select(g => (g.Key, g.Count())).OrderByDescending(g => g.Item1).ToList();
    }
}
