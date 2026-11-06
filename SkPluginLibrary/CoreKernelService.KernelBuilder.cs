using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ChatComponents;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using SemanticKernelAgentOrchestration.Models;
using SkPluginComponents;
using SkPluginComponents.Models;
using SkPluginLibrary.Plugins.NativePlugins;
using SkPluginLibrary.Services;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    private const string SkillStepsHeader = "<tr><th>Name</th> <th>Value</th></tr>";

    #region Tool (plugin) catalog - YAML prompt tools, native C# tools, OpenAPI tools

    public async Task<string> ExecuteFunction(AIFunction function, Dictionary<string, object>? variables = null)
    {
        var args = variables?.ToDictionary(x => x.Key, x => (object?)x.Value);
        return await function.InvokeToStringAsync(args);
    }

    private Dictionary<string, object>? _nativePlugins;
    private Dictionary<string, object>? _customNativePlugins;

    private Dictionary<string, object> GetCoreNativePlugins()
    {
        _nativePlugins ??= new Dictionary<string, object>();
        var chatClient = CreateChatClient();
        _nativePlugins.TryAdd(nameof(FileIOPlugin), new FileIOPlugin());
        _nativePlugins.TryAdd(nameof(HttpPlugin), new HttpPlugin());
        _nativePlugins.TryAdd(nameof(TimePlugin), new TimePlugin());
        _nativePlugins.TryAdd("TextMemoryPlugin", new MemoryToolPlugin(_textMemory));
        _nativePlugins.TryAdd(nameof(TextPlugin), new TextPlugin());
        _nativePlugins.TryAdd(nameof(ConversationSummaryPlugin), new ConversationSummaryPlugin(chatClient));
        _nativePlugins.TryAdd(nameof(WebSearchEnginePlugin), new WebSearchEnginePlugin(_bingSearchService));
        return _nativePlugins;
    }

    private Dictionary<string, object> GetCustomNativePlugins()
    {
        _customNativePlugins ??= [];
        var chatClient = CreateChatClient();
        try
        {
            _customNativePlugins.TryAdd(nameof(AgenticPlanningPlugin), new AgenticPlanningPlugin(_loggerFactory, new AgentPlanState()));
            _customNativePlugins.TryAdd(nameof(NovelWriterPlugin), new NovelWriterPlugin());
            try
            {
                _customNativePlugins.TryAdd(nameof(ReplCsharpPlugin), new ReplCsharpPlugin(chatClient));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading C# repl plugin");
            }

            _customNativePlugins.TryAdd(nameof(CodeExecutorPlugin), new CodeExecutorPlugin());
            _customNativePlugins.TryAdd(nameof(WebCrawlPlugin), new WebCrawlPlugin(_bingSearchService));
            _customNativePlugins.TryAdd(nameof(WebResearchPlugin), new WebResearchPlugin(_bingSearchService, _configuration, _loggerFactory));
            _customNativePlugins.TryAdd(nameof(ArxivPlugin), new ArxivPlugin(ConsoleLogger.LoggerFactory));
            _customNativePlugins.TryAdd(nameof(ArxivResearchPlugin), new ArxivResearchPlugin(_configuration, _loggerFactory));
            _customNativePlugins.TryAdd(nameof(DndPlugin), new DndPlugin(chatClient));
            _customNativePlugins.TryAdd(nameof(HandleJsonPlugin), new HandleJsonPlugin());
            _customNativePlugins.TryAdd(nameof(PromptExpertPlugin), new PromptExpertPlugin(_configuration));
            _customNativePlugins.TryAdd(nameof(WikiChatPlugin), new WikiChatPlugin());
            _customNativePlugins.TryAdd(nameof(YouTubePlugin), new YouTubePlugin(_configuration["YouTubeSearch:ApiKey"]!));
            _customNativePlugins.TryAdd(nameof(YouTubeResearchPlugin), new YouTubeResearchPlugin(_configuration, _loggerFactory));
            _customNativePlugins.TryAdd(nameof(AskUserPlugin), new AskUserPlugin(_askUserService));
            _customNativePlugins.TryAdd(nameof(BlazorMemoryPlugin), new BlazorMemoryPlugin());
            _customNativePlugins.TryAdd(nameof(KernelChatPlugin), new KernelChatPlugin());
            _customNativePlugins.TryAdd(nameof(LangchainChatPlugin), new LangchainChatPlugin());
            _customNativePlugins.TryAdd(nameof(WeatherPlugin), new WeatherPlugin());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading custom native plugins");
        }

        return _customNativePlugins;
    }

    private IEnumerable<string> CustomNativePluginNames =>
        _customNativePlugins?.Keys.ToList() ?? [.. GetCustomNativePlugins().Keys];

    private IEnumerable<string> CoreNativePluginNames => _nativePlugins?.Keys.ToList() ?? [.. GetCoreNativePlugins().Keys];

    private static ToolPlugin ToToolPlugin(string name, object instance) =>
        instance is AskUserPlugin askUser ? ToolPlugin.FromTools(name, askUser.GetTools()) : ToolPlugin.FromObject(instance, name);

    private static IEnumerable<string?> SemanticPlugins =>
        Directory.GetDirectories(RepoFiles.PathToYamlPlugins).Select(Path.GetFileName).ToList();

    private static IEnumerable<string?> ApiPlugins => Directory.GetDirectories(RepoFiles.ApiPluginDirectoryPath)
        .Select(Path.GetFileName).ToList();

    public async Task<Dictionary<PluginType, List<ToolPlugin>>> GetAllPlugins()
    {
        var result = new Dictionary<PluginType, List<ToolPlugin>>
        {
            { PluginType.Prompt, []}, { PluginType.Native, []}, { PluginType.Api, []}
        };
        var chatClient = CreateChatClient();
        result[PluginType.Prompt] = SemanticPlugins.Where(x => x is not null)
            .Select(x => KernelPluginExtensions.CreatePluginFromPromptDirectoryYaml(x!, () => chatClient)).ToList();
        var coreNative = GetCoreNativePlugins().Select(x => ToToolPlugin(x.Key, x.Value));
        var customNative = GetCustomNativePlugins().Select(x => ToToolPlugin(x.Key, x.Value));
        result[PluginType.Native] = coreNative.Concat(customNative).ToList();

        var tasks = ApiPlugins.Where(api => api is not null).Select(GetApiPluginFunctions).ToList();
        var apiResults = await Task.WhenAll(tasks);
        result[PluginType.Api] = [.. apiResults];
        return result;
    }

    private Task<ToolPlugin> GetApiPluginFunctions(string? api)
    {
        if (string.IsNullOrEmpty(api)) throw new ArgumentNullException(nameof(api));
        return OpenApiToolFactory.FromFileAsync(Path.GetFileName(api), Path.Combine(RepoFiles.ApiPluginDirectoryPath, api, "openapi.json"));
    }

    public async Task<Dictionary<string, AIFunction>> GetExternalPluginFunctions(ChatGptPluginManifest manifest)
    {
        var uri = manifest.Api.Url;
        var replaceNonAsciiWithUnderscore = manifest.NameForModel.ReplaceNonAsciiWithUnderscore();
        ToolPlugin plugin;
        try
        {
            plugin = await OpenApiToolFactory.FromUrlAsync(replaceNonAsciiWithUnderscore, uri.ToString(),
                string.IsNullOrEmpty(manifest.OverrideUrl) ? null : manifest.OverrideUrl, _httpClientFactory.CreateClient());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw new Exception($"Error importing plugin from {uri}", ex);
        }
        return plugin.Functions.ToDictionary(f => f.Name, f => f);
    }

    #endregion

    #region Chat modes: tool-calling agent, step-by-step agent, plan-and-execute workflow

    private ChatHistory _chatHistory = [];
    private readonly JsonSerializerOptions _optionsAsIndented = JsonExtensions.JsonOptionsIndented;
    public event Action<string>? YieldAdditionalText;

    /// <summary>Collects the tools an agent may use from the request's selected plugins, honouring excluded functions.</summary>
    private static List<AITool> ToolsFor(ChatRequestModel request)
    {
        var excluded = request.ExcludedFunctions ?? [];
        return request.SelectedPlugins
            .SelectMany(plugin => plugin.Functions.Where(f => !excluded.Contains(f.Name)).Select(plugin.Prefixed))
            .ToList();
    }

    private AgentFunctionMiddleware CreateReportingMiddleware(bool showArguments)
    {
        var middleware = new AgentFunctionMiddleware();
        middleware.FunctionInvoking += (_, call) => HandleFunctionInvoking(call, showArguments);
        middleware.FunctionInvoked += (_, call) => HandleFunctionInvoked(call);
        return middleware;
    }

    public async IAsyncEnumerable<string> ChatWithAutoFunctionCalling(string query, ChatRequestModel requestModel, bool runAsChat = false, string? askOverride = null, [EnumeratorCancellation] CancellationToken cancellationToken = default, bool resetChat = false)
    {
        if (resetChat)
            _chatHistory = [];
        var systemPrompt = "You are a helpful assistant. Use the tools available to fulfill the user's request";
        var chatClient = CreateChatClient(requestModel.SelectedModel);
        var agent = CreateAgent(chatClient, systemPrompt, ToolsFor(requestModel), "ToolAgent", middleware: [CreateReportingMiddleware(false)]);
        if (!runAsChat)
        {
            await foreach (var token in StreamText(agent, [new ChatMessage(ChatRole.User, query)], cancellationToken))
                yield return token;
        }
        else
        {
            _chatHistory.AddUserMessage(query);
            var assistantMessage = new StringBuilder();
            await foreach (var token in StreamText(agent, _chatHistory, cancellationToken))
            {
                assistantMessage.Append(token);
                yield return token;
            }
            _chatHistory.AddAssistantMessage(assistantMessage.ToString());
        }
    }

    private const string StepwiseInstructions =
        """
        You are a methodical problem solver. Solve the user's goal step by step.
        On each step, briefly think about what information you still need, then call one of the available tools to get it.
        Use the Think-style reasoning in your own words, never guess at facts a tool can provide, and stop calling tools once you can answer.
        When you have everything you need, reply with the complete final answer for the user.
        """;

    public async IAsyncEnumerable<string> ChatWithStepwiseAgent(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default, bool resetChat = false)
    {
        // A tool-calling agent is already a "plan as you go" loop. Cap its iterations so it cannot run away.
        var chatClient = ChatClientFactory.Create(chatRequestModel.SelectedModel == AIModel.None ? AIModel.Gpt4OCurrent : chatRequestModel.SelectedModel, maxIterations: 15, loggerFactory: _loggerFactory);
        var middleware = CreateReportingMiddleware(true);
        var agent = CreateAgent(chatClient, StepwiseInstructions, ToolsFor(chatRequestModel), "StepwiseAgent",
            new ChatOptions { MaxOutputTokens = 16000 }, [middleware]);

        askOverride ??= query;
        yield return "<br/><h3>Executing plan...</h3><br/>";
        var response = await agent.RunAsync(askOverride, cancellationToken: cancellationToken);
        var historyBuilder = new StringBuilder();
        historyBuilder.AppendLine("<ol>");
        foreach (var message in response.Messages)
        {
            historyBuilder.AppendLine($"<li>{message.Role} - {message.Text}</li>");
        }
        historyBuilder.AppendLine("</ol>");
        yield return $"""
        <details>
          <summary>Internal Chat History</summary>
          
          <h5>Plan</h5>
          <p>
          {historyBuilder}
          </p>
          <br/>
        </details>
        """;
        var finalResult = response.Text;
        if (runAsChat)
        {
            yield return "<h3> Execution Complete</h3><br/><h4> Chat:</h4><hr/>";
            await foreach (var p in ExecuteChatStream(query, finalResult, chatRequestModel.SelectedModel).WithCancellation(cancellationToken))
                yield return p;
        }
        else
        {
            yield return "<h3> Execution Complete<h3>";
            yield return "<h4> Final Result<h4>";
            yield return finalResult;
        }
    }

    private const string PlannerInstructions =
        """
        You are a planning specialist. Given the user's goal and the list of available tools, write a clear, numbered, step-by-step plan.
        Each step names the tool to call (if any), the inputs it needs, and the output expected from that step.
        Only use the listed tools. Do NOT execute the plan and do NOT answer the goal yourself - output the plan only.
        """;

    private const string ExecutorInstructions =
        """
        You are an execution specialist. You will be given a plan. Carry out each step of the plan in order using the available tools,
        adapting if a tool returns something unexpected. When every step is done, reply with the complete final answer to the user's original goal.
        """;

    public async IAsyncEnumerable<string> ChatWithPlanAndExecuteWorkflow(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default, bool resetChat = false)
    {
        var model = chatRequestModel.SelectedModel == AIModel.None ? AIModel.Gpt4OCurrent : chatRequestModel.SelectedModel;
        var chatClient = ChatClientFactory.Create(model, maxIterations: 15, loggerFactory: _loggerFactory);
        var tools = ToolsFor(chatRequestModel);
        var toolList = string.Join("\n", tools.Select(t => $"- {t.Name}: {t.Description}"));
        // Two agents wired into a sequential Agent Framework workflow: planner -> executor.
        var planner = CreateAgent(chatClient, $"{PlannerInstructions}\n\n# Available tools\n{toolList}", name: "Planner");
        var executor = CreateAgent(chatClient, ExecutorInstructions, tools, "Executor", middleware: [CreateReportingMiddleware(true)]);
        var workflow = AgentWorkflowBuilder.BuildSequential(planner, executor);

        var ask = askOverride ?? query;
        yield return "<h2> Generating Plan</h2>";
        var planText = new StringBuilder();
        var finalText = new StringBuilder();
        string? currentExecutor = null;
        List<ChatMessage> input = [new ChatMessage(ChatRole.User, ask)];
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input, cancellationToken: cancellationToken);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
        {
            if (evt is not AgentResponseUpdateEvent update || string.IsNullOrEmpty(update.Update.Text)) continue;
            var isPlanner = update.ExecutorId.Contains("Planner", StringComparison.OrdinalIgnoreCase);
            if (update.ExecutorId != currentExecutor)
            {
                currentExecutor = update.ExecutorId;
                if (!isPlanner) yield return "\n\n<h3>Executing plan...</h3>\n\n";
            }
            if (isPlanner) planText.Append(update.Update.Text);
            else finalText.Append(update.Update.Text);
            yield return update.Update.Text;
        }

        var finalResult = finalText.ToString();
        var planDetails = $"""

                               <details>
                                 <summary>See Plan</summary>
                                 
                                 <h5>Plan</h5>
                                 <p>
                                 {planText}
                                 </p>
                                 <br/>
                               </details>
                               """;
        if (runAsChat)
        {
            yield return "<h3> Execution Complete</h3><br/> Chat:<br/>";
            await foreach (var p in ExecuteChatStream(query, finalResult, chatRequestModel.SelectedModel).WithCancellation(cancellationToken))
                yield return p;
        }
        else
        {
            yield return "<h3> Execution Complete</h3>";
            yield return "<h4> Final Result</h4>\n";
        }
        yield return planDetails;
    }

    private void HandleFunctionInvoking(AgentFunctionCall call, bool showArguments)
    {
        YieldAdditionalText?.Invoke($"\n<h4> Executing {call.Function.Name}</h4>\n\n");
        if (!showArguments) return;
        var metaData = $"<strong>{JsonSerializer.Serialize(call.Arguments, _optionsAsIndented)}</strong>";
        YieldAdditionalText?.Invoke($"""

                               <details>
                                 <summary>See Arguments</summary>
                                 
                                 <h5>Arguments</h5>
                                 <p>
                                 {metaData}
                                 </p>
                                 <br/>
                               </details>
                               """);
    }

    private void HandleFunctionInvoked(AgentFunctionCall call)
    {
        YieldAdditionalText?.Invoke($"\n<h4> {call.Function.Name} Completed</h4>\n\n");
        var result = $"<strong>{KernelPluginExtensions.ResultToString(call.Result)}</strong>";
        YieldAdditionalText?.Invoke($"""

                              <details>
                                <summary>See Results</summary>
                                
                                <h5>Results</h5>
                                <p>
                                {result}
                                </p>
                                <br/>
                              </details>
                              """);
    }

    private async IAsyncEnumerable<string> ExecuteChatStream(string query, string result, AIModel model)
    {
        var instructions =
            $"""
                You are "Adventures in Microsoft Agent Framework" a helpful, intelligent and resourceful AI chatbot. Respond to the user to the best of your ability using the following Context. If Context indicates an error. Aplogize for the error and explain the error as well as possible.
                The following [Context] is based on the user's query and with in a medium with access to real-time information and web access. Assume [Context] was generated with access to real-time information. Use the [Context] along with the query to respond. Respond with the exact language in [Context] when appropriate.
                NEVER REVEAL THAT ANY CONTEXT WAS PROVIDED.
                [Context]
                {result}
                """;
        Console.WriteLine($"InitialSysPrompt:\n{instructions}");
        var chatClient = CreateChatClient(model == AIModel.None ? AIModel.Gpt41Mini : model);
        var agent = CreateAgent(chatClient, instructions, name: "ChatAgent", options: new ChatOptions { Temperature = 0.7f, TopP = 1.0f, MaxOutputTokens = 3000 });
        _chatHistory.AddUserMessage(query);
        var response = new StringBuilder();
        await foreach (var token in StreamText(agent, _chatHistory))
        {
            response.Append(token);
            yield return token;
        }
        _chatHistory.AddAssistantMessage(response.ToString());
    }

    #endregion
}
