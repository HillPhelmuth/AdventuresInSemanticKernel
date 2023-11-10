using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;

namespace SkPluginLibrary.Services;

public class YouTubeSearch
{
    private readonly YouTubeService _youtubeService;
    public YouTubeSearch(string apiKey, string channelId = "")
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentNullException(nameof(apiKey));

        _youtubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            ApiKey = apiKey,
            ApplicationName = GetType().ToString(),
        });
    }

    public async Task<List<YouTubeSearchResult>> Search(string keyWords, int count = 10)
    {
        if (string.IsNullOrEmpty(keyWords))
            throw new ArgumentNullException(nameof(keyWords));

        
        try
        {
            var searchListRequest = _youtubeService.Search.List("snippet");

           searchListRequest.Type = "video";

            searchListRequest.MaxResults = count;

            searchListRequest.Q = keyWords;
            
            SearchListResponse? searchListResponse = await searchListRequest.ExecuteAsync();
            var results = searchListResponse.Items.Select(searchResult => new YouTubeSearchResult(searchResult.Id.VideoId, searchResult.Snippet.Description)).ToList();
            return results;
            //var options = new JsonSerializerOptions { WriteIndented = true };
            //result = JsonSerializer.Serialize(searchListResponse.Items, options);

        }
        catch (Exception e)
        {
            //result = string.Empty;
            Console.WriteLine($"Youtube search error:\n{e}");
            return new List<YouTubeSearchResult>();
        }

        //return result;
    }
    
}
[TypeConverter(typeof(GenericTypeConverter<YouTubeSearchResult>))]
public record YouTubeSearchResult(string Id, string Description);

internal class GenericTypeConverter<T> : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => true;

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        Console.WriteLine($"Converting {value} to {typeof(T)}");
        return JsonSerializer.Deserialize<T>((string)value);
    }
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions{WriteIndented =true});
    }
}   
