using System.Numerics.Tensors;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using SkPluginLibrary.Services;
using UglyToad.PdfPig;
using DistanceFunction = SkPluginLibrary.Services.DistanceFunction;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Memory and Embeddings (VectorPlaygroundPage.razor, ClusteringPage.razor)

    private (string titleResultValue, string summaryValue) _valueTuple;
    private TextMemory? _playgroundTextMemory;
    private TextMemory? _dbScanMemory;
    private VectorMemoryStore _playgroundStore = new();

    // Records for the vector-search playground (embedding generation is Microsoft.Extensions.AI; the store is in-process).
    private static readonly Dictionary<string, List<VectorStoreContextItem>> _vectorCollections = new();

    private async Task DeleteCollection(string collection)
    {
        if (await _playgroundStore.DoesCollectionExistAsync(collection))
            await _playgroundStore.DeleteCollectionAsync(collection);
    }

    public async IAsyncEnumerable<VectorSearchResult<VectorStoreContextItem>> GetVectorSearchResults(string query,
        string collection, int topN = 3,
        double minThreshold = 0.0, StringFilter filter = StringFilter.None, string filterText = "")
    {
        var items = _vectorCollections.TryGetValue(collection, out var list) ? list.ToList() : [];
        // Optional text filter on the stored content
        Func<VectorStoreContextItem, bool> predicate = filter switch
        {
            StringFilter.Contains when !string.IsNullOrEmpty(filterText) => x => x.Content != null && x.Content.Contains(filterText, StringComparison.OrdinalIgnoreCase),
            StringFilter.NotContains when !string.IsNullOrEmpty(filterText) => x => x.Content == null || !x.Content.Contains(filterText, StringComparison.OrdinalIgnoreCase),
            StringFilter.StartsWith when !string.IsNullOrEmpty(filterText) => x => x.Content != null && x.Content.StartsWith(filterText),
            StringFilter.EndsWith when !string.IsNullOrEmpty(filterText) => x => x.Content != null && x.Content.EndsWith(filterText),
            StringFilter.Equals when !string.IsNullOrEmpty(filterText) => x => x.Content != null && x.Content.Equals(filterText, StringComparison.OrdinalIgnoreCase),
            _ => _ => true
        };
        var queryEmbedding = (await EmbeddingGenerator.GenerateAsync(query)).Vector;
        var results = items.Where(predicate)
            .Where(x => x.Embedding.Length == queryEmbedding.Length)
            .Select(x => (Record: x, Score: (double)TensorPrimitives.CosineSimilarity(queryEmbedding.Span, x.Embedding.Span)))
            .Where(x => x.Score >= minThreshold)
            .OrderByDescending(x => x.Score)
            .Take(topN)
            .ToList();

        Console.WriteLine("\n--- Text Search Results ---\n");
        foreach (var (record, score) in results)
        {
            Console.WriteLine($"Id:  {record.id}");
            Console.WriteLine($"Prompt: {record.Content}");
            Console.WriteLine($"Tag:  {record.Tag}");
            yield return new VectorSearchResult<VectorStoreContextItem>(record, score);
        }
    }

    public async IAsyncEnumerable<VectorStoreContextItem> CreateVectorStoreTextSearch(string collection,
        List<VectorStoreContextItem> items, bool delete = false, bool useCosmos = false)
    {
        if (delete || !_vectorCollections.ContainsKey(collection))
            _vectorCollections[collection] = [];
        var embeddingTasks = items.Select(entry => Task.Run(async () =>
        {
            entry.Embedding = (await EmbeddingGenerator.GenerateAsync(entry.Content!)).Vector;
        }));
        await Task.WhenAll(embeddingTasks);
        foreach (var item in items)
        {
            var existing = _vectorCollections[collection].FindIndex(x => x.id == item.id);
            if (existing >= 0) _vectorCollections[collection][existing] = item;
            else _vectorCollections[collection].Add(item);
            yield return item;
        }
    }

    public async IAsyncEnumerable<VectorStoreContextItem> SaveBatchToMemory(string collection,
        List<VectorStoreContextItem> items,
        MemoryStoreType storeType = MemoryStoreType.InMemory, bool delete = false,
        string model = "text-embedding-3-small")
    {
        _playgroundStore = storeType == MemoryStoreType.SqlLite
            ? await SqliteVectorMemoryStore.ConnectAsync(_configuration!["Sqlite:ConnectionString"]!)
            : new VectorMemoryStore();
        _playgroundTextMemory = new TextMemory(_playgroundStore, ChatClientFactory.CreateEmbeddingGenerator(model));
        if (delete)
        {
            await DeleteCollection(collection);
        }

        foreach (var item in items.Where(x => !string.IsNullOrEmpty(x.Content)))
        {
            var generatedMemoryId = await _playgroundTextMemory.SaveInformationAsync(collection, item.Content ?? "", item.id);
            yield return new VectorStoreContextItem() { MemoryId = generatedMemoryId, Content = item.Content };
        }
    }

    public async Task<List<MemoryQueryResult>> SearchKernelMemory(string query, string collection, int topN = 3,
          double minThreshold = 0.0)
    {
        var records = _playgroundTextMemory!.SearchAsync(collection, query, topN, minThreshold);
        return await records.ToListAsync();
    }

    public async Task<List<string>> GenerateRandomSentances(int count = 10)
    {
        var randomSentences = await PromptRunner.RunAsync(CreateChatClient(),
            $"Generate {count} random and distinct sentences or math equations that are each 30 to 90 characters long. Each sentence and equation should be on its own line. Do not label the lines",
            options: new ChatOptions { MaxOutputTokens = 250, Temperature = 1.2f, TopP = 1.0f });
        return randomSentences.Split("\n").ToList();
    }

    public async Task<List<MemoryResult>> GetItemClustersFromCollection(int numberOfItems = 100,
        string searchTerm = "*", int minpoints = 3, int minCluster = 3,
        DistanceFunction distanceFunction = DistanceFunction.CosineSimilarity, string? collection = null)
    {
        _loggerFactory.LogInformation("Getting clusters from collection {collection}", collection ?? "null");
        if (collection is null)
        {
            _dbScanMemory = new TextMemory(await SqliteVectorMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ConnectionString!), EmbeddingGenerator);
        }
        collection ??= CollectionName.ClusterCollection;
        var memory = _dbScanMemory!;
        List<MemoryQueryResult> items;
        if (searchTerm is "*" or "")
        {
            // A wildcard means "take what is there" - no need to embed a query.
            items = memory.Store.GetAll(collection).Take(numberOfItems)
                .Select(r => new MemoryQueryResult(r.Metadata, 0.0, r.Embedding)).ToList();
        }
        else
        {
            items = await memory.SearchAsync(collection, searchTerm, numberOfItems, 0.0, true).ToListAsync();
        }

        Console.WriteLine($"\n{items.Count} items found\n");

        var itemList = items;
        var deduped = itemList.GroupBy(x => x.Metadata.Text).Select(grp => grp.First()).ToList();
        _loggerFactory.LogInformation("Deduped Items from Vector store: {dedupeCount} from total: {itemCount}",
            deduped.Count, itemList.Count);

        return await ItemClustersFromCollection(minpoints, minCluster, distanceFunction, itemList);
    }

    public async Task<List<MemoryResult>> ItemClustersFromCollection(int minpoints, int minCluster,
        DistanceFunction distanceFunction,
        IEnumerable<MemoryQueryResult> itemList)
    {
        var result = _hdbscanService.ClusterAsync(itemList, minpoints, minCluster, distanceFunction);
        var chatClient = CreateChatClient();
        var writerPlugin = KernelPluginExtensions.CreatePluginFromPromptDirectoryYaml("WriterPlugin", () => chatClient);
        var summaryPlugin = KernelPluginExtensions.CreatePluginFromPromptDirectoryYaml("SummarizePlugin", () => chatClient);
        var clusterTitles = await AddClusterTitles(result, writerPlugin.GetFunction("TitleGen")!, summaryPlugin.GetFunction("SummarizeLong")!);

        foreach (var item in result)
        {
            item.ClusterTitle = clusterTitles[item.Cluster].Item1;
            var itemTagString = clusterTitles[item.Cluster].Item2;

            item.ClusterSummary = itemTagString;
        }
        await File.WriteAllTextAsync($"clusterTitles_{distanceFunction}.json", JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        return result;
    }

    private async Task<Dictionary<int, (string, string)>> AddClusterTitles(IEnumerable<MemoryResult> result,
        AIFunction titleGen, AIFunction summarize)
    {
        var clusterTitles = new Dictionary<int, (string, string)>();
        var clusterGroups = result.GroupBy(x => x.Cluster);
        foreach (var group in clusterGroups)
        {
            var grpItems = group.Select(x => $"{x.Text}\n\n").ToList();
            var documents = string.Join("\n\n", grpItems);
            var tokenCount = StringHelpers.GetTokens(documents);
            var groupTitle = $" {group.FirstOrDefault()?.ClusterTitle}";
            var groupKey = group.Key;
            _loggerFactory.LogInformation("Cluster {groupTitle}\nKey: {key}\nTokenCount {tokenCountLog}\nItem Count: {itemCount}", groupTitle, group.Key, tokenCount, group.Count());
            if (tokenCount > 5000)
            {
                var lines = TextChunker.SplitMarkDownLines(documents, 200, StringHelpers.GetTokens);
                var paragraphs =
                    TextChunker.SplitMarkdownParagraphs(lines, 15000, 300, group.Key.ToString(), StringHelpers.GetTokens);
                var summarySoFar = "";

                foreach (var doc in paragraphs)
                {
                    var index = paragraphs.IndexOf(doc) + 1;
                    var summaryCurrent = await summarize.InvokeToStringAsync(new() { ["input"] = doc, ["summarySoFar"] = summarySoFar });
                    summarySoFar += $"\n{index}. {summaryCurrent}";
                }
                _valueTuple.summaryValue = summarySoFar;
                var title = await titleGen.InvokeToStringAsync(new() { ["articles"] = summarySoFar, ["input"] = summarySoFar });

                _valueTuple.titleResultValue = title;
                clusterTitles.Add(groupKey, _valueTuple);
            }
            else
            {
                var tagResult = await summarize.InvokeToStringAsync(new() { ["articles"] = documents, ["input"] = documents });
                var titleResult = await titleGen.InvokeToStringAsync(new() { ["articles"] = documents, ["input"] = documents });

                _valueTuple.titleResultValue = titleResult;
                _valueTuple.summaryValue = tagResult;
                clusterTitles.Add(groupKey, _valueTuple);
                _loggerFactory.LogInformation("Cluster {groupKey} received Title: {titleResultValue}", groupKey, titleResult);
            }
        }

        return clusterTitles;
    }

    public async Task ChunkAndSaveFileCluster(byte[] file, string filename, FileType fileType = FileType.Pdf,
        string? collection = null)
    {
        _dbScanMemory = new TextMemory(new VectorMemoryStore(), EmbeddingGenerator);
        collection ??= CollectionName.ClusterCollection;
        _loggerFactory.LogInformation("Chunking and saving file {filename} to collection {collection}", filename, collection);

        var paragraphs = await ReadAndChunkFile(file, filename, fileType);
        var index = 0;
        foreach (var paragraph in paragraphs)
        {
            var id = $"{filename}_p{++index}";
            await _dbScanMemory.SaveInformationAsync(collection, paragraph, id, filename);
        }
    }

    private static async Task<List<string>> ReadAndChunkFile(byte[] file, string filename, FileType fileType)
    {
        var sb = new StringBuilder();
        switch (fileType)
        {
            case FileType.Pdf:
                {
                    using var document = PdfDocument.Open(file, new ParsingOptions { UseLenientParsing = true });
                    foreach (var page in document.GetPages())
                    {
                        sb.Append(page.Text);
                    }

                    break;
                }
            case FileType.Text:
                {
                    var stream = new StreamReader(new MemoryStream(file));
                    sb.Append(await stream.ReadToEndAsync());
                    break;
                }
        }

        var textString = sb.ToString();
        var lines = TextChunker.SplitPlainTextLines(textString, 128, StringHelpers.GetTokens);
        return TextChunker.SplitPlainTextParagraphs(lines, 512, 96, filename, StringHelpers.GetTokens);
    }

    #endregion
}

public enum StringFilter
{
    None,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    Equals
}
