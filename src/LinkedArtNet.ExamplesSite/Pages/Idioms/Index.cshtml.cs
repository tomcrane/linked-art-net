using LinkedArtNet.Examples;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Idioms;

public class IndexModel(ExampleIndex index) : PageModel
{
    public IReadOnlyList<(Idiom Idiom, IReadOnlyList<ExamplePage> Examples)> Entries { get; private set; } = [];

    public void OnGet()
    {
        Entries = LinkedArtNet.Examples.Idioms.All
            .Select(i => (i, (IReadOnlyList<ExamplePage>)(index.ByIdiom.GetValueOrDefault(i.Key) ?? [])
                .Select(index.Find).Where(e => e is not null).Select(e => e!).OrderBy(e => e.Id, StringComparer.Ordinal).ToList()))
            .ToList();
    }

    public string SectionTitle(string slug) => index.FindSection(slug)?.Title ?? slug;
}
