using System.Diagnostics;
using System.Text.Json;
using SkPluginLibrary.Services;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class WebCrawlPlugin
{

    private readonly AIFunction _summarizeWebContent;
    private const int MaxTokens = 1024;
    private readonly IChatClient _chatClient;
    private readonly IWebSearchService? _searchService;


    public WebCrawlPlugin(BingWebSearchService bingWebSearchService)
    {
        _chatClient = ChatClientFactory.Create(AIModel.Gpt41Nano);
        var summarizePlugin = KernelPluginExtensions.CreatePluginFromPromptDirectoryYaml("SummarizePlugin", () => _chatClient);
        _summarizeWebContent = summarizePlugin.GetFunction("CreateOutline")!;
        _searchService = bingWebSearchService;
    }

    public WebCrawlPlugin(GoogleSearchService googleSearchService)
    {
        _chatClient = ChatClientFactory.Create();
        var summarizePlugin = KernelPluginExtensions.CreatePluginFromPromptDirectoryYaml("SummarizePlugin", () => _chatClient);
        _summarizeWebContent = summarizePlugin.GetFunction("CreateOutline")!;
        _searchService = googleSearchService;
    }


    [Description("Extract a web search query from a question")]
    public async Task<string> ExtractWebSearchQuery(string input)
    {
        return await PromptRunner.RunAsync(_chatClient, "Extract terms for a simple web search query from a question. Include no other content\nquestion:{{$input}}",
            new Dictionary<string, object?> { ["input"] = input }, new ChatOptions { MaxOutputTokens = 128, Temperature = 0.0f, TopP = 0 });
    }
    [Description("Retrive the content from a url in markdown format")]
    public async Task<string> ConvertWebToMarkdown([Description("The url to retrieve as markdown")] string url)
    {
        var crawler = new CrawlService(ConsoleLogger.LoggerFactory);
        var markdown = await crawler.CrawlAsync(url);
        return markdown;
    }
    [Description("Search Web, summarize the content of each result and generate citations.")]
    [return: Description("A json collection of objects designed to facilitate web citations including url, title, and content")]
    public async Task<string> SearchAndCiteWeb([Description("Web search query")] string input, [Description("Number of web search results to use")] int resultCount = 3, [Description("Defines the freshness of the results. Options are 'Day', 'Week', or 'Month'")] string? freshness = null)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        var results = await _searchService!.SearchAsync(input, resultCount, freshness) ?? [];
        var scraperTaskList = new List<Task<List<SearchResultItem>>>();
        foreach (var result in results.Take(Math.Min(results.Count, 5)))
        {

            try
            {
                scraperTaskList.Add(ScrapeChunkAndSummarize(result.Url, result.Title, input, result.Snippet));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to scrape text from {result.Url}\n\n{ex.Message}");
            }

        }
       
        var scrapeResults = await Task.WhenAll(scraperTaskList);
        var searchResultItems = scrapeResults.SelectMany(x => x).ToList();
        var resultItems = ReconstituteResults(searchResultItems);
        var searchCiteJson = JsonSerializer.Serialize(resultItems, new JsonSerializerOptions { WriteIndented = true });
        stopWatch.Stop();
        Console.WriteLine($"Search and cite web took {stopWatch.ElapsedMilliseconds} ms");
        return searchCiteJson;
    }

    

    [Description("Run a deep web search and extract content from the search result pages and all embedded links on those pages")]
    [return: Description("A json collection of objects designed to facilitate web citations including url, title, and content")]
    public async Task<string> DeepSearchAndCiteWeb([Description("Web search query")] string input, [Description("Number of web search results to use")] int resultCount = 3,[Description("search depth")] int maxDepth = 2, [Description("Defines the freshness of the results. Options are 'Day', 'Week', or 'Month'")] string? freshness = null)
    {
        var results = await _searchService!.DeepSearchAsync(input, freshness: freshness) ?? [];
        var scraperTaskList = new List<Task<List<SearchResultItem>>>();
        foreach (var result in results.Take(Math.Min(results.Count, 5)))
        {

            try
            {
                scraperTaskList.Add(ScrapeChunkAndSummarize(result.Url, result.Title, input, result.Snippet));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to scrape text from {result.Url}\n\n{ex.Message}");
            }

        }

        var scrapeResults = await Task.WhenAll(scraperTaskList);
        var searchResultItems = scrapeResults.SelectMany(x => x).ToList();
        
        var resultItems = ReconstituteResults(searchResultItems);
        foreach (var item in resultItems)
        {
            item.AdditionalLinks = await new CrawlService(ConsoleLogger.LoggerFactory).CrawlAndExtractUrls(item.Url);

        }
        var searchCiteJson = JsonSerializer.Serialize(resultItems, new JsonSerializerOptions { WriteIndented = true });
        return searchCiteJson;
    }
    private async Task<List<SearchResultItem>> ScrapeChunkAndSummarize(string url, string title, string input, string summary)
    {

        try
        {
            var crawler = new CrawlService(ConsoleLogger.LoggerFactory);
            var text = await crawler.CrawlAsync(url);
            var summarizeWebContent = await SummarizeContent(url, title, input, text, 10000);
            return summarizeWebContent;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to scrape text from {url}\n\n{ex.Message}\n{ex.StackTrace}");
            return [new SearchResultItem(url) { Title = title, Content = summary }];
        }

    }
    private static List<SearchResultItem> ReconstituteResults(List<SearchResultItem> searchResultItems)
    {
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
                    groupItem.Content += $"{item.Content}\n";
                }
                resultItems.Add(groupItem);
            }
            else
            {
                resultItems.Add(new SearchResultItem(group.Key) { Title = group.First().Title, Content = group.First().Content });
            }
        }

        return resultItems;
    }
    private async Task<List<SearchResultItem>> SummarizeContent(string url, string title, string input, string text, int maxPerSegment)
    {
        List<SearchResultItem> scrapeChunkAndSummarize;
        var tokens = StringHelpers.GetTokens(text);
        var count = tokens / maxPerSegment;
        var segments = ChunkIntoSegments(text, Math.Max(count, 1), maxPerSegment, title).ToList();
        Console.WriteLine($"Segment count: {segments.Count}");
        var argList = segments.Select(segment => new Dictionary<string, object?> { ["input"] = segment, ["query"] = input }).ToList();
        var summaryResults = new List<SearchResultItem>();
        foreach (var arg in argList)
        {
            var result = await _summarizeWebContent.InvokeToStringAsync(arg);
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

    

    
}