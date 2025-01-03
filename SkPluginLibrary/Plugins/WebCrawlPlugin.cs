﻿using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using static SkPluginLibrary.CoreKernelService;
using System.Collections.Concurrent;

namespace SkPluginLibrary.Plugins;

public class WebCrawlPlugin
{

    private readonly KernelFunction _summarizeWebContent;
    private const int MaxTokens = 1024;
    private readonly Kernel _kernel;
    private readonly BingWebSearchService? _searchService;


    public WebCrawlPlugin(BingWebSearchService bingWebSearchService)
    {
        var kernel = CreateKernel();
        var summarizePlugin = kernel.ImportPluginFromPromptDirectoryYaml("SummarizePlugin");
        _summarizeWebContent = summarizePlugin["SummarizeLong"];
        _kernel = kernel;
        _searchService = bingWebSearchService;

    }


    [KernelFunction, Description("Extract a web search query from a question")]
    public async Task<string> ExtractWebSearchQuery(string input)
    {
        var extractPlugin = _kernel.CreateFunctionFromPrompt("Extract terms for a simple web search query from a question. Include no other content\nquestion:{{$input}}", executionSettings: new OpenAIPromptExecutionSettings { MaxTokens = 128, Temperature = 0.0, TopP = 0 });
        var result = await _kernel.InvokeAsync(extractPlugin, new KernelArguments() { { "input", input } });
        return result.Result();
    }
    [KernelFunction, Description("Search Web, summarize the content of each result and generate citations.")]
    [return: Description("A json collection of objects designed to facilitate web citations including url, title, and content")]
    public async Task<string> SearchAndCiteWeb([Description("Web search query")] string input, [Description("Number of web search results to use")] int resultCount = 2)
    {
        var results = await _searchService!.SearchAsync(input, resultCount) ?? [];
        var scraperTaskList = new List<Task<List<SearchResultItem>>>();
        foreach (var result in results.Take(Math.Min(results.Count, 5)))
        {

            try
            {
                scraperTaskList.Add(ScrapeChunkAndSummarize(result.Url, result.Name, input, result.Snippet));
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
                    groupItem.Content += $"{item.Content}\n";
                }
                resultItems.Add(groupItem);
            }
            else
            {
                resultItems.Add(new SearchResultItem(group.Key) { Title = group.First().Title, Content = group.First().Content });
            }
        }
        var searchCiteJson = JsonSerializer.Serialize(resultItems, new JsonSerializerOptions { WriteIndented = true });
        return searchCiteJson;
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

    private async Task<List<SearchResultItem>> ScrapeChunkAndSummarize(string url, string title, string input, string summary)
    {

        try
        {
            var crawler = new CrawlService(ConsoleLogger.LoggerFactory);
            var text = await crawler.CrawlAsync(url, true);
            var tokens = StringHelpers.GetTokens(text);
            var count = tokens / 4096;
            var segments = ChunkIntoSegments(text, Math.Max(count, 1), 4096, title).ToList();
            Console.WriteLine($"Segment count: {segments.Count}");
            var argList = segments.Select(segment => new KernelArguments { ["input"] = segment, ["query"] = input }).ToList();
            var summaryResults = new List<SearchResultItem>();
            foreach (var arg in argList)
            {
                var result = await _kernel.InvokeAsync<string>(_summarizeWebContent, arg);
                summaryResults.Add(new SearchResultItem(url){Title = title, Content = result});
            }

            return summaryResults;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to scrape text from {url}\n\n{ex.Message}\n{ex.StackTrace}");
            return [new SearchResultItem(url) { Title = title, Content = summary }];
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
