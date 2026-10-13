using LinkedArtNet.Dates.Corpus;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Dates;

public class CorpusModel(CorpusIndex corpus) : PageModel
{
    public const int PageSize = 100;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "file")] public string? FileName { get; set; }
    [BindProperty(SupportsGet = true)] public string? Tag { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Result { get; set; }
    [BindProperty(SupportsGet = true, Name = "p")] public int PageNumber { get; set; } = 1; // "page" is a reserved Razor Pages route value

    public IReadOnlyList<EntryResult> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int PassedTotal { get; private set; }
    public int Filtered { get; private set; }
    public int Pages => Math.Max(1, (Filtered + PageSize - 1) / PageSize);
    public IReadOnlyList<string> Files => corpus.Files;
    public IReadOnlyList<(string Tag, int Count)> Tags => corpus.Tags;

    public void OnGet()
    {
        var all = corpus.Results;
        Total = all.Count;
        PassedTotal = all.Count(r => r.Passed);
        IEnumerable<EntryResult> q = all;
        if (!string.IsNullOrWhiteSpace(Q)) q = q.Where(r => r.Entry.Input.Contains(Q.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(FileName)) q = q.Where(r => r.Entry.File == FileName);
        if (!string.IsNullOrWhiteSpace(Tag)) q = q.Where(r => r.Entry.Tags.Contains(Tag, StringComparer.OrdinalIgnoreCase));
        if (Enum.TryParse<EntryStatus>(Status, true, out var status)) q = q.Where(r => r.Entry.Status == status);
        if (Result == "pass") q = q.Where(r => r.Passed);
        if (Result == "fail") q = q.Where(r => !r.Passed);
        var list = q.ToList();
        Filtered = list.Count;
        PageNumber = Math.Clamp(PageNumber, 1, Pages);
        Items = list.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
    }

    public string PageLink(int page)
    {
        var parts = new List<string> { $"p={page}" };
        if (!string.IsNullOrWhiteSpace(Q)) parts.Add("q=" + Uri.EscapeDataString(Q));
        if (!string.IsNullOrWhiteSpace(FileName)) parts.Add("file=" + Uri.EscapeDataString(FileName));
        if (!string.IsNullOrWhiteSpace(Tag)) parts.Add("tag=" + Uri.EscapeDataString(Tag));
        if (!string.IsNullOrWhiteSpace(Status)) parts.Add("status=" + Uri.EscapeDataString(Status));
        if (!string.IsNullOrWhiteSpace(Result)) parts.Add("result=" + Uri.EscapeDataString(Result));
        return "/dates/corpus?" + string.Join("&", parts);
    }
}
