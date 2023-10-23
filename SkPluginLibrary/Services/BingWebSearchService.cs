using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SkPluginLibrary.Services
{
    public class BingWebSearchService
    {
        //private readonly WebSearchClient _webSearchClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClient _httpClient;

        private readonly ILogger<BingWebSearchService> _logger;
        public BingWebSearchService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory;
            _logger = loggerFactory.CreateLogger<BingWebSearchService>();
            var subscriptionKey = TestConfiguration.Bing.ApiKey;
            _httpClient = _httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri("https://api.bing.microsoft.com/");
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
        }

        public async Task<List<BingSearchResult>?> SearchAsync(string query, int answerCount = 10)
        {
            if (answerCount < 3) answerCount = 3;
            _logger.LogInformation("Searching Bing for {query} with answerCount {answerCount}", query, answerCount);
            var response = await _httpClient.GetAsync($"v7.0/search?q={query}&answerCount={answerCount}");
            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<SearchResult>(content);
            var bingSearchResults = searchResult?.BingSearchResults;
            _logger.LogInformation("Search Bing Results:\n {result}",
                string.Join("\n", bingSearchResults?.Select(x => x.ToString()) ?? Array.Empty<string>()));
            return bingSearchResults;

        }
    }
}
