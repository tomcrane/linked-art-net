using System.Text.RegularExpressions;
using System.Globalization;
using System.Data;
using System.Reflection;
using Npgsql;
using NpgsqlTypes;

namespace LinkedArtNet.Alignment.Store;

/// <summary>
/// The store on PostgreSQL (design decision 2), plain Npgsql, one schema in <c>Schema.sql</c>. Construct with a
/// connection string or a shared <see cref="NpgsqlDataSource"/>; call <see cref="InitialiseAsync"/> once per
/// process. Timestamps are UTC. Text keys are compared as stored; callers normalise with <see cref="StringKey"/>.
/// </summary>
public sealed class PostgresAlignmentStore : IAlignmentStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource _db;
    private readonly bool _ownsDataSource;

    public PostgresAlignmentStore(string connectionString) : this(NpgsqlDataSource.Create(connectionString), ownsDataSource: true) { }

    public PostgresAlignmentStore(NpgsqlDataSource dataSource, bool ownsDataSource = false)
    {
        _db = dataSource;
        _ownsDataSource = ownsDataSource;
    }

    public ValueTask DisposeAsync() => _ownsDataSource ? _db.DisposeAsync() : ValueTask.CompletedTask;

    /// <summary>The connection source, for loaders that stage data beside the store's tables.</summary>
    public NpgsqlDataSource DataSource => _db;

    /// <summary>The schema statements, from the embedded Schema.sql.</summary>
    public static string SchemaSql { get; } = ReadSchema();

    private static string ReadSchema()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LinkedArtNet.Alignment.Schema.sql") ?? throw new InvalidOperationException("Schema.sql is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The version the embedded Schema.sql brings a store to: its last schema_version insert.</summary>
    public static int SchemaVersion { get; } = int.Parse(Regex.Matches(SchemaSql, @"insert into schema_version \(version\) values \((\d+)\)")[^1].Groups[1].Value, CultureInfo.InvariantCulture);

    /// <summary>
    /// Brings the store's schema up to date. A store already at the current version is left alone: the script's
    /// "create index if not exists" statements take a share lock on each table even when the index exists, and would
    /// wait behind a running load's insert transaction.
    /// </summary>
    public async Task InitialiseAsync(CancellationToken ct = default)
    {
        if (await CurrentSchemaVersionAsync(ct) >= SchemaVersion) return;
        // a migration may rebuild an index over millions of rows; no client timeout. The script runs as one transaction.
        await using var cmd = _db.CreateCommand(SchemaSql);
        cmd.CommandTimeout = 0;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> CurrentSchemaVersionAsync(CancellationToken ct)
    {
        await using var cmd = _db.CreateCommand("select coalesce(max(version), 0) from schema_version");
        try { return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UndefinedTable) { return 0; }
    }

    /// <summary>Drops every table of the store. For disposable test databases only.</summary>
    internal async Task DropAllAsync(CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("drop table if exists decisions, entity_equivalents, overrides, source_strings, authority_records, runs, entities, schema_version, getty_staging, triple_staging, memberships, notes, projects, users cascade; do $$ declare t text; begin for t in select tablename from pg_tables where schemaname = current_schema() and tablename like 'triple_staging_%' loop execute 'drop table if exists ' || quote_ident(t); end loop; end $$");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---- entities --------------------------------------------------------------------------------------------------

    private const string EntityColumns = "e.id, e.dataset, e.class, e.label, e.labels, e.begin_earliest, e.begin_latest, e.end_earliest, e.end_latest, e.lifespan_text, e.nationalities, e.roles, e.status, e.merged_into, e.created_at, e.updated_at, e.run_id";

    public async Task<Entity> AddEntityAsync(string dataset, NewEntity entity, IdentifierScheme scheme, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var labels = (entity.Labels ?? []).Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.Ordinal).ToArray();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var id = entity.Id ?? scheme.Mint(entity.Class);
            await using var cmd = _db.CreateCommand(
                """
                insert into entities (id, dataset, class, label, labels, normalised_labels, begin_earliest, begin_latest, end_earliest, end_latest, lifespan_text, nationalities, roles, status, created_at, updated_at, run_id)
                values (@id, @dataset, @class, @label, @labels, @normalised, @b1, @b2, @e1, @e2, @ltext, @nat, @roles, @status, @now, @now, @run)
                on conflict (id) do nothing
                """);
            Add(cmd, "id", id); Add(cmd, "dataset", dataset); Add(cmd, "class", entity.Class.ToString()); Add(cmd, "label", entity.Label);
            Add(cmd, "labels", labels); Add(cmd, "normalised", NormalisedLabels(entity.Label, labels));
            AddLifespan(cmd, entity.Lifespan);
            Add(cmd, "nat", (entity.Nationalities ?? []).ToArray()); Add(cmd, "roles", (entity.Roles ?? []).ToArray());
            Add(cmd, "status", Status(entity.Status)); Add(cmd, "now", now); Add(cmd, "run", entity.RunId);
            var inserted = await cmd.ExecuteNonQueryAsync(ct);
            if (inserted == 1) return (await GetEntityAsync(id, ct))!;
            if (entity.Id is not null) throw new InvalidOperationException($"An entity with id {entity.Id} already exists.");
        }
        throw new InvalidOperationException("Could not mint an unused identifier after 20 attempts.");
    }

    public async Task<Entity?> GetEntityAsync(string id, CancellationToken ct = default)
    {
        var list = await QueryEntitiesAsync($"select {EntityColumns} from entities e where e.id = @id", cmd => Add(cmd, "id", id), ct);
        return list.SingleOrDefault();
    }

    public async Task<Entity?> ResolveEntityAsync(string id, CancellationToken ct = default)
    {
        var current = id;
        for (var hops = 0; hops < 10; hops++)
        {
            var entity = await GetEntityAsync(current, ct);
            if (entity is null) return null;
            if (entity.MergedInto is null) return entity;
            current = entity.MergedInto;
        }
        throw new InvalidOperationException($"Merge chain from {id} is longer than ten hops.");
    }

    public Task<IReadOnlyList<Entity>> FindEntitiesByLabelAsync(string normalisedLabel, string? dataset = null, EntityClass? cls = null, CancellationToken ct = default) =>
        QueryEntitiesAsync(
            $"select {EntityColumns} from entities e where e.normalised_labels @> @key and e.status <> 'merged'" +
            (dataset is null ? "" : " and e.dataset = @dataset") + (cls is null ? "" : " and e.class = @class") + " order by e.created_at",
            cmd => { Add(cmd, "key", new[] { normalisedLabel }); if (dataset is not null) Add(cmd, "dataset", dataset); if (cls is not null) Add(cmd, "class", cls.ToString()!); }, ct);

    public Task<IReadOnlyList<Entity>> ListEntitiesAsync(string dataset, string? equivalentSource = null, string? afterId = null, int limit = 500, CancellationToken ct = default) =>
        QueryEntitiesAsync(
            $"select {EntityColumns} from entities e where e.dataset = @dataset and e.merged_into is null"
            + (afterId is null ? "" : " and e.id > @after")
            + (equivalentSource is null ? "" : " and exists (select 1 from entity_equivalents q where q.entity_id = e.id and q.source = @source)")
            + " order by e.id limit @limit",
            cmd => { Add(cmd, "dataset", dataset); if (afterId is not null) Add(cmd, "after", afterId); if (equivalentSource is not null) Add(cmd, "source", equivalentSource); Add(cmd, "limit", limit); }, ct);

    public Task<IReadOnlyList<Entity>> FindEntitiesByLabelContainingAsync(string dataset, string containing, int limit = 20, CancellationToken ct = default)
    {
        if (containing.Length == 0) return Task.FromResult<IReadOnlyList<Entity>>([]);
        var pattern = "%" + containing.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
        return QueryEntitiesAsync(
            $"select {EntityColumns} from entities e where e.dataset = @dataset and e.merged_into is null and exists (select 1 from unnest(e.normalised_labels) l where l like @pattern) order by e.label limit @limit",
            cmd => { Add(cmd, "dataset", dataset); Add(cmd, "pattern", pattern); Add(cmd, "limit", limit); }, ct);
    }

    public Task<IReadOnlyList<Entity>> FindEntitiesByEquivalentAsync(string uri, CancellationToken ct = default) =>
        QueryEntitiesAsync($"select {EntityColumns} from entities e join entity_equivalents q on q.entity_id = e.id where q.uri = @uri order by e.created_at", cmd => Add(cmd, "uri", uri), ct);

    public async Task UpdateEntityAsync(Entity entity, CancellationToken ct = default)
    {
        var labels = entity.Labels.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.Ordinal).ToArray();
        await using var cmd = _db.CreateCommand(
            """
            update entities set label = @label, labels = @labels, normalised_labels = @normalised, begin_earliest = @b1, begin_latest = @b2, end_earliest = @e1, end_latest = @e2,
                lifespan_text = @ltext, nationalities = @nat, roles = @roles, status = @status, updated_at = @now
            where id = @id
            """);
        Add(cmd, "id", entity.Id); Add(cmd, "label", entity.Label); Add(cmd, "labels", labels); Add(cmd, "normalised", NormalisedLabels(entity.Label, labels));
        AddLifespan(cmd, entity.Lifespan); Add(cmd, "nat", entity.Nationalities.ToArray()); Add(cmd, "roles", entity.Roles.ToArray());
        Add(cmd, "status", Status(entity.Status)); Add(cmd, "now", DateTimeOffset.UtcNow);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new KeyNotFoundException($"No entity {entity.Id}.");
    }

    public async Task AddEquivalentAsync(string entityId, Equivalent equivalent, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var cmd = new NpgsqlCommand(
            """
            insert into entity_equivalents (entity_id, uri, source, method, confidence, decided_at, dataset_version, run_id)
            values (@entity, @uri, @source, @method, @confidence, @at, @version, @run)
            on conflict (entity_id, uri) do update set source = excluded.source, method = excluded.method, confidence = excluded.confidence, decided_at = excluded.decided_at, dataset_version = excluded.dataset_version, run_id = excluded.run_id
            """, conn, tx))
        {
            Add(cmd, "entity", entityId); Add(cmd, "uri", equivalent.Uri); Add(cmd, "source", equivalent.Source); Add(cmd, "method", equivalent.Method);
            Add(cmd, "confidence", equivalent.Confidence); Add(cmd, "at", equivalent.DecidedAt.ToUniversalTime()); Add(cmd, "version", equivalent.DatasetVersion); Add(cmd, "run", equivalent.RunId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand("update entities set status = 'aligned', updated_at = @now where id = @id and status = 'provisional'", conn, tx))
        {
            Add(cmd, "id", entityId); Add(cmd, "now", DateTimeOffset.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public async Task MergeAsync(string loserId, string survivorId, string? reviewer, CancellationToken ct = default)
    {
        if (loserId == survivorId) throw new ArgumentException("An entity cannot be merged into itself.");
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        // lock both rows; the survivor is followed to the end of its merge chain, and a loser already merged, or sitting on that chain, is refused
        string? loserDataset = null, loserStatus = null;
        await using (var cmd = new NpgsqlCommand("select dataset, status from entities where id = @id for update", conn, tx))
        {
            Add(cmd, "id", loserId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) { loserDataset = r.GetString(0); loserStatus = r.GetString(1); }
        }
        if (loserDataset is null) throw new InvalidOperationException($"Entity {loserId} does not exist.");
        if (loserStatus == "merged") throw new InvalidOperationException($"Entity {loserId} is already merged.");
        var chain = new List<string>();
        for (var current = survivorId; ;)
        {
            if (current == loserId) throw new InvalidOperationException($"Entity {survivorId} resolves through {loserId}: the merge would make a cycle.");
            if (chain.Contains(current) || chain.Count > 10) throw new InvalidOperationException($"Merge chain from {survivorId} does not end.");
            chain.Add(current);
            string? next = null, dataset = null;
            await using (var cmd = new NpgsqlCommand("select merged_into, dataset from entities where id = @id for update", conn, tx))
            {
                Add(cmd, "id", current);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct)) throw new InvalidOperationException($"Entity {current} does not exist.");
                next = Str(r, 0); dataset = r.GetString(1);
            }
            if (dataset != loserDataset) throw new InvalidOperationException("Entities of different projects cannot be merged.");
            if (next is null) { survivorId = current; break; }
            current = next;
        }
        await using (var cmd = new NpgsqlCommand(
            """
            insert into entity_equivalents (entity_id, uri, source, method, confidence, decided_at, dataset_version, run_id)
            select @survivor, uri, source, method, confidence, decided_at, dataset_version, run_id from entity_equivalents where entity_id = @loser
            on conflict (entity_id, uri) do nothing
            """, conn, tx))
        {
            Add(cmd, "survivor", survivorId); Add(cmd, "loser", loserId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand("delete from entity_equivalents where entity_id = @loser", conn, tx)) { Add(cmd, "loser", loserId); await cmd.ExecuteNonQueryAsync(ct); }
        await using (var cmd = new NpgsqlCommand("update decisions set entity_id = @survivor where entity_id = @loser and valid", conn, tx)) { Add(cmd, "survivor", survivorId); Add(cmd, "loser", loserId); await cmd.ExecuteNonQueryAsync(ct); }
        await using (var cmd = new NpgsqlCommand("update entities set status = 'merged', merged_into = @survivor, updated_at = @now where id = @loser and status <> 'merged'", conn, tx))
        {
            Add(cmd, "survivor", survivorId); Add(cmd, "loser", loserId); Add(cmd, "now", now);
            if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException($"Entity {loserId} does not exist or is already merged.");
        }
        await using (var cmd = new NpgsqlCommand("update entities set updated_at = @now, status = case when status = 'provisional' and exists (select 1 from entity_equivalents where entity_id = @survivor) then 'aligned' else status end where id = @survivor", conn, tx))
        {
            Add(cmd, "survivor", survivorId); Add(cmd, "now", now);
            if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new InvalidOperationException($"Entity {survivorId} does not exist.");
        }
        // the loser's labels come across, so its spellings keep finding the survivor
        string? loserLabel = null; string[] loserLabels = []; string? survivorLabel = null; string[] survivorLabels = [];
        await using (var cmd = new NpgsqlCommand("select id, label, labels from entities where id in (@loser, @survivor)", conn, tx))
        {
            Add(cmd, "loser", loserId); Add(cmd, "survivor", survivorId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (r.GetString(0) == loserId) { loserLabel = Str(r, 1); loserLabels = r.GetFieldValue<string[]>(2); }
                else { survivorLabel = Str(r, 1); survivorLabels = r.GetFieldValue<string[]>(2); }
            }
        }
        var merged = survivorLabels.Concat(loserLabels).Append(loserLabel).OfType<string>().Where(l => l.Length > 0 && l != survivorLabel).Distinct(StringComparer.Ordinal).ToArray();
        await using (var cmd = new NpgsqlCommand("update entities set labels = @labels, normalised_labels = @normalised where id = @survivor", conn, tx))
        {
            Add(cmd, "survivor", survivorId); Add(cmd, "labels", merged); Add(cmd, "normalised", NormalisedLabels(survivorLabel, merged));
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private async Task<IReadOnlyList<Entity>> QueryEntitiesAsync(string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        var entities = new List<Entity>();
        await using (var cmd = _db.CreateCommand(sql))
        {
            bind(cmd);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                entities.Add(new Entity(
                    r.GetString(0), r.GetString(1), Enum.Parse<EntityClass>(r.GetString(2)), Str(r, 3), r.GetFieldValue<string[]>(4),
                    ReadLifespan(r, 5), r.GetFieldValue<string[]>(10), r.GetFieldValue<string[]>(11),
                    Enum.Parse<EntityStatus>(r.GetString(12), ignoreCase: true), Str(r, 13), [], r.GetFieldValue<DateTimeOffset>(14), r.GetFieldValue<DateTimeOffset>(15), Str(r, 16)));
            }
        }
        if (entities.Count == 0) return entities;
        var byId = entities.ToDictionary(e => e.Id, e => new List<Equivalent>());
        await using (var cmd = _db.CreateCommand("select entity_id, uri, source, method, confidence, decided_at, dataset_version, run_id from entity_equivalents where entity_id = any(@ids) order by decided_at"))
        {
            Add(cmd, "ids", byId.Keys.ToArray());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                byId[r.GetString(0)].Add(new Equivalent(r.GetString(1), r.GetString(2), r.GetString(3), r.GetDouble(4), r.GetFieldValue<DateTimeOffset>(5), Str(r, 6), Str(r, 7)));
        }
        return entities.Select(e => e with { Equivalents = byId[e.Id] }).ToList();
    }

    /// <summary>A session-level advisory lock on a key, held on its own connection until disposed: two writers that would create an entity for the same authority URI take turns.</summary>
    public async Task<IAsyncDisposable> LockAsync(string key, CancellationToken ct = default)
    {
        var conn = await _db.OpenConnectionAsync(ct);
        try
        {
            await using var take = new NpgsqlCommand("select pg_advisory_lock(hashtext(@key))", conn);
            take.Parameters.AddWithValue("key", key);
            await take.ExecuteNonQueryAsync(ct);
        }
        catch { await conn.DisposeAsync(); throw; }
        return new AdvisoryLock(conn, key);
    }

    private sealed class AdvisoryLock(NpgsqlConnection conn, string key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var release = new NpgsqlCommand("select pg_advisory_unlock(hashtext(@key))", conn);
                release.Parameters.AddWithValue("key", key);
                await release.ExecuteNonQueryAsync();
            }
            finally { await conn.DisposeAsync(); }
        }
    }

    /// <summary>Recomputes every entity's and authority record's lookup keys from its labels, after the key rules change (<see cref="StringKey.Variants"/>). Returns how many rows were rewritten.</summary>
    public async Task<(int Entities, int AuthorityRecords)> ReindexLabelsAsync(string? source = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var entities = await ReindexAsync("entities", "id", null, progress, ct);
        var records = await ReindexAsync("authority_records", "uri", source, progress, ct);
        await using (var cmd = _db.CreateCommand("analyze entities; analyze authority_records")) await cmd.ExecuteNonQueryAsync(ct);
        return (entities, records);
    }

    private async Task<int> ReindexAsync(string table, string keyColumn, string? source, IProgress<string>? progress, CancellationToken ct)
    {
        var seen = 0; var written = 0;
        string? after = null;
        while (true)
        {
            var batch = new List<(string Key, string? Label, string[] Labels, string[] Current)>();
            await using (var cmd = _db.CreateCommand($"select {keyColumn}, label, labels, normalised_labels from {table} where true"
                + (source is null ? "" : " and source = @source") + (after is null ? "" : $" and {keyColumn} > @after") + $" order by {keyColumn} limit 2000"))
            {
                if (source is not null) Add(cmd, "source", source);
                if (after is not null) Add(cmd, "after", after);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) batch.Add((r.GetString(0), Str(r, 1), r.GetFieldValue<string[]>(2), r.GetFieldValue<string[]>(3)));
            }
            if (batch.Count == 0) break;
            await using (var conn = await _db.OpenConnectionAsync(ct))
            await using (var b = conn.CreateBatch())
            {
                foreach (var (key, label, labels, current) in batch)
                {
                    var keys = NormalisedLabels(label, labels);
                    if (keys.SequenceEqual(current, StringComparer.Ordinal)) continue; // already current: no write, no index churn
                    var c = new NpgsqlBatchCommand($"update {table} set normalised_labels = @n where {keyColumn} = @k");
                    c.Parameters.AddWithValue("n", keys); c.Parameters.AddWithValue("k", key);
                    b.BatchCommands.Add(c);
                    written++;
                }
                if (b.BatchCommands.Count > 0) await b.ExecuteNonQueryAsync(ct);
            }
            seen += batch.Count;
            after = batch[^1].Key;
            if (seen % 20000 == 0) progress?.Report($"{table}: {seen:N0} checked, {written:N0} rewritten");
        }
        return written;
    }

    /// <summary>Removes a source's records that a load did not touch (fetched before the load began): retired or merged ids. Returns how many went.</summary>
    public async Task<int> RetireAuthoritiesAsync(string source, DateTimeOffset notFetchedSince, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("delete from authority_records where source = @source and fetched_at < @since");
        Add(cmd, "source", source); Add(cmd, "since", notFetchedSince.ToUniversalTime());
        cmd.CommandTimeout = 0;
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> RemoveEquivalentAsync(string entityId, string uri, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("delete from entity_equivalents where entity_id = @e and uri = @uri");
        Add(cmd, "e", entityId); Add(cmd, "uri", uri);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public Task<IReadOnlyList<Entity>> BrowseEntitiesAsync(string dataset, EntityStatus? status = null, EntityClass? cls = null, string? afterId = null, int limit = 50, CancellationToken ct = default) =>
        QueryEntitiesAsync(
            $"select {EntityColumns} from entities e where e.dataset = @dataset and e.merged_into is null"
            + (status is null ? "" : " and e.status = @status") + (cls is null ? "" : " and e.class = @class") + (afterId is null ? "" : " and e.id > @after")
            + " order by e.id limit @limit",
            cmd => { Add(cmd, "dataset", dataset); if (status is not null) Add(cmd, "status", Status(status.Value)); if (cls is not null) Add(cmd, "class", cls.ToString()!); if (afterId is not null) Add(cmd, "after", afterId); Add(cmd, "limit", limit); }, ct);

    public async Task<IReadOnlyList<DecidedString>> StringsForEntityAsync(string entityId, int limit = 200, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            $"select {StringColumns}, {DecisionColumns} from decisions d join source_strings s on s.id = d.source_string_id where d.entity_id = @e and d.valid order by s.context, s.raw limit @limit");
        Add(cmd, "e", entityId); Add(cmd, "limit", limit);
        var list = new List<DecidedString>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var s = ReadString(r);
            var d = new Decision(r.GetInt64(9), r.GetInt64(10), Str(r, 11), r.GetString(12), Str(r, 13), Str(r, 14), r.GetFieldValue<DateTimeOffset>(15), Str(r, 16), Str(r, 17), r.GetBoolean(18), r.IsDBNull(19) ? null : r.GetInt64(19));
            list.Add(new DecidedString(s, d));
        }
        return list;
    }

    public async Task<IReadOnlyList<SourceString>> FindStringsAsync(string dataset, string containing, int limit = 200, CancellationToken ct = default)
    {
        var pattern = "%" + containing.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        await using var cmd = _db.CreateCommand($"select {StringColumns} from source_strings s where s.dataset = @dataset and s.normalised like @pattern order by s.raw limit @limit");
        Add(cmd, "dataset", dataset); Add(cmd, "pattern", pattern); Add(cmd, "limit", limit);
        var list = new List<SourceString>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadString(r));
        return list;
    }

    public async Task<bool> ReopenStringAsync(long stringId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("update decisions set valid = false where source_string_id = @s and valid");
        Add(cmd, "s", stringId);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<int> RemoveOrphanProvisionalsAsync(string dataset, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            """
            delete from entities e where e.dataset = @dataset and e.status = 'provisional' and e.merged_into is null
              and not exists (select 1 from decisions d where d.entity_id = e.id and d.valid)
              and not exists (select 1 from entities m where m.merged_into = e.id)
              and not exists (select 1 from entity_equivalents q where q.entity_id = e.id)
              and not exists (select 1 from decisions d where d.entity_id = e.id)
            """);
        Add(cmd, "dataset", dataset);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---- source strings --------------------------------------------------------------------------------------------

    private const string StringColumns = "s.id, s.dataset, s.context, s.raw, s.normalised, s.parsed::text, s.first_seen, s.last_seen, s.occurrences";

    public async Task<SourceString> CollectAsync(string dataset, string context, string raw, string? parsedJson = null, CancellationToken ct = default)
    {
        var normalised = StringKey.Normalise(raw);
        if (normalised.Length == 0) throw new ArgumentException("An empty string cannot be collected.", nameof(raw));
        await using var cmd = _db.CreateCommand(
            $"""
            insert into source_strings as s (dataset, context, raw, normalised, parsed, first_seen, last_seen, occurrences)
            values (@dataset, @context, @raw, @normalised, @parsed, @now, @now, 1)
            on conflict (dataset, context, normalised) do update set last_seen = @now, occurrences = s.occurrences + 1, parsed = coalesce(excluded.parsed, s.parsed)
            returning {StringColumns}
            """);
        Add(cmd, "dataset", dataset); Add(cmd, "context", context); Add(cmd, "raw", raw.Trim()); Add(cmd, "normalised", normalised); AddJson(cmd, "parsed", parsedJson); Add(cmd, "now", DateTimeOffset.UtcNow);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return ReadString(r);
    }

    public async Task<SourceString?> GetSourceStringAsync(long id, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {StringColumns} from source_strings s where s.id = @id");
        Add(cmd, "id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadString(r) : null;
    }

    public async Task<SourceString?> FindSourceStringAsync(string dataset, string context, string raw, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {StringColumns} from source_strings s where s.dataset = @dataset and s.context = @context and s.normalised = @normalised");
        Add(cmd, "dataset", dataset); Add(cmd, "context", context); Add(cmd, "normalised", StringKey.Normalise(raw));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadString(r) : null;
    }

    public async Task<IReadOnlyList<SourceString>> UndecidedAsync(string dataset, int limit = 1000, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {StringColumns} from source_strings s where s.dataset = @dataset and not exists (select 1 from decisions d where d.source_string_id = s.id and d.valid) order by s.first_seen, s.id limit @limit");
        Add(cmd, "dataset", dataset); Add(cmd, "limit", limit);
        var list = new List<SourceString>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadString(r));
        return list;
    }

    private static SourceString ReadString(NpgsqlDataReader r) =>
        new(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), Str(r, 5), r.GetFieldValue<DateTimeOffset>(6), r.GetFieldValue<DateTimeOffset>(7), r.GetInt32(8));

    // ---- decisions -------------------------------------------------------------------------------------------------

    private const string DecisionColumns = "d.id, d.source_string_id, d.entity_id, d.method, d.evidence::text, d.reviewer, d.decided_at, d.dataset_version, d.run_id, d.valid, d.supersedes";

    public async Task<Decision> DecideAsync(long sourceStringId, string? entityId, string method, string? evidenceJson = null, string? reviewer = null, string? datasetVersion = null, string? runId = null, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        // the string row is the lock: two deciders for one string take turns, and the unique index on valid decisions is the backstop
        await using (var cmd = new NpgsqlCommand("select 1 from source_strings where id = @s for update", conn, tx))
        {
            Add(cmd, "s", sourceStringId);
            if (await cmd.ExecuteScalarAsync(ct) is null) throw new KeyNotFoundException($"No source string {sourceStringId}.");
        }
        long? superseded;
        await using (var cmd = new NpgsqlCommand("update decisions set valid = false where source_string_id = @s and valid returning id", conn, tx))
        {
            Add(cmd, "s", sourceStringId);
            superseded = (long?)await cmd.ExecuteScalarAsync(ct);
        }
        long id;
        await using (var cmd = new NpgsqlCommand(
            """
            insert into decisions (source_string_id, entity_id, method, evidence, reviewer, decided_at, dataset_version, run_id, valid, supersedes)
            values (@s, @entity, @method, @evidence, @reviewer, @now, @version, @run, true, @supersedes) returning id
            """, conn, tx))
        {
            Add(cmd, "s", sourceStringId); Add(cmd, "entity", entityId); Add(cmd, "method", method); AddJson(cmd, "evidence", evidenceJson); Add(cmd, "reviewer", reviewer);
            Add(cmd, "now", DateTimeOffset.UtcNow); Add(cmd, "version", datasetVersion); Add(cmd, "run", runId); Add(cmd, "supersedes", superseded);
            id = (long)(await cmd.ExecuteScalarAsync(ct))!;
        }
        await tx.CommitAsync(ct);
        return (await GetDecisionAsync(id, ct))!;
    }

    private async Task<Decision?> GetDecisionAsync(long id, CancellationToken ct)
    {
        await using var cmd = _db.CreateCommand($"select {DecisionColumns} from decisions d where d.id = @id");
        Add(cmd, "id", id);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadDecision(r) : null;
    }

    public async Task<bool> HasValidDecisionAsync(string entityId, string method, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select exists (select 1 from decisions where entity_id = @e and method = @m and valid)");
        Add(cmd, "e", entityId); Add(cmd, "m", method);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<Decision?> DecisionForAsync(long sourceStringId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {DecisionColumns} from decisions d where d.source_string_id = @s and d.valid order by d.decided_at desc limit 1");
        Add(cmd, "s", sourceStringId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadDecision(r) : null;
    }

    public async Task<Entity?> ResolveAsync(string dataset, string context, string raw, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            """
            select d.entity_id from source_strings s join decisions d on d.source_string_id = s.id and d.valid
            where s.dataset = @dataset and s.context = @context and s.normalised = @normalised order by d.decided_at desc limit 1
            """);
        Add(cmd, "dataset", dataset); Add(cmd, "context", context); Add(cmd, "normalised", StringKey.Normalise(raw));
        var entityId = await cmd.ExecuteScalarAsync(ct) as string;
        return entityId is null ? null : await ResolveEntityAsync(entityId, ct);
    }

    public async Task<int> InvalidateDecisionsForEntityAsync(string entityId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("update decisions set valid = false where entity_id = @e and valid");
        Add(cmd, "e", entityId);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<DecidedString>> DecidedStringsAsync(string dataset, string? method = null, string? context = null, long afterStringId = 0, int limit = 500, bool sample = false, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            $"select s.id, s.dataset, s.context, s.raw, s.normalised, s.parsed::text, s.first_seen, s.last_seen, s.occurrences, {DecisionColumns} " +
            "from source_strings s join decisions d on d.source_string_id = s.id and d.valid and d.entity_id is not null where s.dataset = @dataset and s.id > @after"
            + (method is null ? "" : " and d.method = @method") + (context is null ? "" : " and s.context = @context")
            + (sample ? " order by random()" : " order by s.id") + " limit @limit");
        Add(cmd, "dataset", dataset); Add(cmd, "after", afterStringId); if (method is not null) Add(cmd, "method", method); if (context is not null) Add(cmd, "context", context); Add(cmd, "limit", limit);
        var list = new List<DecidedString>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var s = ReadString(r);
            var d = new Decision(r.GetInt64(9), r.GetInt64(10), Str(r, 11), r.GetString(12), Str(r, 13), Str(r, 14), r.GetFieldValue<DateTimeOffset>(15), Str(r, 16), Str(r, 17), r.GetBoolean(18), r.IsDBNull(19) ? null : r.GetInt64(19));
            list.Add(new DecidedString(s, d));
        }
        return list;
    }

    public async Task<IReadOnlyList<DecidedString>> DecisionsForRunAsync(string runId, string? method = null, int limit = 50, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            $"select s.id, s.dataset, s.context, s.raw, s.normalised, s.parsed::text, s.first_seen, s.last_seen, s.occurrences, {DecisionColumns} " +
            "from decisions d join source_strings s on s.id = d.source_string_id where d.run_id = @run and d.valid" + (method is null ? "" : " and d.method = @method") + " order by d.id limit @limit");
        Add(cmd, "run", runId); if (method is not null) Add(cmd, "method", method); Add(cmd, "limit", limit);
        var list = new List<DecidedString>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var s = ReadString(r);
            var d = new Decision(r.GetInt64(9), r.GetInt64(10), Str(r, 11), r.GetString(12), Str(r, 13), Str(r, 14), r.GetFieldValue<DateTimeOffset>(15), Str(r, 16), Str(r, 17), r.GetBoolean(18), r.IsDBNull(19) ? null : r.GetInt64(19));
            list.Add(new DecidedString(s, d));
        }
        return list;
    }

    public async Task<UndoResult> UndoRunAsync(string runId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        int decisions, entities;
        await using (var cmd = new NpgsqlCommand("update decisions set valid = false where run_id = @run and valid", conn, tx))
        {
            cmd.Parameters.AddWithValue("run", runId);
            decisions = await cmd.ExecuteNonQueryAsync(ct);
        }
        // the equivalents the run added to any entity go; an entity the run left aligned with nothing behind it is provisional again
        await using (var cmd = new NpgsqlCommand(
            """
            create temp table undo_touched on commit drop as select distinct entity_id from entity_equivalents where run_id = @run;
            delete from entity_equivalents where run_id = @run;
            update entities e set status = 'provisional', updated_at = now()
              where e.id in (select entity_id from undo_touched) and e.status = 'aligned'
                and not exists (select 1 from entity_equivalents q where q.entity_id = e.id)
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("run", runId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        // entities the run minted (provisional or voted) that nothing points at any more; the run's own invalidated decisions let go of them first
        await using (var cmd = new NpgsqlCommand(
            """
            create temp table undo_entities on commit drop as
              select e.id from entities e where e.run_id = @run and e.merged_into is null
                and not exists (select 1 from decisions d where d.entity_id = e.id and d.valid)
                and not exists (select 1 from entities m where m.merged_into = e.id)
                and not exists (select 1 from entity_equivalents q where q.entity_id = e.id);
            update decisions set entity_id = null where not valid and entity_id in (select id from undo_entities)
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("run", runId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand("delete from entities where id in (select id from undo_entities)", conn, tx))
            entities = await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = new NpgsqlCommand("update runs set report = coalesce(report, '') || ' [undone]' where id = @run", conn, tx))
        {
            cmd.Parameters.AddWithValue("run", runId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new UndoResult(decisions, entities);
    }

    public async Task<IReadOnlyList<ContextMethodCount>> RunBreakdownAsync(string runId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select s.context, d.method, count(*) from decisions d join source_strings s on s.id = d.source_string_id where d.run_id = @run and d.valid group by s.context, d.method order by s.context, d.method");
        Add(cmd, "run", runId);
        var list = new List<ContextMethodCount>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new ContextMethodCount(r.GetString(0), r.GetString(1), r.GetInt64(2)));
        return list;
    }

    private static Decision ReadDecision(NpgsqlDataReader r) =>
        new(r.GetInt64(0), r.GetInt64(1), Str(r, 2), r.GetString(3), Str(r, 4), Str(r, 5), r.GetFieldValue<DateTimeOffset>(6), Str(r, 7), Str(r, 8), r.GetBoolean(9), r.IsDBNull(10) ? null : r.GetInt64(10));

    // ---- overrides -------------------------------------------------------------------------------------------------

    public async Task PutOverrideAsync(Override item, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            """
            insert into overrides (dataset, context, normalised, target, source, note) values (@dataset, @context, @normalised, @target, @source, @note)
            on conflict (dataset, context, normalised) do update set target = excluded.target, source = excluded.source, note = excluded.note
            """);
        Add(cmd, "dataset", item.Dataset); Add(cmd, "context", item.Context ?? ""); Add(cmd, "normalised", item.Normalised); Add(cmd, "target", item.Target); Add(cmd, "source", item.Source); Add(cmd, "note", item.Note);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Override>> OverridesAsync(string dataset, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select dataset, context, normalised, target, source, note from overrides where dataset = @dataset order by normalised");
        Add(cmd, "dataset", dataset);
        var list = new List<Override>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) { var c = r.GetString(1); list.Add(new Override(r.GetString(0), c.Length == 0 ? null : c, r.GetString(2), r.GetString(3), r.GetString(4), Str(r, 5))); }
        return list;
    }

    public async Task<bool> RemoveOverrideAsync(string dataset, string? context, string normalised, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("delete from overrides where dataset = @dataset and context = @context and normalised = @normalised");
        Add(cmd, "dataset", dataset); Add(cmd, "context", context ?? ""); Add(cmd, "normalised", normalised);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<Override?> OverrideForAsync(string dataset, string context, string normalised, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select dataset, context, normalised, target, source, note from overrides where dataset = @dataset and normalised = @normalised and context in (@context, '') order by context desc limit 1");
        Add(cmd, "dataset", dataset); Add(cmd, "normalised", normalised); Add(cmd, "context", context);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var storedContext = r.GetString(1);
        return new Override(r.GetString(0), storedContext.Length == 0 ? null : storedContext, r.GetString(2), r.GetString(3), r.GetString(4), Str(r, 5));
    }

    // ---- authority records -----------------------------------------------------------------------------------------

    private const string AuthorityColumns = "a.uri, a.source, a.label, a.labels, a.type, a.begin_earliest, a.begin_latest, a.end_earliest, a.end_latest, a.lifespan_text, a.same_as, a.data::text, a.fetched_at, a.source_version";

    public async Task<AuthorityRecord?> GetAuthorityAsync(string uri, CancellationToken ct = default)
    {
        var list = await QueryAuthoritiesAsync($"select {AuthorityColumns} from authority_records a where a.uri = @uri", cmd => Add(cmd, "uri", uri), ct);
        return list.SingleOrDefault();
    }

    public async Task PutAuthorityAsync(AuthorityRecord record, CancellationToken ct = default)
    {
        var labels = record.Labels.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.Ordinal).ToArray();
        await using var cmd = _db.CreateCommand(
            """
            insert into authority_records (uri, source, label, labels, normalised_labels, type, begin_earliest, begin_latest, end_earliest, end_latest, lifespan_text, same_as, data, fetched_at, source_version)
            values (@uri, @source, @label, @labels, @normalised, @type, @b1, @b2, @e1, @e2, @ltext, @sameas, @data, @fetched, @version)
            on conflict (uri) do update set source = excluded.source, label = excluded.label, labels = excluded.labels, normalised_labels = excluded.normalised_labels, type = excluded.type,
                begin_earliest = excluded.begin_earliest, begin_latest = excluded.begin_latest, end_earliest = excluded.end_earliest, end_latest = excluded.end_latest, lifespan_text = excluded.lifespan_text,
                same_as = excluded.same_as, data = excluded.data, fetched_at = excluded.fetched_at, source_version = excluded.source_version
            """);
        Add(cmd, "uri", record.Uri); Add(cmd, "source", record.Source); Add(cmd, "label", record.Label); Add(cmd, "labels", labels); Add(cmd, "normalised", NormalisedLabels(record.Label, labels));
        Add(cmd, "type", record.Type); AddLifespan(cmd, record.Lifespan); Add(cmd, "sameas", record.SameAs.ToArray()); AddJson(cmd, "data", record.DataJson);
        Add(cmd, "fetched", record.FetchedAt.ToUniversalTime()); Add(cmd, "version", record.SourceVersion);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesByLabelAsync(string normalisedLabel, string? source = null, CancellationToken ct = default) =>
        QueryAuthoritiesAsync($"select {AuthorityColumns} from authority_records a where a.normalised_labels @> @key" + (source is null ? "" : " and a.source = @source") + " order by a.uri",
            cmd => { Add(cmd, "key", new[] { normalisedLabel }); if (source is not null) Add(cmd, "source", source); }, ct);

    public Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesByLabelsAsync(IReadOnlyCollection<string> keys, string? source = null, string? type = null, int limit = 50, CancellationToken ct = default)
    {
        if (keys.Count == 0) return Task.FromResult<IReadOnlyList<AuthorityRecord>>([]);
        // the limit is applied before the ordering so the planner takes the label index rather than walking the primary key in order
        var sql = $"select {AuthorityColumns} from (select * from authority_records x where x.normalised_labels && @keys"
            + (source is null ? "" : " and x.source = @source") + (type is null ? "" : " and x.type = @type") + " limit @limit) a order by a.uri";
        return QueryAuthoritiesAsync(sql, cmd => { Add(cmd, "keys", keys.ToArray()); if (source is not null) Add(cmd, "source", source); if (type is not null) Add(cmd, "type", type); Add(cmd, "limit", limit); }, ct);
    }

    public Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesBySameAsAsync(string uri, CancellationToken ct = default) =>
        QueryAuthoritiesAsync($"select {AuthorityColumns} from authority_records a where a.same_as @> @uri order by a.uri", cmd => Add(cmd, "uri", new[] { uri }), ct);

    public async Task<int> PutAuthoritiesAsync(IAsyncEnumerable<AuthorityRecord> records, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        // the merge of millions of rows and its index maintenance want memory; a local setting for this session only
        await using (var tune = new NpgsqlCommand("set work_mem = '512MB'; set maintenance_work_mem = '1GB'", conn)) await tune.ExecuteNonQueryAsync(ct);
        await using (var create = new NpgsqlCommand("drop table if exists authority_records_in; create temp table authority_records_in (like authority_records including defaults)", conn))
            await create.ExecuteNonQueryAsync(ct);
        var count = 0;
        await using (var writer = await conn.BeginBinaryImportAsync("copy authority_records_in (uri, source, label, labels, normalised_labels, type, begin_earliest, begin_latest, end_earliest, end_latest, lifespan_text, same_as, data, fetched_at, source_version) from stdin (format binary)", ct))
        {
            await foreach (var record in records.WithCancellation(ct))
            {
                var labels = record.Labels.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.Ordinal).ToArray();
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(record.Uri, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(record.Source, NpgsqlDbType.Text, ct);
                await WriteText(writer, record.Label, ct);
                await writer.WriteAsync(labels, NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                await writer.WriteAsync(NormalisedLabels(record.Label, labels), NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                await WriteText(writer, record.Type, ct);
                await WriteTime(writer, record.Lifespan?.BeginEarliest, ct); await WriteTime(writer, record.Lifespan?.BeginLatest, ct);
                await WriteTime(writer, record.Lifespan?.EndEarliest, ct); await WriteTime(writer, record.Lifespan?.EndLatest, ct);
                await WriteText(writer, record.Lifespan?.Text, ct);
                await writer.WriteAsync(record.SameAs.ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                if (record.DataJson is null) await writer.WriteNullAsync(ct); else await writer.WriteAsync(record.DataJson, NpgsqlDbType.Jsonb, ct);
                await writer.WriteAsync(record.FetchedAt.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);
                await WriteText(writer, record.SourceVersion, ct);
                count++;
            }
            await writer.CompleteAsync(ct);
        }
        // A large batch rebuilds the two GIN indexes after the insert instead of maintaining them row by row, which
        // is several times cheaper (the per-row path is bound by writing the transaction log). All in one implicit
        // transaction, so a failure rolls the drops back; the table is locked against readers while it runs.
        var rebuild = count >= RebuildIndexesAbove;
        await using (var merge = new NpgsqlCommand(
            (rebuild ? "drop index if exists authority_records_source_labels, authority_records_same_as;" + " " : "")
            + """
            insert into authority_records (uri, source, label, labels, normalised_labels, type, begin_earliest, begin_latest, end_earliest, end_latest, lifespan_text, same_as, data, fetched_at, source_version)
            select distinct on (uri) uri, source, label, labels, normalised_labels, type, begin_earliest, begin_latest, end_earliest, end_latest, lifespan_text, same_as, data, fetched_at, source_version from authority_records_in order by uri
            on conflict (uri) do update set source = excluded.source, label = excluded.label, labels = excluded.labels, normalised_labels = excluded.normalised_labels, type = excluded.type,
                begin_earliest = excluded.begin_earliest, begin_latest = excluded.begin_latest, end_earliest = excluded.end_earliest, end_latest = excluded.end_latest, lifespan_text = excluded.lifespan_text,
                same_as = excluded.same_as, data = excluded.data, fetched_at = excluded.fetched_at, source_version = excluded.source_version;
            drop table authority_records_in;
            """
            + (rebuild ? "create index authority_records_source_labels on authority_records using gin (source, type, normalised_labels); create index authority_records_same_as on authority_records using gin (same_as);" + " " : "")
            + "analyze authority_records", conn))
        {
            merge.CommandTimeout = 0;
            await merge.ExecuteNonQueryAsync(ct);
        }
        if (rebuild)
        {
            // a large load leaves dead row versions (replaced records) and a full GIN pending list; autovacuum would
            // get to them throttled over an hour or more, slowing every lookup meanwhile, so vacuum now, unthrottled.
            // Must run outside the transaction above, so a separate command.
            await using var vacuum = new NpgsqlCommand("vacuum (analyze) authority_records", conn) { CommandTimeout = 0 };
            await vacuum.ExecuteNonQueryAsync(ct);
        }
        return count;
    }

    /// <summary>Batches of at least this many records drop and rebuild the label indexes around the insert rather than maintaining them row by row. Settable for tests.</summary>
    internal int RebuildIndexesAbove { get; set; } = 500_000;

    private static Task WriteText(NpgsqlBinaryImporter w, string? value, CancellationToken ct) => value is null ? w.WriteNullAsync(ct) : w.WriteAsync(value, NpgsqlDbType.Text, ct);
    private static Task WriteTime(NpgsqlBinaryImporter w, DateTimeOffset? value, CancellationToken ct) => value is null ? w.WriteNullAsync(ct) : w.WriteAsync(value.Value.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);

    private async Task<IReadOnlyList<AuthorityRecord>> QueryAuthoritiesAsync(string sql, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        var list = new List<AuthorityRecord>();
        await using var cmd = _db.CreateCommand(sql);
        bind(cmd);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new AuthorityRecord(r.GetString(0), r.GetString(1), Str(r, 2), r.GetFieldValue<string[]>(3), Str(r, 4), ReadLifespan(r, 5), r.GetFieldValue<string[]>(10), Str(r, 11), r.GetFieldValue<DateTimeOffset>(12), Str(r, 13)));
        return list;
    }

    // ---- runs ------------------------------------------------------------------------------------------------------

    private const string RunColumns = "r.id, r.dataset, r.manifest::text, r.dumps::text, r.started_at, r.finished_at, r.git_sha, r.counts::text, r.report";

    public async Task<Run> StartRunAsync(string dataset, string? manifestJson = null, string? dumpsJson = null, string? gitSha = null, CancellationToken ct = default)
    {
        var id = $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{IdMinter.Generate(6)}";
        await using var cmd = _db.CreateCommand("insert into runs (id, dataset, manifest, dumps, started_at, git_sha) values (@id, @dataset, @manifest, @dumps, @now, @sha)");
        Add(cmd, "id", id); Add(cmd, "dataset", dataset); AddJson(cmd, "manifest", manifestJson); AddJson(cmd, "dumps", dumpsJson); Add(cmd, "now", DateTimeOffset.UtcNow); Add(cmd, "sha", gitSha);
        await cmd.ExecuteNonQueryAsync(ct);
        return (await GetRunAsync(id, ct))!;
    }

    public async Task FinishRunAsync(string runId, string? countsJson, string? report, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("update runs set finished_at = @now, counts = @counts, report = @report where id = @id");
        Add(cmd, "id", runId); Add(cmd, "now", DateTimeOffset.UtcNow); AddJson(cmd, "counts", countsJson); Add(cmd, "report", report);
        if (await cmd.ExecuteNonQueryAsync(ct) == 0) throw new KeyNotFoundException($"No run {runId}.");
    }

    public async Task<Run?> GetRunAsync(string runId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {RunColumns} from runs r where r.id = @id");
        Add(cmd, "id", runId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadRun(r) : null;
    }

    public async Task<IReadOnlyList<Run>> RunsAsync(string dataset, int limit = 20, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {RunColumns} from runs r where r.dataset = @dataset order by r.started_at desc limit @limit");
        Add(cmd, "dataset", dataset); Add(cmd, "limit", limit);
        var list = new List<Run>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadRun(r));
        return list;
    }

    private static Run ReadRun(NpgsqlDataReader r) =>
        new(r.GetString(0), r.GetString(1), Str(r, 2), Str(r, 3), r.GetFieldValue<DateTimeOffset>(4), r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5), Str(r, 6), Str(r, 7), Str(r, 8));

    // ---- projects, users, memberships, notes -------------------------------------------------------------------------

    public async Task UpsertProjectAsync(Project project, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            """
            insert into projects (dataset, name, description, identifier_base, manifest, created_at, updated_at) values (@dataset, @name, @description, @base, @manifest, @now, @now)
            on conflict (dataset) do update set name = excluded.name, description = excluded.description, identifier_base = excluded.identifier_base, manifest = excluded.manifest, updated_at = excluded.updated_at
            """);
        Add(cmd, "dataset", project.Dataset); Add(cmd, "name", project.Name); Add(cmd, "description", project.Description); Add(cmd, "base", project.IdentifierBase); AddJson(cmd, "manifest", project.ManifestJson); Add(cmd, "now", DateTimeOffset.UtcNow);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private const string ProjectColumns = "p.dataset, p.name, p.description, p.identifier_base, p.manifest::text, p.created_at, p.updated_at";
    private static Project ReadProject(NpgsqlDataReader r) => new(r.GetString(0), r.GetString(1), Str(r, 2), r.GetString(3), Str(r, 4), r.GetFieldValue<DateTimeOffset>(5), r.GetFieldValue<DateTimeOffset>(6));

    public async Task<Project?> GetProjectAsync(string dataset, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {ProjectColumns} from projects p where p.dataset = @dataset");
        Add(cmd, "dataset", dataset);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? ReadProject(r) : null;
    }

    public async Task<IReadOnlyList<Project>> ListProjectsAsync(CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {ProjectColumns} from projects p order by p.name");
        var list = new List<Project>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadProject(r));
        return list;
    }

    public async Task UpsertUserAsync(string login, string? displayName, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("insert into users (login, display_name, last_seen) values (@login, @name, @now) on conflict (login) do update set display_name = coalesce(excluded.display_name, users.display_name), last_seen = excluded.last_seen");
        Add(cmd, "login", login); Add(cmd, "name", displayName); Add(cmd, "now", DateTimeOffset.UtcNow);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<User?> GetUserAsync(string login, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select login, display_name, created_at, last_seen from users where login = @login");
        Add(cmd, "login", login);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? new User(r.GetString(0), Str(r, 1), r.GetFieldValue<DateTimeOffset>(2), r.IsDBNull(3) ? null : r.GetFieldValue<DateTimeOffset>(3)) : null;
    }

    public async Task SetMembershipAsync(string dataset, string login, string role, CancellationToken ct = default)
    {
        if (!ProjectRole.All.Contains(role)) throw new ArgumentException($"Unknown role '{role}'.", nameof(role));
        await using var cmd = _db.CreateCommand("insert into memberships (dataset, login, role) values (@dataset, @login, @role) on conflict (dataset, login) do update set role = excluded.role");
        Add(cmd, "dataset", dataset); Add(cmd, "login", login); Add(cmd, "role", role);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveMembershipAsync(string dataset, string login, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("delete from memberships where dataset = @dataset and login = @login");
        Add(cmd, "dataset", dataset); Add(cmd, "login", login);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<IReadOnlyList<Membership>> MembershipsWhereAsync(string where, Action<NpgsqlCommand> bind, CancellationToken ct)
    {
        await using var cmd = _db.CreateCommand($"select dataset, login, role, added_at from memberships where {where} order by dataset, login");
        bind(cmd);
        var list = new List<Membership>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new Membership(r.GetString(0), r.GetString(1), r.GetString(2), r.GetFieldValue<DateTimeOffset>(3)));
        return list;
    }

    public Task<IReadOnlyList<Membership>> MembershipsAsync(string dataset, CancellationToken ct = default) => MembershipsWhereAsync("dataset = @dataset", cmd => Add(cmd, "dataset", dataset), ct);
    public Task<IReadOnlyList<Membership>> MembershipsForAsync(string login, CancellationToken ct = default) => MembershipsWhereAsync("login = @login", cmd => Add(cmd, "login", login), ct);

    public async Task<Note> AddNoteAsync(string dataset, string target, string? author, string text, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("insert into notes (dataset, target, author, text) values (@dataset, @target, @author, @text) returning id, created_at");
        Add(cmd, "dataset", dataset); Add(cmd, "target", target); Add(cmd, "author", author); Add(cmd, "text", text);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new Note(r.GetInt64(0), dataset, target, author, text, r.GetFieldValue<DateTimeOffset>(1));
    }

    public async Task<IReadOnlyList<Note>> NotesAsync(string dataset, string target, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select id, dataset, target, author, text, created_at from notes where dataset = @dataset and target = @target order by created_at");
        Add(cmd, "dataset", dataset); Add(cmd, "target", target);
        var list = new List<Note>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new Note(r.GetInt64(0), r.GetString(1), r.GetString(2), Str(r, 3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5)));
        return list;
    }

    public async Task<DecidedString?> DecidedStringAsync(long stringId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            $"select s.id, s.dataset, s.context, s.raw, s.normalised, s.parsed::text, s.first_seen, s.last_seen, s.occurrences, {DecisionColumns} " +
            "from source_strings s join decisions d on d.source_string_id = s.id and d.valid where s.id = @id order by d.decided_at desc limit 1");
        Add(cmd, "id", stringId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        var s = ReadString(r);
        var d = new Decision(r.GetInt64(9), r.GetInt64(10), Str(r, 11), r.GetString(12), Str(r, 13), Str(r, 14), r.GetFieldValue<DateTimeOffset>(15), Str(r, 16), Str(r, 17), r.GetBoolean(18), r.IsDBNull(19) ? null : r.GetInt64(19));
        return new DecidedString(s, d);
    }

    public async Task<IReadOnlyList<ContextMethodCount>> QueueCountsAsync(string dataset, string method, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select s.context, d.method, count(*) from source_strings s join decisions d on d.source_string_id = s.id and d.valid where s.dataset = @dataset and d.method = @method group by s.context, d.method order by s.context");
        Add(cmd, "dataset", dataset); Add(cmd, "method", method);
        var list = new List<ContextMethodCount>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new ContextMethodCount(r.GetString(0), r.GetString(1), r.GetInt64(2)));
        return list;
    }

    public async Task<Decision?> RevertDecisionAsync(long stringId, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var cmd = new NpgsqlCommand("select 1 from source_strings where id = @s for update", conn, tx)) { cmd.Parameters.AddWithValue("s", stringId); if (await cmd.ExecuteScalarAsync(ct) is null) return null; }
        long? latest = null, supersedes = null;
        await using (var cmd = new NpgsqlCommand("select id, supersedes from decisions where source_string_id = @s and valid order by id desc limit 1", conn, tx))
        {
            cmd.Parameters.AddWithValue("s", stringId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) { latest = r.GetInt64(0); supersedes = r.IsDBNull(1) ? null : r.GetInt64(1); }
        }
        if (latest is null) return null;
        await using (var cmd = new NpgsqlCommand("update decisions set valid = false where id = @id", conn, tx)) { cmd.Parameters.AddWithValue("id", latest.Value); await cmd.ExecuteNonQueryAsync(ct); }
        // exactly the decision this one replaced comes back; a decision made before supersedes was recorded falls back to the one before it
        await using (var cmd = new NpgsqlCommand(supersedes is null
            ? "update decisions set valid = true where id = (select id from decisions where source_string_id = @s and id < @latest and entity_id is not null order by id desc limit 1)"
            : "update decisions set valid = true where id = @supersedes and source_string_id = @s", conn, tx))
        {
            cmd.Parameters.AddWithValue("s", stringId); cmd.Parameters.AddWithValue("latest", latest.Value); cmd.Parameters.AddWithValue("supersedes", (object?)supersedes ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return await DecisionForAsync(stringId, ct);
    }

    // ---- overview --------------------------------------------------------------------------------------------------

    public async Task<StoreCounts> CountsAsync(string? dataset = null, CancellationToken ct = default)
    {
        var where = dataset is null ? "" : " where dataset = @dataset";
        await using var cmd = _db.CreateCommand(
            $"""
            select
              (select count(*) from entities{where}),
              (select count(*) from source_strings{where}),
              (select count(*) from source_strings s where exists (select 1 from decisions d where d.source_string_id = s.id and d.valid and d.entity_id is not null){(dataset is null ? "" : " and s.dataset = @dataset")}),
              (select count(*) from source_strings s where not exists (select 1 from decisions d where d.source_string_id = s.id and d.valid and d.entity_id is not null){(dataset is null ? "" : " and s.dataset = @dataset")}),
              (select count(*) from overrides{where}),
              (select count(*) from authority_records),
              (select count(*) from runs{where})
            """);
        if (dataset is not null) Add(cmd, "dataset", dataset);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return new StoreCounts(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5), r.GetInt64(6));
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------

    private static void Add(NpgsqlCommand cmd, string name, object? value) => cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static void AddJson(NpgsqlCommand cmd, string name, string? json) =>
        cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Jsonb) { Value = json is null ? DBNull.Value : json });

    private static void AddLifespan(NpgsqlCommand cmd, Lifespan? l)
    {
        Add(cmd, "b1", l?.BeginEarliest?.ToUniversalTime()); Add(cmd, "b2", l?.BeginLatest?.ToUniversalTime());
        Add(cmd, "e1", l?.EndEarliest?.ToUniversalTime()); Add(cmd, "e2", l?.EndLatest?.ToUniversalTime());
        Add(cmd, "ltext", l?.Text);
    }

    private static Lifespan? ReadLifespan(NpgsqlDataReader r, int first)
    {
        DateTimeOffset? At(int i) => r.IsDBNull(i) ? null : r.GetFieldValue<DateTimeOffset>(i);
        var l = new Lifespan(At(first), At(first + 1), At(first + 2), At(first + 3), Str(r, first + 4));
        return l.IsEmpty && l.Text is null ? null : l;
    }

    private static string? Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private static string Status(EntityStatus s) => s.ToString().ToLowerInvariant();

    public async Task<IReadOnlyList<EntityCount>> EntityCountsAsync(string dataset, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select status, class, count(*) from entities where dataset = @dataset and merged_into is null group by status, class");
        Add(cmd, "dataset", dataset);
        var list = new List<EntityCount>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new EntityCount(Enum.Parse<EntityStatus>(r.GetString(0), true), Enum.Parse<EntityClass>(r.GetString(1), true), r.GetInt64(2)));
        return list;
    }

    /// <summary>The lookup keys for a set of labels (<see cref="StringKey.Variants"/>): normalised, folded, bare and canonical forms, so "Dürer" is found by "Durer" and "Thos. Agnew &amp; Sons Ltd" by "Thomas Agnew &amp; Sons".</summary>
    private static string[] NormalisedLabels(string? label, IEnumerable<string> labels) =>
        labels.Prepend(label ?? "").SelectMany(StringKey.Variants).Distinct(StringComparer.Ordinal).ToArray();
}
