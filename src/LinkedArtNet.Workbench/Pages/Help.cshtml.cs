using System.Reflection;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.Workbench.Pages;

/// <summary>
/// The user guide (docs/13-workbench-guide.md), rendered from the copy embedded at build time so that the help a reviewer reads is
/// the committed guide and never a second text. Headings carry ids ("the-desk") for links from the pages. Open to anyone, since
/// the question "what am I supposed to do here?" comes before signing in as often as after.
/// </summary>
[AllowAnonymous]
public sealed class HelpModel : PageModel
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAutoIdentifiers().UsePipeTables().Build();
    private static readonly Lazy<string> Rendered = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("guide.md") ?? throw new InvalidOperationException("the guide is not embedded");
        using var reader = new StreamReader(stream);
        return Markdown.ToHtml(reader.ReadToEnd(), Pipeline);
    });

    public string Html => Rendered.Value;

    public void OnGet() { }
}
