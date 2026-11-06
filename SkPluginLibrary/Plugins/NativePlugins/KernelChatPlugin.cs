using Microsoft.Extensions.Logging;
using SkPluginLibrary.Services;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class KernelChatPlugin
{
    private const string ChatWithDocsSystemPromptTemplate = 
        """
        Use the [Agent Framework CONTEXT] below to answer the user's questions.
        Microsoft Agent Framework (and Semantic Kernel before it) is an open-source SDK that lets you easily build generative AI agents that can interact with your code, external REST apis and other AI agents.

        [Agent Framework CONTEXT]
        {{$memory_context}}


        """;
    [Description("Retreive relevant information from the indexed Agent Framework / Semantic Kernel documentation to form specific chat instructions")]
    [return: Description("Chat instructions with relevant content documents to provide additional up-to-date context")]
    public async Task<string> LearnAboutAgentFramework([Description("Latest user chat query")]string query,[Description("Chat history to include as part of search query")] string? history = null, [Description("Number of most similar items to return from search")] int topN = 5)
    {
        var semanticMemory = await DocsMemory.GetAsync();
        var memoryItems = await semanticMemory.SearchAsync(CollectionName.SkDocsCollection, $"{query} {history}", 10, 0.58).ToListAsync();
        var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
        ConsoleLogger.LoggerFactory.CreateLogger<KernelChatPlugin>().LogInformation("Memory:\n {memory}", memory);
        return PromptRunner.Render(ChatWithDocsSystemPromptTemplate, new Dictionary<string, object?> { ["memory_context"] = memory });
    }
}
