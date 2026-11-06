namespace SkPluginLibrary.Services;

/// <summary>Access to the app's pre-built documentation embeddings (SQLite, same layout as before).</summary>
public static class DocsMemory
{
    public static async Task<TextMemory> GetAsync()
    {
        var store = await SqliteVectorMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
        return new TextMemory(store, ChatClientFactory.CreateEmbeddingGenerator());
    }

    public static async Task<bool> CollectionExistsAsync(string collection)
    {
        var store = await SqliteVectorMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
        return await store.DoesCollectionExistAsync(collection);
    }
}
