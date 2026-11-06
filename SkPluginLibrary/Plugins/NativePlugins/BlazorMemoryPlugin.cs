using Microsoft.Extensions.Logging;
using SkPluginLibrary.Services;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins.NativePlugins;

internal class BlazorMemoryPlugin
{
    public const string ChatWithBlazorSystemPromptTemplate =
        """
        Use the [Blazor CONTEXT] below to answer the user's questions.
        Blazor is a .NET frontend web framework that supports both server-side rendering and
        client interactivity in a single programming model.


        [Blazor CONTEXT]
        {{$memory_context}}

        """;
    [Description("Retreive relevant information from Blazor documentation to form Blazor specific chat instructions")]
    [return: Description("Chat instructions with relevant information to provide additional context")]
    public async Task<string> LearnAboutBlazor([Description("Latest user chat query")] string query, [Description("chat history to include as part of the search query")] string? history = null, [Description("Number of most similar items to return from search")] int topN = 10)
    {
        var ready = await CollectionExists();

        var semanticMemory = await DocsMemory.GetAsync();
        var memoryItems = await semanticMemory.SearchAsync(CollectionName.BlazorDocsCollection, $"{query} {history}", topN, 0.58).ToListAsync();
        var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
        ConsoleLogger.LoggerFactory.CreateLogger<KernelChatPlugin>().LogInformation("Memory:\n {memory}", memory);
        return PromptRunner.Render(ChatWithBlazorSystemPromptTemplate, new Dictionary<string, object?> { ["memory_context"] = memory });


    }
    protected static Task<bool> CollectionExists() => DocsMemory.CollectionExistsAsync(CollectionName.BlazorDocsCollection);
}