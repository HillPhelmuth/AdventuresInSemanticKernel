using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using Microsoft.SemanticKernel.Connectors.Memory.Redis;
using Microsoft.SemanticKernel.Connectors.Memory.Sqlite;
using Microsoft.SemanticKernel.Connectors.Memory.Weaviate;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.Planners;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Reliability.Basic;
using NCalcPlugins;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Plugins;
using SkPluginLibrary.Services;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using StringHelpers = SkPluginLibrary.Models.Helpers.StringHelpers;

namespace SkPluginLibrary;

public partial class CoreKernelService : ICoreKernelExecution, ISemanticKernelSamples, IMemoryConnectors, ITokenization, ICustomNativePlugins, ICustomCombinations, IChatWithSk
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryStore _memoryStore;
    private readonly CompilerService _compilerService;
    private readonly ScriptService _scriptService;
    private readonly HdbscanService _hdbscanService;
    private IMemoryStore? _sqliteStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly BingWebSearchService _bingSearchService;
    private readonly ISemanticTextMemory _semanticTextMemory;
    public class CollectionName
    {
        public const string ClusterCollection = "clusterCollection";
        public const string ChatCollection = "chatCollection";
        public const string SkDocsCollection = "skDocsCollection";
        public const string SkCodeCollection = "skCodeCollection";
    }
    public CoreKernelService(IConfiguration configuration, ScriptService scriptService, CompilerService compilerService, HdbscanService hdbscanService,  ILoggerFactory loggerFactory, BingWebSearchService bingSearchService)
    {
        _configuration = configuration;
        _scriptService = scriptService;
        _compilerService = compilerService;
        _hdbscanService = hdbscanService;
        _loggerFactory = loggerFactory;
        _bingSearchService = bingSearchService;

        _memoryStore = new VolatileMemoryStore();
        CreateKernel();
        _semanticTextMemory = new MemoryBuilder()
            .WithMemoryStore(_memoryStore)
            .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", TestConfiguration.OpenAI!.ApiKey)
            .WithLoggerFactory(_loggerFactory)
            .Build();
    }

    public static IKernel ChatCompletionKernel(string chatModel = "gpt-3.5-turbo-1106")
    {
        return new KernelBuilder().WithOpenAIChatCompletionService(chatModel, TestConfiguration.OpenAI!.ApiKey, alsoAsTextCompletion: true)
            .Build();
    }
    private IKernel CreateKernel(string chatModel = "gpt-3.5-turbo-1106")
    {
        var kernel = new KernelBuilder()
            .WithLoggerFactory(_loggerFactory)
            .WithOpenAIChatCompletionService(chatModel, TestConfiguration.OpenAI!.ApiKey, alsoAsTextCompletion: true)
            .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .WithRetryBasic(new BasicRetryConfig { MaxRetryCount = 3, MinRetryDelay = TimeSpan.FromSeconds(1), UseExponentialBackoff = true })
            .Build();
        kernel.FunctionInvoked += FunctionInvokedHandler;
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

    private ISemanticTextMemory? CreateSemanticMemory(MemoryStoreType memoryStoreType)
    {
        return new MemoryBuilder()
            .WithMemoryStore(CreateMemoryStore(memoryStoreType))
            .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .WithLoggerFactory(_loggerFactory)
            .Build();
    }
    private async Task<IKernel> CreateSqliteKernel(string chatModel = "gpt-3.5-turbo-1106")
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ConnectionString!);

        _sqliteStore = sqliteMemoryStore;
        var collections = await _sqliteStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        return new KernelBuilder()
            .WithLoggerFactory(_loggerFactory)
            .WithOpenAIChatCompletionService(chatModel, TestConfiguration.OpenAI!.ApiKey, alsoAsTextCompletion: true)
            .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .WithRetryBasic(new BasicRetryConfig { MaxRetryCount = 3, MinRetryDelay = TimeSpan.FromSeconds(1), UseExponentialBackoff = true })
            .Build();
    }

    private async Task<ISemanticTextMemory?> CreateSqliteMemoryAsync(bool isSkChat = false)
    {
        var connectionString = isSkChat ? TestConfiguration.Sqlite!.ChatContentConnectionString : TestConfiguration.Sqlite!.ConnectionString!;
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(connectionString);

        _sqliteStore = sqliteMemoryStore;
        var collections = await _sqliteStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        return new MemoryBuilder()
            .WithMemoryStore(_sqliteStore)
            .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .WithLoggerFactory(_loggerFactory)
            .Build();
    }

    public static ISemanticTextMemory CreateMemoryStore(IMemoryStore memory)
    {
        return new MemoryBuilder()
            .WithMemoryStore(memory)
            .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .Build();
    }
    private async Task<IKernel> ChatWithSkKernal(string chatModel = "gpt-3.5-turbo-1106")
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);

        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        if (!collections.Contains(CollectionName.SkDocsCollection))
        {
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.SkDocsCollection);
            await GenerateAndSaveEmbeddings();
        }

        return new KernelBuilder()
            .WithLoggerFactory(_loggerFactory)
            .WithOpenAIChatCompletionService(chatModel, TestConfiguration.OpenAI!.ApiKey, alsoAsTextCompletion: true)
            .WithOpenAITextEmbeddingGenerationService("text-embedding-ada-002", TestConfiguration.OpenAI.ApiKey)
            .WithRetryBasic(new BasicRetryConfig { MaxRetryCount = 3, MinRetryDelay = TimeSpan.FromSeconds(1), UseExponentialBackoff = true })
            .Build();
    }

    private async Task<ISemanticTextMemory> ChatWithSkKernelMemory()
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        if (!collections.Contains(CollectionName.SkDocsCollection))
        {
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.SkDocsCollection);
            await GenerateAndSaveEmbeddings();
        }
        return new MemoryBuilder()
            .WithMemoryStore(sqliteMemoryStore)
            .WithLoggerFactory(_loggerFactory)
            .WithOpenAITextEmbeddingGenerationService(TestConfiguration.OpenAI.EmbeddingModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();
    }

    public static async Task<ISemanticTextMemory> CodeWithSkKernelMemory()
    {
        var filename = TestConfiguration.Sqlite!.ChatContentConnectionString!/* _configuration["Sqlite:CodeContentConnectionString"]*/;
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(filename);
        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        if (!collections.Contains(CollectionName.SkCodeCollection))
        {
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.SkCodeCollection);
            await GenerateAndSaveCodeEmbeddings();
        }
        return new MemoryBuilder()
            .WithMemoryStore(sqliteMemoryStore)
            .WithOpenAITextEmbeddingGenerationService(TestConfiguration.OpenAI.EmbeddingModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();
    
    }

    private static async Task GenerateAndSaveCodeEmbeddings()
    {
        var memory = await CodeWithSkKernelMemory();
        var pathExamples = @"C:\Users\adamh\source\repos\AdventuresInSemanticKernel\SkPluginLibrary\Examples";
        var pathGenDocs = Path.Combine(pathExamples, "GenDocs");
        var readMeFiles = Directory.EnumerateFiles(pathGenDocs, "*.md", SearchOption.AllDirectories);
        foreach (var file in readMeFiles)
        {
            var text = await File.ReadAllTextAsync(file);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file);
            var codeFilePath = Path.Combine(RepoFiles.CodeTextDirectoryPath, $"{fileNameWithoutExtension.Replace(".readme",".txt")}");

            var code = await File.ReadAllTextAsync(codeFilePath);
            //var codeLines = TextChunker.SplitPlainTextLines(code, 128, StringHelpers.GetTokens);
            //var codeSections = TextChunker.SplitPlainTextParagraphs(codeLines, 512, 128, fileNameWithoutExtension, StringHelpers.GetTokens);
            await memory.SaveInformationAsync(CollectionName.SkCodeCollection, text, fileNameWithoutExtension, additionalMetadata:code);
        }
        
    }
    private async Task<bool> GenerateAndSaveEmbeddings()
    {
        var memory = await ChatWithSkKernelMemory();
        var repoPath = @"C:\Users\adamh\source\repos\semantic-kernel-docs\semantic-kernel";
        var readmeFiles = Directory.EnumerateFiles(repoPath, "*.md", SearchOption.AllDirectories);
        var sb = new StringBuilder();
        var paragraphs = new List<string>();
        foreach (var content in readmeFiles)
        {
            var pageText = await File.ReadAllTextAsync(content);
            var lines = TextChunker.SplitMarkDownLines(pageText, 128, StringHelpers.GetTokens);
            var innerparagraphs = TextChunker.SplitMarkdownParagraphs(lines, 512, 128, $"{Path.GetFileNameWithoutExtension(content)}", StringHelpers.GetTokens);
            paragraphs.AddRange(innerparagraphs);
            //sb.Append(pageText);
        }

        var index = 0;
        Console.WriteLine($"SK README.md - saving {paragraphs.Count} chunks");
        foreach (var paragraph in paragraphs)
        {
            await memory.SaveInformationAsync(CollectionName.SkDocsCollection, paragraph, $"SkMd_p{++index}");
        }
        return true;
    }

    private const string ChatWithSkSystemPromptTemplate = """
    You are a Semantic Kernel Expert and a helpful and friendly Instructor. Use the [Semantic Kernel CONTEXT] below to answer the user's questions. 

    [Semantic Kernel CONTEXT]
    {{$memory_context}}

    {{$history}}
    
    """;
    private IKernel? _skChatKernel;
    public async IAsyncEnumerable<string> ExecuteChatWithSkStream(string query, string? history = null)
    {
        _skChatKernel ??= await ChatWithSkKernal();
        var semanticMemory = await CreateSqliteMemoryAsync(true);
        var memoryItems = await semanticMemory.SearchAsync(CollectionName.SkDocsCollection, query, 10, 0.75).ToListAsync();
        var memory = string.Join("\n", memoryItems.Select(x => x.Metadata.Text));
        _loggerFactory.CreateLogger<CoreKernelService>().LogInformation("Memory:\n {memory}", memory);
        var context = _skChatKernel.CreateNewContext();
        context.Variables["memory_context"] = memory;
        if (history is not null)
            context.Variables["history"] = history;
        var engine = _skChatKernel.PromptTemplateEngine;
        var systemPrompt = await engine.RenderAsync(ChatWithSkSystemPromptTemplate, context);
        var chatService = _skChatKernel.GetService<IChatCompletion>();
        var chat = chatService.CreateNewChat(systemPrompt);
        chat.AddUserMessage(query);
        await foreach (var token in chatService.GenerateMessageStreamAsync(chat, new OpenAIRequestSettings { MaxTokens = 2000, Temperature = 1 }))
        {
            yield return token;
        }

    }
    public event EventHandler<string>? StringWritten;
    public event EventHandler<string>? KernelError;

    #region D&D Writer with Sequential Planner and DndApiSkill (DndOpenApiSkillPage.razor)

    public async Task<string> SequentialDndApi(string characterDescription, (string Race, string Class, string Alignment) details)
    {
        //var monsterDescription = await GetDndMonsterDescription();
        var initialDescription = characterDescription;
        var detailString = $" a {details.Race} {details.Class} with a {details.Alignment} alignment";
        characterDescription += detailString;
        var planGenKernel = CreateKernel("gpt-4-1106-preview");
        var dndSkill = await planGenKernel.ImportPluginFunctionsAsync("DndApiPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "DndApiPlugin", "openapi.json"), new OpenApiFunctionExecutionParameters { IgnoreNonCompliantErrors = true });
        var writer = planGenKernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "WriterPlugin");
        planGenKernel.ImportFunctions(new DndPlugin());
        var context = await PopulateContext(dndSkill["Monsters"]);
        context.Variables["race"] = details.Race;
        context.Variables["class"] = details.Class;
        context.Variables["alignment"] = details.Alignment;
        context.Variables["characterDetails"] = characterDescription;
        context.Variables["charcterDescription"] = initialDescription;
        var config = new SequentialPlannerConfig()
        {
            SemanticMemoryConfig = new SemanticMemoryConfig
            {
                MaxRelevantFunctions = 10,
                RelevancyThreshold = .75,
                Memory = _semanticTextMemory
            },
            MaxTokens = 1500,
        };
        var included = new List<string>() { "ParseCharacterInfo", "Races", "Alignments", "Classes", "ShortStory", "DndMonster" };
        var excluded = new List<string> { "AbilityScores", "RacesTraits", "GenerateMonsterDescription", "RacesProficiencies", "Monster", "Monsters" };
        foreach (var include in included)
        {
            config.SemanticMemoryConfig.IncludedFunctions.Add(("DndApiPlugin", include));
        }

        excluded.AddRange(dndSkill.Keys.Where(function => !included.Contains(function)));
        var excludeditems = string.Join(", ", excluded);
        _loggerFactory.LogInformation("Excluded functions: {excludeditems}", excludeditems);
        foreach (var exclude in excluded)
        {
            config.ExcludedFunctions.Add(exclude);
        }
        var ask = $"Invent a D&D character based on the description below as the protagonist. Generate a short story using the available relevant details of the character and a DndMonster as a primary antagonist.\ndescription: \n {characterDescription}.";
        var planner = new SequentialPlanner(planGenKernel, config).WithInstrumentation(_loggerFactory);
        Plan plan = new(ask);

        try
        {
            plan = await planner.CreatePlanAsync(ask);
            //plan.AddSteps(planb.Steps.ToArray());

        }
        catch (Exception ex)
        {
            var exceptionLog = $"{ex.Message}\n{ex.StackTrace}\n{ex.InnerException}";

            //Console.WriteLine($"Failed to create plan. \n{exceptionLog}");
            _loggerFactory.LogInformation("Failed to create plan. \n{exceptionLog}", exceptionLog);
            KernelError?.Invoke(this, exceptionLog);
        }
        var planJson = plan.ToJson();
        Console.WriteLine($"-----------Squential PLAN---------\n{planJson}\n----------------------------");
        await File.WriteAllTextAsync("TestPlan-new.json", planJson);
        //var planResult = await ExecutePlanAsync(_kernel, plan, _kernel.CreateNewContext());

        var planResult = await PlanResult(plan, context);
        return planResult?.GetValue<string>() ?? "no result";

    }

    private async Task<SKContext> PopulateContext(ISKFunction dndSkill)
    {
        var random = new Random();
        var kernel = CreateKernel();
        var context = kernel.CreateNewContext();
        if (!context.Variables.ContainsKey("challenge_rating"))
        {
            var rating = random.Next(3, 12);
            context.Variables.Set("challenge_rating", JsonSerializer.Serialize(new List<string> { rating.ToString() }.ToArray()));
        }

        var monstersResult = await kernel.RunAsync(context.Variables, dndSkill);
        var semanticSkills = kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, "SummarizePlugin",
            "WriterPlugin", "MiscPlugin");
        var result = monstersResult.GetValue<object>();
        var monsterList = new MonsterList() { Count = 1, Results = new List<Result> { new() { Index = "young-black-dragon", Name = "Young Black Dragon" } } };
        if (result is RestApiOperationResponse restApiResponse)
        {
            var content = restApiResponse.Content.ToString();
            monsterList = JsonSerializer.Deserialize<MonsterList>(content);
        }


        var index = random.Next(0, monsterList.Results.Count);
        var monsterResult = monsterList.Results[index];
        var summarizeMonsterFunction = semanticSkills["MonsterGen"];
        var monsterDescription = await kernel.RunAsync(monsterResult.Name, summarizeMonsterFunction);
        //kernel.ImportFunctions(new DndSkill(_kernel));
        context.Variables["monster"] = monsterDescription.Result();
        return context;
    }

    private async Task<KernelResult?> PlanResult(Plan plan, SKContext ctx)
    {
        //var ctx = _kernel.CreateNewContext();
        try
        {

            var sw = new Stopwatch();
            sw.Start();

            _loggerFactory.LogInformation("Plan:\n{plan.ToPlanString()}", plan.ToPlanString());
            var kernel = CreateKernel();
            var planResult = await kernel.RunAsync(plan, ctx.Variables); /*await plan.InvokeAsync(ctx);*/


            var result = planResult.GetValue<string>();

            var timeTaken = "Time Taken: " + sw.Elapsed;
            Console.WriteLine(timeTaken);
            _loggerFactory.LogInformation("Plan {timeTaken}", timeTaken);
            return planResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to execute plan.\n{ex.Message}");
            Console.WriteLine($"Internal Plan Error:\n{ex.InnerException}");
            KernelError?.Invoke(this, $"Failed to execute plan.\n{ex}");
            return null;
        }
    }

    #endregion

}