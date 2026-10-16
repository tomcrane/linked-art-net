using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Dates;

public class ApiModel(DateClient client) : PageModel
{
    public const string SampleInput = "c. 1850";

    /// <summary>The service's public base URL, with no trailing slash.</summary>
    public string Base => client.ApiBase;

    public IReadOnlyList<(string Name, string? Description)> Profiles => client.Profiles;

    /// <summary>A real response for <see cref="SampleInput"/>, so the documented shape is the current one.</summary>
    public string? SampleJson { get; private set; }

    public string SampleUrl => client.ApiUrl(SampleInput, "auto", "default");

    public async Task OnGetAsync(CancellationToken ct)
    {
        SampleJson = await client.RawJsonAsync(SampleInput, "auto", "default", ct);
    }
}
