using System.Security.Claims;
using System.Text.Json;
using LinkedArtNet.ExamplesSite.Services;
using LinkedArtNet.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ExamplesOptions>(builder.Configuration.GetSection("Examples"));
builder.Services.Configure<TimeSpanServiceOptions>(builder.Configuration.GetSection("TimeSpanService"));
builder.Services.Configure<TrainerOptions>(builder.Configuration.GetSection("Trainer"));
var trainerOptions = builder.Configuration.GetSection("Trainer").Get<TrainerOptions>() ?? new TrainerOptions();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ExampleIndex>();
builder.Services.AddSingleton<DateClient>();
builder.Services.AddSingleton<CorpusIndex>();
builder.Services.AddSingleton<TrainerStore>();
builder.Services.AddSingleton<Trainer>();
builder.Services.AddHttpClient<GitHubSubmitter>(c => c.BaseAddress = new Uri("https://api.github.com/"));
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Dates/Train", "Reviewer");
    o.Conventions.AllowAnonymousToPage("/Dates/Train/Unavailable");
});
builder.Services.AddResponseCaching();

// ---- trainer sign-in: GitHub OAuth with an allow-list, or a local reviewer for development ---------------------------
var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "linkedartnet.trainer";
        o.LoginPath = "/dates/train/login";
        o.AccessDeniedPath = "/dates/train/unavailable";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
    });
if (trainerOptions.GitHubLoginConfigured)
{
    auth.AddOAuth("GitHub", o =>
    {
        o.ClientId = trainerOptions.GitHub.ClientId!;
        o.ClientSecret = trainerOptions.GitHub.ClientSecret!;
        o.CallbackPath = "/dates/train/signin-github";
        o.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        o.TokenEndpoint = "https://github.com/login/oauth/access_token";
        o.UserInformationEndpoint = "https://api.github.com/user";
        o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        o.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        o.Events.OnCreatingTicket = async context =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("linked-art-net-trainer");
            using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();
            using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
            context.RunClaimActions(user.RootElement);
        };
    });
}
builder.Services.AddAuthorization(o => o.AddPolicy("Reviewer", p => p.RequireAuthenticatedUser().RequireAssertion(ctx => trainerOptions.IsReviewer(ctx.User.Identity?.Name))));

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseResponseCaching();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/healthz", (ExampleIndex index) => Results.Ok(new { examples = index.Total, implemented = index.Implemented }));

// The raw API response for a playground request, in the dates service's shape, for when no public service is configured.
app.MapGet("/dates/result.json", async (DateClient client, string? s, string? intent, string? profile, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(s)) return Results.BadRequest(new { error = "Missing query parameter 's'." });
    if (s.Length > 500) s = s[..500];
    var json = await client.RawJsonAsync(s, intent ?? "auto", profile ?? "default", ct);
    return json is null ? Results.NotFound() : Results.Content(json, "application/json; charset=utf-8");
});

app.MapGet("/dates/train/login", async (HttpContext http, string? returnUrl) =>
{
    var target = returnUrl is { Length: > 0 } && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) ? returnUrl : "/dates/train";
    if (trainerOptions.GitHubLoginConfigured) return Results.Challenge(new AuthenticationProperties { RedirectUri = target }, ["GitHub"]);
    if (trainerOptions.AllowLocalReviewer)
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, TrainerOptions.LocalReviewer)], CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(new ClaimsPrincipal(identity));
        return Results.Redirect(target);
    }
    return Results.Redirect("/dates/train/unavailable");
});
app.MapPost("/dates/train/logout", async (HttpContext http) =>
{
    await http.SignOutAsync();
    return Results.Redirect("/dates");
}).DisableAntiforgery();

// Live preview for the review form: realise a canonical expression and return the spans as JSON.
app.MapGet("/dates/train/realise", (Trainer trainer, string? e, string? profile, string? intent) =>
{
    var (result, error) = trainer.Realise(e ?? "", profile, intent);
    if (result is null) return Results.Ok(new { ok = false, error });
    return Results.Ok(new
    {
        ok = true,
        canonical = result.Canonical,
        edtf = result.Edtf,
        status = result.Status.ToString().ToLowerInvariant(),
        kind = char.ToLowerInvariant(result.Kind.ToString()[0]) + result.Kind.ToString()[1..],
        timespan = result.TimeSpan is null ? null : JsonSerializer.Deserialize<JsonElement?>(LinkedArtJson.Serialize(result.TimeSpan)),
        begin = result.Begin is null ? null : JsonSerializer.Deserialize<JsonElement?>(LinkedArtJson.Serialize(result.Begin)),
        end = result.End is null ? null : JsonSerializer.Deserialize<JsonElement?>(LinkedArtJson.Serialize(result.End)),
    });
}).RequireAuthorization("Reviewer");

app.Run();

public partial class Program;
