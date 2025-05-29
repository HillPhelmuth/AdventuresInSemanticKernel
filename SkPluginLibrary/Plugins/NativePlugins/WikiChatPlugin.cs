using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Services;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class WikiChatPlugin
{
    private Kernel _kernel;
    private readonly KernelFunction _summarizeWebContent;
    public WikiChatPlugin()
    {
        _kernel = CreateKernel();
        var summarizePlugin = _kernel.ImportPluginFromPromptDirectoryYaml("SummarizePlugin");
        _summarizeWebContent = summarizePlugin["CreateOutline"];
    }
    [KernelFunction, Description("Search Wikipedia, summarize the content of each result and generate citations.")]
    [return: Description("A json collection of objects designed to facilitate Wikipedia citations including url, title, and content")]
    public async Task<string> SearchAndCiteWikipedia([Description("Wikipedia search query")] string input, [Description("Number of Wikipedia search results to use")] int resultCount = 2)
    {
        var searchUrl = SearchRequestString(input, resultCount);
        var client = new HttpClient();
        var pages = await client.GetFromJsonAsync<WikiSearchResult>(searchUrl);
        var searchAndScrapeTasks = pages.Pages.Select(page => ScrapeChunkAndSummarize(page.PageRequestString, page.Title, input, $"{page.Description}\n{page.Excerpt}")).ToList();
        var results = new ConcurrentBag<List<SearchResultItem>>();
        await Parallel.ForEachAsync(searchAndScrapeTasks, new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (task, _) =>
        {
            var result = await task;
            Console.WriteLine($"Scraped {result.Count} segments");
            results.Add(result);
            await Task.Delay(500);
        });
        var searchCiteJson = JsonSerializer.Serialize(results.SelectMany(x => x), new JsonSerializerOptions { WriteIndented = true });
        return searchCiteJson;
    }
    private async Task<List<SearchResultItem>> ScrapeChunkAndSummarize(string url, string title, string input, string summary)
    {

        try
        {
            var crawler = new CrawlService(ConsoleLogger.LoggerFactory);
            var text = await crawler.CrawlAsync(url);
            var tokens = StringHelpers.GetTokens(text);
            var count = tokens / 2048;
            var segments = ChunkIntoSegments(text, Math.Max(count,1), 2048, title, false);
            var summaryTasks = segments.Select(segment => new KernelArguments { ["input"] = segment, ["query"] = input }).Select(segmentArgs => _kernel.InvokeAsync(_summarizeWebContent, segmentArgs)).ToList();

            var summaryResults = new ConcurrentBag<FunctionResult>()/* await Task.WhenAll(summaryTasks)*/;

            await Parallel.ForEachAsync(summaryTasks, new ParallelOptions { MaxDegreeOfParallelism = 2 }, async (task, _) =>
            {
                var result = await task;
                summaryResults.Add(result);
                // ReSharper disable once MethodSupportsCancellation
#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
                await Task.Delay(500);
#pragma warning restore CA2016 // Forward the 'CancellationToken' parameter to methods
            });
            return summaryResults.Select(x => new SearchResultItem(url) { Title = title, Content = x.Result() }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to scrape text from {url}\n\n{ex.Message}\n{ex.StackTrace}");
            return [new SearchResultItem(url) { Title = title, Content = summary }];
        }

    }
    

    public static string SearchRequestString(string searchQuery, int maxResults)
    {
        var searchTerm = Uri.EscapeDataString(searchQuery ?? string.Empty);
        return $"https://en.wikipedia.org/w/rest.php/v1/search/page?q={searchTerm}&limit={maxResults}";
    }
    
    public static string PageRequestString(string pageKey)
    {
        const string wikiBaseUrl = "https://en.wikipedia.org/w/rest.php/v1/page/";
        var requestUri = $"{wikiBaseUrl}{pageKey}/html";
        return requestUri;
    }

    private static IEnumerable<string> ChunkIntoSegments(string text, int segmentCount, int maxPerSegment = 4096, string description = "", bool ismarkdown = true)
    {
        var total = StringHelpers.GetTokens(text);
        var perSegment = total/segmentCount;
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
public class WikiSearchResult
{
    [JsonPropertyName("pages")]
    public List<WikiPage>? Pages { get; set; }
}
public class WikiPage
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("excerpt")]
    public string? Excerpt { get; set; }

    [JsonPropertyName("matched_title")]
    public string? MatchedTitle { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("thumbnail")]
    public Thumbnail? Thumbnail { get; set; }
    public string? PageRequestString => $"https://en.wikipedia.org/w/rest.php/v1/page/{Key}/html";
}

public class Thumbnail
{
    [JsonPropertyName("mimetype")]
    public string? Mimetype { get; set; }

    [JsonPropertyName("size")]
    public object? Size { get; set; }

    [JsonPropertyName("width")]
    public long Width { get; set; }

    [JsonPropertyName("height")]
    public long Height { get; set; }

    [JsonPropertyName("duration")]
    public object? Duration { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}