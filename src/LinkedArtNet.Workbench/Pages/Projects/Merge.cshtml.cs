using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// The merge page behind the tray (doc 12 section 13, item 2): the collected entities side by side, the survivor, label and class chosen,
/// one merge for all of them. The tray itself lives in the browser (wwwroot/tray.js); this page takes its ids.
/// </summary>
public sealed class MergeModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    /// <summary>The entities' ids, comma-separated, as the tray sends them.</summary>
    [BindProperty(SupportsGet = true)] public string? Ids { get; set; }

    public IReadOnlyList<Row> Rows { get; private set; } = [];
    public string? Message { get; private set; }
    /// <summary>The suggested survivor: the one with most strings, then the firmer status, then the one with more identifiers.</summary>
    public string? Suggested => Rows.OrderByDescending(r => r.Strings).ThenByDescending(r => r.Entity.Status).ThenByDescending(r => r.Entity.Equivalents.Count).FirstOrDefault()?.Entity.Id;
    public bool ClassesDiffer => Rows.Select(r => r.Entity.Class).Distinct().Count() > 1;

    public sealed record Row(Entity Entity, int Strings, int Queued);

    public async Task<IActionResult> OnGetAsync()
    {
        Message = TakeFlash();
        Rows = await LoadAsync();
        return Page();
    }

    private async Task<IReadOnlyList<Row>> LoadAsync()
    {
        var rows = new List<Row>();
        foreach (var id in (Ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().Take(20))
        {
            var entity = await Services.Store.ResolveEntityAsync(id);
            if (entity is null || entity.Dataset != Dataset || rows.Any(r => r.Entity.Id == entity.Id)) continue;
            var strings = await Services.Store.StringsForEntityAsync(entity.Id, 1000);
            rows.Add(new Row(entity, strings.Count, strings.Count(s => s.Decision.Method == DecisionMethod.Queued)));
        }
        return rows;
    }

    public async Task<IActionResult> OnPostAsync(string survivor, string? label, EntityClass? cls)
    {
        Rows = await LoadAsync();
        var keep = Rows.FirstOrDefault(r => r.Entity.Id == survivor)?.Entity;
        if (keep is null || Rows.Count < 2) { Message = "Choose the survivor among at least two entities."; return Page(); }
        var merged = new List<string>();
        try
        {
            foreach (var row in Rows.Where(r => r.Entity.Id != keep.Id))
            {
                await Project.Curation.MergeAsync(Dataset, row.Entity.Id, keep.Id, Reviewer, ClassesDiffer ? cls : null);
                merged.Add(row.Entity.Label ?? row.Entity.Id);
            }
            if (!string.IsNullOrWhiteSpace(label) && label.Trim() != keep.Label) await Project.Curation.SetLabelAsync(Dataset, keep.Id, label, Reviewer);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            Message = e.Message + (merged.Count == 0 ? "" : $" Merged before it: {string.Join(", ", merged)}.");
            Rows = await LoadAsync();
            return Page();
        }
        Services.Invalidate(Dataset);
        TempData["Flash"] = $"Merged {string.Join(", ", merged.Select(m => $"\"{m}\""))} into \"{keep.Label}\".";
        // the fragment tells the tray script the tray's work is done, so it empties itself
        return RedirectToPage("/Projects/Entity", pageHandler: null, routeValues: new { dataset = Dataset, id = keep.Id }, fragment: "tray-cleared");
    }
}
