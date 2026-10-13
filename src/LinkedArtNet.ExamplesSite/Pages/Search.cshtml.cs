using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages;

public class SearchModel(ExampleIndex index) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")] public string Query { get; set; } = "";
    public IReadOnlyList<ExamplePage> Results { get; private set; } = [];

    public void OnGet()
    {
        Query = (Query ?? "").Trim();
        if (Query.Length > 200) Query = Query[..200];
        Results = index.Search(Query);
    }

    public string SectionTitle(string slug) => index.FindSection(slug)?.Title ?? slug;
}
