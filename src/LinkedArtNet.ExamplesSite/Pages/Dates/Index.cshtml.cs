using LinkedArtNet.ExamplesSite.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.ExamplesSite.Pages.Dates;

public class IndexModel(DateClient client) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? S { get; set; }
    [BindProperty(SupportsGet = true)] public string Intent { get; set; } = "auto";
    [BindProperty(SupportsGet = true)] public string Profile { get; set; } = "default";

    public PlaygroundResult? Result { get; private set; }
    public IReadOnlyList<(string Name, string? Description)> Profiles => client.Profiles;
    public bool UsesService => client.UsesService;
    /// <summary>Where the raw API response for this request can be seen.</summary>
    public string? ApiUrl { get; private set; }
    public long AxisStart { get; private set; }
    public long AxisEnd { get; private set; }

    public static readonly IReadOnlyList<string> Samples =
        ["late 1950s", "c. 1850", "1606-1669", "fl. 1480-1510", "2nd quarter of the 14th century", "saec. XIX", "Edo period", "um 1500", "4.5 Ga ago", "n.d."];

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (!Profiles.Any(p => p.Name.Equals(Profile, StringComparison.OrdinalIgnoreCase))) Profile = "default";
        if (Intent is not ("auto" or "single" or "pair")) Intent = "auto";
        if (string.IsNullOrWhiteSpace(S)) return;
        if (S.Length > 500) S = S[..500];
        Result = await client.ParseAsync(S, Intent, Profile, ct);
        ApiUrl = client.ApiUrl(S, Intent, Profile);
        if (Result is { Bars.Count: > 0 })
        {
            var start = Result.Bars.Min(b => b.StartYear);
            var end = Result.Bars.Max(b => b.EndYear);
            var pad = Math.Max(1, (end - start) / 10);
            AxisStart = start - pad;
            AxisEnd = end + pad;
        }
    }

    /// <summary>Left offset and width in percent; deep time is drawn on a log scale of distance from the present.</summary>
    public (double Left, double Width) Place(long start, long end)
    {
        var a = X(start); var b = X(end); var lo = X(AxisStart); var hi = X(AxisEnd);
        var range = hi - lo;
        if (range <= 0) return (0, 100);
        var left = Math.Clamp((a - lo) / range * 100, 0, 99.5);
        var width = Math.Max(0.5, (b - a) / range * 100);
        return (left, Math.Min(width, 100 - left));
    }

    private double X(long year)
    {
        if (AxisEnd - AxisStart <= 100_000) return year;
        var distance = 2100 - year; // years before a fixed "present"
        return distance <= 0 ? 0 : -Math.Log10(1 + distance);
    }

    public static string YearLabel(long year) => year switch
    {
        <= -1_000_000 => $"{-year / 1_000_000.0:0.#} Ma ago",
        <= -10_000 => $"{-year / 1000.0:0.#} ka ago",
        <= 0 => $"{1 - year} BCE",
        _ => year.ToString(),
    };
}
