namespace LinkedArtNet.Examples.Tests;

internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string CachedExamples => Path.Combine(RepoRoot, "examples", "cached");

    public static string Schemas => Path.Combine(RepoRoot, "schemas", "linked-art", "1.0");

    public static string CachedFile(string id) => Path.Combine(CachedExamples, id.Replace('/', Path.DirectorySeparatorChar) + ".json");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root (linked-art-net.slnx).");
    }
}
