using Microsoft.Extensions.Logging;
using SkPluginLibrary.Services;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins.NativePlugins;

internal class LangchainChatPlugin
{
    protected string Name = "LangchainChat";
    protected const string ChatPromptTemplate =
        """
        Use the [LangChain CONTEXT] below to answer the user's questions.
        LangChain is an open-source SDK that lets you easily build genertive AI agents that can interact with your code, external REST apis and other AI agents.

        [LangChain CONTEXT]
        {{$memory_context}}


        """;


    [Description("Retreive relevant information from LangChain documentation to form LangChain specific chat instructions")]
    [return: Description("Chat instructions with relevant content documents to provide additional up-to-date context")]
    public async Task<string> LearnAboutLangChain([Description("Latest user chat query")] string query, [Description("Chat history to include as part of search query")] string? history = null, [Description("Number of most similar items to return from search")] int topN = 5)
    {
        var semanticMemory = await DocsMemory.GetAsync();
        var memoryItems = await semanticMemory.SearchAsync(CollectionName.LangchainDocsCollection, $"{query} {history}", topN, 0.58).ToListAsync();
        var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
        ConsoleLogger.LoggerFactory.CreateLogger<LangchainChatPlugin>().LogInformation("Memory:\n {memory}", memory);
        return PromptRunner.Render(ChatPromptTemplate, new Dictionary<string, object?> { ["memory_context"] = memory });


    }      
      
        
}