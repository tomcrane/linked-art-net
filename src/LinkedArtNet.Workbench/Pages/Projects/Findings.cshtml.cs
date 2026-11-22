using System.Text;
using LinkedArtNet.Alignment;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinkedArtNet.Workbench.Pages.Projects;

/// <summary>
/// The cataloguers' list (design doc 12 section 14.4; the owner, 2026-11-19: "our page of source fixes - cataloguer makes a note a
/// record needs fixing"): things a source record could say better, a row a finding, one record's rows together. Worked together,
/// each row is marked mended or declined here; worked apart, the open rows are exported as a spreadsheet and marked sent.
/// The first source of findings is the decision "not a name: the record wants fixing" (2026-11-22).
/// </summary>
public sealed class FindingsModel(ProjectServices services, ProjectAccess access) : ProjectPageModel(services, access)
{
    /// <summary>Which rows: a status, or "all"; the open ones when not given.</summary>
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    public IReadOnlyList<Finding> Findings { get; private set; } = [];
    public IReadOnlyDictionary<string, int> Counts { get; private set; } = new Dictionary<string, int>();
    public string? Message { get; private set; }
    public string Showing => string.IsNullOrWhiteSpace(Status) ? Finding.Open : Status;

    public async Task OnGetAsync()
    {
        Message = TakeFlash();
        Counts = await Services.Store.FindingCountsAsync(Dataset, HttpContext.RequestAborted);
        Findings = await Services.Store.FindingsAsync(Dataset, Showing == "all" ? null : Showing, 500, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostStatusAsync(long id, string status, string? note)
    {
        try
        {
            if (!await Services.Store.SetFindingStatusAsync(Dataset, id, status, Reviewer, string.IsNullOrWhiteSpace(note) ? null : note.Trim(), HttpContext.RequestAborted)) return NotFound();
        }
        catch (ArgumentException e) { return Flash(e.Message, new { dataset = Dataset, status = Status }); }
        return Flash(status switch { Finding.Mended => "Marked as mended.", Finding.Declined => "Marked as declined.", Finding.Open => "Open again.", _ => "Changed." }, new { dataset = Dataset, status = Status });
    }

    /// <summary>The open rows as a spreadsheet, and each marked sent with the date: for a cataloguer who works from the file.</summary>
    public async Task<IActionResult> OnPostExportAsync()
    {
        var sent = await Services.Store.SendOpenFindingsAsync(Dataset, Reviewer, HttpContext.RequestAborted);
        if (sent.Count == 0) return Flash("There is nothing open to export.", new { dataset = Dataset, status = Status });
        static string Cell(string? x) => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"";
        var csv = new StringBuilder("system,record,title,date,field,value,kind,suggestion,why,raised by,raised,link\r\n");
        foreach (var f in sent)
            csv.Append(string.Join(',', new[] { Project.Manifest.SystemName(f.System), f.RecordId, f.Title, f.Date, Project.Manifest.FieldName(f.Field), f.Value, f.Kind, f.Suggestion, f.Why, f.RaisedBy, f.RaisedAt.ToString("yyyy-MM-dd"), Project.Manifest.RecordUrl(f.System, f.RecordId) }.Select(Cell))).Append("\r\n");
        // a byte order mark, so that a spreadsheet reads the accents
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(), "text/csv", $"{Dataset}-for-cataloguers-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
