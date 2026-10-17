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
    public IReadOnlyList<string> AgentProfiles => trainer.AgentProfileNames;
    public int PendingAgents { get; private set; }
    public int UnexportedDates => Unexported.Count(s => s.Entry is not null);
    public int UnexportedAgents => Unexported.Count(s => s.AgentEntry is not null);
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

    public IActionResult OnPostAddAgents(string? inputs, string profile = "default")
    {
        var lines = string.IsNullOrWhiteSpace(inputs) ? [] : inputs.Split('\n');
        if (!trainer.AgentProfileNames.Contains(profile, StringComparer.OrdinalIgnoreCase)) profile = "default";
        var added = store.Add(lines, profile, "pair", "manual:" + User.Identity?.Name, ItemKind.Agent);
        Message = $"{added} agent string{(added == 1 ? "" : "s")} added to the queue.";
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
        var built = Build(store.Unexported);
        if (built.Dates is null) return RedirectToPage();
        if (built.Errors.Count > 0) { Message = "The file does not validate against the corpus schema: " + string.Join("; ", built.Errors.Take(5)); return RedirectToPage(); }
        return File(Encoding.UTF8.GetBytes(built.Dates.Value.Json), "application/json", built.Dates.Value.Name);
    }

    public IActionResult OnPostDownloadAgents()
    {
        var built = Build(store.Unexported);
        if (built.Agents is null) return RedirectToPage();
        if (built.Errors.Count > 0) { Message = "The file does not validate: " + string.Join("; ", built.Errors.Take(5)); return RedirectToPage(); }
        return File(Encoding.UTF8.GetBytes(built.Agents.Value.Json), "application/json", built.Agents.Value.Name);
    }

    public async Task<IActionResult> OnPostPullRequestAsync(CancellationToken ct)
    {
        var pending = store.Unexported;
        if (pending.Count == 0 || !github.Configured) return RedirectToPage();
        var built = Build(pending);
        if (built.Errors.Count > 0)
        {
            Message = "The file does not validate against the corpus schema: " + string.Join("; ", built.Errors.Take(5));
            return RedirectToPage();
        }
        var reviewers = string.Join(", ", pending.Select(s => s.Reviewer).Distinct());
        var dates = pending.Where(s => s.Entry is not null).ToList();
        var agents = pending.Where(s => s.AgentEntry is not null).ToList();
        var total = dates.Count + agents.Count;
        var title = $"Corpus: {total} proposed entr{(total == 1 ? "y" : "ies")} from the trainer";
        var body = new StringBuilder();
        body.Append($"Reviewed in the examples-site trainer by {reviewers}. Entries are `proposed`; a maintainer verifies them.\n\n");
        if (dates.Count > 0)
        {
            body.Append($"**{dates.Count} date string{(dates.Count == 1 ? "" : "s")}** in `{options.Value.Submission.Directory}/{built.Dates!.Value.Name}`; verify with `dotnet run --project tools/Corpus -- promote {options.Value.Submission.Directory}/{built.Dates.Value.Name}` (`--dry-run` shows the evaluation first).\n\n");
            body.Append(string.Join("\n", dates.Select(s => $"- `{s.Entry!.Input}` → {s.Entry.Expect.Status}{(s.Entry.Expect.Canonical is null ? "" : $" `{s.Entry.Expect.Canonical}`")}{(s.Entry.Tags.Contains("agent-derived") ? " (from an agent string)" : "")}")));
            body.Append("\n\n");
        }
        if (agents.Count > 0)
        {
            body.Append($"**{agents.Count} agent string{(agents.Count == 1 ? "" : "s")}** in `{options.Value.Submission.AgentDirectory}/{built.Agents!.Value.Name}`; `dotnet run --project tools/Corpus -- agents --all` shows how the parser does against them.\n\n");
            body.Append(string.Join("\n", agents.Select(s => $"- `{s.AgentEntry!.Input}` → {s.AgentEntry.Expect.Kind}{(s.AgentEntry.Expect.Name is null ? "" : $" `{s.AgentEntry.Expect.Name}`")}{(s.AgentEntry.Expect.DateText is null ? "" : $", dates `{s.AgentEntry.Expect.DateText}`")}")));
        }
        var files = new List<(string Path, string Content)>();
        if (built.Dates is { } d) files.Add(($"{options.Value.Submission.Directory.TrimEnd('/')}/{d.Name}", d.Json));
        if (built.Agents is { } a) files.Add(($"{options.Value.Submission.AgentDirectory.TrimEnd('/')}/{a.Name}", a.Json));
        try
        {
            var url = await github.CreatePullRequestAsync(files, title, body.ToString().TrimEnd(), ct);
            store.MarkExported(pending.Select(s => s.Id), url);
            Message = $"Pull request opened: <a href=\"{url}\">{url}</a>";
        }
        catch (HttpRequestException e)
        {
            Message = "GitHub refused the pull request: " + System.Net.WebUtility.HtmlEncode(e.Message);
        }
        return RedirectToPage();
    }

    private sealed record Built((string Name, string Json)? Dates, (string Name, string Json)? Agents, IReadOnlyList<string> Errors);

    /// <summary>The dates file and the agent file the pending submissions make, each only when it would have entries.</summary>
    private Built Build(IReadOnlyList<Submission> pending)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm");
        var errors = new List<string>();
        (string, string)? dates = null, agents = null;
        var withDates = pending.Where(s => s.Entry is not null).ToList();
        if (withDates.Count > 0)
        {
            var json = Trainer.Serialise(trainer.BuildFile(withDates));
            dates = ($"trainer-{stamp}.json", json);
            errors.AddRange(trainer.Validate(json, store.Directory));
        }
        var withAgents = pending.Where(s => s.AgentEntry is not null).ToList();
        if (withAgents.Count > 0)
        {
            var json = Trainer.Serialise(trainer.BuildAgentFile(withAgents));
            agents = ($"trainer-agents-{stamp}.json", json);
            errors.AddRange(trainer.ValidateAgents(json));
        }
        return new Built(dates, agents, errors);
    }

    private void Load()
    {
        Pending = store.Count(QueueState.Pending);
        PendingAgents = store.Count(QueueState.Pending, ItemKind.Agent);
        Proposable = interpreters.Available ? store.AwaitingProposal(trainer.NeedsInterpreter).Count : 0;
        Done = store.Count(QueueState.Done);
        Skipped = store.Count(QueueState.Skipped);
        var all = store.Submissions;
        Unexported = all.Where(s => s.ExportedTo is null).OrderBy(s => s.At).ToList();
        Exported = all.Where(s => s.ExportedTo is not null).GroupBy(s => s.ExportedTo!).Select(g => (g.Key, g.Count())).OrderByDescending(g => g.Item1).ToList();
    }
}
