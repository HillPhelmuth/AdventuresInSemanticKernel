using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Text;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace SkPluginLibrary.Plugins;

public class WikiChatPlugin
{
    private IKernel _kernel;
    public WikiChatPlugin(IKernel kernel)
    {
        _kernel = kernel;
    }
    [SKFunction, Description("Chat with wikipedia as source. Streaming response.")]
    public async IAsyncEnumerable<string> WikiSearchAndChat(string input)
    {
        yield return "Searching Wikipedia...\n\n";
        var searchUrl = SearchRequestString(input, 3);
        var client = new HttpClient();
        var pages = await client.GetFromJsonAsync<WikiSearchResult>(searchUrl);
        yield return "Parsing Wikipedia pages...\n\n";
        var tasks = pages.Pages.Select(page => SummarizeTextFromScraperPlugin(PageRequestString(page.Key), input)).ToList();
        var sb = new StringBuilder();
        var index = 1;
        foreach (var page in pages.Pages)
        {
            sb.AppendLine($"{index++}. [{page.Title}](https://en.wikipedia.org/wiki/{page.Key})");
        }

        var sources = sb.ToString();
        var results = await Task.WhenAll(tasks);
        yield return "Wikipedia plugin complete...\n\n";
        var systemPrompt =
            $"You are a friendly, talkative and helpful AI with knowledge of all things on Wikipedia. Answer the user's query using the [Context] below. The Context is a summary of current wikipedia pages.\n[Context]\n{string.Join("\n", results)}";
        Console.WriteLine($"\n-------------\n{systemPrompt}\n---------------\n");
        var chatService = _kernel.GetService<IChatCompletion>();
        var chatHistory = chatService.CreateNewChat(systemPrompt);
        chatHistory.AddUserMessage(input);
        await foreach (var token in chatService.GenerateMessageStreamAsync(chatHistory,
                           new OpenAIRequestSettings { MaxTokens = 1500, Temperature = 1.0 }))
        {
            yield return token;
        }
        yield return "\n\n-----------------\n\n";
        yield return "\n\n### Sources:\n\n";

        yield return sources;
    }

    public static string SearchRequestString(string searchQuery, int maxResults)
    {
        var searchTerm = Uri.EscapeDataString(searchQuery ?? string.Empty);
        return $"https://en.wikipedia.org/w/rest.php/v1/search/page?q={searchTerm}&limit={maxResults}";
    }

    public static string PageRequestString(string pageId)
    {
        const string wikiBaseUrl = "https://en.wikipedia.org/w/rest.php/v1/page/";
        var requestUri = $"{wikiBaseUrl}{pageId}/html";
        return requestUri;
    }
    private async Task<string> SummarizeTextFromScraperPlugin(string url, string input)
    {
        try
        {
            var scraperPlugin = await _kernel.ImportPluginFunctionsAsync("ScraperPlugin",
                new Uri("https://scraper.gafo.tech/.well-known/ai-plugin.json"),
                new OpenApiFunctionExecutionParameters { EnableDynamicPayload = true, IgnoreNonCompliantErrors = true });
            var summarizePlugin = _kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "SummarizePlugin")["Summarize"];
            var context = _kernel.CreateNewContext();
            context.Variables["url"] = url;
            var kernelResult = await _kernel.RunAsync(context.Variables, scraperPlugin["scrape"]);
            var scrapedString = kernelResult.Result();
            var segmentCount = StringHelpers.GetTokens(scrapedString) / 4096;
            segmentCount = Math.Min(3, segmentCount);
            List<Task<KernelResult>> kernelResults = new();
            var segments = ChunkIntoSegments(scrapedString, segmentCount, 4096, "", false);
            foreach (var segment in segments)
            {
                var ctx = _kernel.CreateNewContext();
                ctx.Variables["input"] = segment;
                ctx.Variables["query"] = input;
                kernelResults.Add(_kernel.RunAsync(ctx.Variables, summarizePlugin));
            }

            var summaryResults = await Task.WhenAll(kernelResults);
            var summary = string.Join("\n", summaryResults.Select(x => x.Result()));
            return summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to scrape text from {url}\n\n{ex.Message}");
            return $"";
        }
    }
    private static IEnumerable<string> ChunkIntoSegments(string text, int segmentCount, int maxPerSegment = 4096, string description = "", bool ismarkdown = true)
    {
        var total = StringHelpers.GetTokens(text);
        var totalPerSegment = Math.Min(total / segmentCount, maxPerSegment);
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