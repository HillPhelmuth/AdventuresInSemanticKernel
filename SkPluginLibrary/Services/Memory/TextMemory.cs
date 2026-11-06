using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace SkPluginLibrary.Services;

/// <summary>
/// Text-in/text-out semantic memory: embeds text with an <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>
/// and stores/searches it in a <see cref="VectorMemoryStore"/>.
/// </summary>
public class TextMemory(VectorMemoryStore store, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    public VectorMemoryStore Store => store;
    public IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator => embeddingGenerator;

    public async Task<string> SaveInformationAsync(string collection, string text, string id, string? description = null,
        string? additionalMetadata = null, CancellationToken cancellationToken = default)
    {
        if (!await store.DoesCollectionExistAsync(collection, cancellationToken))
            await store.CreateCollectionAsync(collection, cancellationToken);
        var embedding = await embeddingGenerator.GenerateAsync(text, cancellationToken: cancellationToken);
        var record = new MemoryRecord
        {
            Key = id,
            Embedding = embedding.Vector,
            Metadata = new MemoryRecordMetadata { Id = id, Text = text, Description = description ?? "", AdditionalMetadata = additionalMetadata ?? "" }
        };
        return await store.UpsertAsync(collection, record, cancellationToken);
    }

    public async Task<MemoryQueryResult?> GetAsync(string collection, string key, bool withEmbedding = false, CancellationToken cancellationToken = default)
    {
        var record = await store.GetAsync(collection, key, cancellationToken);
        return record is null ? null : new MemoryQueryResult(record.Metadata, 1.0, record.Embedding);
    }

    public Task RemoveAsync(string collection, string key, CancellationToken cancellationToken = default) =>
        store.RemoveAsync(collection, key, cancellationToken);

    public async IAsyncEnumerable<MemoryQueryResult> SearchAsync(string collection, string query, int limit = 1,
        double minRelevanceScore = 0.0, bool withEmbeddings = false, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await store.DoesCollectionExistAsync(collection, cancellationToken)) yield break;
        var embedding = await embeddingGenerator.GenerateAsync(query, cancellationToken: cancellationToken);
        foreach (var match in await store.GetNearestMatchesAsync(collection, embedding.Vector, limit, minRelevanceScore, cancellationToken))
            yield return match;
    }

    public Task<IReadOnlyList<string>> GetCollectionsAsync(CancellationToken cancellationToken = default) =>
        store.GetCollectionsAsync(cancellationToken);
}
