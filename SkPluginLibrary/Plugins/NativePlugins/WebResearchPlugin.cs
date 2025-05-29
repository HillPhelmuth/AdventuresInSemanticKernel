using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Services;
using static SkPluginLibrary.Models.Helpers.AppConstants;
using static SkPluginLibrary.CoreKernelService;

#pragma warning disable SKEXP0120

namespace SkPluginLibrary.Plugins.NativePlugins;

public class WebResearchPlugin
{
    private KernelFunction _summarizeWebContent;
    private const int MaxTokens = 1024;
    private Kernel _kernel;
    private readonly IWebSearchService? _searchService;
    private readonly IConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private ISemanticTextMemory? _semanticTextMemory;
    public WebResearchPlugin(BingWebSearchService bingWebSearchService, IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        //var kernel = CreateKernel();
        //var summarizePlugin = kernel.ImportPluginFromPromptDirectoryYaml("SummarizePlugin");
        //_summarizeWebContent = summarizePlugin["SummarizeLong"];
        //_kernel = kernel;
        _searchService = bingWebSearchService;
        _configuration = configuration;
        _loggerFactory = loggerFactory;
    }


    [KernelFunction, Description("Extract a web search query from a question")]
    public async Task<string> ExtractWebSearchQuery(Kernel kernel, string input)
    {
        var extractPlugin = kernel.CreateFunctionFromPrompt("Extract terms for a simple web search query from a question. Include no other content\nquestion:{{$input}}", executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 128, Temperature = 0.0, TopP = 0 });
        var result = await kernel.InvokeAsync<string>(extractPlugin, new KernelArguments { { "input", input } });
        return result;
    }
    [KernelFunction, Description("Conduct a deep Web Search, and save content for later retrieval")]
    [return: Description("A json collection of objects designed to facilitate web citations including url, title, and content")]
    public async Task<string> DeepSearchAndSaveWebContent(Kernel kernel, [Description("Web search query")] string query, [Description("Maximum depth of related links to follow")] int maxDepth = 2, [Description("Number of web search results to use")] int resultCount = 3)
    {
        _kernel = kernel.Clone();
        _summarizeWebContent = KernelFunctionFactory.CreateFromPrompt(SummarizePrompt, functionName: "SummarizeLong");

        var results = await _searchService!.DeepSearchAsync(query, resultCount);
        var scraperTaskList = new List<Task<List<SearchResultItem>>>();

        foreach (var result in results.Take(Math.Min(results.Count, 5)))
        {
            try
            {
                var scraped = ScrapeChunkAndSummarize(result.Url, result.Title, query, result.Snippet);


                scraperTaskList.Add(scraped);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing result {result.Url}: {ex.Message}");
            }
        }

        var scrapeResults = await Task.WhenAll(scraperTaskList);
        var searchResultItems = scrapeResults.SelectMany(x => x).ToList();
        var items = await SaveResearchItems(searchResultItems, kernel);
        return JsonSerializer.Serialize(searchResultItems, new JsonSerializerOptions { WriteIndented = true });
    }
    [KernelFunction, Description("Search Web and save content for later retrieval")]
    [return: Description("A json collection of objects ")]
    public async Task<string> SearchAndSaveWeb(Kernel kernel, [Description("Web search query")] string query, [Description("Number of web search results to use")] int resultCount = 3, [Description("Defines the freshness of the results. Options are 'Day', 'Week', or 'Month'")] string? freshness = null)
    {
        _kernel = CreateKernel();

        //var summarizePlugin = kernel.ImportPluginFromPromptDirectoryYaml("SummarizePlugin");
        _summarizeWebContent = KernelFunctionFactory.CreateFromPrompt(SummarizePrompt, functionName: "SummarizeLong");
        var results = await _searchService!.SearchAsync(query, resultCount, freshness) ?? [];
        var scraperTaskList = new List<Task<List<SearchResultItem>>>();
        foreach (var result in results.Take(Math.Min(results.Count, 5)))
        {

            try
            {
                scraperTaskList.Add(ScrapeChunkAndSummarize(result.Url, result.Title, query, result.Snippet));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to scrape text from {result.Url}\n\n{ex.Message}");
            }

        }

        var scrapeResults = await Task.WhenAll(scraperTaskList);
        var searchResultItems = scrapeResults.SelectMany(x => x).ToList();
        var resultItems = new List<SearchResultItem>();
        foreach (var group in searchResultItems.GroupBy(x => x.Url))
        {
            var count = group.Count();
            if (count > 1)
            {
                var index = 1;
                var groupItem = new SearchResultItem(group.Key)
                {
                    Title = group.First().Title,
                    Content = ""
                };
                foreach (var item in group)
                {
                    groupItem.Content += $"{index++}. {item.Content}\n";
                }
                resultItems.Add(groupItem);
            }
            else
            {
                resultItems.Add(new SearchResultItem(group.Key) { Title = group.First().Title, Content = group.First().Content });
            }
        }
        var savedItems = await SaveResearchItems(resultItems, kernel);
        var searchCiteJson = JsonSerializer.Serialize(savedItems, new JsonSerializerOptions { WriteIndented = true });
#if DEBUG
        await File.WriteAllTextAsync("researchJson.json", searchCiteJson);
#endif
        return searchCiteJson;
    }
    public const string CollectionName = WebResearchCollection;
    private async Task<List<string>> SaveResearchItems(List<SearchResultItem> searchResultItems, Kernel kernel)
    {
        var textEmbeddingGeneration = new OpenAITextEmbeddingGenerationService(
            modelId: "text-embedding-3-small",
            apiKey: TestConfiguration.OpenAI.ApiKey!);

        var config = kernel.Services.GetRequiredService<IConfiguration>();
        //var pineconeClient = new PineconeClient(config["Pinecone:ApiKey"]);
        var memory = await CreateMemory(textEmbeddingGeneration, "text-embedding-3-small");
        var contextItems = searchResultItems.Select(x => new ResearchVectorStoreContextItem(x.Url, x.Title, x.Content, WebContentSourceName)).ToList();
        var saveTasks = contextItems.Select(contextItem => memory.SaveInformationAsync($"{CollectionName}-{contextItem.Source}", contextItem.Content!, contextItem.id, contextItem.Title!, JsonSerializer.Serialize(contextItem.Metadata), cancellationToken: new CancellationToken())).ToList();
        var ids = await Task.WhenAll(saveTasks);
        
        return contextItems.Select(item => JsonSerializer.Serialize(new { item.Source, item.Title, item.Link })).ToList();
    }
    private async Task<ISemanticTextMemory> CreateMemory(OpenAITextEmbeddingGenerationService embeddingGenService,
        string? model = null, string? collection = null, bool reset = false)
    {
        model ??= _configuration["OpenAI:EmbeddingModelId"] ?? "";
        collection ??= $"{CollectionName}-{WebContentSourceName}";
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
    [KernelFunction, Description("Find most relevant web content snippets from previously saved web search results for a given prompt or task")]
    public async Task<string> SearchResearchResults(Kernel kernel,[Description("Prompt or task description")] string query, int maxResults = 10)
    {
        var textEmbeddingGeneration = new OpenAITextEmbeddingGenerationService(
            modelId: "text-embedding-3-small",
            apiKey: TestConfiguration.OpenAI.ApiKey!);

        var memory = await CreateMemory(textEmbeddingGeneration, "text-embedding-3-small");
        var searchResults = memory.SearchAsync($"{CollectionName}-{WebContentSourceName}", query, maxResults);
        var sb = new StringBuilder();
        await foreach (var result in searchResults)
        {
            var metadata = JsonSerializer.Deserialize<ResearchMetadata>(result.Metadata.AdditionalMetadata);
            var contextItem = new ResearchVectorStoreContextItem(result.Metadata.Id, result.Metadata.Description, result.Metadata.Text) {Metadata = metadata};
            sb.AppendLine(contextItem.AsContextString());
        }
        return sb.ToString();
    }
    private async Task<List<SearchResultItem>> ScrapeChunkAndSummarize(string url, string title, string input, string summary)
    {

        try
        {
            var crawler = new CrawlService(ConsoleLogger.LoggerFactory);
            var webPageContent = await crawler.CrawlAsync(url);
            var summarizeWebContent = await SummarizeContent(url, title, input, webPageContent);
            return summarizeWebContent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to scrape text from {url}\n\n{ex.Message}\n{ex.StackTrace}");
            return [new SearchResultItem(url) { Title = title, Content = summary }];
        }

    }
    private async Task<List<SearchResultItem>> SummarizeContent(string url, string title, string input, string text)
    {
        List<SearchResultItem> scrapeChunkAndSummarize;
        var tokens = StringHelpers.GetTokens(text);
        var count = tokens / 8192;
        var segments = ChunkIntoSegments(text, Math.Max(count, 1), 8192, title).ToList();
        Console.WriteLine($"Segment count: {segments.Count}");
        var argList = segments.Select(segment => new KernelArguments { ["input"] = segment, ["query"] = input }).ToList();
        var summaryResults = new List<SearchResultItem>();
        foreach (var arg in argList)
        {
            var result = await _kernel.InvokeAsync<string>(_summarizeWebContent, arg);
            summaryResults.Add(new SearchResultItem(url) { Title = title, Content = result });
        }

        scrapeChunkAndSummarize = summaryResults;
        return summaryResults;
    }

    private static IEnumerable<string> ChunkIntoSegments(string text, int segmentCount, int maxPerSegment = 4096, string description = "", bool ismarkdown = true)
    {
        var total = StringHelpers.GetTokens(text);
        var perSegment = total / segmentCount;
        var totalPerSegment = perSegment > maxPerSegment ? maxPerSegment : perSegment;
        List<string> paragraphs;
        if (ismarkdown)
        {
            var lines = TextChunker.SplitMarkDownLines(text, 200, StringHelpers.GetTokens);
            paragraphs = TextChunker.SplitMarkdownParagraphs(lines, totalPerSegment, 0, description, StringHelpers.GetTokens);
        }
        else
        {
            var lines = TextChunker.SplitPlainTextLines(text, 200, StringHelpers.GetTokens);
            paragraphs = TextChunker.SplitPlainTextParagraphs(lines, totalPerSegment, 0, description, StringHelpers.GetTokens);
        }
        return paragraphs.Take(segmentCount);
    }

    private async Task<string> DescribeImageAsync(string imageUrl)
    {
        var history = new ChatHistory
        {
            new(AuthorRole.User,
            [
                new ImageContent(new Uri(imageUrl)),
                new TextContent("describe and summarize image")
            ])
        };
        var chatService = _kernel.Services.GetRequiredService<IChatCompletionService>();
        var result = await chatService.GetChatMessageContentAsync(history);
        return result.Content;
    }
    public const string SummarizePrompt = """
                                           Create a detailed and comprehensive outline of a given document by structuring and paraphrasing its contents. The outline should retain all relevant information and present the ideas clearly and cohesively while maintaining the original context. Consider the original user query `{{ $query }}` for context in this outline.
                                           
                                           # Steps
                                           
                                           1. **Read the Document Thoroughly**: Understand the main ideas, key points, and supporting details.
                                           2. **Identify Key Information**: Select important facts, concepts, and arguments that are essential to the understanding of the document.
                                           3. **Organize Content into Sections**: Create sections and subsections reflecting the structure of the document. 
                                           4. **Paraphrase Content**: Rewrite the identified information of each section and sub-section in your own words, ensuring to maintain the original meaning and context.
                                           5. **Provide Subheadings**: Use subheadings to clearly define each section, ensuring logical progression and clarity.
                                           6. **Review and Revise**: Ensure the outline is coherent, free of errors, and accurately reflects the source material.
                                           
                                           # Output Format
                                           
                                           - The outline should include all crucial points structured logically with clear subheadings.
                                           - The language should be clear, concise, and free of unnecessary jargon or filler.
                                           - Outlines should succinctly cover the full scope of the document while adhering to the document's original structure.
                                           
                                           # Notes
                                           
                                           - Ensure that no significant details are omitted.
                                           - Organize information in a way that maintains relevance and coherence.
                                           - For large and complex documents, make use of detailed subheadings to enhance readability and understanding.
                                           - Incorporate `{{ $query }}` to provide context and focus for the outline.
                                           
                                           Outline this:
                                           {{ $input }}
                                           """;

    private const string? WebContentSourceName = "WebContent";
}

