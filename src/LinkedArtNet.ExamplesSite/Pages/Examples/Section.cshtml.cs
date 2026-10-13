using LinkedArtNet.Examples;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Examples;

public class SectionModel(ExampleIndex index) : PageModel
{
    public Section Section { get; private set; } = null!;

    public IActionResult OnGet(string slug)
    {
        var section = index.FindSection(slug);
        if (section is null) return NotFound();
        Section = section;
        return Page();
    }

    public static string Label(CoverageReport.Status status) => status switch
    {
        CoverageReport.Status.Validated => "matches the site",
        CoverageReport.Status.SiteIssue => "site issue",
        CoverageReport.Status.Mismatch => "mismatch",
        CoverageReport.Status.NotCached => "not cached",
        CoverageReport.Status.Missing => "not yet written",
        _ => status.ToString(),
    };
}
