using Microsoft.Extensions.Logging;
using System.Net;
using static SkPluginLibrary.CoreKernelService;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace SkPluginLibrary.Plugins;

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
    [KernelFunction, Description("Retreive relevant information from Blazor documentation to form Blazor specific chat instructions")]
    [return: Description("Chat instructions with relevant information to provide additional context")]
    public async Task<string> LearnAboutBlazor([Description("Latest user chat query")] string query, [Description("chat history to include as part of the search query")] string? history = null, [Description("Number of most similar items to return from search")] int topN = 10)
    {
        var ready = await CollectionExists();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.ConfigureHttpClientDefaults(c =>
        {
            c.AddStandardResilienceHandler().Configure(o =>
            {
                o.Retry.ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests);
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.AttemptTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(90) };
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
                o.TotalRequestTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromMinutes(5) };
            });
        });
        var kernel = kernelBuilder
            .AddOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt35ModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();
        var semanticMemory = await GetSemanticTextMemory();
        var memoryItems = await semanticMemory.SearchAsync(CollectionName.BlazorDocsCollection, $"{query} {history}", topN, 0.58).ToListAsync();
        var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
        ConsoleLogger.LoggerFactory.CreateLogger<KernelChatPlugin>().LogInformation("Memory:\n {memory}", memory);
        var args = new KernelArguments
        {
            ["memory_context"] = memory
        };

        var promptTemplateFactory = new KernelPromptTemplateFactory();
        var engine = promptTemplateFactory.Create(new PromptTemplateConfig(ChatWithBlazorSystemPromptTemplate));
        var systemPrompt = await engine.RenderAsync(kernel, args);
        return systemPrompt;


    }
    protected static async Task<bool> CollectionExists()
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        return collections.Contains(CollectionName.BlazorDocsCollection);
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