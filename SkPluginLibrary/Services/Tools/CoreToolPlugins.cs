using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.AI;

namespace SkPluginLibrary.Services;

// Small built-in tool sets. Each class is a plain C# type whose [Description]-annotated methods become
// AIFunction tools (see ToolPlugin.FromObject).

/// <summary>Date and time information.</summary>
public class TimePlugin
{
    [Description("Get the current date and time in the local time zone")]
    public string Now() => DateTimeOffset.Now.ToString("f", CultureInfo.CurrentCulture);

    [Description("Get the current date and time in UTC")]
    public string UtcNow() => DateTimeOffset.UtcNow.ToString("f", CultureInfo.CurrentCulture);

    [Description("Get the current date")]
    public string Date() => DateTimeOffset.Now.ToString("D", CultureInfo.CurrentCulture);

    [Description("Get the current time")]
    public string Time() => DateTimeOffset.Now.ToString("t", CultureInfo.CurrentCulture);

    [Description("Get the current day of the week")]
    public string DayOfWeek() => DateTimeOffset.Now.DayOfWeek.ToString();

    [Description("Get the current year")]
    public string Year() => DateTimeOffset.Now.Year.ToString(CultureInfo.CurrentCulture);

    [Description("Get the current month name")]
    public string Month() => DateTimeOffset.Now.ToString("MMMM", CultureInfo.CurrentCulture);

    [Description("Get the date of the same weekday in the past")]
    public string DaysAgo([Description("Number of days to go back")] double days) =>
        DateTimeOffset.Now.AddDays(-days).ToString("D", CultureInfo.CurrentCulture);
}

/// <summary>Simple text manipulation.</summary>
public class TextPlugin
{
    [Description("Change all string chars to uppercase")]
    public string Uppercase([Description("Text to uppercase")] string input) => input.ToUpperInvariant();

    [Description("Change all string chars to lowercase")]
    public string Lowercase([Description("Text to lowercase")] string input) => input.ToLowerInvariant();

    [Description("Remove spaces from the start and end of a string")]
    public string Trim([Description("Text to edit")] string input) => input.Trim();

    [Description("Concatenate two strings into one")]
    public string Concat([Description("First input to concatenate")] string input, [Description("Second input to concatenate")] string input2) => input + input2;

    [Description("Get the length of a string")]
    public int Length([Description("Text to measure")] string input) => input.Length;
}

/// <summary>Read and write local files.</summary>
public class FileIOPlugin
{
    [Description("Read a file")]
    public async Task<string> Read([Description("Source file")] string path) => await File.ReadAllTextAsync(path);

    [Description("Write a file")]
    public async Task Write([Description("Destination file")] string path, [Description("File content")] string content) =>
        await File.WriteAllTextAsync(path, content);
}

/// <summary>Make HTTP requests.</summary>
public class HttpPlugin
{
    private static readonly HttpClient Client = new();

    [Description("Makes a GET request to a uri")]
    public async Task<string> Get([Description("The URI to send the request to.")] string uri) => await Client.GetStringAsync(uri);

    [Description("Makes a POST request to a uri")]
    public async Task<string> Post([Description("The URI to send the request to.")] string uri, [Description("The body of the request")] string body)
    {
        using var response = await Client.PostAsync(uri, new StringContent(body));
        return await response.Content.ReadAsStringAsync();
    }
}

/// <summary>Semantic recall over a <see cref="TextMemory"/> (the Agent Framework counterpart of a memory plugin).</summary>
public class MemoryToolPlugin(TextMemory memory, string collection = "generic")
{
    [Description("Recall a fact from the long term memory")]
    public async Task<string> Recall([Description("The information to retrieve")] string input,
        [Description("The memory collection to search")] string? collectionName = null,
        [Description("The relevance score, from 0.0 to 1.0; 1.0 means perfect match")] double relevance = 0.0,
        [Description("The maximum number of relevant memories to recall")] int limit = 1)
    {
        var results = await memory.SearchAsync(collectionName ?? collection, input, limit, relevance).ToListAsync();
        return results.Count == 0 ? "" : limit == 1 ? results[0].Metadata.Text : System.Text.Json.JsonSerializer.Serialize(results.Select(r => r.Metadata.Text));
    }

    [Description("Save information to semantic memory")]
    public async Task Save([Description("The information to save")] string input, [Description("The key to save the information under")] string key,
        [Description("The memory collection to save to")] string? collectionName = null) =>
        await memory.SaveInformationAsync(collectionName ?? collection, input, key);
}

/// <summary>Summarize conversation text with the chat model.</summary>
public class ConversationSummaryPlugin(IChatClient chatClient)
{
    [Description("Given a long conversation transcript, summarize the conversation.")]
    public async Task<string> SummarizeConversation([Description("A long conversation transcript.")] string input)
    {
        var response = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.System, "BEGIN CONTENT TO SUMMARIZE:\n" + input + "\nEND CONTENT TO SUMMARIZE.\nSummarize the conversation in 'CONTENT TO SUMMARIZE', identifying main points of discussion and any conclusions that were reached. Do not incorporate other general knowledge. Summary is in plain text, in complete sentences, with no markup or tags.")],
            new ChatOptions { Temperature = 0.1f });
        return response.Text;
    }
}

/// <summary>Web search over the app's Bing search service.</summary>
public class WebSearchEnginePlugin(IWebSearchService searchService)
{
    [Description("Perform a web search.")]
    public async Task<string> Search([Description("The search query")] string query, [Description("The number of results to return")] int count = 5)
    {
        var results = await searchService.SearchAsync(query, count) ?? [];
        return System.Text.Json.JsonSerializer.Serialize(results.Select(r => new { r.Title, r.Url, r.Snippet }));
    }
}
