using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Services;
using static SkPluginLibrary.Models.Helpers.AppConstants;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class YouTubeResearchPlugin
{
    private readonly YouTubeSearch _youTubeSearch;
    private Kernel? _kernel;
    private const string CollectionName = WebResearchCollection;
    private const string YoutubeSourceName = "YouTube";
    private IConfiguration _configuration;
    private ILoggerFactory _loggerFactory;
    public YouTubeResearchPlugin(IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _youTubeSearch = new YouTubeSearch(configuration["YouTubeSearch:ApiKey"]!);
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }

    [KernelFunction, Description("Search YouTube for videos. Outputs a json array of youtube video descriptions and Ids")]
    [return: Description("YouTube search results")]
    public async Task<string> SearchVideos([Description("Youtube search query")] string query,
        [Description("Number of results")] int count = 10)
    {
        List<YouTubeSearchResult> results = await _youTubeSearch.Search(query, count);
        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });

    }
    private HttpClient _httpClient = new HttpClient();
    [KernelFunction, Description("Transcribe and save YouTube videos for future research")]
    public async Task<string> TranscribeAndSaveVideos([Description("One sentance description of the research task or query")] string researchTaskOrQuery, [Description("Youtube search results most relevant to the research task")] YouTubeSearchResults results, string language = "en")
    {
        var researchItems = new List<SearchResultItem>();
        foreach (var result in results.Results)
        {
            var transcription = await _youTubeSearch.TranscribeVideo(result.Id, language);
            var lines = TextChunker.SplitPlainTextLines(transcription, 200, StringHelpers.GetTokens);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 1096, 200, researchTaskOrQuery, tokenCounter: StringHelpers.GetTokens);
            var transcriptionSegments = paragraphs.Select((segment, index) => new SearchResultItem(result.Id) { Content = segment, Title =
                $"{result.Description} - {index}"
            }).ToList();
            researchItems.AddRange(transcriptionSegments);
            
        }
        var items = await SaveResearchItems(researchItems, _kernel);
        return "Transcriptions saved!";
    }
    [KernelFunction, Description("Find most relevant Youtube video transcription sections from previously saved search results for a given prompt or task")]
    public async Task<string> SearchResearchResults(Kernel kernel, [Description("Prompt or task description")] string query, int maxResults = 10)
    {
       var textEmbeddingGeneration = new OpenAITextEmbeddingGenerationService(
            modelId: "text-embedding-3-small",
            apiKey: TestConfiguration.OpenAI.ApiKey!);
        var memory = await CreateMemory(textEmbeddingGeneration, "text-embedding-3-small");
        var searchResults = memory.SearchAsync($"{CollectionName}-{YoutubeSourceName}", query, maxResults);
        var sb = new StringBuilder();
        await foreach (var result in searchResults)
        {
            var metadata = JsonSerializer.Deserialize<ResearchMetadata>(result.Metadata.AdditionalMetadata);
            var contextItem = new ResearchVectorStoreContextItem(result.Metadata.Id, result.Metadata.Description, result.Metadata.Text) { Metadata = metadata };
            sb.AppendLine(contextItem.AsContextString());
        }
        return sb.ToString();
    }
    private async Task<List<string>> SaveResearchItems(List<SearchResultItem> searchResultItems, Kernel kernel)
    {
        var textEmbeddingGeneration = new OpenAITextEmbeddingGenerationService(
            modelId: "text-embedding-3-small",
            apiKey: TestConfiguration.OpenAI.ApiKey!);

        var config = kernel.Services.GetRequiredService<IConfiguration>();
        //var pineconeClient = new PineconeClient(config["Pinecone:ApiKey"]);
        var memory = await CreateMemory(textEmbeddingGeneration, "text-embedding-3-small");
        var contextItems = searchResultItems.Select(x => new ResearchVectorStoreContextItem(x.Url, x.Title, x.Content, YoutubeSourceName)).ToList();
        var saveTasks = contextItems.Select(contextItem => memory.SaveInformationAsync($"{CollectionName}-{contextItem.Source}", contextItem.Content!, contextItem.id, contextItem.Title!, JsonSerializer.Serialize(contextItem.Metadata), cancellationToken: new CancellationToken())).ToList();
        var ids = await Task.WhenAll(saveTasks);

        return contextItems.Select(item => JsonSerializer.Serialize(new { item.Source, item.Title, item.Link })).ToList();
    }
    private async Task<ISemanticTextMemory> CreateMemory(OpenAITextEmbeddingGenerationService embeddingGenService,
        string? model = null, string? collection = null, bool reset = false)
    {
        model ??= _configuration["OpenAI:EmbeddingModelId"] ?? "";
        collection ??= $"{CollectionName}-{YoutubeSourceName}"; ;
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
        memoryBuilder.WithLoggerFactory(_loggerFactory);
        memoryBuilder.WithMemoryStore(sqliteConnect);
        memoryBuilder.WithTextEmbeddingGeneration(embeddingGenService!);
        return memoryBuilder.Build();
    }
}