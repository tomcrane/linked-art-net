using System.Diagnostics;
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

    static PostgresAlignmentStore()
    {
        // Npgsql writes DateTime.MinValue and MaxValue as -infinity and infinity unless told not to; the first day of year 1
        // is MinValue exactly, and LoC has people born "approximately 1" (n82239358, Scribonius Largus). Set before any
        // data source is created by this process; the test fixture and the hosts construct the store first.
        AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
    }

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
        await using var cmd = _db.CreateCommand("drop table if exists decisions, entity_equivalents, overrides, source_strings, authority_records, authority_sources, runs, entities, schema_version, getty_staging, triple_staging, memberships, notes, projects, users cascade; do $$ declare t text; begin for t in select tablename from pg_tables where schemaname = current_schema() and tablename like 'triple_staging_%' loop execute 'drop table if exists ' || quote_ident(t); end loop; end $$");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---- entities --------------------------------------------------------------------------------------------------

    private const string EntityColumns = "e.id, e.dataset, e.class, e.label, e.labels, e.begin_earliest, e.begin_latest, e.end_earliest, e.end_latest, e.lifespan_text, e.nationalities, e.roles, e.status, e.merged_into, e.created_at, e.updated_at, e.run_id, e.set_aside_labels";

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

    public async Task<IReadOnlySet<string>> CompositeEntityIdsAsync(IReadOnlyCollection<string> entityIds, CancellationToken ct = default)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (entityIds.Count == 0) return found;
        await using var cmd = _db.CreateCommand("select distinct entity_id from decisions where valid and method = 'composite' and entity_id = any(@ids)");
        Add(cmd, "ids", entityIds.ToArray());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) found.Add(r.GetString(0));
        return found;
    }

    public async Task<bool> SetLabelAsideAsync(string entityId, string name, bool aside, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        string? label; string[] labels; string[] set;
        await using (var cmd = new NpgsqlCommand("select label, labels, set_aside_labels from entities where id = @id for update", conn, tx))
        {
            Add(cmd, "id", entityId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw new KeyNotFoundException($"No entity {entityId}.");
            label = Str(r, 0); labels = r.GetFieldValue<string[]>(1); set = r.GetFieldValue<string[]>(2);
        }
        if (aside)
        {
            if (name == label) throw new InvalidOperationException("The entity's own label cannot be set aside: change the label first.");
            if (!labels.Contains(name, StringComparer.Ordinal) || set.Contains(name, StringComparer.Ordinal)) return false;
            labels = labels.Where(l => l != name).ToArray(); set = [.. set, name];
        }
        else
        {
            if (!set.Contains(name, StringComparer.Ordinal)) return false;
            set = set.Where(l => l != name).ToArray(); if (name != label && !labels.Contains(name, StringComparer.Ordinal)) labels = [.. labels, name];
        }
        await using (var cmd = new NpgsqlCommand("update entities set labels = @labels, normalised_labels = @normalised, set_aside_labels = @set, updated_at = @now where id = @id", conn, tx))
        {
            Add(cmd, "id", entityId); Add(cmd, "labels", labels); Add(cmd, "normalised", NormalisedLabels(label, labels)); Add(cmd, "set", set); Add(cmd, "now", DateTimeOffset.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task UpdateEntityAsync(Entity entity, CancellationToken ct = default)
    {
        // a name set aside stays out, whoever holds the entity and however old their copy of it (schema version 12)
        string[] aside = [];
        await using (var read = _db.CreateCommand("select set_aside_labels from entities where id = @id"))
        {
            Add(read, "id", entity.Id);
            if (await read.ExecuteScalarAsync(ct) is string[] found) aside = found;
        }
        var labels = entity.Labels.Where(l => !string.IsNullOrWhiteSpace(l) && !aside.Contains(l, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
        await using var cmd = _db.CreateCommand(
            """
            update entities set class = @class, label = @label, labels = @labels, normalised_labels = @normalised, begin_earliest = @b1, begin_latest = @b2, end_earliest = @e1, end_latest = @e2,
                lifespan_text = @ltext, nationalities = @nat, roles = @roles, status = @status, updated_at = @now
            where id = @id
            """);
        Add(cmd, "id", entity.Id); Add(cmd, "class", entity.Class.ToString()); Add(cmd, "label", entity.Label); Add(cmd, "labels", labels); Add(cmd, "normalised", NormalisedLabels(entity.Label, labels));
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
        // and a name set aside on either stays aside on the survivor
        string? loserLabel = null; string[] loserLabels = []; string? survivorLabel = null; string[] survivorLabels = []; var setAside = new List<string>();
        await using (var cmd = new NpgsqlCommand("select id, label, labels, set_aside_labels from entities where id in (@loser, @survivor)", conn, tx))
        {
            Add(cmd, "loser", loserId); Add(cmd, "survivor", survivorId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (r.GetString(0) == loserId) { loserLabel = Str(r, 1); loserLabels = r.GetFieldValue<string[]>(2); }
                else { survivorLabel = Str(r, 1); survivorLabels = r.GetFieldValue<string[]>(2); }
                setAside.AddRange(r.GetFieldValue<string[]>(3));
            }
        }
        var asideNow = setAside.Where(l => l != survivorLabel).Distinct(StringComparer.Ordinal).ToArray();
        var merged = survivorLabels.Concat(loserLabels).Append(loserLabel).OfType<string>().Where(l => l.Length > 0 && l != survivorLabel && !asideNow.Contains(l, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
        await using (var cmd = new NpgsqlCommand("update entities set labels = @labels, normalised_labels = @normalised, set_aside_labels = @set where id = @survivor", conn, tx))
        {
            Add(cmd, "survivor", survivorId); Add(cmd, "labels", merged); Add(cmd, "normalised", NormalisedLabels(survivorLabel, merged)); Add(cmd, "set", asideNow);
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
                    Enum.Parse<EntityStatus>(r.GetString(12), ignoreCase: true), Str(r, 13), [], r.GetFieldValue<DateTimeOffset>(14), r.GetFieldValue<DateTimeOffset>(15), Str(r, 16)) { SetAsideLabels = r.GetFieldValue<string[]>(17) });
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
        await using var cmd = _db.CreateCommand("delete from authority_records where source = @source and fetched_at < @since; "
            + "update authority_sources set records = (select count(*) from authority_records where source = @source) where source = @source");
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

    public Task<IReadOnlyList<Entity>> EntitiesSharingLabelAsync(string dataset, CancellationToken ct = default) =>
        QueryEntitiesAsync(
            $"""
            with twins as (
              select class, lower(trim(label)) as l from entities
              where dataset = @dataset and merged_into is null and label is not null
              group by class, lower(trim(label)) having count(*) > 1)
            select {EntityColumns} from entities e
            join twins t on t.class = e.class and t.l = lower(trim(e.label))
            where e.dataset = @dataset and e.merged_into is null
            order by e.class, lower(trim(e.label)), e.created_at, e.id
            """, cmd => Add(cmd, "dataset", dataset), ct);

    public async Task<bool> RemoveProvisionalIfUnreferencedAsync(string entityId, CancellationToken ct = default)
    {
        // as the undo does: the superseded decisions that still name the placeholder let go of it first (the foreign key), then it goes
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            """
            create temp table remove_entity on commit drop as
              select e.id from entities e where e.id = @id and e.status = 'provisional' and e.merged_into is null
                and not exists (select 1 from decisions d where d.entity_id = e.id and d.valid)
                and not exists (select 1 from entities m where m.merged_into = e.id)
                and not exists (select 1 from entity_equivalents q where q.entity_id = e.id);
            update decisions set entity_id = null where not valid and entity_id in (select id from remove_entity);
            delete from entities where id in (select id from remove_entity)
            """, conn, tx);
        cmd.Parameters.AddWithValue("id", entityId);
        var removed = await cmd.ExecuteNonQueryAsync(ct) == 1;
        await tx.CommitAsync(ct);
        return removed;
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

    public async Task<SourceString> CollectAsync(string dataset, string context, string raw, string? parsedJson = null, CancellationToken ct = default, bool count = true)
    {
        var normalised = StringKey.Normalise(raw);
        if (normalised.Length == 0) throw new ArgumentException("An empty string cannot be collected.", nameof(raw));
        await using var cmd = _db.CreateCommand(
            $"""
            insert into source_strings as s (dataset, context, raw, normalised, parsed, first_seen, last_seen, occurrences)
            values (@dataset, @context, @raw, @normalised, @parsed, @now, @now, 1)
            on conflict (dataset, context, normalised) do update set last_seen = @now, occurrences = s.occurrences + {(count ? 1 : 0)}, parsed = coalesce(excluded.parsed, s.parsed)
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

    public async Task<DecidedString?> NextForDeskAsync(string dataset, string method, string? context = null, long afterStringId = 0, CancellationToken ct = default)
    {
        // the order is (skipped?, skipped_at, id); "after" a string means later than it in that order: after an unskipped string, the
        // unskipped with a greater id and then every skipped one; after a skipped string, the skipped ones skipped later (or later by id)
        await using var cmd = _db.CreateCommand(
            $"with a as (select skipped_at from source_strings where id = @after) " +
            $"select s.id, s.dataset, s.context, s.raw, s.normalised, s.parsed::text, s.first_seen, s.last_seen, s.occurrences, {DecisionColumns} " +
            "from source_strings s join decisions d on d.source_string_id = s.id and d.valid and d.entity_id is not null " +
            "where s.dataset = @dataset and d.method = @method" + (context is null ? "" : " and s.context = @context") +
            " and (@after = 0" +
            "   or ((select skipped_at from a) is null and ((s.skipped_at is null and s.id > @after) or s.skipped_at is not null))" +
            "   or ((select skipped_at from a) is not null and s.skipped_at is not null and (s.skipped_at, s.id) > ((select skipped_at from a), @after)))" +
            " order by (s.skipped_at is not null), s.skipped_at, s.id limit 1");
        Add(cmd, "dataset", dataset); Add(cmd, "method", method); Add(cmd, "after", afterStringId); if (context is not null) Add(cmd, "context", context);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new DecidedString(ReadString(r), new Decision(r.GetInt64(9), r.GetInt64(10), Str(r, 11), r.GetString(12), Str(r, 13), Str(r, 14), r.GetFieldValue<DateTimeOffset>(15), Str(r, 16), Str(r, 17), r.GetBoolean(18), r.IsDBNull(19) ? null : r.GetInt64(19)));
    }

    public async Task SkipAsync(long stringId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("update source_strings set skipped_at = now() where id = @id");
        Add(cmd, "id", stringId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UnskipAsync(long stringId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("update source_strings set skipped_at = null where id = @id");
        Add(cmd, "id", stringId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<DateTimeOffset?> SkippedAtAsync(long stringId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select skipped_at from source_strings where id = @id");
        Add(cmd, "id", stringId);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is DateTimeOffset t ? t : v is DateTime d ? new DateTimeOffset(d, TimeSpan.Zero) : null;
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

    public async Task<IReadOnlyList<DecidedString>> QueuedByKeysAsync(string dataset, IReadOnlyList<string> keys, int limit = 20, CancellationToken ct = default)
    {
        if (keys.Count == 0) return [];
        await using var cmd = _db.CreateCommand(
            $"select s.id, s.dataset, s.context, s.raw, s.normalised, s.parsed::text, s.first_seen, s.last_seen, s.occurrences, {DecisionColumns} " +
            "from source_strings s join decisions d on d.source_string_id = s.id and d.valid and d.entity_id is not null " +
            "where s.dataset = @dataset and d.method = 'queued' and s.normalised = any(@keys) order by s.id limit @limit");
        Add(cmd, "dataset", dataset); Add(cmd, "keys", keys.ToArray()); Add(cmd, "limit", limit);
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

    public async Task LinkPartAsync(long parentId, int position, long partId, string? role = null, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("insert into source_string_parts (parent_id, position, part_id, role) values (@parent, @position, @part, @role) on conflict (parent_id, position) do update set part_id = excluded.part_id, role = excluded.role");
        Add(cmd, "parent", parentId); Add(cmd, "position", position); Add(cmd, "part", partId); Add(cmd, "role", role);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ClearPartsAsync(long parentId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("delete from source_string_parts where parent_id = @id");
        Add(cmd, "id", parentId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SourceString>> OrphanPartStringsAsync(string dataset, IReadOnlyCollection<string> partContexts, CancellationToken ct = default)
    {
        if (partContexts.Count == 0) return [];
        await using var cmd = _db.CreateCommand(
            $"select {StringColumns} from source_strings s where s.dataset = @dataset and s.context = any(@contexts) " +
            "and not exists (select 1 from source_string_parts p where p.part_id = s.id) order by s.context, s.raw");
        Add(cmd, "dataset", dataset); Add(cmd, "contexts", partContexts.ToArray());
        var list = new List<SourceString>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadString(r));
        return list;
    }

    public async Task<int> DeleteStringsAsync(IReadOnlyCollection<long> stringIds, CancellationToken ct = default)
    {
        if (stringIds.Count == 0) return 0;
        await using var cmd = _db.CreateCommand("delete from source_strings where id = any(@ids)");
        cmd.CommandTimeout = 0; // the cascades over decisions, parts and examples; no client timeout, the transaction keeps it whole
        Add(cmd, "ids", stringIds.ToArray());
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public Task<IReadOnlyList<SourceString>> PartsAsync(long parentId, CancellationToken ct = default) =>
        StringsAsync($"select {StringColumns} from source_string_parts p join source_strings s on s.id = p.part_id where p.parent_id = @id order by p.position", parentId, ct);

    public Task<IReadOnlyList<SourceString>> ParentsAsync(long partId, CancellationToken ct = default) =>
        StringsAsync($"select {StringColumns} from source_string_parts p join source_strings s on s.id = p.parent_id where p.part_id = @id order by s.id", partId, ct);

    private async Task<IReadOnlyList<SourceString>> StringsAsync(string sql, long id, CancellationToken ct)
    {
        await using var cmd = _db.CreateCommand(sql);
        Add(cmd, "id", id);
        var list = new List<SourceString>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadString(r));
        return list;
    }

    public async Task<IReadOnlyList<DecidedString>> DecisionsForRunAsync(string runId, string? method = null, int limit = 50, string? context = null, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            $"select s.id, s.dataset, s.context, s.raw, s.normalised, s.parsed::text, s.first_seen, s.last_seen, s.occurrences, {DecisionColumns} " +
            "from decisions d join source_strings s on s.id = d.source_string_id where d.run_id = @run and d.valid" + (method is null ? "" : " and d.method = @method") + (context is null ? "" : " and s.context = @context") + " order by d.id limit @limit");
        Add(cmd, "run", runId); if (method is not null) Add(cmd, "method", method); if (context is not null) Add(cmd, "context", context); Add(cmd, "limit", limit);
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
        // a corpus-wide run has tens of thousands of decisions: no client timeout on any step; the transaction keeps it all-or-nothing
        int decisions, entities;
        await using (var cmd = new NpgsqlCommand("update decisions set valid = false where run_id = @run and valid", conn, tx) { CommandTimeout = 0 })
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
            """, conn, tx) { CommandTimeout = 0 })
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
            """, conn, tx) { CommandTimeout = 0 })
        {
            cmd.Parameters.AddWithValue("run", runId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand("delete from entities where id in (select id from undo_entities)", conn, tx) { CommandTimeout = 0 })
            entities = await cmd.ExecuteNonQueryAsync(ct);
        await using (var cmd = new NpgsqlCommand("update runs set report = coalesce(report, '') || ' [undone]' where id = @run", conn, tx) { CommandTimeout = 0 })
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

    public async Task PutPulledAsync(string uri, string source, string? label, string pulledJson, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            """
            insert into authority_records (uri, source, label, labels, normalised_labels, data)
            values (@uri, @source, @label, @labels, @normalised, jsonb_build_object('pulled', @pulled::jsonb))
            on conflict (uri) do update set data = coalesce(authority_records.data, '{}'::jsonb) || jsonb_build_object('pulled', @pulled::jsonb)
            """);
        var labels = label is null ? Array.Empty<string>() : [label];
        Add(cmd, "uri", uri); Add(cmd, "source", source); Add(cmd, "label", label); Add(cmd, "labels", labels); Add(cmd, "normalised", NormalisedLabels(label, labels));
        cmd.Parameters.Add(new NpgsqlParameter("pulled", NpgsqlTypes.NpgsqlDbType.Text) { Value = pulledJson });
        await cmd.ExecuteNonQueryAsync(ct);
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
        await using (var conn = await _db.OpenConnectionAsync(ct)) await EnsureSourceIndexesAsync(conn, [record.Source], ct);
    }

    public Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesByLabelAsync(string normalisedLabel, string? source = null, CancellationToken ct = default) =>
        QueryAuthoritiesAsync($"select {AuthorityColumns} from authority_records a where a.normalised_labels @> @key" + (source is null ? "" : " and a.source = @source") + " order by a.uri",
            cmd => { Add(cmd, "key", new[] { normalisedLabel }); if (source is not null) Add(cmd, "source", source); }, ct);

    public async Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesByLabelsAsync(IReadOnlyCollection<string> keys, string? source = null, string? type = null, int limit = 50, CancellationToken ct = default)
    {
        if (keys.Count == 0) return [];
        // Each source has its own partial label index, matched only when the source appears as a literal in the
        // predicate; a lookup across sources is a union of one branch per loaded source. Each branch finds its rows in
        // a materialised CTE before any limit applies: with a limit in reach the planner may pick a sequential scan,
        // expecting to meet 201 matches early, and for a rare name that reads the whole table (22 seconds over ten
        // million rows). Materialising makes it use the label index; the cost is bounded by the rows sharing a key.
        // a source with no records has no index either, and asking for it would scan the whole table: answer at once
        var loaded = await SourcesAsync(ct);
        var sources = source is null ? loaded : loaded.Contains(source, StringComparer.Ordinal) ? [source] : [];
        if (sources.Count == 0) { if (source is not null) Literal(source); return []; }
        string Branch(string src) => $"(with h as materialized (select * from authority_records x where x.normalised_labels && @keys and x.source = {Literal(src)}" + (type is null ? "" : " and x.type = @type") + ") select * from h limit @limit)";
        var sql = $"select {AuthorityColumns} from ({string.Join(" union all ", sources.Select(Branch))}) a order by a.uri limit @limit";
        var started = Stopwatch.GetTimestamp();
        var found = await QueryAuthoritiesAsync(sql, cmd => { Add(cmd, "keys", keys.ToArray()); if (type is not null) Add(cmd, "type", type); Add(cmd, "limit", limit); }, ct);
        var elapsed = Stopwatch.GetElapsedTime(started);
        if (elapsed > SlowLookup && SlowLookupReported is { } report)
            report($"slow label lookup {elapsed.TotalSeconds:0.0}s: source {source ?? "any"}, type {type ?? "any"}, {found.Count} found, keys [{string.Join(" | ", keys)}]");
        return found;
    }

    /// <summary>A label lookup slower than this is reported through <see cref="SlowLookupReported"/>, with its keys, so the cause can be seen rather than guessed.</summary>
    public TimeSpan SlowLookup { get; set; } = TimeSpan.FromSeconds(2);
    public Action<string>? SlowLookupReported { get; set; }

    private static readonly Regex SourceName = new("^[a-z0-9_]+$", RegexOptions.Compiled);

    /// <summary>A source name as a SQL literal; source names are short lower-case tokens (ulan, loc, aat, tgn), checked here before they reach SQL or an index name.</summary>
    private static string Literal(string source) => SourceName.IsMatch(source) ? "'" + source + "'" : throw new ArgumentException($"Not a source name: {source}", nameof(source));

    private IReadOnlyList<string>? _sources;
    private DateTimeOffset _sourcesRead;

    /// <summary>The sources with records in the store, from authority_sources; cached a few minutes per store instance.</summary>
    public async Task<IReadOnlyList<string>> SourcesAsync(CancellationToken ct = default)
    {
        if (_sources is not null && DateTimeOffset.UtcNow - _sourcesRead < TimeSpan.FromMinutes(5)) return _sources;
        var list = new List<string>();
        await using (var cmd = _db.CreateCommand("select source from authority_sources where records > 0 order by source"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct)) list.Add(r.GetString(0));
        _sources = list; _sourcesRead = DateTimeOffset.UtcNow;
        return list;
    }

    /// <summary>The sources loaded, with record counts and when they were last loaded.</summary>
    public async Task<IReadOnlyList<AuthoritySource>> AuthoritySourcesAsync(CancellationToken ct = default)
    {
        var list = new List<AuthoritySource>();
        await using var cmd = _db.CreateCommand("select source, records, loaded_at from authority_sources order by source");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new AuthoritySource(r.GetString(0), r.GetInt64(1), r.GetFieldValue<DateTimeOffset>(2)));
        return list;
    }

    /// <summary>Makes sure each source has its partial label index and an authority_sources row with a fresh count.</summary>
    private async Task EnsureSourceIndexesAsync(NpgsqlConnection conn, IEnumerable<string> sources, CancellationToken ct)
    {
        foreach (var src in sources.Distinct(StringComparer.Ordinal))
        {
            await using var cmd = new NpgsqlCommand(
                $"create index if not exists authority_records_labels_{src} on authority_records using gin (normalised_labels) where source = {Literal(src)}; "
                + $"insert into authority_sources (source, records, loaded_at) select {Literal(src)}, count(*), now() from authority_records where source = {Literal(src)} "
                + "on conflict (source) do update set records = excluded.records, loaded_at = excluded.loaded_at", conn) { CommandTimeout = 0 };
            await cmd.ExecuteNonQueryAsync(ct);
        }
        _sources = null;
    }

    public Task<IReadOnlyList<AuthorityRecord>> FindAuthoritiesBySameAsAsync(string uri, CancellationToken ct = default) =>
        QueryAuthoritiesAsync($"select {AuthorityColumns} from authority_records a where a.same_as @> @uri order by a.uri", cmd => Add(cmd, "uri", new[] { uri }), ct);

    /// <summary>Merges a JSON object into each named authority record's data (the Wikidata loader's sitelink counts, written after the records): keys given replace, others stay. Returns how many patches were given.</summary>
    public async Task<int> MergeAuthorityDataAsync(IAsyncEnumerable<(string Uri, string DataJson)> patches, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using (var create = new NpgsqlCommand("drop table if exists authority_data_in; create temp table authority_data_in (uri text primary key, data jsonb)", conn))
            await create.ExecuteNonQueryAsync(ct);
        var count = 0;
        await using (var writer = await conn.BeginBinaryImportAsync("copy authority_data_in (uri, data) from stdin (format binary)", ct))
        {
            await foreach (var (uri, json) in patches.WithCancellation(ct))
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(uri, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(json, NpgsqlDbType.Jsonb, ct);
                count++;
            }
            await writer.CompleteAsync(ct);
        }
        await using (var update = new NpgsqlCommand("update authority_records a set data = coalesce(a.data, '{}'::jsonb) || i.data from authority_data_in i where i.uri = a.uri", conn) { CommandTimeout = 0 })
            await update.ExecuteNonQueryAsync(ct);
        return count;
    }

    public async Task<int> SetAuthorityTypesByClassAsync(string source, IReadOnlyDictionary<string, string> typeByClass, CancellationToken ct = default)
    {
        if (typeByClass.Count == 0) return 0;
        await using var conn = await _db.OpenConnectionAsync(ct);
        await using (var create = new NpgsqlCommand("drop table if exists class_type_in; create temp table class_type_in (class text primary key, type text not null)", conn))
            await create.ExecuteNonQueryAsync(ct);
        await using (var writer = await conn.BeginBinaryImportAsync("copy class_type_in (class, type) from stdin (format binary)", ct))
        {
            foreach (var (cls, type) in typeByClass)
            {
                await writer.StartRowAsync(ct);
                await writer.WriteAsync(cls, NpgsqlDbType.Text, ct);
                await writer.WriteAsync(type, NpgsqlDbType.Text, ct);
            }
            await writer.CompleteAsync(ct);
        }
        // the first instance-of value, in the record's order, that the map resolves
        await using var update = new NpgsqlCommand(
            """
            update authority_records a
               set type = (select m.type from jsonb_array_elements_text(a.data->'instanceOf') with ordinality e(class, ord)
                           join class_type_in m on m.class = e.class order by e.ord limit 1)
             where a.source = @source and a.type is null
               and exists (select 1 from jsonb_array_elements_text(a.data->'instanceOf') e(class) join class_type_in m on m.class = e.class)
            """, conn) { CommandTimeout = 0 };
        update.Parameters.AddWithValue("source", source);
        return await update.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> PutAuthoritiesAsync(IAsyncEnumerable<AuthorityRecord> records, CancellationToken ct = default)
    {
        await using var conn = await _db.OpenConnectionAsync(ct);
        // the merge of millions of rows and its index maintenance want memory; a local setting for this session only
        await using (var tune = new NpgsqlCommand("set work_mem = '512MB'; set maintenance_work_mem = '1GB'", conn)) await tune.ExecuteNonQueryAsync(ct);
        await using (var create = new NpgsqlCommand("drop table if exists authority_records_in; create temp table authority_records_in (like authority_records including defaults)", conn))
            await create.ExecuteNonQueryAsync(ct);
        var count = 0;
        var sources = new HashSet<string>(StringComparer.Ordinal);
        await using (var writer = await conn.BeginBinaryImportAsync("copy authority_records_in (uri, source, label, labels, normalised_labels, type, begin_earliest, begin_latest, end_earliest, end_latest, lifespan_text, same_as, data, fetched_at, source_version) from stdin (format binary)", ct))
        {
            await foreach (var record in records.WithCancellation(ct))
            {
                var labels = record.Labels.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct(StringComparer.Ordinal).ToArray();
                sources.Add(record.Source);
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
        foreach (var src in sources) Literal(src); // validated before any of them reaches SQL
        var dropIndexes = "drop index if exists authority_records_same_as" + string.Concat(sources.Select(src => $", authority_records_labels_{src}")) + "; ";
        var createSame = "create index authority_records_same_as on authority_records using gin (same_as); ";
        await using (var merge = new NpgsqlCommand(
            (rebuild ? dropIndexes : "")
            + """
            insert into authority_records (uri, source, label, labels, normalised_labels, type, begin_earliest, begin_latest, end_earliest, end_latest, lifespan_text, same_as, data, fetched_at, source_version)
            select distinct on (uri) uri, source, label, labels, normalised_labels, type, begin_earliest, begin_latest, end_earliest, end_latest, lifespan_text, same_as, data, fetched_at, source_version from authority_records_in order by uri
            on conflict (uri) do update set source = excluded.source, label = excluded.label, labels = excluded.labels, normalised_labels = excluded.normalised_labels, type = excluded.type,
                begin_earliest = excluded.begin_earliest, begin_latest = excluded.begin_latest, end_earliest = excluded.end_earliest, end_latest = excluded.end_latest, lifespan_text = excluded.lifespan_text,
                same_as = excluded.same_as, data = excluded.data, fetched_at = excluded.fetched_at, source_version = excluded.source_version;
            drop table authority_records_in;
            """
            + (rebuild ? createSame : "")
            + "analyze authority_records", conn))
        {
            merge.CommandTimeout = 0;
            await merge.ExecuteNonQueryAsync(ct);
        }
        // the per-source label indexes: rebuilt after a large batch (dropped above), created for a source new to the store, and the source counts refreshed
        await EnsureSourceIndexesAsync(conn, sources, ct);
        if (count >= VacuumAbove)
        {
            // a load leaves dead row versions (replaced records) and the GIN indexes with pending lists of new entries that
            // every lookup scans until a vacuum folds them in: reloading ULAN (404k rows) made each lookup 20 times slower
            // until one ran. Autovacuum would get there throttled, over an hour or more. So vacuum now, unthrottled; it
            // must run outside the transaction above, as a command of its own.
            await using (var stats = new NpgsqlCommand("alter table authority_records alter column normalised_labels set statistics 1000", conn)) await stats.ExecuteNonQueryAsync(ct);
            await using var vacuum = new NpgsqlCommand("vacuum (analyze) authority_records", conn) { CommandTimeout = 0 };
            await vacuum.ExecuteNonQueryAsync(ct);
        }
        return count;
    }

    /// <summary>Batches of at least this many records drop and rebuild the label indexes around the insert rather than maintaining them row by row. Settable for tests.</summary>
    internal int RebuildIndexesAbove { get; set; } = 500_000;

    /// <summary>Batches of at least this many records are followed by an unthrottled vacuum of the table, which folds the GIN pending lists in and clears the replaced rows.</summary>
    internal int VacuumAbove { get; set; } = 5_000;

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
        // a run that never finished and is hours old was interrupted (the process died or was stopped); say so rather than
        // leave it looking live. Six hours is longer than any run so far; the jobs step will own run state properly.
        await using (var stale = _db.CreateCommand("update runs set report = 'interrupted' where dataset = @dataset and finished_at is null and report is null and started_at < now() - interval '6 hours'"))
        {
            Add(stale, "dataset", dataset);
            await stale.ExecuteNonQueryAsync(ct);
        }
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

    public async Task<IReadOnlyList<Note>> RecentNotesAsync(string dataset, int limit = 50, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select id, dataset, target, author, text, created_at from notes where dataset = @dataset order by created_at desc, id desc limit @limit");
        Add(cmd, "dataset", dataset); Add(cmd, "limit", limit);
        var list = new List<Note>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new Note(r.GetInt64(0), r.GetString(1), r.GetString(2), Str(r, 3), r.GetString(4), r.GetFieldValue<DateTimeOffset>(5)));
        return list;
    }

    public async Task<IReadOnlyList<DecidedString>> RecentHumanDecisionsAsync(string dataset, int limit = 50, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            $"select {StringColumns}, {DecisionColumns} " +
            "from decisions d join source_strings s on s.id = d.source_string_id where s.dataset = @dataset and d.reviewer is not null order by d.decided_at desc, d.id desc limit @limit");
        Add(cmd, "dataset", dataset); Add(cmd, "limit", limit);
        var list = new List<DecidedString>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new DecidedString(ReadString(r), new Decision(r.GetInt64(9), r.GetInt64(10), Str(r, 11), r.GetString(12), Str(r, 13), Str(r, 14), r.GetFieldValue<DateTimeOffset>(15), Str(r, 16), Str(r, 17), r.GetBoolean(18), r.IsDBNull(19) ? null : r.GetInt64(19))));
        return list;
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

    // ---- the records behind the strings ----------------------------------------------------------------------

    public async Task<IReadOnlyDictionary<(string Context, string Normalised), long>> StringKeysAsync(string dataset, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select id, context, normalised from source_strings where dataset = @dataset");
        Add(cmd, "dataset", dataset);
        var keys = new Dictionary<(string, string), long>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) keys[(r.GetString(1), r.GetString(2))] = r.GetInt64(0);
        return keys;
    }

    public async Task PutExamplesAsync(IReadOnlyList<StringExample> examples, CancellationToken ct = default)
    {
        if (examples.Count == 0) return;
        // the record's row first, when no collect has written it (a test, or a caller with examples alone); a collect's own row is left as it is
        const string rows = "from unnest(@s, @r, @src, @t, @d, @f, @j, @p, @ps) as x(s, r, src, t, d, f, j, p, ps) join source_strings ss on ss.id = x.s ";
        await using var batch = _db.CreateBatch();
        var records = new NpgsqlBatchCommand(
            "insert into records (dataset, system, record_id, title, date, fields) " +
            "select distinct on (ss.dataset, x.src, x.r) ss.dataset, x.src, x.r, x.t, x.d, x.j::jsonb " + rows +
            "on conflict (dataset, system, record_id) do nothing");
        var occurrences = new NpgsqlBatchCommand(
            "insert into occurrences (dataset, system, record_id, string_id, field, position, parent_string_id) " +
            "select distinct on (ss.dataset, x.src, x.r, x.s) ss.dataset, x.src, x.r, x.s, x.f, x.p, x.ps " + rows +
            "on conflict (dataset, system, record_id, string_id) do update set field = excluded.field, position = excluded.position, parent_string_id = excluded.parent_string_id");
        foreach (var cmd in new[] { records, occurrences })
        {
            cmd.Parameters.AddWithValue("s", examples.Select(e => e.StringId).ToArray());
            cmd.Parameters.AddWithValue("r", examples.Select(e => e.RecordId).ToArray());
            cmd.Parameters.AddWithValue("src", examples.Select(e => e.Source).ToArray());
            cmd.Parameters.AddWithValue("t", examples.Select(e => e.Title).ToArray());
            cmd.Parameters.AddWithValue("d", examples.Select(e => e.Date).ToArray());
            cmd.Parameters.AddWithValue("f", examples.Select(e => e.Field).ToArray());
            cmd.Parameters.AddWithValue("j", examples.Select(e => e.RecordJson).ToArray());
            cmd.Parameters.AddWithValue("p", examples.Select(e => e.Position).ToArray());
            cmd.Parameters.AddWithValue("ps", examples.Select(e => e.ParentStringId).ToArray());
            batch.BatchCommands.Add(cmd);
        }
        await batch.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> ClearExamplesAsync(string dataset, string source, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("delete from occurrences where dataset = @dataset and system = @source");
        cmd.CommandTimeout = 0;
        Add(cmd, "dataset", dataset); Add(cmd, "source", source);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private const string ExampleColumns = "o.string_id, o.record_id, o.system, r.title, r.date, o.field, r.fields::text, o.parent_string_id, o.position";
    private static StringExample ReadExample(NpgsqlDataReader r) =>
        new(r.GetInt64(0), r.GetString(1), r.GetString(2), Str(r, 3), Str(r, 4), r.GetString(5), Str(r, 6), r.IsDBNull(7) ? null : r.GetInt64(7), r.GetInt32(8));

    public async Task<IReadOnlyList<StringExample>> ExamplesForAsync(long stringId, int limit = 3, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand($"select {ExampleColumns} from occurrences o join records r using (dataset, system, record_id) where o.string_id = @id order by o.record_id limit @limit");
        Add(cmd, "id", stringId); Add(cmd, "limit", limit);
        var list = new List<StringExample>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadExample(r));
        return list;
    }

    public async Task PutRecordsAsync(string dataset, string collect, IReadOnlyList<SourceRecord> records, CancellationToken ct = default)
    {
        if (records.Count == 0) return;
        await using var cmd = _db.CreateCommand(
            "insert into records (dataset, system, record_id, title, date, fields, fingerprint, first_collect, last_collect) " +
            "select @dataset, sys, r, t, d, j::jsonb, fp, @collect, @collect from unnest(@sys, @r, @t, @d, @j, @fp) as x(sys, r, t, d, j, fp) " +
            "on conflict (dataset, system, record_id) do update set title = excluded.title, date = excluded.date, fields = excluded.fields, fingerprint = excluded.fingerprint, " +
            "first_collect = coalesce(records.first_collect, excluded.first_collect), last_collect = excluded.last_collect, gone_at = null");
        Add(cmd, "dataset", dataset); Add(cmd, "collect", collect);
        Add(cmd, "sys", records.Select(x => x.System).ToArray());
        Add(cmd, "r", records.Select(x => x.RecordId).ToArray());
        Add(cmd, "t", records.Select(x => x.Title).ToArray());
        Add(cmd, "d", records.Select(x => x.Date).ToArray());
        Add(cmd, "j", records.Select(x => x.FieldsJson).ToArray());
        Add(cmd, "fp", records.Select(x => x.Fingerprint).ToArray());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> MarkRecordsGoneAsync(string dataset, string system, string collect, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("update records set gone_at = now() where dataset = @dataset and system = @system and last_collect is distinct from @collect and gone_at is null");
        Add(cmd, "dataset", dataset); Add(cmd, "system", system); Add(cmd, "collect", collect);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> RecordCountAsync(long stringId, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select count(*) from occurrences where string_id = @id");
        Add(cmd, "id", stringId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<AwayRow>> ImportedPlacesWithHomeNamesakesAsync(string dataset, string homePlace, CancellationToken ct = default)
    {
        // the source literal lets the lookup use TGN's own label index (schema version 5)
        // and each name is looked up on its own (lateral, offset 0): joined plainly, the planner scanned every authority record, eleven seconds
        // where this takes a quarter of one (the owner, 2026-11-15: "the checks page takes a long time to load")
        await using var cmd = _db.CreateCommand(
            """
            with c as materialized (
                -- what a pull brought says what the item is when the index has no row of its own for it ("Berwick": a hamlet in Kent)
                select e.id, e.label, e.status, q.uri as item, coalesce(w.data->>'description', w.data->'pulled'->>'description') as descr, coalesce((w.data->>'sitelinks')::int, 0) as sitelinks,
                       coalesce(w.data->>'lat', w.data->'pulled'->>'lat')::double precision as ilat, coalesce(w.data->>'long', w.data->'pulled'->>'long')::double precision as ilong,
                       (select count(*) from decisions d where d.valid and d.entity_id = e.id) as strings
                from entities e join entity_equivalents q on q.entity_id = e.id and q.source = 'wikidata' and q.method = 'imported'
                     left join authority_records w on w.uri = q.uri
                where e.dataset = @dataset and e.class = 'Place' and e.merged_into is null and e.status <> 'confirmed' and e.label !~ '[,(\[]')
            select c.id, c.label, c.status, c.strings, c.item, c.descr, c.sitelinks, t.uri, t.data->>'parentString', coalesce(h.sitelinks, 0), h.uri,
                   exists (select 1 from entity_equivalents x where x.entity_id = c.id and x.uri = t.uri),
                   (select k.label from authority_records k where k.uri = t.data->>'placeType'),
                   c.ilat, c.ilong, (t.data->>'lat')::double precision, (t.data->>'long')::double precision
            from c cross join lateral (select t.uri, t.data from authority_records t where t.source = 'tgn' and t.normalised_labels @> array[lower(c.label)]
                                         and t.label = c.label and t.data->>'parentString' like @home offset 0) t
                 left join lateral (select x.uri, coalesce((x.data->>'sitelinks')::int, 0) as sitelinks from authority_records x
                                    where x.source = 'wikidata' and x.same_as @> array[t.uri] order by 2 desc limit 1) h on true
            order by c.label, c.id
            """);
        Add(cmd, "dataset", dataset); Add(cmd, "home", "%" + homePlace + "%");
        cmd.CommandTimeout = 120;
        var list = new List<AwayRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new AwayRow(r.GetString(0), Str(r, 1) ?? "", r.GetString(2), Convert.ToInt32(r.GetInt64(3)), r.GetString(4), Str(r, 5), r.GetInt32(6), r.GetString(7), Str(r, 8) ?? "", r.GetInt32(9), Str(r, 10), r.GetBoolean(11), Str(r, 12))
            {
                ItemAt = r.IsDBNull(13) || r.IsDBNull(14) ? null : (r.GetDouble(13), r.GetDouble(14)),
                HomeAt = r.IsDBNull(15) || r.IsDBNull(16) ? null : (r.GetDouble(15), r.GetDouble(16)),
            });
        return list;
    }

    public async Task<IReadOnlyDictionary<string, string>> NotedAsync(string dataset, IReadOnlyList<string> prefixes, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select distinct on (target) target, text from notes where dataset = @dataset and text like any(@patterns) order by target, created_at desc");
        Add(cmd, "dataset", dataset); Add(cmd, "patterns", prefixes.Select(p => p.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%").ToArray());
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) found[r.GetString(0)] = r.GetString(1);
        return found;
    }

    private static string Like(string text) => "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

    public async Task<IReadOnlyList<StringHit>> SearchStringsAsync(string dataset, string text, int limit = 30, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            """
            select s.id, s.dataset, s.context, s.raw, s.normalised, s.parsed::text, s.first_seen, s.last_seen, s.occurrences, d.method, d.entity_id,
                   (select count(*) from occurrences o where o.string_id = s.id)
            from source_strings s left join decisions d on d.source_string_id = s.id and d.valid
            where s.dataset = @dataset and s.raw ilike @pattern
            order by (lower(s.raw) = lower(@text)) desc, length(s.raw), s.raw, s.context limit @limit
            """);
        Add(cmd, "dataset", dataset); Add(cmd, "pattern", Like(text)); Add(cmd, "text", text); Add(cmd, "limit", limit);
        var list = new List<StringHit>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new StringHit(ReadString(r), Str(r, 9), Str(r, 10), Convert.ToInt32(r.GetInt64(11))));
        return list;
    }

    public async Task<IReadOnlyList<LongerForm>> LongerFormsAsync(long stringId, int limit = 8, CancellationToken ct = default)
    {
        // whole words at the head or the tail, compared without "like" so that a "%" in a string is only a "%"
        await using var cmd = _db.CreateCommand(
            """
            select t.id, t.raw, (select count(*) from occurrences o where o.string_id = t.id) as records, d.entity_id, e.label
            from source_strings s
            join source_strings t on t.dataset = s.dataset and t.context = s.context and t.id <> s.id and length(t.raw) > length(s.raw) + 1
                 and (left(lower(t.raw), length(s.raw) + 1) = lower(s.raw) || ' ' or right(lower(t.raw), length(s.raw) + 1) = ' ' || lower(s.raw))
            join decisions d on d.source_string_id = t.id and d.valid and d.method not in ('queued', 'minted') and d.entity_id is not null
            join entities e on e.id = d.entity_id
            where s.id = @id and length(s.raw) >= 4
            order by records desc, t.raw limit @limit
            """);
        Add(cmd, "id", stringId); Add(cmd, "limit", limit);
        var list = new List<LongerForm>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new LongerForm(r.GetInt64(0), r.GetString(1), Convert.ToInt32(r.GetInt64(2)), r.GetString(3), Str(r, 4)));
        return list;
    }

    public async Task<IReadOnlyDictionary<string, Standing>> StandingOnAsync(string dataset, IReadOnlyCollection<string> uris, CancellationToken ct = default)
    {
        var found = new Dictionary<string, Standing>(StringComparer.Ordinal);
        if (uris.Count == 0) return found;
        await using var cmd = _db.CreateCommand(
            """
            select q.uri, e.id, e.label, count(distinct d.source_string_id), count(distinct (o.system, o.record_id))
            from entity_equivalents q join entities e on e.id = q.entity_id and e.dataset = @dataset
            join decisions d on d.entity_id = e.id and d.valid and d.method not in ('queued', 'minted')
            left join occurrences o on o.string_id = d.source_string_id
            where q.uri = any(@uris) group by q.uri, e.id, e.label order by 5
            """);
        Add(cmd, "dataset", dataset); Add(cmd, "uris", uris.ToArray());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) found[r.GetString(0)] = new Standing(r.GetString(1), Str(r, 2), Convert.ToInt32(r.GetInt64(3)), Convert.ToInt32(r.GetInt64(4))); // the most used last, and kept
        return found;
    }

    public async Task<IReadOnlyList<RecordHit>> SearchRecordsAsync(string dataset, string text, int limit = 15, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            """
            select r.system, r.record_id, r.title, r.date,
                   (select string_agg(x, chr(31)) from (select o.string_id || chr(30) || s.context || chr(30) || s.raw as x
                        from occurrences o join source_strings s on s.id = o.string_id
                        where o.dataset = r.dataset and o.system = r.system and o.record_id = r.record_id order by o.field, o.position limit 12) z)
            from records r where r.dataset = @dataset and r.gone_at is null and (r.title ilike @pattern or r.record_id = @text)
            order by (r.record_id = @text) desc, length(r.title), r.title limit @limit
            """);
        Add(cmd, "dataset", dataset); Add(cmd, "pattern", Like(text)); Add(cmd, "text", text); Add(cmd, "limit", limit);
        var list = new List<RecordHit>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var strings = (Str(r, 4) ?? "").Split((char)31, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split((char)30)).Where(x => x.Length == 3)
                .Select(x => (long.Parse(x[0], CultureInfo.InvariantCulture), x[1], x[2])).ToList();
            list.Add(new RecordHit(r.GetString(0), r.GetString(1), Str(r, 2), Str(r, 3), strings));
        }
        return list;
    }

    public async Task<(string Uri, string? Description, int Sitelinks)?> BestKnownNamesakeAsync(string label, string? exceptUri, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            "select a.uri, a.data->>'description', coalesce((a.data->>'sitelinks')::int, 0) from authority_records a " +
            "where a.source = 'wikidata' and a.normalised_labels @> array[lower(@label)] and a.label = @label and a.type = 'Place' and a.uri <> @except order by 3 desc, a.uri limit 1");
        Add(cmd, "label", label); Add(cmd, "except", exceptUri ?? "");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? (r.GetString(0), Str(r, 1), r.GetInt32(2)) : null;
    }

    public async Task<IReadOnlyList<(string Value, int Records)>> FieldValuesWhereAsync(string dataset, string whereField, string whereValue, string field, int limit = 40, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand(
            "select v, count(*) from records r, jsonb_array_elements_text(case when jsonb_typeof(r.fields->@field) = 'array' then r.fields->@field else '[]'::jsonb end) v " +
            "where r.dataset = @dataset and r.gone_at is null and jsonb_typeof(r.fields->@where) = 'array' and r.fields->@where ? @value group by v order by count(*) desc, v limit @limit");
        Add(cmd, "dataset", dataset); Add(cmd, "where", whereField); Add(cmd, "value", whereValue); Add(cmd, "field", field); Add(cmd, "limit", limit);
        var list = new List<(string, int)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add((r.GetString(0), Convert.ToInt32(r.GetInt64(1))));
        return list;
    }

    public async Task<IReadOnlyList<RecordGroup>> RecordGroupsAsync(long stringId, string byField, int groups = 8, int perGroup = 3, CancellationToken ct = default)
    {
        // the field's values joined as the export orders them; a record without the field is in the group of none
        await using var cmd = _db.CreateCommand(
            "with o as (select " + ExampleColumns + ", (select string_agg(v, ' · ') from jsonb_array_elements_text(case when jsonb_typeof(r.fields->@by) = 'array' then r.fields->@by else '[]'::jsonb end) v) as val " +
            "           from occurrences o join records r using (dataset, system, record_id) where o.string_id = @id), " +
            "g as (select val, count(*) as n from o group by val order by count(*) desc, val nulls last limit @groups), " +
            "ranked as (select o.*, g.n, row_number() over (partition by o.val order by o.record_id) as rn from o join g on g.val is not distinct from o.val) " +
            "select string_id, record_id, system, title, date, field, fields, parent_string_id, position, val, n from ranked where rn <= @per order by n desc, val nulls last, record_id");
        Add(cmd, "id", stringId); Add(cmd, "by", byField); Add(cmd, "groups", groups); Add(cmd, "per", perGroup);
        var list = new List<(string? Value, int N, List<StringExample> First)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var value = Str(r, 9);
            if (list.Count == 0 || list[^1].Value != value) list.Add((value, Convert.ToInt32(r.GetInt64(10)), []));
            list[^1].First.Add(ReadExample(r));
        }
        return list.Select(g => new RecordGroup(g.Value, g.N, g.First)).ToList();
    }

    public async Task<IReadOnlyList<(string System, int Records, int Gone, string? LastCollect)>> RecordSystemsAsync(string dataset, CancellationToken ct = default)
    {
        await using var cmd = _db.CreateCommand("select system, count(*), count(*) filter (where gone_at is not null), max(last_collect) from records where dataset = @dataset group by system order by system");
        Add(cmd, "dataset", dataset);
        var list = new List<(string, int, int, string?)>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add((r.GetString(0), Convert.ToInt32(r.GetInt64(1)), Convert.ToInt32(r.GetInt64(2)), Str(r, 3)));
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

    /// <summary>
    /// Vacuums and analyses the store's tables, unthrottled, each as a command of its own: the housekeeping a load does for itself at
    /// its end, for the dead rows that come otherwise (the pulls rewrite a record's data; a run taken back leaves its decisions' and
    /// entities' rows). The owner, 2026-11-14: <c>authority_records</c> last vacuumed on 2026-10-18, 2.26 million dead rows, and the
    /// project's home page timing out on a cold scan of it. Returns each table with the dead rows the statistics counted before.
    /// </summary>
    public async Task<IReadOnlyList<(string Table, long DeadBefore)>> VacuumAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        string[] tables = ["authority_records", "decisions", "source_strings", "entities", "entity_equivalents"];
        await using var conn = await _db.OpenConnectionAsync(ct);
        var done = new List<(string, long)>();
        foreach (var table in tables)
        {
            long dead;
            await using (var count = new NpgsqlCommand("select coalesce(n_dead_tup, 0) from pg_stat_user_tables where relname = @t", conn))
            {
                count.Parameters.AddWithValue("t", table);
                dead = await count.ExecuteScalarAsync(ct) is long n ? n : 0;
            }
            progress?.Report($"{table}: {dead:N0} dead rows; vacuuming");
            await using var vacuum = new NpgsqlCommand($"vacuum (analyze) {table}", conn) { CommandTimeout = 0 };
            await vacuum.ExecuteNonQueryAsync(ct);
            done.Add((table, dead));
        }
        return done;
    }

    // The authority records are counted from what the loaders wrote per source (authority_sources.records), never by scanning the table: nineteen
    // million rows read cold took longer than the command's thirty seconds and the project's home page failed with a timeout (the owner, 2026-11-14)
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
              (select coalesce(sum(records), 0)::bigint from authority_sources),
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
