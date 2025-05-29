using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Services;
using static SkPluginLibrary.Models.Helpers.AppConstants;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class ArxivResearchPlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
{
    private readonly ArxivApiService _arxivApiService = new(loggerFactory);
    [KernelFunction, Description("Searches arXiv for relevant papers")]
    [return: Description("Metadata for the papers that meet the search criteria")]
    public async Task<string> SearchArxiv(
        [Description("The search query")] string query,
        [Description("The number of results to return")] int numResults = 20)
    {
        var searchResults = await _arxivApiService.QueryAsync(new ArxivQueryParameters { SearchQuery = query, MaxResults = numResults });
        return searchResults?.ToString() ?? "Search Failed!";

    }
    //[KernelFunction, Description("Full content for selected arXiv paper")]
    public async Task<string> GetArxivPaper([Description("The arXiv metadata for the paper")] ArxivEntry arxivEntry)
    {
        var pdf = await _arxivApiService.GetContentAsync(arxivEntry);
        return pdf ?? "Failed to retrieve PDF!";
    }
    [KernelFunction, Description("Saves most relevant arXiv papers for later retrieval")]
    public async Task<string> SaveArxivPapers(Kernel kernel,
        [Description("The arXiv metadata for the papers to save")] List<ArxivEntry> arxivEntries)
    {
        foreach (var entry in arxivEntries)
        {
            var pdf = await _arxivApiService.GetContentAsync(entry);
            var segments = ChunkIntoSegments(pdf, 1096, entry.Title, false);
            var url = entry.PdfLink;
            var title = entry.Title;
            var searchResultItems = segments.Select(segment => new SearchResultItem(url) { Content = segment, Title = title }).ToList();
            var items = await SaveResearchItems(searchResultItems, kernel);
            var sb = new StringBuilder();
            sb.AppendLine("Full text of the following articles successfully saved.");
            var count = 1;
            foreach (var item in items)
            {
                sb.AppendLine($"{count++}.\n{item}");
            }
        }
        return "Full content of articles saved successfully!";
    }

    [KernelFunction,
     Description("Find most relevant content from previously saved arXiv papers for a given prompt or research task")]
    public async Task<string> SearchResearchResults(Kernel kernel, [Description("Prompt or research task description")] string query, int maxResults = 10)
    {
        
        var textEmbeddingGeneration = new OpenAITextEmbeddingGenerationService(
            modelId: "text-embedding-3-small",
            apiKey: TestConfiguration.OpenAI.ApiKey!);
        var memory = await CreateMemory(textEmbeddingGeneration);
        var searchResults = memory.SearchAsync($"{CollectionName}-{ArxivContentSourceName}", query, maxResults);
        var sb = new StringBuilder();
        await foreach (var result in searchResults)
        {
            var metadata = JsonSerializer.Deserialize<ResearchMetadata>(result.Metadata.AdditionalMetadata);
            var contextItem = new ResearchVectorStoreContextItem(result.Metadata.Id, result.Metadata.Description, result.Metadata.Text) { Metadata = metadata };
            sb.AppendLine(contextItem.AsContextString());
        }
        return sb.ToString();
    }
    
    public const string CollectionName = WebResearchCollection;
    private const string? ArxivContentSourceName = "ArXivContent";

    private async Task<List<string>> SaveResearchItems(List<SearchResultItem> searchResultItems, Kernel kernel)
    {
        var textEmbeddingGeneration = new OpenAITextEmbeddingGenerationService(
            modelId: "text-embedding-3-small",
            apiKey: TestConfiguration.OpenAI.ApiKey!);
        //DbConnection connection = new SqliteConnection(@".\Data\PromptLab.db");
        var config = kernel.Services.GetRequiredService<IConfiguration>();
        var memory = await CreateMemory(textEmbeddingGeneration);
        var contextItems = searchResultItems.Select(x => new ResearchVectorStoreContextItem(x.Url, x.Title, x.Content, ArxivContentSourceName)).ToList();
        var saveTasks = contextItems.Select(contextItem => memory.SaveInformationAsync($"{CollectionName}-{ArxivContentSourceName}", contextItem.Content!, contextItem.id, contextItem.Title!, JsonSerializer.Serialize(contextItem.Metadata), cancellationToken: new CancellationToken())).ToList();
        var ids = await Task.WhenAll(saveTasks);

        var result = new List<string>();
        foreach (var item in contextItems)
        {
            result.Add(JsonSerializer.Serialize(new { item.Source, item.Title, item.Link }, new JsonSerializerOptions() { WriteIndented = true }));
        }
        return result;
    }
    private async Task<ISemanticTextMemory> CreateMemory(ITextEmbeddingGenerationService embeddingGenService, string? collection = null, bool reset = false)
    {
        collection ??= $"{CollectionName}-{ArxivContentSourceName}";
        var sqliteConnect = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite.ConnectionString);
        var collections = await sqliteConnect.GetCollectionsAsync().ToListAsync();
        if (!collections.Contains(collection))
        {
            await sqliteConnect.CreateCollectionAsync(collection);
        }
        else if (reset)
        {
            await sqliteConnect.DeleteCollectionAsync(collection);
            await sqliteConnect.CreateCollectionAsync(collection);
        }

        var memoryBuilder = new MemoryBuilder();
        memoryBuilder.WithLoggerFactory(loggerFactory);
        memoryBuilder.WithMemoryStore(sqliteConnect);
        memoryBuilder.WithTextEmbeddingGeneration(embeddingGenService!);
        return memoryBuilder.Build();
    }
    private static IEnumerable<string> ChunkIntoSegments(string text, int maxPerSegment = 1096, string description = "", bool ismarkdown = true)
    {
        List<string> paragraphs;
        if (ismarkdown)
        {
            var lines = TextChunker.SplitMarkDownLines(text, 200, StringHelpers.GetTokens);
            paragraphs = TextChunker.SplitMarkdownParagraphs(lines, maxPerSegment, 200, description, StringHelpers.GetTokens);
        }
        else
        {
            var lines = TextChunker.SplitPlainTextLines(text, 200, StringHelpers.GetTokens);
            paragraphs = TextChunker.SplitPlainTextParagraphs(lines, maxPerSegment, 200, description, StringHelpers.GetTokens);
        }
        return paragraphs;
    }
}