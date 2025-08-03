using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Pinecone;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Text;
using Pinecone;
using Polly;
using SkPluginComponents;
using SkPluginComponents.Models;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Models.Hooks;
using SkPluginLibrary.Plugins.NativePlugins;
using SkPluginLibrary.Services;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using PDFtoImage;
using SkiaSharp;


namespace SkPluginLibrary;

public partial class CoreKernelService : ICoreKernelExecution, ISemanticKernelSamples, IMemoryConnectors, ITokenization, ICustomNativePlugins, IChatWithSk
{
    private static IConfiguration? _configuration;
    private readonly IMemoryStore _memoryStore;
    private readonly CompilerService _compilerService;
    private readonly ScriptService _scriptService;
    private readonly HdbscanService _hdbscanService;
    private SqliteMemoryStore? _sqliteStore;
    private readonly ILoggerFactory _loggerFactory;
    private readonly BingWebSearchService _bingSearchService;
    private readonly ISemanticTextMemory _semanticTextMemory;
    private readonly IHttpClientFactory _httpClientFactory;
    private static string _appInsightConnectionString;
    private readonly ILogger<CoreKernelService> _logger;
    public class CollectionName
    {
        public const string ClusterCollection = "clusterCollection";
        public const string ChatCollection = "chatCollection";
        public const string SkDocsCollection = "skDocsCollection";
        public const string SkCodeCollection = "skCodeCollection";
        public const string BlazorDocsCollection = "blazorDocsCollection";
        public const string LangchainDocsCollection = "langchainDocsCollection";
        public const string PromptEngineerCollection = "promptEngineerCollection";
    }
    public CoreKernelService(IConfiguration configuration, ScriptService scriptService, CompilerService compilerService,
        HdbscanService hdbscanService, ILoggerFactory loggerFactory, BingWebSearchService bingSearchService,
        AskUserService modalService, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _scriptService = scriptService;
        _compilerService = compilerService;
        _hdbscanService = hdbscanService;
        _loggerFactory = loggerFactory;
        _bingSearchService = bingSearchService;
        _httpClientFactory = httpClientFactory;
        _askUserService = modalService;
        _logger = loggerFactory.CreateLogger<CoreKernelService>();
        _memoryStore = new VolatileMemoryStore();
        CreateKernel();
        _semanticTextMemory = new MemoryBuilder()
            .WithMemoryStore(_memoryStore)
            .WithOpenAITextEmbeddingGeneration("text-embedding-ada-002", TestConfiguration.OpenAI!.ApiKey)
            .WithLoggerFactory(_loggerFactory)
            .Build();
        _appInsightConnectionString = _configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? _configuration["ApplicationInsights:ConnectionString"]!;
    }

    public static Kernel ChatCompletionKernel()
    {
        return Kernel.CreateBuilder().AddAIChatCompletion(AIModel.Gpt41Mini)
            .Build();
    }
    public static Kernel CreateKernel(AIModel aiModel = AIModel.Gpt41Mini)
    {
        var kernelBuilder = GetKernelBuilder(aiModel);
        kernelBuilder.Services.AddSingleton(new AgentPlanState());
        return kernelBuilder
            .Build();
    }

    public static IKernelBuilder GetKernelBuilder(AIModel aiModel)
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(builder =>
        {
            builder.AddConsole();
        });
        if (_configuration is not null)
            kernelBuilder.Services.AddSingleton(_configuration);
        kernelBuilder.Services.ConfigureHttpClientDefaults(c =>
        {
            c.AddStandardResilienceHandler().Configure(o =>
            {
                o.Retry.ShouldHandle = args => ValueTask.FromResult(args.Outcome.Result?.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError);
                o.Retry.BackoffType = DelayBackoffType.Exponential;
                o.AttemptTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(90) };
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(180);
                o.TotalRequestTimeout = new HttpTimeoutStrategyOptions { Timeout = TimeSpan.FromMinutes(5) };
            });
        });
        var providor = aiModel.GetModelProvidors().FirstOrDefault();
        if (providor.Contains("OpenAI"))
            kernelBuilder.AddAIChatCompletion(aiModel);
        if (providor == "GoogleAI")
            kernelBuilder.AddGoogleAIGeminiChatCompletion(aiModel.GetOpenAIModelName(), TestConfiguration.GoogleAI.ApiKey);
        if (providor == "MistralAI")
            kernelBuilder.AddMistralChatCompletion(aiModel.GetOpenAIModelName(), TestConfiguration.MistralAI.ApiKey);
        return kernelBuilder;
    }
    public static Kernel CreateKernelGoogle(string modelId = "gemini-1.0-pro")
    {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId, TestConfiguration.GoogleAI.ApiKey);
        kernelBuilder.Services.AddLogging(builder =>
        {
            builder.AddConsole();
        });
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
        return kernelBuilder.Build();
    }
    private IMemoryStore _playgroundStore = new VolatileMemoryStore();
    private IMemoryStore CreateMemoryStore(MemoryStoreType memoryStoreType)
    {
        return memoryStoreType switch
        {
            MemoryStoreType.InMemory => new VolatileMemoryStore(),            
        };
    }

    private ISemanticTextMemory CreateSemanticMemory(MemoryStoreType memoryStoreType, string model = "text-embedding-3-small")
    {
        return new MemoryBuilder()
            .WithMemoryStore(CreateMemoryStore(memoryStoreType))
            .WithOpenAITextEmbeddingGeneration(model, TestConfiguration.OpenAI.ApiKey)
            .WithLoggerFactory(_loggerFactory)
            .Build();
    }

    private static async Task<ISemanticTextMemory> CreateSqliteMemoryAsync()
    {
        var connectionString = TestConfiguration.Sqlite!.ConnectionString!;
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(connectionString);

        //_sqliteStore = sqliteMemoryStore;
        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        if (collections.Count == 0)
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.ClusterCollection);
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        return new MemoryBuilder()
            .WithMemoryStore(sqliteMemoryStore)
            .WithOpenAITextEmbeddingGeneration(TestConfiguration.OpenAI.EmbeddingModelId, TestConfiguration.OpenAI!.ApiKey)
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .Build();
    }

    public static ISemanticTextMemory CreateMemoryStore(IMemoryStore? memory = null, string model = "text-embedding-3-small")
    {
        memory ??= new VolatileMemoryStore();
        return new MemoryBuilder()
            .WithMemoryStore(memory)
            .WithOpenAITextEmbeddingGeneration(model, TestConfiguration.OpenAI.ApiKey)
            .Build();
    }
    internal static async Task<Kernel> ChatWithSkKernal()
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);

        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        if (!collections.Contains(CollectionName.SkDocsCollection))
        {
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.SkDocsCollection);
        }

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddLogging(builder => builder.AddConsole());
        var kernel = kernelBuilder
            .AddAIChatCompletion(AIModel.Gpt41Mini)
            .Build();
        var semanticMemory = await ChatWithSkKernelMemory();
        var textMemory = KernelPluginFactory.CreateFromObject(new TextMemoryPlugin(semanticMemory), "TextMemoryPlugin");
        kernel.Plugins.Add(textMemory);
        return kernel;
    }
    internal static PineconeVectorStore GetPineconeVectorStore()
    {
        var pineconeApiKey = TestConfiguration.Pinecone!.ApiKey;
        var pineconeEnvironment = TestConfiguration.Pinecone.Environment;
        if (string.IsNullOrEmpty(pineconeApiKey) || string.IsNullOrEmpty(pineconeEnvironment))
        {
            throw new ArgumentException("Pinecone API Key and Environment must be set in the configuration.");
        }
        var client = new PineconeClient(pineconeApiKey);
        return new PineconeVectorStore(client,new PineconeVectorStoreOptions(){EmbeddingGenerator = _textEmbeddingGeneration});
    }

    internal static async Task<ReadOnlyMemory<float>> GenerateEmbeddingsAsync(string text)
    {
        var result = await _textEmbeddingGeneration.GenerateAsync([text]);
        return result[0].Vector;
    }
    internal static async Task<PineconeCollection<string, VectorStoreContextItem>> ChatWithSKVectorCollection()
    {
        var vectorStore = GetPineconeVectorStore();
        var collection = vectorStore.GetCollection<string, VectorStoreContextItem>(TestConfiguration.Pinecone.Environment);
        await collection.EnsureCollectionExistsAsync();
        return collection;
    }
    internal static async Task<ISemanticTextMemory> ChatWithSkKernelMemory(bool drop = false)
    {
        var sqliteMemoryStore = await SqliteMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
        var collections = await sqliteMemoryStore.GetCollectionsAsync().ToListAsync();
        Console.WriteLine($"Collections: {string.Join((string?)"\n", collections)}");
        if (!collections.Contains(CollectionName.SkDocsCollection))
        {
            await sqliteMemoryStore.CreateCollectionAsync(CollectionName.SkDocsCollection);
        }
        else if (drop)
        {
            await sqliteMemoryStore.DeleteCollectionAsync(CollectionName.SkDocsCollection);
        }
        return new MemoryBuilder()
            .WithMemoryStore(sqliteMemoryStore)
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            .WithOpenAITextEmbeddingGeneration(TestConfiguration.OpenAI.EmbeddingModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();
    }
    private VectorStoreCollection<string, VectorStoreContextItem>? _sqliteCollection;
    private async Task<IEnumerable<string>> SearchPinecone(string text, int limit = 10, CancellationToken cancellationToken = default)
    {
        var collection = await ChatWithSKVectorCollection();
        var vectorSearchResults = await collection.SearchAsync(text, limit, cancellationToken: cancellationToken).ToListAsync(cancellationToken: cancellationToken);
        foreach (var result in vectorSearchResults)
        {
            Console.WriteLine($"Found: {result.Record.MemoryId} with score: {result.Score}");
        }
        var search = vectorSearchResults.Select(x => x.Record.Content);
        return search;
    }
    public static async Task SaveNewSKPdf()
    {
        var pdfPath = @"C:\Users\adamh\Downloads\semantic-kernel_Microsoft Learn_0802.pdf";
        var fileData = await File.ReadAllBytesAsync(pdfPath);
        //var fileDataBase64 = Convert.ToBase64String(fileData);
        var pdfImages = Conversion.ToImages(fileData);
        var count = pdfImages.Count();
        Console.WriteLine($"Pdf page images: {count}");
        List<byte[]> rawImageData = [];
        foreach (var bitmap in pdfImages)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            var raw = data.ToArray();
            rawImageData.Add(raw);
        }
        var rawCount = rawImageData.Count;
        if (rawCount != count)
        {
            Console.WriteLine($"WARNING: raw data count does not equal initial image count: {rawCount} vs {count}");
            await Task.Delay(TimeSpan.FromSeconds(30));
        }
        var textExtractSp = """
                            Convert the contents of PDF page images into well-formatted, semantic Markdown.
                            Extract all relevant text, headings, lists, tables, code blocks, and formatting as accurately as possible from the images.
                            If math, diagrams, or non-text elements are present, describe them in plain text while clearly indicating it is an image description.
                            """;
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(fileData, new ParsingOptions { UseLenientParsing = true });
        var kernel = CreateKernel(AIModel.Gpt41);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        foreach (var imageData in rawImageData)
        {
            var history = new ChatHistory(textExtractSp)
            {
                new ChatMessageContent(AuthorRole.User,
                [
                    new ImageContent(imageData, "image/jpeg"),
                    new TextContent("Extract content from this pdf page image")
                ])
            };
            var content = await chatService.GetChatMessageContentAsync(history, new OpenAIPromptExecutionSettings { Temperature = 0.5});

            Console.WriteLine($"{content.Content}");
            sb.AppendLine(content.Content ?? "");
        }
        
        //foreach (var page in document.GetPages())
        //{
        //    var pageText = page.Text;
        //    var images = page.GetImages();
        //    var imageBuilder = new StringBuilder();
        //    imageBuilder.AppendLine("## Page Image Descriptions");
        //    var imageIndex = 0;
        //    foreach (var image in images ?? [])
        //    {
        //        var history = new ChatHistory("Provide a detailed description and summary of provided images");
        //        //var imageBytes = image.RawBytes.ToArray();
        //        if (!image.TryGetPng(out var imageBytes))
        //        {
        //            Console.WriteLine("Could not get PNG image bytes");
        //            continue;
        //        }

        //        history.Add(new ChatMessageContent(AuthorRole.User,
        //        [
        //            new ImageContent(new ReadOnlyMemory<byte>(imageBytes), "image/png"),
        //            new TextContent("describe and summarize image")
        //        ]));
        //        var content = await chatService.GetChatMessageContentAsync(history, new OpenAIPromptExecutionSettings { MaxTokens = 512 });
        //        Console.WriteLine($"{content.Content}");
        //        var text = content.Content;
        //        text = $"Image {++imageIndex}\n{text}";
        //        imageBuilder.AppendLine(text);
        //    }

        //    pageText += $"\n{imageBuilder}";

        //    sb.Append(pageText);
        //}
        var textString = sb.ToString();
        await File.WriteAllTextAsync("TempSKContent0802_fromImages.txt", textString);
        var lines = TextChunker.SplitPlainTextLines(textString, 200, StringHelpers.GetTokens);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 1024, 200, tokenCounter: StringHelpers.GetTokens);
        //var memory = await ChatWithSkKernelMemory(true);
        var collection = await ChatWithSKVectorCollection();
        var index = 0;
        var ids = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            await collection.UpsertAsync(new VectorStoreContextItem()
            {
                Content = paragraph,
                Embedding = await GenerateEmbeddingsAsync(paragraph),
                MemoryId = $"SKDocs_P_{index++}"

            });
            //var id = await memory.SaveInformationAsync(CollectionName.SkDocsCollection, paragraph, $"SKDocs_P_{index++}", "semantic-kernel Documentation");
            //ids.Add(id);
        }
        Console.WriteLine($"Saved {ids.Count} Items to {CollectionName.SkDocsCollection}");
    }
    private const string ChatWithSkSystemPromptTemplate =
        """
        You are a Semantic Kernel Expert and a helpful and friendly Instructor. Use the [Semantic Kernel CONTEXT] below to answer the user's questions.

        # Semantic Kernel CONTEXT
        
        {{$context}}

        """;

    private Kernel? _skChatKernel;
    public async IAsyncEnumerable<string> ExecuteChatWithSkStream(string query, ChatHistory? chatHistory = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _skChatKernel ??= await ChatWithSkKernal();
        var searchResults = (await SearchPinecone(query, 5, cancellationToken: cancellationToken)).Where(x => !string.IsNullOrEmpty(x));
        var index = 1;
        var searchContent = $"1. {string.Join($"\n{++index}. ", searchResults)}";
        var context = new KernelArguments
        {
            ["context"] = searchContent
        };
        _logger.LogInformation("Context retrieved:\n\n{context}", searchContent);
        var promptTemplateFactory = new KernelPromptTemplateFactory();
        var engine = promptTemplateFactory.Create(new PromptTemplateConfig(ChatWithSkSystemPromptTemplate));
        var systemPrompt = await engine.RenderAsync(_skChatKernel, context, cancellationToken);
        var chatService = new OpenAIChatCompletionService(TestConfiguration.OpenAI.Gpt4MiniModelId, TestConfiguration.OpenAI.ApiKey);
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

    #region D&D Writer with Sequential Planner and DndApiSkill (DndOpenApiSkillPage.razor)
    public event Action<SimpleChatMessage>? DndPlannerFunctionHook;
    public async Task<string> FunctionCallStepwiseDndApi(string characterDescription,
        (string? Race, string? Class, string? Alignment) details,
        CancellationToken cancellationToken = default)
    {
        var detailString = $" a {details.Race} {details.Class} with a {details.Alignment} alignment";
        characterDescription += detailString;

        var kernel = CreateKernel(AIModel.Gpt4OCurrent);
        var functionHook = new FunctionFilterHook();
        kernel.FunctionInvocationFilters.Add(functionHook);
        var dndApiPlugin = await kernel.ImportPluginFromOpenApiAsync("DndApiPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "DndApiPlugin", "openapi.json"), new OpenApiFunctionExecutionParameters { IgnoreNonCompliantErrors = true }, cancellationToken: cancellationToken);
        var writer = kernel.ImportPluginFromPromptDirectoryYaml("WriterPlugin");
        var dndPlugin = new DndPlugin();
        kernel.ImportPluginFromObject(dndPlugin);
        var askUserplugin = new AskUserPlugin(_askUserService);
        var askUser = kernel.ImportPluginFromObject(askUserplugin, "AskUserPlugin");

        var config = new FunctionCallingStepwisePlannerOptions
        {
            MaxTokens = 9500,
            MaxIterations = 15,
            ExecutionSettings = new OpenAIPromptExecutionSettings { Temperature = 0.3, TopP = 1.0, ModelId = TestConfiguration.OpenAI.Gpt4MiniModelId, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions },

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
        var ask = $"Invent a D&D character based on the description below as the protagonist. Generate a short story using the available relevant details of the character and a DndMonster as a primary antagonist. The DndMonster should be selected from a list of all dnd monsters by Asking the User filtereed by challenge rating, also selected by asking the user.\ndescription: \n {characterDescription}.\n\n YOUR FINAL RESPONSE MUST BE A COMPLETE STORY.";
        functionHook.FunctionInvoked += HandleDndFunctionFilterInvoked;

        var stepwisePlanner = new FunctionCallingStepwisePlanner(config);
        var planResult = await stepwisePlanner.ExecuteAsync(kernel, ask, cancellationToken: cancellationToken)/*await PlanResult(plan, context)*/;
        foreach (var item in planResult.ChatHistory ?? [])
        {
            DndPlannerFunctionHook?.Invoke(new SimpleChatMessage(item.Role.ToString(), item.Content ?? ""));
        }
        return planResult.FinalAnswer;


    }

    private void HandleDndFunctionFilterInvoked(object? sender, FunctionInvocationContext context)
    {
        var name = context.Function.Name;
        var plugin = context.Function.Metadata.PluginName;
        DndPlannerFunctionHook?.Invoke(new SimpleChatMessage("ToolCall", $"{plugin}_{name}"));
    }

    #endregion

}

public record SimpleChatMessage(string Role, string Content);