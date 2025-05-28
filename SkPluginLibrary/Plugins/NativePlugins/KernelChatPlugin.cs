using Microsoft.Extensions.Logging;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class KernelChatPlugin
{
    private const string ChatWithSkSystemPromptTemplate = 
        """
        Use the [Semantic Kernel CONTEXT] below to answer the user's questions.
        Semantic Kernel is an open-source SDK that lets you easily build genertive AI agents that can interact with your code, external REST apis and other AI agents.

        [Semantic Kernel CONTEXT]
        {{$memory_context}}


        """;
    [KernelFunction, Description("Retreive relevant information from Semantic Kernel documentation to form Semantic Kernel specific chat instructions")]
    [return: Description("Chat instructions with relevant content documents to provide additional up-to-date context")]
    public async Task<string> LearnAboutSemanticKernel([Description("Latest user chat query")]string query,[Description("Chat history to include as part of search query")] string? history = null, [Description("Number of most similar items to return from search")] int topN = 5)
    {
            var kernel = CreateKernel();
            var semanticMemory = await ChatWithSkKernelMemory();
            var memoryItems = await semanticMemory.SearchAsync(CollectionName.SkDocsCollection, $"{query} {history}", 10, 0.58).ToListAsync();
            var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
            ConsoleLogger.LoggerFactory.CreateLogger<KernelChatPlugin>().LogInformation("Memory:\n {memory}", memory);
            var context = new KernelArguments
            {
                ["memory_context"] = memory                
            };
           
            var promptTemplateFactory = new KernelPromptTemplateFactory();
            var engine = promptTemplateFactory.Create(new PromptTemplateConfig(ChatWithSkSystemPromptTemplate));
            var systemPrompt = await engine.RenderAsync(kernel, context);
            return systemPrompt;
            //var chatService = new OpenAIChatCompletionService(TestConfiguration.OpenAI!.ChatModelId, TestConfiguration.OpenAI.ApiKey);
            //var chat = new ChatHistory(systemPrompt);
            //chat.AddUserMessage(query);
            //await foreach (var token in chatService.GetStreamingChatMessageContentsAsync(chat, new OpenAIPromptExecutionSettings { MaxTokens = 2000, Temperature = 1 }))
            //{
            //    yield return token.Content ?? "";
            //}

        }
}