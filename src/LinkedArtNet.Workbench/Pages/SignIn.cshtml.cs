using System.Security.Claims;
using LinkedArtNet.Alignment.Store;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LinkedArtNet.Workbench.Pages;

[AllowAnonymous]
public sealed class SignInModel(WorkbenchOptions options, IAlignmentStore store, IWebHostEnvironment environment) : PageModel
{
    public WorkbenchOptions Options => options;

    /// <summary>The typed-name sign-in is for a developer's own machine: the configuration must allow it, the environment must be Development or Testing, and the request must come from this machine.</summary>
    public bool LocalSignInAvailable => options.AllowLocalSignIn && (environment.IsDevelopment() || environment.IsEnvironment("Testing")) && (HttpContext.Connection.RemoteIpAddress is null || System.Net.IPAddress.IsLoopback(HttpContext.Connection.RemoteIpAddress));

    public void OnGet() { }

    public IActionResult OnPostGitHub() =>
        options.GitHubLoginConfigured ? Challenge(new AuthenticationProperties { RedirectUri = "/" }, "GitHub") : NotFound();

    public async Task<IActionResult> OnPostLocalAsync(string name)
    {
        if (!LocalSignInAvailable) return NotFound();
        name = (name ?? "").Trim();
        if (name.Length is 0 or > 60) return Page();
        await store.UpsertUserAsync(name, null);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Redirect("/");
    }
}
