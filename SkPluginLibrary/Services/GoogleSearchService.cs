using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Services;
using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Microsoft.Extensions.Logging;

namespace SkPluginLibrary.Services;

public class GoogleSearchService(ILoggerFactory loggerFactory) : IWebSearchService
{
    private readonly CustomSearchAPIService _googleCustomSearch = new(new BaseClientService.Initializer
    {
        ApiKey = TestConfiguration.Google.ApiKey,
        ApplicationName = "GoogleSearchService"
    });
    private readonly CrawlService _crawler = new(loggerFactory);
    private readonly ILogger<GoogleSearchService> _logger = loggerFactory.CreateLogger<GoogleSearchService>();

    public async Task<List<SearchResultItem>?> SearchAsync(string query, int answerCount = 10, string? freshness = null)
    {
        var searchRequest = _googleCustomSearch.Cse.List();
        searchRequest.Q = query;
        searchRequest.Cx = TestConfiguration.Google.SearchEngineId;
        searchRequest.Num = answerCount;
        var resultItem = await searchRequest.ExecuteAsync();
        IList<Result>? pages = resultItem.Items;

        if (pages is null) return [];
        _logger.LogInformation("Search Google Results:\n {result}",
            string.Join("\n", pages.Select(x => x.Link)));
        var searchResultItems = ConvertToSearchResults(pages);
        return searchResultItems;
    }

    public async Task<List<SearchResultItem>> DeepSearchAsync(string query, int maxResults = 3, string? freshness = null)
    {
        var results = await SearchAsync(query, maxResults, freshness);
        foreach (var searchResultItem in results ?? [])
        {
            var additionalLinks = await _crawler.CrawlAndExtractUrls(searchResultItem.Url);
            searchResultItem.AdditionalLinks = additionalLinks;
        }
        

        return results;
    }
    private static List<SearchResultItem> ConvertToSearchResults(IList<Result>? webPages)
    {
        if (webPages is null) return [];
        return webPages.Select(x => new SearchResultItem(x.Link)
        {
            Title = x.Title,
            Content = x.Snippet,
            Url = x.Link,
            Snippet = x.Snippet,
        }).ToList();
    }
}
