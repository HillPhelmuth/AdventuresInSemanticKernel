using Microsoft.Extensions.Logging;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class ArxivPlugin(ILoggerFactory loggerFactory)
{
    private readonly ArxivApiService _arxivApiService = new(loggerFactory);
    [KernelFunction, Description("Searches arXiv for papers")]
    [return:Description("Metadata for the papers that meet the search criteria")]
    public async Task<string> SearchArxiv(
        [Description("The search query")] string query,
        [Description("The number of results to return")] int numResults = 20)
    {
        var searchResults = await _arxivApiService.QueryAsync(new ArxivQueryParameters { SearchQuery = query, MaxResults = numResults });
        return searchResults?.ToString() ?? "Search Failed!";

    }
    [KernelFunction, Description("Full content for selected arXiv paper")]
    public async Task<string> GetArxivPaper(
        [Description("The arXiv metadata for the paper")] ArxivEntry arxivEntry)
    {
        var pdf = await _arxivApiService.GetContentAsync(arxivEntry);
        return pdf ?? "Failed to retrieve PDF!";
    }
}