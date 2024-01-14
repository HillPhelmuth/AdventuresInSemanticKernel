using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Services;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.OpenApi;

namespace SkPluginLibrary.Plugins;

public class WebCrawlPlugin
{

    private readonly KernelFunction _summarizeWebContent;
    private const int MaxTokens = 1024;
    private const string Collection = "WikiCrawlCollection";
    private readonly Kernel _kernel;
    private readonly BingWebSearchService? _searchService;
    private ISemanticTextMemory? _semanticTextMemory;
    public WebCrawlPlugin(Kernel kernel)
    {
        var tryis = kernel.ImportPluginFromPromptDirectory(RepoFiles.PluginDirectoryPath, "SummarizePlugin").TryGetFunction("QueryNoteGen", out _summarizeWebContent);
        _kernel = kernel;
    }

    public WebCrawlPlugin(Kernel kernel, BingWebSearchService bingWebSearchService)
    {
        var tryis = kernel.ImportPluginFromPromptDirectory(RepoFiles.PluginDirectoryPath, "SummarizePlugin").TryGetFunction("Summarize", out _summarizeWebContent);
        Console.WriteLine($"SummarizePlugin loaded:{tryis}");
        _kernel = kernel;
        _searchService = bingWebSearchService;

    }

    public WebCrawlPlugin(Kernel kernel, BingWebSearchService bingWebSearchService,
        ISemanticTextMemory? semanticTextMemory)
    {
        var tryit = kernel.ImportPluginFromPromptDirectory(RepoFiles.PluginDirectoryPath, "SummarizePlugin").TryGetFunction("Summarize", out _summarizeWebContent);
        Console.WriteLine($"SummarizePlugin loaded:{tryit}");
        _kernel = kernel;
        _searchService = bingWebSearchService;
        _semanticTextMemory = semanticTextMemory;
    }
    [KernelFunction, Description("Crawl Web url and summarize the content found")]
    public async Task<string> CrawlAndSummarize([Description("Url to crawl")] string url, SKContext context)
    {

        var crawlService = new CrawlService();
        var text = await crawlService.CrawlAsync(url);
        Console.WriteLine($"------------------Crawl Text--------------\n{text}");
        var output = await _kernel.InvokeAsync(_summarizeWebContent, new KernelArguments() { { "input", text } });
        return output.Result();


    }
    [KernelFunction, Description("Search Web url and summarize the content found")]
    public async Task<string> SearchAndSummarize([Description("Web search query")] string input, [Description("Number of web search results to use")] int resultCount = 1)
    {
        return await SearchAndCiteWeb(input, resultCount);
    }
    [KernelFunction, Description("Extract a web search query from a question")]
    public async Task<string> ExtractWebSearchQuery(string input)
    {
        var extractPlugin = _kernel.CreateFunctionFromPrompt("Extract terms for a simple web search query from a question. Include no other content\nquestion:{{$input}}", executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 128, Temperature = 0.0, TopP = 0 });
        var result = await _kernel.InvokeAsync(extractPlugin, new KernelArguments() { { "input", input } });
        return result.Result();
    }
    [KernelFunction, Description("Search Web, summarize the content of each result and generate citations. Result facilitates citations by including url, title, and content properties")]
    public async Task<string> SearchAndCiteWeb([Description("Web search query")] string input, [Description("Number of web search results to use")] int resultCount = 2)
    {
        var results = await _searchService!.SearchAsync(input, resultCount) ?? new List<BingSearchResult>();
        var scrapeList = new List<Task<List<SearchResultItem>>>();
        foreach (var result in results.Take(Math.Min(results.Count, 5)))
        {

            try
            {
                scrapeList.Add(ScrapeChunkAndSummarize(result.Url, result.Name, input));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to scrape text from {result.Url}\n\n{ex.Message}");
            }
            
        }

        var scrapeResults = await Task.WhenAll(scrapeList);
        var searchCiteJson = JsonSerializer.Serialize(scrapeResults.SelectMany(x => x), new JsonSerializerOptions { WriteIndented = true });
        return searchCiteJson;
    }
    
    private static string ChunkToMaxTokens(string text, int maxTokens = 8192, string description = "")
    {
        var lines = TextChunker.SplitMarkDownLines(text, 200, StringHelpers.GetTokens);
        var paragraphs = TextChunker.SplitMarkdownParagraphs(lines, maxTokens, 0, description, StringHelpers.GetTokens);
        return paragraphs?.FirstOrDefault() ?? text;
    }

    private static IEnumerable<string> ChunkIntoSegments(string text, int segmentCount, int maxPerSegment = 4096, string description = "", bool ismarkdown = true)
    {
        var total = StringHelpers.GetTokens(text);
        var totalPerSegment = Math.Min(total / Math.Min(segmentCount,1), maxPerSegment);
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
    private static IEnumerable<string> ChunkForMemory(string text, string description = "")
    {
        var lines = TextChunker.SplitMarkDownLines(text, 200, StringHelpers.GetTokens);
        var paragraphs = TextChunker.SplitMarkdownParagraphs(lines, 512, 100, description, StringHelpers.GetTokens);
        return paragraphs;

    }
    private KernelPlugin? _scraperPlugin;
    private async Task<List<SearchResultItem>> ScrapeChunkAndSummarize(string url, string title, string input)
    {

        try
        {
            _scraperPlugin ??= await _kernel.ImportPluginFromOpenApiAsync("ScraperPlugin",
                Path.Combine(RepoFiles.ApiPluginDirectoryPath,"ScraperApiPlugin", "openapi.json"),
                new OpenApiFunctionExecutionParameters { EnableDynamicPayload = true, IgnoreNonCompliantErrors = true });
            var args = new KernelArguments
            {
                ["url"] = url
            };
            var kernelResult = await _kernel.InvokeAsync(_scraperPlugin["scrape"], args);
            var tokens = StringHelpers.GetTokens(kernelResult.Result());
            var count = tokens / 2048;
            var segments = ChunkIntoSegments(kernelResult.Result(), count, 2048, title, false);
            var summaryTasks = segments.Select(segment => new KernelArguments {["input"] = segment, ["query"] = input}).Select(segmentArgs => _kernel.InvokeAsync(_summarizeWebContent, segmentArgs)).ToList();

            var summaryResults = await Task.WhenAll(summaryTasks);
            return summaryResults.Select(x => new SearchResultItem(url){Title = title, Content = x.Result()}).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to scrape text from {url}\n\n{ex.Message}\n{ex.StackTrace}");
            return new List<SearchResultItem>();
        }

    }
}

public record SearchResultItem(string Url)
{
    [JsonPropertyName("content"), JsonPropertyOrder(3)]
    public string? Content { get; set; }
    [JsonPropertyName("title"), JsonPropertyOrder(1)]
    public string? Title { get; set; }
    [JsonPropertyName("url"), JsonPropertyOrder(2)]
    public string Url { get; set; } = Url;
}
