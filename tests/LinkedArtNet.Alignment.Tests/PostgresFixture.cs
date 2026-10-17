using System.Diagnostics;
using LinkedArtNet.Alignment.Store;
using Npgsql;
using Testcontainers.PostgreSql;

namespace LinkedArtNet.Alignment.Tests;

/// <summary>
/// A disposable database for the store tests: the connection string in LINKEDART_TEST_PG when set (its tables are
/// dropped and recreated, so point it at a scratch database), else a PostgreSQL 15 container through Testcontainers.
/// Tests marked <see cref="PostgresFactAttribute"/> are skipped when neither is available.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public PostgresAlignmentStore Store { get; private set; } = null!;

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
        DataSource = NpgsqlDataSource.Create(connection);
        Store = new PostgresAlignmentStore(DataSource);
        await Store.DropAllAsync();
        await Store.InitialiseAsync();
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }
}

/// <summary>A fact that runs only when a PostgreSQL is reachable (LINKEDART_TEST_PG or Docker); otherwise it is skipped with the reason.</summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresFixture.Available) Skip = "No PostgreSQL available: set LINKEDART_TEST_PG or run Docker for a test container.";
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
