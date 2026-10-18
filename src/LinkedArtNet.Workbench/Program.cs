// The alignment workbench (design doc 12): the human side of the toolkit. Projects with members, a review desk for
// the reconciler's queued strings with the evidence beside each question, and the actions that write a person's
// judgement back to the store. Runs against the PostgreSQL store; the connection string comes from
// ConnectionStrings:Store or the LINKEDART_PG environment variable, never from a file in the repository.

using System.Security.Claims;
using System.Text.Json;
using LinkedArtNet.Alignment.Store;
using LinkedArtNet.Workbench.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
var options = builder.Configuration.GetSection("Workbench").Get<WorkbenchOptions>() ?? new WorkbenchOptions();

// options and the store read the configuration when first resolved, so a test host's settings are seen
builder.Services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection("Workbench").Get<WorkbenchOptions>() ?? new WorkbenchOptions());
builder.Services.AddSingleton<IAlignmentStore>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connection = configuration.GetConnectionString("Store") ?? Environment.GetEnvironmentVariable("LINKEDART_PG");
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set LINKEDART_PG (or ConnectionStrings:Store) to the alignment store's connection string.");
    return new PostgresAlignmentStore(connection);
});
builder.Services.AddSingleton<ProjectServices>();
builder.Services.AddSingleton<ProjectAccess>();
builder.Services.AddRazorPages(o =>
{
    o.Conventions.AuthorizeFolder("/Projects");
    o.Conventions.AuthorizePage("/Index");
});

// ---- sign-in: GitHub OAuth when configured, a typed name for development -----------------------------------------
var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "linkedartnet.workbench";
        o.LoginPath = "/signin";
        o.AccessDeniedPath = "/signin";
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
    });
if (options.GitHubLoginConfigured)
{
    auth.AddOAuth("GitHub", o =>
    {
        o.ClientId = options.GitHub.ClientId!;
        o.ClientSecret = options.GitHub.ClientSecret!;
        o.CallbackPath = "/signin-github";
        o.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
        o.TokenEndpoint = "https://github.com/login/oauth/access_token";
        o.UserInformationEndpoint = "https://api.github.com/user";
        o.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
        o.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
        o.ClaimActions.MapJsonKey("urn:github:name", "name");
        o.Events.OnCreatingTicket = async context =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("linked-art-net-workbench");
            using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
            response.EnsureSuccessStatusCode();
            using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
            context.RunClaimActions(user.RootElement);
        };
    });
}
builder.Services.AddAuthorization();

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapGet("/healthz", async (IAlignmentStore store) => Results.Ok(new { projects = (await store.ListProjectsAsync()).Count }));

// the schema is applied on start, and the configured owners exist as users
using (var scope = app.Services.CreateScope())
{
    var store = scope.ServiceProvider.GetRequiredService<IAlignmentStore>();
    await store.InitialiseAsync();
    foreach (var owner in scope.ServiceProvider.GetRequiredService<WorkbenchOptions>().Owners) await store.UpsertUserAsync(owner, null);
}

app.Run();

public partial class Program;
