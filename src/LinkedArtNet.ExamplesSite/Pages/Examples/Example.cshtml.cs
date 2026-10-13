using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Examples;

public class ExampleModel(ExampleIndex index) : PageModel
{
    public Section Section { get; private set; } = null!;
    public ExamplePage Example { get; private set; } = null!;

    public IActionResult OnGet(string slug, string id)
    {
        var example = index.Find(id);
        if (example is null) return NotFound();
        var section = index.FindSection(slug);
        if (section is null || section.Examples.All(e => e.Id != id))
        {
            // The id exists but not on this page: send the reader to the page that owns it.
            var owner = index.Sections.FirstOrDefault(s => s.Examples.Any(e => e.Id == id));
            return owner is null ? NotFound() : RedirectPermanent($"/examples/{owner.Slug}/{id}");
        }
        Section = section;
        Example = example;
        return Page();
    }
}
