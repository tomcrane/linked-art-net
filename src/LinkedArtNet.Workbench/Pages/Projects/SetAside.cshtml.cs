using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// What the desk could not decide as it stood, by the way out a reviewer took (the owner, 2026-11-23: "a clear path out of such
/// scenarios"): strings whose records mean different things by them, strings set aside with a question, strings skipped with no
/// reason, and entities reported wrong from a card. What the cataloguers are to mend has its own page.
/// </summary>
public sealed class SetAsideModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    public IReadOnlyList<ParkedString> Mixed { get; private set; } = [];
    public IReadOnlyList<ParkedString> Questions { get; private set; } = [];
    public IReadOnlyList<ParkedString> Skipped { get; private set; } = [];
    public IReadOnlyList<(Entity Entity, string Report)> Reported { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task OnGetAsync()
    {
        Message = TakeFlash();
        var parked = await Services.Store.ParkedStringsAsync(Dataset, 500, HttpContext.RequestAborted);
        Mixed = parked.Where(p => p.Note?.StartsWith(ReviewService.SetAsideMixed, StringComparison.Ordinal) == true).ToList();
        Questions = parked.Where(p => p.Note?.StartsWith(ReviewService.SetAsideQuestion, StringComparison.Ordinal) == true).ToList();
        Skipped = parked.Except(Mixed).Except(Questions).ToList();
        Reported = await Project.Desk.ReportedEntitiesAsync(Dataset, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostDealtAsync(string id, string? note)
    {
        var entity = await Services.Store.GetEntityAsync(id, HttpContext.RequestAborted);
        if (entity is null || entity.Dataset != Dataset) return NotFound();
        await Project.Curation.NoteAsync(Dataset, id, Reviewer, ReviewService.EntityReportDealt + (string.IsNullOrWhiteSpace(note) ? "." : ": " + note.Trim()));
        return Flash($"\"{entity.Label}\" marked as dealt with.", new { dataset = Dataset });
    }
}
