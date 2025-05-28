using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;


namespace SkPluginLibrary.Services;

public class GithubCodeReaderService
{
    private readonly HttpClient _httpClient;
    private readonly string _repoOwner;
    private readonly string _repoName;

    public GithubCodeReaderService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClient = httpClientFactory.CreateClient();
        _repoOwner = "HillPhelmuth";
        _repoName = "AdventuresInSemanticKernel";
        var githubAccessToken = configuration["GitHub:AccessToken"];
        Console.WriteLine(githubAccessToken);
        // Set GitHub API Base Address
        _httpClient.BaseAddress = new Uri("https://api.github.com/");
        //_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Adventures in Semantic Kernel", "1.0"));
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "application/vnd.github.v3.raw");
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "AdventuresInSk");
        // Set Authorization header if access token is available
        if (!string.IsNullOrEmpty(githubAccessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubAccessToken);
        }
    }

    public async Task<string> GetCodeForFileAsync(string filePath, string branch = "master")
    {
        try
        {
            // Encode the file path to be URL safe
            var encodedFilePath = Uri.EscapeDataString(filePath);

            // GitHub REST API to get file contents
            var url = $"repos/{_repoOwner}/{_repoName}/contents/{encodedFilePath}";

            //var response = await _httpClient.GetAsync(url);
            var result = await _httpClient.GetStringAsync(url);
            return !string.IsNullOrEmpty(result) ? result :
                //response.EnsureSuccessStatusCode();
                //// Read content as JSON
                //var responseBody = await response.Content.ReadAsStringAsync();
                //using var jsonDocument = JsonDocument.Parse(responseBody);
                //var root = jsonDocument.RootElement;
                //// The content is encoded in Base64 by GitHub, we need to decode it
                //if (root.TryGetProperty("content", out var contentElement))
                //{
                //    var base64Content = contentElement.GetString();
                //    var data = Convert.FromBase64String(base64Content);
                //    return System.Text.Encoding.UTF8.GetString(data);
                //}
                "Code not found or invalid format.";
        }
        catch (HttpRequestException e)
        {
            return $"Error retrieving code: {e.Message}";
        }
    }
}
