using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using SkPluginLibrary.Services;
//using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class YouTubePlugin
{
    private readonly YouTubeSearch _youTubeSearch;

    public YouTubePlugin(string youtubeApiKey)
    {
        _youTubeSearch = new YouTubeSearch(youtubeApiKey);
    }

    [KernelFunction,
     Description("Search YouTube for videos. Outputs a json array of youtube video descriptions and Ids")]
    [return: Description("YouTube search results")]
    public async Task<string> SearchVideos([Description("Youtube search query")] string query,
        [Description("Number of results")] int count = 10)
    {
        var results = await _youTubeSearch.Search(query, count);
        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });

    }
    
    [KernelFunction,
     Description("Transcribe a YouTube video. Outputs the transcript of the video")]
    [return: Description("Youtube Video transcript")]
    public async Task<string> TranscribeVideo(string videoId, string language = "en")
    {
        try
        {
            return await _youTubeSearch.TranscribeVideo(videoId, language);
        }
        catch (Exception ex)
        {
            return ex.ToString();
        }
    }

    private string ExtractPlainTextFromTranscript(string xmlTranscript)
    {
        try
        {
            var doc = XDocument.Parse(xmlTranscript);
            var textElements = doc.Descendants("text");

            // Join all text elements with spaces to ensure words don't run together
            return string.Join(" ", textElements.Select(x => x.Value));
        }
        catch (Exception)
        {
            return "Failed to parse transcript XML";
        }
    }


    private class CaptionTrack
    {
        [JsonPropertyName("baseUrl")]
        public string BaseUrl { get; set; }
    }
}
