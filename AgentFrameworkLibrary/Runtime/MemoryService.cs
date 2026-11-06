using System.Text.Json;
using AgentFrameworkOrchestration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;

namespace AgentFrameworkLibrary;

public sealed record MemoryItem(string Id, string Text, float[] Vector, string Model, string? Tag = null, string? Source = null);
public sealed record MemoryMatch(MemoryItem Item, double Score);
public enum MemoryStorage { InMemory, Sqlite }

public sealed class MemoryService(RuntimeConfiguration configuration,
    IEmbeddingGenerator<string, Embedding<float>>? embeddingGenerator = null)
{
    private readonly Dictionary<string, Dictionary<string, MemoryItem>> _collections = [];
    public string Owner { get; set; } = Guid.NewGuid().ToString("N");
    public MemoryStorage Storage { get; set; }

    private string DatabasePath => Path.Combine(configuration.Get("AgentFramework:MemoryDirectory") ??
        Path.Combine(AppContext.BaseDirectory, "Data"), "agent-framework-memory.db");

    public async Task<MemoryItem> AddAsync(string collection, string text, string? tag = null, string? source = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var model = ModelKey;
        var vector = await EmbedAsync(text, cancellationToken);
        var item = new MemoryItem(Guid.NewGuid().ToString("N"), text, vector, model, tag, source);
        await PutAsync(collection, item, cancellationToken);
        return item;
    }

    public string ModelKey => $"{configuration.Current.Provider}:{configuration.Current.EmbeddingModel}:{configuration.Current.EmbeddingDimensions}";

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (embeddingGenerator is not null)
            return (await embeddingGenerator.GenerateAsync([text], cancellationToken: cancellationToken))[0].Vector.ToArray();
        using var generator = configuration.Current.CreateClient().GetEmbeddingClient(configuration.Current.EmbeddingModel).AsIEmbeddingGenerator();
        var result = await generator.GenerateAsync([text], new EmbeddingGenerationOptions
        { Dimensions = configuration.Current.EmbeddingDimensions }, cancellationToken);
        return result[0].Vector.ToArray();
    }

    public async Task PutAsync(string collection, MemoryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        if (item.Vector.Length != configuration.Current.EmbeddingDimensions || item.Vector.Any(v => !float.IsFinite(v)) || item.Vector.All(v => v == 0))
            throw new ArgumentException("Embeddings must match the configured dimensions and contain finite, nonzero values.");
        if (Storage == MemoryStorage.InMemory)
        {
            if (!_collections.TryGetValue(collection, out var items)) _collections[collection] = items = [];
            items[item.Id] = item;
            return;
        }
        await using var db = await OpenAsync(cancellationToken);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO af_memory(owner,collection,id,payload) VALUES($owner,$collection,$id,$payload) ON CONFLICT(owner,collection,id) DO UPDATE SET payload=excluded.payload";
        cmd.Parameters.AddWithValue("$owner", Owner);
        cmd.Parameters.AddWithValue("$collection", collection);
        cmd.Parameters.AddWithValue("$id", item.Id);
        cmd.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(item));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryItem>> ListAsync(string collection, CancellationToken cancellationToken = default)
    {
        if (Storage == MemoryStorage.InMemory)
            return _collections.TryGetValue(collection, out var items) ? items.Values.ToArray() : [];
        await using var db = await OpenAsync(cancellationToken);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT payload FROM af_memory WHERE owner=$owner AND collection=$collection ORDER BY id";
        cmd.Parameters.AddWithValue("$owner", Owner);
        cmd.Parameters.AddWithValue("$collection", collection);
        var result = new List<MemoryItem>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(JsonSerializer.Deserialize<MemoryItem>(reader.GetString(0)) ?? throw new InvalidDataException("Invalid stored memory record."));
        return result;
    }

    public async Task<IReadOnlyList<string>> CollectionsAsync(CancellationToken cancellationToken = default)
    {
        if (Storage == MemoryStorage.InMemory) return _collections.Keys.Order().ToArray();
        await using var db = await OpenAsync(cancellationToken);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT collection FROM af_memory WHERE owner=$owner ORDER BY collection";
        cmd.Parameters.AddWithValue("$owner", Owner);
        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task DeleteAsync(string collection, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        if (Storage == MemoryStorage.InMemory) { _collections.Remove(collection); return; }
        await using var db = await OpenAsync(cancellationToken);
        await using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM af_memory WHERE owner=$owner AND collection=$collection";
        cmd.Parameters.AddWithValue("$owner", Owner);
        cmd.Parameters.AddWithValue("$collection", collection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryMatch>> SearchAsync(string collection, string query, int top = 3,
        double threshold = 0, string? tag = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collection);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (top is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(top));
        if (!double.IsFinite(threshold) || threshold is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
        var items = await ListAsync(collection, cancellationToken);
        if (items.Count == 0) return [];
        var model = ModelKey;
        var vector = await EmbedAsync(query, cancellationToken);
        return Rank(items.Where(i => tag is null || i.Tag == tag), vector, model, top, threshold);
    }

    public static IReadOnlyList<MemoryMatch> Rank(IEnumerable<MemoryItem> items, float[] vector, string model, int top, double threshold)
    {
        if (top is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(top));
        if (!double.IsFinite(threshold) || threshold is < -1 or > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
        return items.Select(item =>
        {
            if (item.Model != model) throw new InvalidOperationException("Embedding model changed. Reindex this collection from its source text.");
            return new MemoryMatch(item, Cosine(item.Vector, vector));
        }).Where(m => m.Score >= threshold).OrderByDescending(m => m.Score).ThenBy(m => m.Item.Id).Take(top).ToArray();
    }

    public static double Cosine(float[] left, float[] right)
    {
        if (left.Length == 0 || left.Length != right.Length) throw new ArgumentException("Embedding dimensions must match.");
        double dot = 0, a = 0, b = 0;
        for (var i = 0; i < left.Length; i++)
        {
            if (!float.IsFinite(left[i]) || !float.IsFinite(right[i])) throw new ArgumentException("Embedding values must be finite.");
            dot += (double)left[i] * right[i]; a += (double)left[i] * left[i]; b += (double)right[i] * right[i];
        }
        if (a == 0 || b == 0) throw new ArgumentException("Zero vectors cannot be compared.");
        return Math.Clamp(dot / Math.Sqrt(a * b), -1, 1);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(Owner, out _)) throw new InvalidOperationException("Invalid memory owner identifier.");
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString());
        try
        {
            await db.OpenAsync(cancellationToken);
            await using var cmd = db.CreateCommand();
            cmd.CommandText = "CREATE TABLE IF NOT EXISTS af_memory(owner TEXT NOT NULL,collection TEXT NOT NULL,id TEXT NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(owner,collection,id))";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return db;
        }
        catch { await db.DisposeAsync(); throw; }
    }
}
