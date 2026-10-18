namespace LinkedArtNet.Workbench.Services;

/// <summary>Configuration section "Workbench". The store's connection string is never here: it comes from ConnectionStrings:Store or the LINKEDART_PG environment variable.</summary>
public sealed class WorkbenchOptions
{
    /// <summary>Lets anyone sign in by typing a name, for development on a laptop. Never on for a shared instance.</summary>
    public bool AllowLocalSignIn { get; set; }

    /// <summary>Logins that own every project and may create projects.</summary>
    public List<string> Owners { get; set; } = [];

    public GitHubOAuthOptions GitHub { get; set; } = new();

    public bool GitHubLoginConfigured => !string.IsNullOrWhiteSpace(GitHub.ClientId) && !string.IsNullOrWhiteSpace(GitHub.ClientSecret);

    public bool IsOwner(string? login) => login is not null && Owners.Any(o => string.Equals(o, login, StringComparison.OrdinalIgnoreCase));
}

public sealed class GitHubOAuthOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
