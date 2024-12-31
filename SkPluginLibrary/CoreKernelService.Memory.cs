using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Services;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using UglyToad.PdfPig;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Memory and Embeddings (VectorPlaygroundPage.razor, ClusteringPage.razor)

    private (string titleResultValue, string summaryValue) _valueTuple;
    private ISemanticTextMemory? _playgroundTextMemory;
    private ISemanticTextMemory? _dbScanMemory;

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
        MemoryStoreType storeType = MemoryStoreType.InMemory, bool delete = false,
        string model = "text-embedding-3-small")
    {
        _playgroundStore = storeType == MemoryStoreType.SqlLite
            ? await SqliteMemoryStore.ConnectAsync(_configuration["Sqlite:ConnectionString"]!)
            : CreateMemoryStore(storeType);
        _playgroundTextMemory = storeType == MemoryStoreType.SqlLite ? await CreateSqliteMemoryAsync() : CreateSemanticMemory(storeType, model);
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
        var kernel = CreateKernel();
        var randoSkill = kernel.CreateFunctionFromPrompt(
            $"Generate {count} random and distinct sentences or math equations that are each 30 to 90 characters long. Each sentence and equation should be on its own line. Do not label the lines", executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 250, Temperature = 1.2, TopP = 1.0 });
        var randomSentances = await kernel.InvokeAsync(randoSkill);
        var sentances = randomSentances.GetValue<string>()?.Split("\n").ToList() ?? [];
        return sentances;
    }

   
    public async Task<List<MemoryResult>> GetItemClustersFromCollection(int numberOfItems = 100,
        string searchTerm = "*", int minpoints = 3, int minCluster = 3,
        DistanceFunction distanceFunction = DistanceFunction.CosineSimilarity, string? collection = null)
    {
        _loggerFactory.LogInformation("Getting clusters from collection {collection}", collection ?? "null");
        if (collection is null)
        {
            _dbScanMemory = await CreateSqliteMemoryAsync();
        }
        collection ??= CollectionName.ClusterCollection;
        var kernel = CreateKernel();
        var memory = _dbScanMemory;
        var items = await memory!.SearchAsync(collection, searchTerm, numberOfItems, 0.0, true).ToListAsync();
        //await _memoryStore.CreateCollectionAsync(collection);

        Console.WriteLine($"\n{items.Count} items found\n");

        var itemList = items;
        var deduped = itemList.GroupBy(x => x.Metadata.Description).Select(grp => grp.First()).ToList();
        var dedupeCount = deduped.Count;
        var itemCount = itemList.Count;
        _loggerFactory.LogInformation("Deduped Items from Vector store: {dedupeCount} from total: {itemCount}",
            dedupeCount, itemCount);

        return await ItemClustersFromCollection(minpoints, minCluster, distanceFunction, itemList, kernel);
    }

    public async Task<List<MemoryResult>> ItemClustersFromCollection(int minpoints, int minCluster,
        DistanceFunction distanceFunction,
        IEnumerable<MemoryQueryResult> itemList, Kernel kernel)
    {
        var result = _hdbscanService.ClusterAsync(itemList, minpoints, minCluster, distanceFunction);
        var writerPlugin = kernel.ImportPluginFromPromptDirectoryYaml("WriterPlugin");
        var summaryPlugin = kernel.ImportPluginFromPromptDirectoryYaml("SummarizePlugin");
        var kernelArgs = new KernelArguments();
        var clusterTitles = await AddClusterTitles(result, kernelArgs, writerPlugin["TitleGen"], summaryPlugin["SummarizeLong"], kernel);

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
        KernelArguments kernelArgs, KernelFunction titleGen, KernelFunction summarize, Kernel kernel)
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
                    TextChunker.SplitMarkdownParagraphs(lines, 4000, 300, group.Key.ToString(), StringHelpers.GetTokens);
                var summarySoFar = "";
                
                foreach (var doc in paragraphs)
                {
                    var index = paragraphs.IndexOf(doc) + 1;
                    kernelArgs["input"] = doc;
                    kernelArgs["summarySoFar"] = summarySoFar;
                    var summaryCurrent = await kernel.InvokeAsync(summarize, kernelArgs);
                    summarySoFar += $"\n{index}. {summaryCurrent.GetValue<string>()}";                
                }
                _valueTuple.summaryValue = summarySoFar;
                //kernelArgs["input"] = documents;
                kernelArgs["articles"] = summarySoFar;
                var title = await kernel.InvokeAsync(titleGen, kernelArgs);
                
                _valueTuple.titleResultValue = title.GetValue<string>() ?? "";
                clusterTitles.Add(groupKey, _valueTuple);
            }
            else
            {
                kernelArgs["articles"] = documents;
                kernelArgs["input"] = documents;
                var tagResult = await kernel.InvokeAsync(summarize, kernelArgs);
                kernelArgs["input"] = documents;
                var titleResult = await kernel.InvokeAsync(titleGen, kernelArgs);
                
                _valueTuple.titleResultValue = titleResult.GetValue<string>() ?? "";

                var summaryValue = tagResult.GetValue<string>() ?? "";
                _valueTuple.summaryValue = summaryValue;
                clusterTitles.Add(groupKey, _valueTuple);
                _loggerFactory.LogInformation("Cluster {groupKey} received Title: {titleResultValue}", groupKey,
                    titleResult.GetValue<string>() ?? "");
            }            
        }

        return clusterTitles;
    }
       
    public async Task ChunkAndSaveFileCluster(byte[] file, string filename, FileType fileType = FileType.Pdf,
        string? collection = null)
    {
        
        _dbScanMemory = CreateSemanticMemory(MemoryStoreType.InMemory);
        collection ??= CollectionName.ClusterCollection;
        _loggerFactory.LogInformation("Chunking and saving file {filename} to collection {collection}", filename, collection);
       
        var paragraphs = await ReadAndChunkFile(file, filename, fileType);
        var index = 0;
        foreach (var paragraph in paragraphs)
        {
            var id = $"{filename}_p{++index}";
            await _dbScanMemory!.SaveInformationAsync(collection, paragraph, id, filename);
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
        var lines = TextChunker.SplitPlainTextLines(textString, 128, StringHelpers.GetTokens);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 512, 96, filename, StringHelpers.GetTokens);
        return paragraphs;
    }
    
    #endregion
}
