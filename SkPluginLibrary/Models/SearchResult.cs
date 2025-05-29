using System.Text.Json.Serialization;

namespace SkPluginLibrary.Models
{
    public class SearchResult
    {
        [JsonPropertyName("_type")]
        public string? Type { get; set; }

        [JsonPropertyName("queryContext")]
        public QueryContext? QueryContext { get; set; }

        [JsonPropertyName("webPages")]
        public WebPages? WebPages { get; set; }
        public List<BingSearchResult> BingSearchResults => WebPages?.PageInfo?.Select(x => new BingSearchResult(x.Url, x.Snippet, x.Name) { WebSearchUrl = WebPages?.WebSearchUrl, DateLastCrawled = x.DateLastCrawled}).ToList() ?? [];
    }
    public class BingSearchResult
    {
        public BingSearchResult(string url, string snippet, string name)
        {
            Url = url;
            Snippet = snippet;
            Name = name;
        }

        public string Url { get; set; }
        public string Snippet { get; set; }
        public string DisplayUrl { get; set; }
        public string Name { get; set; }
        public string? WebSearchUrl { get; set; }
        public DateTimeOffset DateLastCrawled { get; set; }
        public List<BingSearchResult>? DeepLinks { get; set; }
        public override string ToString()
        {
            return $"Name: {Name}\n Url: {Url}\n Snippet: {Snippet}\n Date last crawled: {DateLastCrawled:yyyy-MM-dd}\n----------------------------\nDeep Links:\n----------------------------\n{string.Join("\n", DeepLinks?.Select(x => x.ToString()) ?? [])}";
        }
    }
    public class QueryContext
    {
        [JsonPropertyName("originalQuery")]
        public string? OriginalQuery { get; set; }
    }

    public class WebPages
    {
        [JsonPropertyName("webSearchUrl")]
        public string? WebSearchUrl { get; set; }

        [JsonPropertyName("totalEstimatedMatches")]
        public long TotalEstimatedMatches { get; set; }

        [JsonPropertyName("value")]
        public List<WebPageInfo>? PageInfo { get; set; }

        [JsonPropertyName("someResultsRemoved")]
        public bool SomeResultsRemoved { get; set; }
    }

    public class WebPageInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("isFamilyFriendly")]
        public bool IsFamilyFriendly { get; set; }

        [JsonPropertyName("displayUrl")]
        public string? DisplayUrl { get; set; }

        [JsonPropertyName("snippet")]
        public string? Snippet { get; set; }

        [JsonPropertyName("dateLastCrawled")]
        public DateTimeOffset DateLastCrawled { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("isNavigational")]
        public bool IsNavigational { get; set; }
    }
}
