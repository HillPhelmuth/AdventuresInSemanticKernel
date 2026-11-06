using System.Runtime.CompilerServices;
using System.Text.Json;
using ChatComponents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SemanticKernelAgentOrchestration.Models;
using SkPluginComponents;
using SkPluginComponents.Models;
using SkPluginLibrary.Abstractions;
using SkPluginLibrary.Plugins.NativePlugins;
using SkPluginLibrary.Services;

namespace SkPluginLibrary;

/// <summary>
/// The app's main service. Everything model-facing goes through Microsoft.Extensions.AI chat clients and
/// Microsoft Agent Framework agents/workflows (see <see cref="ChatClientFactory"/>).
/// </summary>
public partial class CoreKernelService : ICoreKernelExecution, ISemanticKernelSamples, IMemoryConnectors, ITokenization, ICustomNativePlugins, IChatWithSk
{
    private static IConfiguration? _configuration;
    private readonly CompilerService _compilerService;
    private readonly ScriptService _scriptService;
    private readonly HdbscanService _hdbscanService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly BingWebSearchService _bingSearchService;
    private readonly TextMemory _textMemory;
    private readonly IHttpClientFactory _httpClientFactory;
    private static string? _appInsightConnectionString;
    private readonly ILogger<CoreKernelService> _logger;
    private readonly AskUserService _askUserService;

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
        _textMemory = new TextMemory(new VectorMemoryStore(), EmbeddingGenerator);
        _appInsightConnectionString = _configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? _configuration["ApplicationInsights:ConnectionString"];
    }

    #region Chat clients and agents

    private static readonly Lazy<IEmbeddingGenerator<string, Embedding<float>>> _embeddingGenerator = new(() => ChatClientFactory.CreateEmbeddingGenerator());
    internal static IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator => _embeddingGenerator.Value;

    /// <summary>Creates a chat client for a model (OpenAI or OpenRouter) with tool invocation enabled.</summary>
    public static IChatClient CreateChatClient(AIModel aiModel = AIModel.Gpt41Mini) => ChatClientFactory.Create(aiModel);

    /// <summary>Creates an Agent Framework agent over a chat client.</summary>
    public static AIAgent CreateAgent(IChatClient chatClient, string instructions, IEnumerable<AITool>? tools = null,
        string name = "Assistant", ChatOptions? options = null, IEnumerable<AgentFunctionMiddleware>? middleware = null, string? description = null)
    {
        var chatOptions = options ?? new ChatOptions();
        chatOptions.Instructions = instructions;
        var toolList = tools?.ToList();
        if (toolList is { Count: > 0 }) chatOptions.Tools = toolList;
        AIAgent agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = ChatAgent.EnsurePattern(name),
            Description = description,
            ChatOptions = chatOptions
        });
        var builder = new AIAgentBuilder(agent);
        var hasMiddleware = false;
        foreach (var m in middleware ?? [])
        {
            builder.Use(m.InvokeAsync);
            hasMiddleware = true;
        }
        return hasMiddleware ? builder.Build() : agent;
    }

    /// <summary>Streams the text of an agent run.</summary>
    internal static async IAsyncEnumerable<string> StreamText(AIAgent agent, IEnumerable<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in agent.RunStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text)) yield return update.Text;
        }
    }

    #endregion

    #region Chat with the documentation (retrieval-augmented agent)

    private const string ChatWithDocsInstructionsTemplate =
        """
        You are a Microsoft Agent Framework and Semantic Kernel Expert and a helpful and friendly Instructor. Use the [CONTEXT] below to answer the user's questions.

        # CONTEXT

        {context}

        """;

    private TextMemory? _docsMemory;
    private async Task<TextMemory> GetDocsMemoryAsync()
    {
        if (_docsMemory is not null) return _docsMemory;
        var store = await SqliteVectorMemoryStore.ConnectAsync(TestConfiguration.Sqlite!.ChatContentConnectionString!);
        _docsMemory = new TextMemory(store, EmbeddingGenerator);
        return _docsMemory;
    }

    public async IAsyncEnumerable<string> ExecuteChatWithSkStream(string query, ChatHistory? chatHistory = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var memory = await GetDocsMemoryAsync();
        var searchResults = await memory.SearchAsync(CollectionName.SkDocsCollection, query, 5, 0.0, cancellationToken: cancellationToken).ToListAsync(cancellationToken);
        var texts = searchResults.Select(x => x.Metadata.Text).Where(x => !string.IsNullOrEmpty(x)).ToList();
        var index = 0;
        var searchContent = texts.Count == 0 ? "" : $"{++index}. {string.Join($"\n{++index}. ", texts)}";
        _logger.LogInformation("Context retrieved:\n\n{context}", searchContent);
        var instructions = ChatWithDocsInstructionsTemplate.Replace("{context}", searchContent);
        var chatClient = ChatClientFactory.Create(TestConfiguration.OpenAI!.Gpt4MiniModelId, ChatClientFactory.OpenAIProvider);
        var agent = CreateAgent(chatClient, instructions, name: "DocsExpert", options: new ChatOptions { MaxOutputTokens = 2000, Temperature = 1 });
        var messages = new List<ChatMessage>(chatHistory ?? []);
        messages.Add(new ChatMessage(ChatRole.User, query));
        await foreach (var token in StreamText(agent, messages, cancellationToken))
            yield return token;
    }

    public event EventHandler<string>? StringWritten;

    #endregion

    #region D&D story writer (agent with tools: DnD API, writer prompts, ask-the-user)

    public event Action<SimpleChatMessage>? DndPlannerFunctionHook;
    public async Task<string> FunctionCallStepwiseDndApi(string characterDescription,
        (string? Race, string? Class, string? Alignment) details,
        CancellationToken cancellationToken = default)
    {
        var detailString = $" a {details.Race} {details.Class} with a {details.Alignment} alignment";
        characterDescription += detailString;

        var chatClient = ChatClientFactory.Create(AIModel.Gpt4OCurrent, _loggerFactory);
        var dndApiPlugin = await OpenApiToolFactory.FromFileAsync("DndApiPlugin", Path.Combine(RepoFiles.ApiPluginDirectoryPath, "DndApiPlugin", "openapi.json"), cancellationToken: cancellationToken);
        var writer = ToolPlugin.FromObject(new NovelWriterPlugin(), "NovelWriterPlugin");
        var writerPrompts = KernelPluginExtensions.CreatePluginFromPromptDirectoryYaml("WriterPlugin", () => chatClient);
        var dndPlugin = ToolPlugin.FromObject(new DndPlugin(chatClient), nameof(DndPlugin));
        var askUser = ToolPlugin.FromTools("AskUserPlugin", new AskUserPlugin(_askUserService).GetTools());

        var included = new List<string> { "ParseCharacterInfo", "Races", "Alignments", "Classes", "ShortStory", "AskUser", "Monster", "Monsters" };
        var tools = new List<AITool>();
        foreach (var plugin in new[] { dndApiPlugin, writerPrompts, dndPlugin, askUser })
            tools.AddRange(plugin.Functions.Where(f => included.Contains(f.Name)).Select(plugin.Prefixed));
        _logger.LogInformation("D&D agent tools: {tools}", string.Join(", ", tools.Select(t => t.Name)));

        var middleware = new AgentFunctionMiddleware();
        middleware.FunctionInvoked += (_, call) => DndPlannerFunctionHook?.Invoke(new SimpleChatMessage("ToolCall", call.Function.Name));
        var agent = CreateAgent(chatClient, "You are a creative D&D storyteller. Work step by step, using the available tools to gather character and monster details, and ask the user when you need a choice made.",
            tools, "DndStoryAgent", new ChatOptions { Temperature = 0.3f, TopP = 1.0f, MaxOutputTokens = 9500 }, [middleware]);
        var ask = $"Invent a D&D character based on the description below as the protagonist. Generate a short story using the available relevant details of the character and a DndMonster as a primary antagonist. The DndMonster should be selected from a list of all dnd monsters by Asking the User filtereed by challenge rating, also selected by asking the user.\ndescription: \n {characterDescription}.\n\n YOUR FINAL RESPONSE MUST BE A COMPLETE STORY.";
        var response = await agent.RunAsync(ask, cancellationToken: cancellationToken);
        foreach (var item in response.Messages)
        {
            DndPlannerFunctionHook?.Invoke(new SimpleChatMessage(item.Role.ToString(), item.Text));
        }
        return response.Text;
    }

    #endregion
}

public record SimpleChatMessage(string Role, string Content);
