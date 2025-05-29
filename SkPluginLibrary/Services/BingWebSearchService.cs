using Microsoft.Bing.WebSearch;
using Microsoft.Bing.WebSearch.Models;
using Microsoft.Extensions.Logging;
using WebSearchApiKeyServiceClientCredentials = Microsoft.Bing.WebSearch.ApiKeyServiceClientCredentials;

namespace SkPluginLibrary.Services;

public interface IWebSearchService
{
    Task<List<SearchResultItem>?> SearchAsync(string query, int answerCount = 10, string? freshness = null);

    Task<List<SearchResultItem>> DeepSearchAsync(string query, int maxResults = 3,
        string? freshness = null);
}

public class BingWebSearchService(ILoggerFactory loggerFactory) : IWebSearchService
{
    //private readonly WebSearchClient _webSearchClient;
    private readonly WebSearchClient _webSearchClient = new(new WebSearchApiKeyServiceClientCredentials(TestConfiguration.Bing.ApiKey));
    private readonly ILogger<BingWebSearchService> _logger = loggerFactory.CreateLogger<BingWebSearchService>();
    private readonly CrawlService _crawler = new(loggerFactory);
    public async Task<List<SearchResultItem>?> SearchAsync(string query, int answerCount = 10, string? freshness = null)
    {
        if (answerCount < 3) answerCount = 3;
        _logger.LogInformation("Searching Bing for {query} with answerCount {answerCount}", query, answerCount);
           
        SearchResponse? serviceResponse = await _webSearchClient.Web.SearchAsync(query, answerCount: answerCount, freshness:freshness);
        var webPages = serviceResponse?.WebPages?.Value;
           
        _logger.LogInformation("Search Bing Results:\n {result}",
            string.Join("\n", webPages?.Select(x => x.DisplayUrl) ?? []));
        return ConvertToSearchResults(webPages?.ToList());

    }
    public async Task<List<SearchResultItem>> DeepSearchAsync(string query, int maxResults = 3,
        string? freshness = null)
    {
        var results = new List<SearchResultItem>();
        var initialResults = (await SearchAsync(query, maxResults, freshness))?.ToList() ?? [];
        foreach (var searchResultItem in initialResults)
        {
            var additionalLinks = await _crawler.CrawlAndExtractUrls(searchResultItem.Url);
            searchResultItem.AdditionalLinks = additionalLinks;
        }
        results.AddRange(initialResults);
        
        return results;
    }

    private static List<SearchResultItem> ConvertToSearchResults(List<WebPage>? webPages)
    {
        if (webPages is null) return [];
        return webPages.Select(x => new SearchResultItem(x.Id)
        {
            Title = x.Name,
            Content = x.Snippet,
            Url = x.Url,
            Snippet = x.Snippet
        }).ToList();
    }
}