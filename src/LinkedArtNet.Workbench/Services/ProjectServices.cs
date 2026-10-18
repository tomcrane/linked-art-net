using System.Collections.Concurrent;
using LinkedArtNet.Alignment;
using LinkedArtNet.Alignment.Reconciliation;
using LinkedArtNet.Alignment.Store;

namespace LinkedArtNet.Workbench.Services;

/// <summary>A project with the services built from its manifest: the identifier scheme, the reconciler and the review desk.</summary>
public sealed record ProjectContext(Project Project, IdentifierScheme Scheme, ProjectManifest Manifest, Reconciler Reconciler, ReviewService Desk, CurationService Curation);

/// <summary>Builds and caches the per-project services; <see cref="Invalidate"/> after the project or its manifest changes.</summary>
public sealed class ProjectServices(IAlignmentStore store)
{
    private readonly ConcurrentDictionary<string, ProjectContext> _cache = new(StringComparer.Ordinal);

    public IAlignmentStore Store => store;

    public async Task<ProjectContext?> GetAsync(string dataset, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(dataset, out var cached)) return cached;
        var project = await store.GetProjectAsync(dataset, ct);
        if (project is null) return null;
        var manifest = ProjectManifest.FromJson(project.ManifestJson);
        var scheme = new IdentifierScheme(project.IdentifierBase);
        var sources = manifest.Sources.Select(s => (IAuthoritySource)new LocalIndexSource(store, s)).ToList();
        var reconciler = new Reconciler(store, scheme, sources, manifest.ToOptions());
        var context = new ProjectContext(project, scheme, manifest, reconciler, new ReviewService(store, scheme, reconciler), new CurationService(store, scheme, reconciler));
        _cache[dataset] = context;
        return context;
    }

    public void Invalidate(string dataset) => _cache.TryRemove(dataset, out _);
}

/// <summary>Who may do what in a project: owners from configuration own everything; everyone else has the role their membership gives.</summary>
public sealed class ProjectAccess(IAlignmentStore store, WorkbenchOptions options)
{
    public async Task<string?> RoleAsync(string dataset, string? login, CancellationToken ct = default)
    {
        if (login is null) return null;
        if (options.IsOwner(login)) return ProjectRole.Owner;
        var memberships = await store.MembershipsForAsync(login, ct);
        return memberships.FirstOrDefault(m => string.Equals(m.Dataset, dataset, StringComparison.Ordinal))?.Role;
    }

    /// <summary>The projects a login may see: all of them for an owner, otherwise those with a membership.</summary>
    public async Task<IReadOnlyList<(Project Project, string Role)>> ProjectsForAsync(string? login, CancellationToken ct = default)
    {
        if (login is null) return [];
        var all = await store.ListProjectsAsync(ct);
        if (options.IsOwner(login)) return all.Select(p => (p, ProjectRole.Owner)).ToList();
        var memberships = (await store.MembershipsForAsync(login, ct)).ToDictionary(m => m.Dataset, m => m.Role, StringComparer.Ordinal);
        return all.Where(p => memberships.ContainsKey(p.Dataset)).Select(p => (p, memberships[p.Dataset])).ToList();
    }
}
