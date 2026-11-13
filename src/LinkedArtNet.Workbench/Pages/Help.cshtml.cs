using System.Collections.Concurrent;
using System.Reflection;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.Workbench.Pages;

/// <summary>
/// The user guide (docs/13-workbench-guide.md) at /help and the worked scenarios (docs/16-workbench-scenarios.md) at /help/scenarios,
/// rendered from the copies embedded at build time so that the help a reviewer reads is the committed text and never a second one.
/// Headings carry ids ("the-desk", "holder-pair") for links from the pages; links between the two documents by file name are turned
/// into links between the two pages, and the scenarios' pictures (docs/img/scenarios) are served from the assembly beside them. Open
/// to anyone, since the question "what am I supposed to do here?" comes before signing in as often as after.
/// </summary>
[AllowAnonymous]
public sealed class HelpModel : PageModel
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAutoIdentifiers().UsePipeTables().Build();
    private static readonly IReadOnlyDictionary<string, string> Documents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [""] = "guide.md", ["scenarios"] = "scenarios.md" };
    private static readonly ConcurrentDictionary<string, string> Rendered = new(StringComparer.Ordinal);

    public string Html { get; private set; } = "";
    public bool Scenarios { get; private set; }

    public IActionResult OnGet(string? doc)
    {
        if (!Documents.TryGetValue(doc ?? "", out var resource)) return NotFound();
        Scenarios = resource == "scenarios.md";
        Html = Rendered.GetOrAdd(resource, Render);
        return Page();
    }

    private static string Render(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource) ?? throw new InvalidOperationException($"{resource} is not embedded");
        using var reader = new StreamReader(stream);
        // the documents name each other by file, as the repository reads them; here they are two pages
        return Markdown.ToHtml(reader.ReadToEnd(), Pipeline)
            .Replace("href=\"13-workbench-guide.md", "href=\"/help", StringComparison.Ordinal)
            .Replace("href=\"16-workbench-scenarios.md", "href=\"/help/scenarios", StringComparison.Ordinal)
            .Replace("src=\"img/scenarios/", "src=\"/help/img/scenarios/", StringComparison.Ordinal);
    }

    /// <summary>A scenario's picture from the assembly, by its file name; null when there is none of the name.</summary>
    public static Stream? Picture(string name) =>
        name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) && name.IndexOfAny(['/', '\\']) < 0 ? Assembly.GetExecutingAssembly().GetManifestResourceStream("scenarios/" + name) : null;
}
