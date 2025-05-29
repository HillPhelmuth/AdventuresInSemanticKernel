using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Memory;
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


    [KernelFunction, Description("Retreive relevant information from LangChain documentation to form LangChain specific chat instructions")]
    [return: Description("Chat instructions with relevant content documents to provide additional up-to-date context")]
    public async Task<string> LearnAboutLangChain([Description("Latest user chat query")] string query, [Description("Chat history to include as part of search query")] string? history = null, [Description("Number of most similar items to return from search")] int topN = 5)
    {
        var kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt4MiniModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();
        var semanticMemory = await GetSemanticTextMemory();
        var memoryItems = await semanticMemory.SearchAsync(CollectionName.LangchainDocsCollection, $"{query} {history}", topN, 0.58).ToListAsync();
        var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
        ConsoleLogger.LoggerFactory.CreateLogger<LangchainChatPlugin>().LogInformation("Memory:\n {memory}", memory);
        var args = new KernelArguments
        {
            ["memory_context"] = memory
        };

        var promptTemplateFactory = new KernelPromptTemplateFactory();
        var engine = promptTemplateFactory.Create(new PromptTemplateConfig(ChatPromptTemplate));
        var systemPrompt = await engine.RenderAsync(kernel, args);
        return systemPrompt;


    }      
      
    protected static async Task<ISemanticTextMemory> GetSemanticTextMemory()
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
        return new MemoryBuilder()
            .WithMemoryStore(sqliteMemoryStore)
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAITextEmbeddingGeneration(TestConfiguration.OpenAI.EmbeddingModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();
    }
        
}