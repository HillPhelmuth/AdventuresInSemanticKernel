using System.Collections.Concurrent;
using System.Numerics.Tensors;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SkPluginLibrary.Services;

/// <summary>
/// A small in-memory vector store (collections of embedded records, searched by cosine similarity).
/// Agent Framework relies on Microsoft.Extensions.AI for embeddings and has no vector store of its own,
/// so this is the one place the app keeps a hand-rolled store.
/// </summary>
public class VectorMemoryStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, MemoryRecord>> _collections = new();

    public virtual Task CreateCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        _collections.TryAdd(collectionName, new());
        return Task.CompletedTask;
    }

    public virtual Task<bool> DoesCollectionExistAsync(string collectionName, CancellationToken ct = default) =>
        Task.FromResult(_collections.ContainsKey(collectionName));

    public virtual Task<IReadOnlyList<string>> GetCollectionsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(_collections.Keys.ToList());

    public virtual Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        _collections.TryRemove(collectionName, out _);
        return Task.CompletedTask;
    }

    public virtual Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken ct = default)
    {
        var collection = _collections.GetOrAdd(collectionName, _ => new());
        var key = string.IsNullOrEmpty(record.Key) ? record.Metadata.Id : record.Key;
        if (string.IsNullOrEmpty(key)) key = Guid.NewGuid().ToString();
        record.Key = key;
        record.Timestamp ??= DateTimeOffset.UtcNow;
        collection[key] = record;
        return Task.FromResult(key);
    }

    public virtual Task<MemoryRecord?> GetAsync(string collectionName, string key, CancellationToken ct = default) =>
        Task.FromResult(_collections.TryGetValue(collectionName, out var c) && c.TryGetValue(key, out var r) ? r : null);

    public virtual Task RemoveAsync(string collectionName, string key, CancellationToken ct = default)
    {
        if (_collections.TryGetValue(collectionName, out var c)) c.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public virtual IEnumerable<MemoryRecord> GetAll(string collectionName) =>
        _collections.TryGetValue(collectionName, out var c) ? c.Values : [];

    public virtual Task<List<MemoryQueryResult>> GetNearestMatchesAsync(string collectionName, ReadOnlyMemory<float> query,
        int limit, double minRelevanceScore = 0.0, CancellationToken ct = default)
    {
        if (!_collections.TryGetValue(collectionName, out var collection)) return Task.FromResult(new List<MemoryQueryResult>());
        var results = collection.Values
            .Where(r => r.Embedding.Length == query.Length)
            .Select(r => new MemoryQueryResult(r.Metadata, TensorPrimitives.CosineSimilarity(query.Span, r.Embedding.Span), r.Embedding))
            .Where(r => r.Relevance >= minRelevanceScore)
            .OrderByDescending(r => r.Relevance)
            .Take(limit)
            .ToList();
        return Task.FromResult(results);
    }
}

/// <summary>
/// A <see cref="VectorMemoryStore"/> backed by a SQLite file using the same <c>SKMemoryTable</c> layout the app's
/// existing databases already use, so previously ingested documents keep working.
/// </summary>
public class SqliteVectorMemoryStore : VectorMemoryStore
{
    private static readonly ConcurrentDictionary<string, Task<SqliteVectorMemoryStore>> Cache = new();
    private readonly string _connectionString;

    private SqliteVectorMemoryStore(string connectionString) => _connectionString = connectionString;

    public static Task<SqliteVectorMemoryStore> ConnectAsync(string filenameOrConnectionString)
    {
        var cs = filenameOrConnectionString.Contains('=') ? filenameOrConnectionString : $"Data Source={filenameOrConnectionString}";
        return Cache.GetOrAdd(cs, async key =>
        {
            var store = new SqliteVectorMemoryStore(key);
            await store.LoadAsync();
            return store;
        });
    }

    private async Task LoadAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE IF NOT EXISTS SKMemoryTable(collection TEXT, key TEXT, metadata TEXT, embedding TEXT, timestamp TEXT, PRIMARY KEY(collection, key))";
            await create.ExecuteNonQueryAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT collection, key, metadata, embedding, timestamp FROM SKMemoryTable";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var collection = reader.GetString(0);
            await base.CreateCollectionAsync(collection);
            var metadata = JsonSerializer.Deserialize<MemoryRecordMetadata>(reader.GetString(2)) ?? new();
            var embedding = JsonSerializer.Deserialize<float[]>(reader.GetString(3)) ?? [];
            DateTimeOffset? timestamp = DateTimeOffset.TryParse(reader.IsDBNull(4) ? null : reader.GetString(4), out var ts) ? ts : null;
            await base.UpsertAsync(collection, new MemoryRecord { Key = reader.GetString(1), Metadata = metadata, Embedding = embedding, Timestamp = timestamp });
        }
    }

    public override async Task CreateCollectionAsync(string collectionName, CancellationToken ct = default) =>
        await base.CreateCollectionAsync(collectionName, ct);

    public override async Task<string> UpsertAsync(string collectionName, MemoryRecord record, CancellationToken ct = default)
    {
        var key = await base.UpsertAsync(collectionName, record, ct);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO SKMemoryTable(collection, key, metadata, embedding, timestamp) VALUES(@c, @k, @m, @e, @t) ON CONFLICT(collection, key) DO UPDATE SET metadata=@m, embedding=@e, timestamp=@t";
        command.Parameters.AddWithValue("@c", collectionName);
        command.Parameters.AddWithValue("@k", key);
        command.Parameters.AddWithValue("@m", JsonSerializer.Serialize(record.Metadata));
        command.Parameters.AddWithValue("@e", JsonSerializer.Serialize(record.Embedding.ToArray()));
        command.Parameters.AddWithValue("@t", record.Timestamp?.ToString("O") ?? "");
        await command.ExecuteNonQueryAsync(ct);
        return key;
    }

    public override async Task DeleteCollectionAsync(string collectionName, CancellationToken ct = default)
    {
        await base.DeleteCollectionAsync(collectionName, ct);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SKMemoryTable WHERE collection=@c";
        command.Parameters.AddWithValue("@c", collectionName);
        await command.ExecuteNonQueryAsync(ct);
    }

    public override async Task RemoveAsync(string collectionName, string key, CancellationToken ct = default)
    {
        await base.RemoveAsync(collectionName, key, ct);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SKMemoryTable WHERE collection=@c AND key=@k";
        command.Parameters.AddWithValue("@c", collectionName);
        command.Parameters.AddWithValue("@k", key);
        await command.ExecuteNonQueryAsync(ct);
    }
}
