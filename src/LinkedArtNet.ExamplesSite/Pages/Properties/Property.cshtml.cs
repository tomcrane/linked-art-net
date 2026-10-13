using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Properties;

public class PropertyModel(ExampleIndex index) : PageModel
{
    public string Title { get; private set; } = "";
    public bool IsTerm { get; private set; }
    public string? TermId { get; private set; }
    public IReadOnlyList<ExamplePage> Examples { get; private set; } = [];

    public IActionResult OnGet(string name)
    {
        if (index.ByProperty.TryGetValue(name, out var ids))
        {
            Title = name;
        }
        else
        {
            var term = index.ByTerm.Keys.FirstOrDefault(t => ExampleIndex.TermSlug(t).Equals(name, StringComparison.OrdinalIgnoreCase));
            if (term is null) return NotFound();
            ids = index.ByTerm[term];
            IsTerm = true;
            Title = ExampleIndex.TermLabel(term);
            TermId = ExampleIndex.TermId(term);
        }
        Examples = ids.Select(index.Find).Where(e => e is not null).Select(e => e!).OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        return Page();
    }

    public string SectionTitle(string slug) => index.FindSection(slug)?.Title ?? slug;
}
