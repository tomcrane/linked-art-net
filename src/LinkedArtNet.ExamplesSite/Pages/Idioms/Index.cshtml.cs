using LinkedArtNet.Examples;
using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Idioms;

public class IndexModel(ExampleIndex index) : PageModel
{
    public IReadOnlyList<(Idiom Idiom, IReadOnlyList<ExamplePage> Examples)> Entries { get; private set; } = [];
    private int _max;

    public void OnGet()
    {
        Entries = LinkedArtNet.Examples.Idioms.All
            .Select(i => (i, (IReadOnlyList<ExamplePage>)(index.ByIdiom.GetValueOrDefault(i.Key) ?? [])
                .Select(index.Find).Where(e => e is not null).Select(e => e!).OrderBy(e => e.Id, StringComparer.Ordinal).ToList()))
            .ToList();
        _max = Entries.Count == 0 ? 0 : Entries.Max(e => e.Examples.Count);
    }

    /// <summary>Font size in rem for the cloud: square-root scaling so the common idioms do not drown the rare ones.</summary>
    public double Size(int count) => _max == 0 ? 1 : 0.95 + 1.1 * Math.Sqrt((double)count / _max);

    public string SectionTitle(string slug) => index.FindSection(slug)?.Title ?? slug;
}
