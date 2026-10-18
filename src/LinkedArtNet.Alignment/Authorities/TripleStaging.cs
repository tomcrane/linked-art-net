using LinkedArtNet.Alignment.Store;
using Npgsql;
using NpgsqlTypes;

namespace LinkedArtNet.Alignment.Authorities;

/// <summary>
/// Stages the triples a vocabulary loader needs in an unlogged table beside the store, so a dump of tens or hundreds
/// of millions of statements can be joined in SQL instead of held in memory. One table, <c>triple_staging</c>, is
/// truncated per load; the loaders decide which predicates (and which subjects or objects) are worth keeping.
/// </summary>
public static class TripleStaging
{
    public const string Table = "triple_staging";

    /// <summary>Clears the staging table and copies in every triple the filter accepts. Returns (read, kept).</summary>
    public static async Task<(long Read, long Kept)> StageAsync(PostgresAlignmentStore store, IEnumerable<Triple> triples, Func<Triple, bool> keep, IProgress<string>? progress, CancellationToken ct)
    {
        var db = store.DataSource;
        await using (var cmd = db.CreateCommand($"create unlogged table if not exists {Table} (s text not null, p text not null, o text not null, lang text); truncate {Table}; drop index if exists {Table}_s; drop index if exists {Table}_p_s; drop index if exists {Table}_p_o"))
            await cmd.ExecuteNonQueryAsync(ct);

        long read = 0, kept = 0;
        await using (var conn = await db.OpenConnectionAsync(ct))
        await using (var writer = await conn.BeginBinaryImportAsync($"copy {Table} (s, p, o, lang) from stdin (format binary)", ct))
        {
            foreach (var t in triples)
            {
                read++;
                if (read % 5_000_000 == 0) progress?.Report($"{read:N0} triples read, {kept:N0} staged");
                if (!keep(t)) continue;
                kept++;
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(t.Subject, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(t.Predicate, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(t.Object, NpgsqlDbType.Text, ct);
                if (t.Lang is null) await writer.WriteNullAsync(ct); else await writer.WriteAsync(t.Lang, NpgsqlDbType.Text, ct);
            }
            await writer.CompleteAsync(ct);
        }
        progress?.Report($"{read:N0} triples read, {kept:N0} staged; indexing");
        await using (var cmd = db.CreateCommand($"create index {Table}_s on {Table} (s); create index {Table}_p_s on {Table} (p, s); create index {Table}_p_o on {Table} (p, o); analyze {Table}"))
        {
            cmd.CommandTimeout = 0;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        return (read, kept);
    }

    public static async Task ClearAsync(PostgresAlignmentStore store, CancellationToken ct)
    {
        await using var cmd = store.DataSource.CreateCommand($"truncate {Table}");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>The N-Triples files a path names: the file itself, or those inside a directory.</summary>
    public static IReadOnlyList<string> Files(string path)
    {
        if (Directory.Exists(path))
            return Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".nt", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".nt.gz", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        return File.Exists(path) ? [path] : [];
    }

    public static IEnumerable<Triple> ReadAll(IReadOnlyList<string> paths, IProgress<string>? progress) =>
        paths.SelectMany(p => { progress?.Report($"reading {Path.GetFileName(p)}"); return NTriples.Read(p); });

    public static string VersionFor(IReadOnlyList<string> paths) =>
        paths.Count == 1 ? Path.GetFileName(paths[0]) : Path.GetFileName(Path.GetDirectoryName(paths[0]) ?? paths[0]);
}
