using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace LinkedArtNet.Workbench.Tests;

/// <summary>
/// The workbench against a disposable PostgreSQL: LINKEDART_TEST_PG when set, else a container through Testcontainers.
/// The app applies the schema on start; tests use their own dataset keys so a shared scratch database stays usable.
/// </summary>
public sealed class WorkbenchFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public static string? ExternalConnectionString => Environment.GetEnvironmentVariable("LINKEDART_TEST_PG");

    public static readonly Lazy<bool> DockerAvailable = new(() =>
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("docker", "info --format {{.ServerVersion}}") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            if (p is null) return false;
            if (!p.WaitForExit(15_000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 0;
        }
        catch { return false; }
    });

    public static bool Available => !string.IsNullOrWhiteSpace(ExternalConnectionString) || DockerAvailable.Value;

    public async Task InitializeAsync()
    {
        string connection;
        if (!string.IsNullOrWhiteSpace(ExternalConnectionString)) connection = ExternalConnectionString;
        else
        {
            _container = new PostgreSqlBuilder("postgres:15-alpine").Build();
            await _container.StartAsync();
            connection = _container.GetConnectionString();
        }
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Store"] = connection,
                ["Workbench:AllowLocalSignIn"] = "true",
                ["Workbench:Owners:0"] = "owner",
            }));
        });
        _ = Factory.Server; // start the host, which applies the schema
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    /// <summary>A client with cookies, not following redirects, signed in through the development form as the login.</summary>
    public async Task<HttpClient> SignedInAsync(string login)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var response = await PostFormAsync(client, "/signin?handler=Local", "/signin", new() { ["name"] = login });
        if (response.StatusCode != HttpStatusCode.Redirect) throw new InvalidOperationException($"Sign-in as {login} gave {(int)response.StatusCode}.");
        return client;
    }

    /// <summary>Posts a Razor Pages form: fetches the page for its antiforgery token first.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string postUrl, string pageUrl, Dictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(pageUrl);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        if (token.Length == 0) token = Regex.Match(page, "__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        fields["__RequestVerificationToken"] = token;
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(fields));
    }
}

public sealed class WorkbenchFactAttribute : FactAttribute
{
    public WorkbenchFactAttribute()
    {
        if (!WorkbenchFixture.Available) Skip = "No PostgreSQL available: set LINKEDART_TEST_PG or run Docker for a test container.";
    }
}

[CollectionDefinition("workbench")]
public sealed class WorkbenchCollection : ICollectionFixture<WorkbenchFixture>;
