using AI.Dev.OpenAI.GPT;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.Memory.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Plugins.Document.OpenXml;
using UglyToad.PdfPig;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Memory and Embeddings (AddEmbeddings.razor, ClusteringPage.razor)

    private (string titleResultValue, string tagResultValue) _valueTuple;
    private ISemanticTextMemory? _playgroundTextMemory;

  
    private async Task<string> SaveToNewKernelMemory(ISemanticTextMemory? memory, string id, string item, string collection)
    {
        return await memory.SaveInformationAsync(collection, item, id);
    }

    private async Task DeleteCollection(string collection)
    {
        if (await _playgroundStore.DoesCollectionExistAsync(collection))
            await _playgroundStore.DeleteCollectionAsync(collection);
    }

    public async IAsyncEnumerable<ContextItem> SaveBatchToMemory(string collection, List<ContextItem> items,
        MemoryStoreType storeType = MemoryStoreType.InMemory, bool delete = false)
    {
        _playgroundStore = storeType == MemoryStoreType.SqlLite
            ? await SqliteMemoryStore.ConnectAsync(_configuration["ConnectionStrings:SqlLite2"]!)
            : CreateMemoryStore(storeType);
        _playgroundTextMemory = storeType == MemoryStoreType.SqlLite ? await CreateSqliteMemoryAsync() : CreateSemanticMemory(storeType);
        if (delete)
        {
            await DeleteCollection(collection);
        }


        foreach (var item in items.Where(x => !string.IsNullOrEmpty(x.Prompt)))
        {
            var generatedMemoryId = await SaveToNewKernelMemory(_playgroundTextMemory, item.Id, item.Prompt ?? "", collection);
            yield return new ContextItem(item.Id) { MemoryId = generatedMemoryId, Prompt = item.Prompt };
        }
    }

    public async Task<List<MemoryQueryResult>> SearchKernelMemory(string query, string collection, int topN = 3,
          double minThreshold = 0.0)
    {
        var records = _playgroundTextMemory.SearchAsync(collection, query, topN, minThreshold);

        return await records.ToListAsync();
    }

    public async Task<List<string>> GenerateRandomSentances(int count = 10)
    {
        var kernel = CreateKernel("gpt-3.5-turbo");
        var randoSkill = kernel.CreateSemanticFunction(
            $"Generate {count} random and distinct sentances or math equations that are each 30 to 60 characters long. Each sentance and equation should be on its own line. Do not label the lines", requestSettings: new OpenAIRequestSettings { MaxTokens = 250, Temperature = 1.2, TopP = 1.0 });
        var randomSentances = await kernel.RunAsync(randoSkill);
        var sentances = randomSentances.GetValue<string>()?.Split("\n").ToList() ?? new List<string>();
        return sentances;
    }

   
    public async Task<List<MemoryResult>> GetItemClustersFromCollection(int numberOfItems = 100,
        string searchTerm = "*", int minpoints = 3, int minCluster = 3,
        DistanceFunction distanceFunction = DistanceFunction.CosineSimilarity, string? collection = null)
    {
        collection ??= CollectionName.ClusterCollection;
        var kernel = await CreateSqliteKernel();
        var memory = await CreateSqliteMemoryAsync();
        var items = await memory.SearchAsync(collection, searchTerm, numberOfItems, 0.0, true).ToListAsync();
        await _memoryStore.CreateCollectionAsync(collection);

        Console.WriteLine($"\n{items.Count} items found\n");

        var itemList = items.Select(x => MemoryRecord.FromMetadata(x.Metadata, x.Embedding.Value)).Take(numberOfItems)
            .ToList();
        var deduped = itemList.GroupBy(x => x.Metadata.Description).Select(grp => grp.First()).ToList();
        var dedupeCount = deduped.Count;
        var itemCount = itemList.Count;
        _loggerFactory.LogInformation("Deduped Items from Vector store: {dedupeCount} from total: {itemCount}",
            dedupeCount, itemCount);

        var result = _hdbscanService.ClusterAsync(itemList, minpoints, minCluster, distanceFunction);
        var writerPlugin = kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "WriterPlugin");
        var summarySkill = kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "SummarizePlugin");

        var context = kernel.CreateNewContext();
        var clusterTitles = await AddClusterTitles(result, context, writerPlugin["TitleGen"], summarySkill["Topics"]);

        foreach (var item in result)
        {
            item.ClusterTitle = clusterTitles[item.Cluster].Item1;
            var itemTagString = clusterTitles[item.Cluster].Item2;
            try
            {
                var topicList = JsonSerializer.Deserialize<Topic>(itemTagString);
                itemTagString = string.Join(", ", topicList.Topics);
            }
            catch (Exception ex)
            {
                _loggerFactory.LogInformation("Failed to deserialize topics: {ex}", ex.Message);
            }

            item.TagString = itemTagString;
        }

        return result;
    }

    private class Topic
    {
        [JsonPropertyName("topics")] public List<string> Topics { get; set; }
    }

    private async Task<Dictionary<int, (string, string)>> AddClusterTitles(IEnumerable<MemoryResult> result,
        SKContext context, ISKFunction titleGen, ISKFunction topicGen)
    {
        var clusterTitles = new Dictionary<int, (string, string)>();
        var clusterGroups = result.GroupBy(x => x.Cluster);
        foreach (var group in clusterGroups)
        {
            var grpItems = group.Select(x => $"Title:{x.Title}\n\n{x.Text}").ToList();
            var documents = string.Join("\n\n", grpItems);
            var tokenCount = GPT3Tokenizer.Encode(documents).Count;
            var tokenCountLog = $"{group.Key}\nToken Count: {tokenCount}";
            var groupTitle = $" {group.FirstOrDefault()?.ClusterTitle}";
            _loggerFactory.LogInformation("Cluster {groupTitle}\nKey: {tokenCountLog}", groupTitle, tokenCountLog);
            if (tokenCount > 12000)
            {
                var lines = TextChunker.SplitMarkDownLines(documents, 100, StringHelpers.GetTokens);
                var paragraphs =
                    TextChunker.SplitMarkdownParagraphs(lines, 12000, 0, group.Key.ToString(), StringHelpers.GetTokens);
                documents = paragraphs[0];
                var count2 = GPT3Tokenizer.Encode(documents).Count;
                _loggerFactory.LogInformation("Cluster {groupTitle} token reduced to {count2}", groupTitle, count2);
            }

            context.Variables["articles"] = documents;
            context.Variables["input"] = documents;
            var titleResult = await titleGen.InvokeAsync(context);
            var groupKey = group.Key;
            _valueTuple.titleResultValue = titleResult.GetValue<string>() ?? "";

            var tagResult = await topicGen.InvokeAsync(context);
            var tagResultValue = tagResult.GetValue<string>() ?? "";
            _valueTuple.tagResultValue = tagResultValue;
            clusterTitles.Add(groupKey, _valueTuple);
            _loggerFactory.LogInformation("Cluster {groupKey} received Title: {titleResultValue}", groupKey,
                titleResult.GetValue<string>() ?? "");
        }

        return clusterTitles;
    }
    private WordDocumentConnector _wordDocumentConnector = new();
    public async Task ChunkAndSaveFileCluster(byte[] file, string filename, FileType fileType = FileType.Pdf)
    {
        var collection = CollectionName.ClusterCollection;
        var sqlLiteMemory = await CreateSqliteMemoryAsync();
        var hasCollection = await _sqliteStore.DoesCollectionExistAsync(collection);
        if (!hasCollection)
            await _sqliteStore.CreateCollectionAsync(collection);
        var paragraphs = await ReadAndChunkFile(file, filename, fileType);
        var index = 0;
        foreach (var paragraph in paragraphs)
        {
            var id = $"{filename}_p{++index}";
            await sqlLiteMemory.SaveInformationAsync(collection, paragraph, id, filename);
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
                        var pageText = page.Text;
                        sb.Append(pageText);
                    }

                    break;
                }
            case FileType.Text:
                {
                    var stream = new StreamReader(new MemoryStream(file));
                    var text = await stream.ReadToEndAsync();
                    sb.Append(text);
                    break;
                }
        }

        var textString = sb.ToString();
        var lines = TextChunker.SplitPlainTextLines(textString, 64, StringHelpers.GetTokens);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 512, 128, filename, StringHelpers.GetTokens);
        return paragraphs;
    }
    
    #endregion
}