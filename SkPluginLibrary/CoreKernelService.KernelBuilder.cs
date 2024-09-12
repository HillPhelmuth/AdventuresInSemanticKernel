using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using SkPluginLibrary.Plugins;
using System.Text.Json;
using SkPluginComponents;
using SkPluginComponents.Models;
using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Microsoft.SemanticKernel.Planning;
using System.Text;
using SkPluginLibrary.Models.Hooks;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Plugins.Core.CodeInterpreter;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    private const string SkillStepsHeader = "<tr><th>Name</th> <th>Value</th></tr>";

    #region Semantic Plugin Picker (SemanticPluginPicker.razor, SequentialPlannerBuilder.razor)


    public async Task<FunctionResult> ExecuteKernelFunction(KernelFunction function,
        Dictionary<string, object>? variables = null)
    {
        var kernel = CreateKernel();
        var args = new KernelArguments();
        if (variables is not null)
        {
            args = [];
        }
        foreach (var item in variables ?? [])
        {
            args[item.Key] = item.Value;
        }

        var result = await kernel.InvokeAsync(function, args);
        return result;
    }


    private Dictionary<string, object>? _nativePlugins;
    private Dictionary<string, object>? _customNativePlugins;

    private Dictionary<string, object> GetCoreNativePlugins()
    {
        _nativePlugins ??= new Dictionary<string, object>();
        //var result = new Dictionary<string, object>();
        var fileIo = new FileIOPlugin();
        _nativePlugins.TryAdd(nameof(FileIOPlugin), fileIo);
        var http = new HttpPlugin();
        _nativePlugins.TryAdd(nameof(HttpPlugin), http);
        var mathPlugin = new MathPlugin();
        _nativePlugins.TryAdd(nameof(MathPlugin), mathPlugin);
        var timePlugin = new TimePlugin();
        _nativePlugins.TryAdd(nameof(TimePlugin), timePlugin);
        var waitPlugin = new WaitPlugin();
        _nativePlugins.TryAdd(nameof(WaitPlugin), waitPlugin);
        var textMemoryPlugin = new TextMemoryPlugin(_semanticTextMemory);
        _nativePlugins.TryAdd(nameof(TextMemoryPlugin), textMemoryPlugin);
        var textPlugin = new TextPlugin();
        _nativePlugins.TryAdd(nameof(TextPlugin), textPlugin);
        var convoSummaryPlugin = new ConversationSummaryPlugin();
        _nativePlugins.TryAdd(nameof(ConversationSummaryPlugin), convoSummaryPlugin);
        var bingConnector = new BingConnector(TestConfiguration.Bing.ApiKey);
        var webSearchPlugin = new WebSearchEnginePlugin(bingConnector);
        _nativePlugins.TryAdd(nameof(WebSearchEnginePlugin), webSearchPlugin);
        var searchUrlPlugin = new SearchUrlPlugin();
        _nativePlugins.TryAdd(nameof(SearchUrlPlugin), searchUrlPlugin);
        return _nativePlugins;
    }

    private Dictionary<string, object> GetCustomNativePlugins()
    {
        _customNativePlugins ??= [];
        //var result = new Dictionary<string, object>();
        var kernel = CreateKernel();
        var novelWriterPlugin = new NovelWriterPlugin();
        _customNativePlugins.TryAdd(nameof(NovelWriterPlugin), novelWriterPlugin);
        var csharpPlugin = new ReplCsharpPlugin(kernel);
        _customNativePlugins.TryAdd(nameof(ReplCsharpPlugin), csharpPlugin);
        var csharpExecutePlugin = new CodeExecuterPlugin();
        _customNativePlugins.TryAdd(nameof(CodeExecuterPlugin), csharpExecutePlugin);
        var webToMarkdownPlugin = new WebToMarkdownPlugin();
        _customNativePlugins.TryAdd(nameof(WebToMarkdownPlugin), webToMarkdownPlugin);
        var webCrawlPlugin = new WebCrawlPlugin(_bingSearchService);
        _customNativePlugins.TryAdd(nameof(WebCrawlPlugin), webCrawlPlugin);
        var dndPlugin = new DndPlugin();
        _customNativePlugins.TryAdd(nameof(DndPlugin), dndPlugin);
        var jsonPlugin = new HandleJsonPlugin();
        _customNativePlugins.TryAdd(nameof(HandleJsonPlugin), jsonPlugin);
        var promptExpertPlugin = new PromptExpertPlugin(_configuration);
        _customNativePlugins.TryAdd(nameof(PromptExpertPlugin), promptExpertPlugin);
        var wikiPlugin = new WikiChatPlugin();
        _customNativePlugins.TryAdd(nameof(WikiChatPlugin), wikiPlugin);
        var youtubePlugin = new YouTubePlugin(kernel, _configuration["YouTubeSearch:ApiKey"]!);
        _customNativePlugins.TryAdd(nameof(YouTubePlugin), youtubePlugin);
        var askUserPlugin = new AskUserPlugin(_askUserService);
        _customNativePlugins.TryAdd(nameof(AskUserPlugin), askUserPlugin);
        var blazorChatPlugin = new BlazorMemoryPlugin();
        _customNativePlugins.TryAdd(nameof(BlazorMemoryPlugin), blazorChatPlugin);
        var chatWithSkPlugin = new KernelChatPlugin();
        _customNativePlugins.TryAdd(nameof(KernelChatPlugin), chatWithSkPlugin);
        var langchainChatPlugin = new LangchainChatPlugin();
        _customNativePlugins.TryAdd(nameof(LangchainChatPlugin), langchainChatPlugin);
        return _customNativePlugins;
    }

    private IEnumerable<string> CustomNativePluginNames =>
        _customNativePlugins?.Keys.ToList() ?? [.. GetCustomNativePlugins().Keys];

    private IEnumerable<string> CoreNativePluginNames => _nativePlugins?.Keys.ToList() ?? [.. GetCoreNativePlugins().Keys];


    private Dictionary<string, KernelFunction> GetNativeFunctionsFromNames(IEnumerable<string> pluginNames, bool isCustom = false)
    {
        var result = new Dictionary<string, KernelFunction>();
        var kernel = CreateKernel();
        var allPlugins = isCustom
            ? _customNativePlugins ?? GetCustomNativePlugins()
            : _nativePlugins ?? GetCoreNativePlugins();
        Dictionary<string, KernelFunction> result1 = result;
        foreach (var plugin in pluginNames)
        {
            var kernelPlugin = kernel.ImportPluginFromObject(allPlugins[plugin], plugin);
            result1 = result1.UpsertConcat(kernelPlugin.ToDictionary(x => x.Name, x => x));
        }

        return result1;
    }

    private static IEnumerable<string?> SemanticPlugins =>
        Directory.GetDirectories(RepoFiles.PathToYamlPlugins).Select(Path.GetFileName).ToList();

    private static IEnumerable<string?> ApiPlugins => Directory.GetDirectories(RepoFiles.ApiPluginDirectoryPath)
        .Select(Path.GetFileName).ToList();

    public async Task<Dictionary<PluginType, List<KernelPlugin>>> GetAllPlugins()
    {
        var result = new Dictionary<PluginType, List<KernelPlugin>>
        {
            { PluginType.Prompt, []}, { PluginType.Native, []}, { PluginType.Api, []}
        };
        //var semantic = SemanticPlugins.Select(x => GetSemanticFunctions(x).ToPlugin(x, PluginType.Prompt));
        var promptPlugins = SemanticPlugins.Select(name => Kernel.CreateBuilder().Build().ImportPluginFromPromptDirectoryYaml(name)).ToList();
        var semanicPlugins = new List<KernelPlugin>();
        result[PluginType.Prompt] = promptPlugins;
        var coreNative = CoreNativePluginNames.Select(x =>
            GetNativeFunctionsFromNames([x]).ToPlugin(x));
        var customNative = CustomNativePluginNames.Select(x =>
            GetNativeFunctionsFromNames([x], true).ToPlugin(x));
        var natives = coreNative.Concat(customNative).ToList();
        result[PluginType.Native] = natives;

        var tasks = ApiPlugins.Where(api => api is not null).Select(GetApiPluginFunctions).ToList();
        var apiResults = await Task.WhenAll(tasks);
        result[PluginType.Api] = [.. apiResults];
        return result;
    }

    private Task<KernelPlugin> GetApiPluginFunctions(string? api)
    {
        if (string.IsNullOrEmpty(api)) throw new ArgumentNullException(nameof(api));
        return Kernel.CreateBuilder().Build().ImportPluginFromOpenApiAsync(Path.GetFileName(api), Path.Combine(RepoFiles.ApiPluginDirectoryPath, api, "openapi.json"), new OpenApiFunctionExecutionParameters { EnableDynamicPayload = true, IgnoreNonCompliantErrors = true, EnablePayloadNamespacing = true });

    }

    public async Task<Dictionary<string, KernelFunction>> GetExternalPluginFunctions(ChatGptPluginManifest manifest)
    {
        var kernel = CreateKernel();
        var uri = manifest.Api.Url;
        var executionParameters = new OpenApiFunctionExecutionParameters(ignoreNonCompliantErrors:true,enableDynamicOperationPayload:true, enablePayloadNamespacing:true);
        if (!string.IsNullOrEmpty(manifest.OverrideUrl))
        {
            executionParameters.ServerUrlOverride = new Uri(manifest.OverrideUrl);
        }

        var replaceNonAsciiWithUnderscore = manifest.NameForModel.ReplaceNonAsciiWithUnderscore();
        KernelPlugin? plugin = null;
        try
        {
            plugin = await kernel.ImportPluginFromOpenApiAsync(replaceNonAsciiWithUnderscore, uri, executionParameters);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw new Exception($"Error importing plugin from {uri}", ex);
        }

        var functionNames = plugin.GetFunctionsMetadata().Select(x => x.Name).ToList();
        var result = new Dictionary<string, KernelFunction>();
        foreach (var functionName in functionNames)
        {
            var function = plugin.TryGetFunction(functionName, out var func) ? func : null;
            if (function is null) continue;
            result.Add(functionName, function);
        }
        return result;
    }

    private ChatHistory _chatHistory = [];

    public async IAsyncEnumerable<string> ChatWithAutoFunctionCalling(string query, ChatRequestModel requestModel, bool runAsChat = false, string? askOverride = null, [EnumeratorCancellation] CancellationToken cancellationToken = default, bool resetChat = false)
    {
        if (resetChat)
            _chatHistory = [];
        var systemPrompt = "You are a helpful assistant. Use the tools available to fulfill the user's request";
        var kernel = CreateKernelWithPlugins(requestModel.SelectedPlugins, requestModel.SelectedModel);
        var functionHook = new FunctionFilterHook();
        functionHook.FunctionInvoking += (_, e) => 
        {
            var soemthing = e.Function;
            YieldAdditionalText?.Invoke($"\n<h4> Executing {soemthing.Name} {soemthing.Metadata.PluginName}</h4>\n");
        };
        functionHook.FunctionInvoked += HandleFunctionInvokedFilter;
        PromptExecutionSettings settings;
        if (requestModel.SelectedModel == AIModel.Gemini10)
        {
            settings = new GeminiPromptExecutionSettings { ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions};
        }
        else
        {
            settings = new OpenAIPromptExecutionSettings
                {ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ChatSystemPrompt = systemPrompt};
        }
        if (!runAsChat)
        {
            //if (requestModel.SelectedModel == AIModel.Gemini10)
            //{
            //    var nonStreamingResult = await kernel.InvokePromptAsync(query, new KernelArguments(settings), cancellationToken: cancellationToken);
            //    yield return nonStreamingResult.GetValue<string>() ?? "Uh oh, null!!";
            //}
            await foreach (var update in kernel.InvokePromptStreamingAsync(query, new KernelArguments(settings), cancellationToken: cancellationToken))
            {
                yield return update.ToString();
            }
        }
        else
        {
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            _chatHistory.AddUserMessage(query);
            var assistantMessage = "";
            await foreach (var streamingChatMessageContent in chat.GetStreamingChatMessageContentsAsync(_chatHistory, settings, kernel, cancellationToken))
            {
                if (requestModel.SelectedModel != AIModel.Gemini10)
                {
                    var update = (OpenAIStreamingChatMessageContent) streamingChatMessageContent;
                    var toolCall = update.ToolCallUpdates?[0];
                    if (toolCall?.FunctionName is not null)
                        YieldAdditionalText?.Invoke($"<h4>Executing {toolCall.Index}. {toolCall.FunctionName}</h4>");
                    if (update.Content is null) continue;
                    assistantMessage += update.Content;
                    yield return update.Content;
                }
                else
                {
                    var gUpdate = (GeminiStreamingChatMessageContent)streamingChatMessageContent;
                    var toolCall = gUpdate.ToolCalls?[0];
                    if (toolCall?.FunctionName is not null)
                        YieldAdditionalText?.Invoke($"<h4>Executing {toolCall.FunctionName} - {toolCall.PluginName}</h4>");
                    if (gUpdate.Content is null) continue;
                    assistantMessage += gUpdate.Content;
                    yield return gUpdate.Content;
                }
            }
            _chatHistory.AddAssistantMessage(assistantMessage);
        }

    }
    private readonly AskUserService _askUserService;
    private readonly JsonSerializerOptions _optionsAsIndented = JsonExtensions.JsonOptionsIndented;
    public event Action<string>? YieldAdditionalText;
    public async IAsyncEnumerable<string> ChatWithStepwisePlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default, bool resetChat = false)
    {

        var kernel = CreateKernelWithPlugins(chatRequestModel.SelectedPlugins, chatRequestModel.SelectedModel);
        var functionHook = new FunctionFilterHook();
        functionHook.FunctionInvoking += HandleFunctionInvokingFilter;
        functionHook.FunctionInvoked += HandleFunctionInvokedFilter;
        kernel.FunctionInvocationFilters.Add(functionHook);
        var config = new FunctionCallingStepwisePlannerOptions
        {
            MaxIterations = 15,
            MaxTokensRatio = 0.15,
            MaxTokens = 16000

        };
        foreach (var function in chatRequestModel.ExcludedFunctions ?? [])
        {
            config.ExcludedFunctions.Add(function);
        }
        var planner = new FunctionCallingStepwisePlanner(config);

        
        askOverride ??= query;


        yield return "<br/><h3>Executing plan...</h3><br/>";
        
        var chatResult = new MyChatHistory();
        chatResult.OnChatMessageContent += (message) =>
        {
            var messageUpdate = $"""
                          <details>
                            <summary>{message.Role}</summary>
                            
                            <h5>Message</h5>
                            <p>
                            {message.Content}
                            </p>
                            <br/>
                          </details>
                          """;
            YieldAdditionalText?.Invoke(messageUpdate);
        };
        var planResults = await planner.ExecuteAsync(kernel, askOverride, chatResult, cancellationToken);
        //var chatResult = planResults.ChatHistory;
        var historyBuilder = new StringBuilder();

        historyBuilder.AppendLine("<ol>");
        foreach (var chat in chatResult)
        {
            historyBuilder.AppendLine($"<li>{chat.Role} - {chat.Content}</li>");
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
        var finalResult = planResults.FinalAnswer;

        if (runAsChat)
        {
            yield return "<h3> Execution Complete</h3><br/><h4> Chat:</h4><hr/>";
            await foreach (var p in ExecuteChatStream(query, finalResult, kernel).WithCancellation(cancellationToken))
                yield return p;
        }
        else
        {
            yield return "<h3> Execution Complete<h3>";
            yield return "<h4> Final Result<h4>";
            yield return finalResult;
        }
    }
    public async IAsyncEnumerable<string> ChatWithHandlebarsPlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default, bool resetChat = false)
    {
        var kernel = CreateKernelWithPlugins(chatRequestModel.SelectedPlugins);
        var functionHook = new FunctionFilterHook();
        kernel.FunctionInvocationFilters.Add(functionHook);
        functionHook.FunctionInvoking += HandleFunctionInvokingFilter;
        functionHook.FunctionInvoked += HandleFunctionInvokedFilter;
        var planner = CreateHandlebarsPlanner(chatRequestModel);
        yield return "<h2> Generating Plan</h2>";
        HandlebarsPlan? plan = null;
        var isError = false;
        var ask = askOverride ?? query;
        var errorMsg = "";
        var planPrompt = "";
        try
        {
            plan = await planner.CreatePlanAsync(kernel, ask, cancellationToken: cancellationToken);
            planPrompt = $"""

                                    <details>
                                      <summary>See Plan</summary>
                                      
                                      <h5>Plan</h5>
                                      <p>
                                      {plan.Prompt}
                                      </p>
                                      <br/>
                                    </details>
                                    """;

        }
        catch (Exception ex)
        {
            isError = true;
            errorMsg = $"<h3> Error Generating Plan</h3><br/><p style=\"color:red;background-color:white\">{ex}</p>";
        }

        yield return "\n\n";
        if (isError)
        {
            yield return planPrompt;
            yield return errorMsg;
            yield break;
        }
        var arguments = chatRequestModel.Variables;
        var kernelArgs = new KernelArguments((arguments?.ToDictionary(x => x.Key, x => (object)x.Value) ?? [])!);
        if (kernelArgs.Count > 0)
        {
            yield return SkillStepsHeader;
            foreach (var arg in kernelArgs)
            {
                yield return $"<tr><td>{arg.Key}</td><td>{arg.Value}</td></tr>";
            }
        }


        yield return "\n<h3>Executing plan...</h3>\n\n";
        var finalResult = "";
       
        Console.WriteLine($"Handlebars plan: {plan}");
        var result = await plan!.InvokeAsync(kernel, kernelArgs, cancellationToken);

        finalResult = result;
        if (runAsChat)
        {
            yield return "<h3> Execution Complete</h3><br/> Chat:<br/>";
            await foreach (var p in ExecuteChatStream(query, finalResult, kernel).WithCancellation(cancellationToken))
                yield return p;
        }
        else
        {
            yield return "<h3> Execution Complete</h3>";
            yield return "<h4> Final Result</h4>\n";
            yield return finalResult;
        }

        var planDeets = plan.ToString();
        var planDeetsExpando = $"""

                               <details>
                                 <summary>See Plan</summary>
                                 
                                 <h5>Plan</h5>
                                 <p>
                                 {planDeets}
                                 </p>
                                 <br/>
                               </details>
                               """;
        yield return planPrompt;
        yield return planDeetsExpando;
    }
    private HandlebarsPlanner CreateHandlebarsPlanner(ChatRequestModel requestModel)
    {
        var config = new HandlebarsPlannerOptions
        {
            ExecutionSettings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 2000
            },
            AllowLoops = true
        };
        foreach (var exclude in requestModel.ExcludedFunctions ?? new List<string>())
        {
            config.ExcludedFunctions.Add(exclude);
        }

        var planner = new HandlebarsPlanner(config);
        return planner;
    }
    
    private void HandleFunctionInvokingFilter(object? sender, FunctionInvocationContext context)
    {
        var function = context.Function;
        YieldAdditionalText?.Invoke($"\n<h4> Executing {function.Name} {function.Metadata.PluginName}</h4>\n\n");
        var metaData =
            $"<strong>{JsonSerializer.Serialize(context.Arguments, _optionsAsIndented)}</strong>";
        var metaDataExpando = $"""

                               <details>
                                 <summary>See Arguments</summary>
                                 
                                 <h5>Arguments</h5>
                                 <p>
                                 {metaData}
                                 </p>
                                 <br/>
                               </details>
                               """;
        YieldAdditionalText?.Invoke(metaDataExpando);
    }
    private void HandleFunctionInvokedFilter(object? sender, FunctionInvocationContext context)
    {
        var function = context.Function;

        YieldAdditionalText?.Invoke($"\n<h4> {function.Name} {function.Metadata.PluginName} Completed</h4>\n\n");
        var result = $"<strong>{context.Result}</strong>";
        var resultsExpando = $"""

                              <details>
                                <summary>See Results</summary>
                                
                                <h5>Results</h5>
                                <p>
                                {result}
                                </p>
                                <br/>
                              </details>
                              """;

        YieldAdditionalText?.Invoke(resultsExpando);
        
    }


    private async IAsyncEnumerable<string> ExecuteChatStream(string query, string result, Kernel kernel)
    {
        var systemPrmpt =
            $"""
                You are "Adventures in Semantic Kernal" a helpful, intelligent and resourceful AI chatbot. Respond to the user to the best of your ability using the following Context. If Context indicates an error. Aplogize for the error and explain the error as well as possible.
                The following [Context] is based on the user's query and with in a medium with access to real-time information and web access. Assume [Context] was generated with access to real-time information. Use the [Context] along with the query to respond. Respond with the exact language in [Context] when appropriate.
                NEVER REVEAL THAT ANY CONTEXT WAS PROVIDED.
                [Context]
                {result}
                """;
        Console.WriteLine($"InitialSysPrompt:\n{systemPrmpt}");
        IChatCompletionService chatService;
        if (TestConfiguration.CoreSettings.Service == "OpenAI")
            chatService = new OpenAIChatCompletionService(TestConfiguration.OpenAI.Gpt35ModelId, TestConfiguration.OpenAI.ApiKey, loggerFactory: _loggerFactory);
        else
            chatService = new AzureOpenAIChatCompletionService(TestConfiguration.AzureOpenAI.Gpt35DeploymentName, TestConfiguration.AzureOpenAI.Endpoint, TestConfiguration.AzureOpenAI.ApiKey, modelId: TestConfiguration.AzureOpenAI.ModelId, loggerFactory: _loggerFactory);

        var settings = new OpenAIPromptExecutionSettings { Temperature = 0.7, TopP = 1.0, MaxTokens = 3000, ChatSystemPrompt = systemPrmpt };
        _chatHistory.AddUserMessage(query);
        var response = "";
        await foreach (var token in chatService.GetStreamingChatMessageContentsAsync(_chatHistory, settings))
        {
            Console.WriteLine($"Response Json:\n {JsonSerializer.Serialize(token)}");
            response += token.Content;
            yield return token.Content;
        }
        _chatHistory.AddAssistantMessage(response);

    }

    private static Kernel CreateKernelWithPlugins(IEnumerable<KernelPlugin> pluginFunctions, AIModel model = AIModel.Gpt4O)
    {
        var kernel = CreateKernel(model);
        kernel.Plugins.AddRange(pluginFunctions);
        return kernel;
    }


    #endregion
}