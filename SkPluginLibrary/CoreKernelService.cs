using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Services;
using System.Text;
using System.Text.Json;
using SkPluginComponents.Models;
using StringHelpers = SkPluginLibrary.Models.Helpers.StringHelpers;
using SkPluginComponents;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using OpenApiFunctionExecutionParameters = Microsoft.SemanticKernel.Plugins.OpenApi.OpenApiFunctionExecutionParameters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Redis;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Connectors.Weaviate;
using SkPluginLibrary.Plugins;
using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel.Plugins.Memory;


namespace SkPluginLibrary;

public partial class CoreKernelService : ICoreKernelExecution, ISemanticKernelSamples, IMemoryConnectors, ITokenization, ICustomNativePlugins, ICustomCombinations, IChatWithSk
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryStore _memoryStore;
    private readonly CompilerService _compilerService;
    private readonly ScriptService _scriptService;
    private readonly HdbscanService _hdbscanService;
    private SqliteMemoryStore? _sqliteStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly BingWebSearchService _bingSearchService;
    private readonly ISemanticTextMemory _semanticTextMemory;
    public class CollectionName
    {
        public const string ClusterCollection = "clusterCollection";
        public const string ChatCollection = "chatCollection";
        public const string SkDocsCollection = "skDocsCollection";
        public const string SkCodeCollection = "skCodeCollection";
        public const string BlazorDocsCollection = "blazorDocsCollection";
        public const string LangchainDocsCollection = "langchainDocsCollection";
    }
    public CoreKernelService(IConfiguration configuration, ScriptService scriptService, CompilerService compilerService, HdbscanService hdbscanService, ILoggerFactory loggerFactory, BingWebSearchService bingSearchService, AskUserService modalService)
    {
        _configuration = configuration;
        _scriptService = scriptService;
        _compilerService = compilerService;
        _hdbscanService = hdbscanService;
        _loggerFactory = loggerFactory;
        _bingSearchService = bingSearchService;
        _modalService = modalService;

        _memoryStore = new VolatileMemoryStore();
        CreateKernel();
        _semanticTextMemory = new MemoryBuilder()
            .WithMemoryStore(_memoryStore)
            .WithOpenAITextEmbeddingGeneration("text-embedding-ada-002", TestConfiguration.OpenAI!.ApiKey)
            .WithLoggerFactory(_loggerFactory)
            .Build();
    }

    public static Kernel ChatCompletionKernel(string chatModel = "gpt-3.5-turbo-1106")
    {
        //var kernel = new Kernel();
        //kernel.Services.AddOpenAiChatCompletionService()
        return Kernel.CreateBuilder().AddOpenAIChatCompletion(chatModel, TestConfiguration.OpenAI!.ApiKey)
            .Build();
    }
    public static Kernel CreateKernel(string chatModel = "gpt-3.5-turbo-1106")
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(builder => builder.AddConsole());
        var kernel = kernelBuilder
            .AddOpenAIChatCompletion(chatModel, TestConfiguration.OpenAI!.ApiKey)
            .AddOpenAITextEmbeddingGeneration("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .Build();

        return kernel;
    }
    private IMemoryStore _playgroundStore = new VolatileMemoryStore();
    private IMemoryStore CreateMemoryStore(MemoryStoreType memoryStoreType)
    {
        return memoryStoreType switch
        {
            MemoryStoreType.InMemory => new VolatileMemoryStore(),
            MemoryStoreType.Weaviate => new WeaviateMemoryStore(TestConfiguration.Weaviate!.Endpoint, TestConfiguration.Weaviate.ApiKey, TestConfiguration.Weaviate.Scheme, _loggerFactory),

            MemoryStoreType.Redis => new RedisMemoryStore(TestConfiguration.Redis!.Configuration!),

            MemoryStoreType.Qdrant => new QdrantMemoryStore(TestConfiguration.Qdrant!.Endpoint, 1536, _loggerFactory),
            _ => _memoryStore
        };
    }

    private ISemanticTextMemory CreateSemanticMemory(MemoryStoreType memoryStoreType)
    {
        return new MemoryBuilder()
            .WithMemoryStore(CreateMemoryStore(memoryStoreType))
            .WithOpenAITextEmbeddingGeneration("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .WithLoggerFactory(_loggerFactory)
            .Build();
    }

    private async Task<ISemanticTextMemory> CreateSqliteMemoryAsync(bool isSkChat = false)
    {
        var connectionString = isSkChat ? TestConfiguration.Sqlite!.ChatContentConnectionString : TestConfiguration.Sqlite!.ConnectionString!;
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(connectionString);

        _sqliteStore = sqliteMemoryStore;
        var collections = await _sqliteStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        return new MemoryBuilder()
            .WithMemoryStore(_sqliteStore)
            .WithOpenAITextEmbeddingGeneration("text-embedding-ada-002", TestConfiguration.OpenAI!.ApiKey)
            .WithLoggerFactory(_loggerFactory)
            .Build();
    }

    public static ISemanticTextMemory CreateMemoryStore(IMemoryStore? memory = null)
    {
        memory ??= new VolatileMemoryStore();
        return new MemoryBuilder()
            .WithMemoryStore(memory)
            .WithOpenAITextEmbeddingGeneration("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .Build();
    }
    internal static async Task<Kernel> ChatWithSkKernal(string chatModel = "gpt-3.5-turbo-1106")
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);

        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        if (!collections.Contains(CollectionName.SkDocsCollection))
        {
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.SkDocsCollection);
            //await GenerateAndSaveEmbeddings();
        }

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(builder => builder.AddConsole());
        var kernel = kernelBuilder
            .AddOpenAIChatCompletion(chatModel, TestConfiguration.OpenAI!.ApiKey)
            .Build();
        var semanticMemory = await ChatWithSkKernelMemory();
        var textMemory = KernelPluginFactory.CreateFromObject(new TextMemoryPlugin(semanticMemory), "TextMemoryPlugin");
        kernel.Plugins.Add(textMemory);
        return kernel;
    }

    internal static async Task<ISemanticTextMemory> ChatWithSkKernelMemory()
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        if (!collections.Contains(CollectionName.SkDocsCollection))
        {
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.SkDocsCollection);
            //await GenerateAndSaveEmbeddings();
        }
        return new MemoryBuilder()
            .WithMemoryStore(sqliteMemoryStore)
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAITextEmbeddingGeneration(TestConfiguration.OpenAI.EmbeddingModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();
    }


    private const string ChatWithSkSystemPromptTemplate = 
        """
        You are a Semantic Kernel Expert and a helpful and friendly Instructor. Use the [Semantic Kernel CONTEXT] below to answer the user's questions.

        [Semantic Kernel CONTEXT]
        {{TextMemoryPlugin.Recall input=$input collection=$collection relevance=$relevance limit=$limit}}

        """;
    private Kernel? _skChatKernel;
    public async IAsyncEnumerable<string> ExecuteChatWithSkStream(string query, ChatHistory? chatHistory = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _skChatKernel ??= await ChatWithSkKernal();

        var context = new KernelArguments
        {
            ["input"] = query,
            ["limit"] = 10,
            ["relevance"] = 0.77,
            ["collection"] = CollectionName.SkDocsCollection
        };
        
        var promptTemplateFactory = new KernelPromptTemplateFactory();
        var engine = promptTemplateFactory.Create(new PromptTemplateConfig(ChatWithSkSystemPromptTemplate));
        var systemPrompt = await engine.RenderAsync(_skChatKernel, context, cancellationToken);
        var chatService = new OpenAIChatCompletionService(TestConfiguration.OpenAI.ModelId, TestConfiguration.OpenAI.ApiKey);
        var chat = new ChatHistory(systemPrompt);
        
        foreach (var message in chatHistory ?? [])
        {
            chat.Add(message);
        }
       
        chat.AddUserMessage(query);
        await foreach (var token in chatService.GetStreamingChatMessageContentsAsync(chat, new OpenAIPromptExecutionSettings { MaxTokens = 2000, Temperature = 1 }, cancellationToken: cancellationToken))
        {
            yield return token.Content ?? "";
        }

    }
    public event EventHandler<string>? StringWritten;
    public event EventHandler<string>? KernelError;

    #region D&D Writer with Sequential Planner and DndApiSkill (DndOpenApiSkillPage.razor)
    public event Action<SimpleChatMessage>? DndPlannerFunctionHook;
    public async Task<string> FunctionCallStepwiseDndApi(string characterDescription,
        (string? Race, string? Class, string? Alignment) details,
        CancellationToken cancellationToken = default)
    {
        var detailString = $" a {details.Race} {details.Class} with a {details.Alignment} alignment";
        characterDescription += detailString;

        var kernel = CreateKernel("gpt-4-1106-preview");
        var dndApiPlugin = await kernel.ImportPluginFromOpenApiAsync("DndApiPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "DndApiPlugin", "openapi.json"), new OpenApiFunctionExecutionParameters { IgnoreNonCompliantErrors = true }, cancellationToken: cancellationToken);
        var writer = kernel.ImportPluginFromPromptDirectoryYaml("WriterPlugin");
        var dndPlugin = new DndPlugin();
        kernel.ImportPluginFromObject(dndPlugin);
        var askUserplugin = new AskUserPlugin(_modalService);
        var askUser = kernel.ImportPluginFromObject(askUserplugin, "AskUserPlugin");

        var config = new FunctionCallingStepwisePlannerConfig
        {
            MaxTokens = 9500,
            MaxIterations = 15,
            ExecutionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.3, TopP = 1.0, ModelId = TestConfiguration.OpenAI.ModelId, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions },

        };

        var included = new List<string> { "ParseCharacterInfo", "Races", "Alignments", "Classes", "ShortStory", "AskUser", "Monster", "Monsters" };
        var excluded = new List<string> { "AbilityScores", "RacesTraits", "GenerateMonsterDescription", "RacesProficiencies" };
        foreach (var include in included)
        {
            config.SemanticMemoryConfig.IncludedFunctions.Add(("DndApiPlugin", include));
        }

        excluded.AddRange(dndApiPlugin.Where(function => !included.Contains(function.Name)).Select(x => x.Name));
        excluded.AddRange(writer.Where(function => !included.Contains(function.Name)).Select(x => x.Name));
        var excludeditems = string.Join(", ", excluded);
        _loggerFactory.LogInformation("Excluded functions: {excludeditems}", excludeditems);
        foreach (var exclude in excluded)
        {
            config.ExcludedFunctions.Add(exclude);
        }
        var ask = $"Invent a D&D character based on the description below as the protagonist. Generate a short story using the available relevant details of the character and a DndMonster as a primary antagonist. The DndMonster should be selected from a list of all dnd monsters by Asking the User filtereed by challenge rating, also selected by asking the user.\ndescription: \n {characterDescription}.\n\n YOUR FINAL RESPONSE MUST BE A STORY.";
        kernel.FunctionInvoked += HandleDndFunctionInvoked;
        var stepwisePlanner = new FunctionCallingStepwisePlanner(config);
        var planResult = await stepwisePlanner.ExecuteAsync(kernel, ask, cancellationToken)/*await PlanResult(plan, context)*/;
        foreach (var item in planResult.ChatHistory ?? [])
        {
            DndPlannerFunctionHook?.Invoke(new SimpleChatMessage(item.Role.ToString(), item.Content ?? ""));
        }
        return planResult.FinalAnswer;


    }
    private void HandleDndFunctionInvoked(object? sender, FunctionInvokedEventArgs invokedArgs)
    {
        var name = invokedArgs.Function.Name;
        var plugin = invokedArgs.Function.Metadata.PluginName;
        DndPlannerFunctionHook?.Invoke(new SimpleChatMessage("ToolCall", $"{plugin}_{name}"));
    }
    
    #endregion

}
public record SimpleChatMessage(string Role, string Content);