using Microsoft.SemanticKernel;
using SkPluginLibrary.Services;
using System.ComponentModel;
using System.Text.Json;

namespace SkPluginLibrary.Plugins
{
    public class YouTubePlugin
    {
        private readonly YouTubeSearch _youTubeSearch;
        private readonly IKernel _kernel;
        public YouTubePlugin(IKernel kernel, string youtubeApiKey)
        {
            _youTubeSearch = new YouTubeSearch(youtubeApiKey);
            _kernel = kernel;
        }

        [SKFunction,
         Description("Search YouTube for videos. Outputs a json array of youtube video descriptions and Ids")]
        public async Task<string> SearchVideos([Description("Youtube search query")] string query,
            [Description("Number of results")] int count = 10)
        {
            List<YouTubeSearchResult> results = await _youTubeSearch.Search(query, count);
            return JsonSerializer.Serialize(results, new JsonSerializerOptions{WriteIndented = true});

        }
    }
}
