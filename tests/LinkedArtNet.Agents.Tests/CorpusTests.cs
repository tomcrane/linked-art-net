using LinkedArtNet.Dates.Corpus;
using LinkedArtNet.Dates.Realisation;

namespace LinkedArtNet.Agents.Tests;

/// <summary>The agent corpus under corpus/agents: files load, ids are unique, and every verified entry passes.</summary>
public class CorpusTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Root = Path.Combine(RepoRoot, "corpus", "agents");
    private static readonly Lazy<IReadOnlyList<AgentCorpusFile>> Files = new(() => AgentCorpusFile.LoadAll(Root));
    private static readonly Lazy<AgentCorpusEvaluator> Evaluator = new(() => new AgentCorpusEvaluator(datesProfiles: ProfileCatalog.Load(Path.Combine(RepoRoot, "profiles"))));

    public static TheoryData<string, string> VerifiedEntries()
    {
        var data = new TheoryData<string, string>();
        foreach (var f in Files.Value) foreach (var e in f.Entries.Where(e => e.Status == EntryStatus.Verified)) data.Add(f.Path, e.Id);
        return data;
    }

    [Fact]
    public void Files_load_with_unique_ids_and_known_profiles()
    {
        Assert.NotEmpty(Files.Value);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in Files.Value)
        {
            Assert.False(string.IsNullOrWhiteSpace(f.Source.Name), f.Path);
            Assert.False(string.IsNullOrWhiteSpace(f.Source.Licence), f.Path);
            foreach (var e in f.Entries)
            {
                Assert.True(ids.Add(e.Id), $"duplicate id {e.Id}");
                Assert.True(AgentProfileCatalog.BuiltIn.TryGet(e.Profile, out _), $"{e.Id}: unknown profile {e.Profile}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(VerifiedEntries))]
    public void Verified_entry_passes(string file, string id)
    {
        var entry = Files.Value.Single(f => f.Path == file).Entries.Single(e => e.Id == id);
        var result = Evaluator.Value.Evaluate(entry);
        Assert.True(result.Passed, $"{file} {id} \"{entry.Input}\":\n  " + string.Join("\n  ", result.Failures) +
            $"\n  parsed: name='{result.Result.Name}' dates='{result.Result.DateText}' ({result.Result.Dates?.Canonical}) roles=[{string.Join(", ", result.Result.Roles.Select(r => r.Label))}] nationalities=[{string.Join(", ", result.Result.Nationalities.Select(n => n.Label))}] honorifics=[{string.Join(", ", result.Result.Honorifics)}] qualifiers=[{string.Join(", ", result.Result.Qualifiers)}] unconsumed='{result.Result.Unconsumed}' confidence={result.Result.Confidence:0.00}\n  notes: {string.Join(" | ", result.Result.Notes)}");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "linked-art-net.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root");
    }
}
