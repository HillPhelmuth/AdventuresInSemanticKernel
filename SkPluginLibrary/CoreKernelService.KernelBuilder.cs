using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Planners;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using NCalcPlugins;
using SkPluginLibrary.Plugins;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.SemanticKernel.Orchestration;
using SkPluginComponents;
using SkPluginComponents.Models;
using Microsoft.SemanticKernel.Functions.OpenAPI.OpenAI;
using Microsoft.SemanticKernel.Planners.Handlebars;
using Microsoft.SemanticKernel.TemplateEngine;
using System.Runtime.CompilerServices;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Semantic Plugin Picker (SemanticPluginPicker.razor, SequentialPlannerBuilder.razor)

    public Dictionary<string, ISKFunction> GetPluginFunctions(List<string> pluginName)
    {
        var kernel = CreateKernel();
        var plugin = kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath, pluginName.ToArray());
        return plugin.ToDictionary(x => x.Key, x => x.Value);
    }

    private Dictionary<string, ISKFunction> GetSemanticFunctions(params string[] pluginNames)
    {
        return GetPluginFunctions(pluginNames.ToList());
    }

    public async Task<ChatGptPluginManifest> GetManifest(ChatGptPlugin chatGptPlugin)
    {
        var client = new HttpClient();
        var manifest = await client.GetFromJsonAsync<ChatGptPluginManifest>(chatGptPlugin.AgentManifestUrl);
        return manifest;
    }

    public async Task<string> ExecuteFunction(ISKFunction function, Dictionary<string, string>? variables = null)
    {
        var kernel = CreateKernel("gpt-4-1106-preview");
        var context = kernel.CreateNewContext();
        foreach (var variable in variables ?? new Dictionary<string, string>())
        {
            context.Variables[variable.Key] = variable.Value;
        }
        var result = await kernel.RunAsync(context.Variables, function);
        return result.Result();
    }

    public async Task<KernelResult> ExecuteKernelFunction(ISKFunction function, Dictionary<string, string>? variables = null)
    {
        var kernel = CreateKernel("gpt-4-1106-preview");
        var context = kernel.CreateNewContext();
        foreach (var variable in variables ?? new Dictionary<string, string>())
        {
            context.Variables[variable.Key] = variable.Value;
        }
        var result = await kernel.RunAsync(context.Variables, function);
        return result;
    }
    public async IAsyncEnumerable<string> ExecuteFunctionStream(ISKFunction function, Dictionary<string, string>? variables = null)
    {
        var kernel = CreateKernel("gpt-4-1106-preview");
        var context = kernel.CreateNewContext();
        foreach (var variable in variables ?? new Dictionary<string, string>())
        {
            context.Variables[variable.Key] = variable.Value;
        }
        var result = await kernel.RunAsync(context.Variables, function);
        await foreach (var item in result.ResultStream<string>())
        {
            yield return item;
        }
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
        var convoSummaryPlugin = new ConversationSummaryPlugin(CreateKernel());
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
        _customNativePlugins ??= new Dictionary<string, object>();
        //var result = new Dictionary<string, object>();
        var kernel = CreateKernel();
        var simpleCalcPlugin = new SimpleCalculatorPlugin(kernel);
        _customNativePlugins.TryAdd(nameof(SimpleCalculatorPlugin), simpleCalcPlugin);
        var languageCalcPlugin = new LanguageCalculatorPlugin(kernel);
        _customNativePlugins.TryAdd(nameof(LanguageCalculatorPlugin), languageCalcPlugin);
        var csharpPlugin = new ReplCsharpPlugin(kernel);
        _customNativePlugins.TryAdd(nameof(ReplCsharpPlugin), csharpPlugin);
        var webCrawlPlugin = new WebCrawlPlugin(kernel, _bingSearchService);
        _customNativePlugins.TryAdd(nameof(WebCrawlPlugin), webCrawlPlugin);
        var dndPlugin = new DndPlugin();
        _customNativePlugins.TryAdd(nameof(DndPlugin), dndPlugin);
        var jsonPlugin = new HandleJsonPlugin();
        _customNativePlugins.TryAdd(nameof(HandleJsonPlugin), jsonPlugin);
        var streamPlugin = new StreamingPlugin();
        _customNativePlugins.TryAdd(nameof(StreamingPlugin), streamPlugin);
        var wikiPlugin = new WikiChatPlugin(kernel);
        _customNativePlugins.TryAdd(nameof(WikiChatPlugin), wikiPlugin);
        var youtubePlugin = new YouTubePlugin(kernel, _configuration["YouTubeSearch:ApiKey"]!);
        _customNativePlugins.TryAdd(nameof(YouTubePlugin), youtubePlugin);
        var askUserPlugin = new AskUserPlugin(_modalService);
        _customNativePlugins.TryAdd(nameof(AskUserPlugin), askUserPlugin);
        var recursivePlugsin = new RecursivePlugin(_modalService);
        _customNativePlugins.TryAdd(nameof(RecursivePlugin), recursivePlugsin);
        return _customNativePlugins;
    }

    private IEnumerable<string> CustomNativePluginNames =>
        _customNativePlugins?.Keys.ToList() ?? GetCustomNativePlugins().Keys.ToList();

    private IEnumerable<string> CoreNativePluginNames => _nativePlugins?.Keys.ToList() ?? GetCoreNativePlugins().Keys.ToList();

    private async Task<Dictionary<string, ISKFunction>> GetApiFunctionsFromNames(IEnumerable<string> apiPluginNames)
    {
        var kernel = CreateKernel();
        var tasks = apiPluginNames.Select(apiPlugin => kernel.ImportOpenApiPluginFunctionsAsync(apiPlugin, Path.Combine(RepoFiles.ApiPluginDirectoryPath, apiPlugin, "openapi.json"), new OpenApiFunctionExecutionParameters { EnableDynamicPayload = true, EnablePayloadNamespacing = true, IgnoreNonCompliantErrors = true })).ToList();

        var taskResults = await Task.WhenAll(tasks);
        var functionsResult = taskResults.SelectMany(x => x).ToDictionary(x => x.Key, x => x.Value, EqualityComparer<string>.Default);
        return functionsResult;
    }

    private async Task<Dictionary<string, ISKFunction>> GetApiFunctionsFromNames(params string[] apiPluginNames)
    {
        return await GetApiFunctionsFromNames(apiPluginNames.ToList());
    }

    private Dictionary<string, ISKFunction> GetNativeFunctionsFromNames(IEnumerable<string> pluginNames, bool isCustom = false)
    {
        var result = new Dictionary<string, ISKFunction>();
        var kernel = CreateKernel();
        var allPlugins = isCustom
            ? _customNativePlugins ?? GetCustomNativePlugins()
            : _nativePlugins ?? GetCoreNativePlugins();
        return pluginNames.Select(plugin => kernel.ImportFunctions(allPlugins[plugin], plugin))
            .Aggregate(result, (current, funcs) => current.UpsertConcat(funcs));
    }

    private Dictionary<string, ISKFunction> GetNativeFunctionsFromNames(bool isCustom = false,
        params string[] pluginNames)
    {
        return GetNativeFunctionsFromNames(pluginNames.ToList(), isCustom);
    }

    private static IEnumerable<string?> SemanticPlugins =>
        Directory.GetDirectories(RepoFiles.PluginDirectoryPath).Select(Path.GetFileName).ToList();

    private static IEnumerable<string?> ApiPlugins => Directory.GetDirectories(RepoFiles.ApiPluginDirectoryPath)
        .Select(Path.GetFileName).ToList();

    public async Task<List<PluginFunctions>> GetAllPlugins()
    {
        var result = new List<PluginFunctions>();
        var coreNative = CoreNativePluginNames.Select(x =>
            GetNativeFunctionsFromNames(false, x).ToPluginFunctions(x, PluginType.Native));
        var customNative = CustomNativePluginNames.Select(x =>
            GetNativeFunctionsFromNames(true, x).ToPluginFunctions(x, PluginType.Native));
        var semantic = SemanticPlugins.Select(x => GetSemanticFunctions(x).ToPluginFunctions(x, PluginType.Semantic));
        result.AddRange(semantic);
        result.AddRange(coreNative);
        result.AddRange(customNative);
        var tasks = ApiPlugins.Where(api => api is not null).Select(GetApiPluginFunctions).ToList();
        var results = await Task.WhenAll(tasks);
        result.AddRange(results);
        return result;
    }

    private async Task<PluginFunctions> GetApiPluginFunctions(string? api)
    {
        if (string.IsNullOrEmpty(api)) throw new ArgumentNullException(nameof(api));
        var pluginFunction = new PluginFunctions(api, PluginType.Api);
        var apiFunctions = await GetApiFunctionsFromNames(api);
        pluginFunction.Functions = apiFunctions.ToFunctionList();
        return pluginFunction;
    }

    public async Task<Dictionary<string, ISKFunction>> GetExternalPluginFunctions(ChatGptPluginManifest manifest)
    {
        var kernel = CreateKernel();
        var uri = string.IsNullOrWhiteSpace(manifest.Api!.PluginUrl)
            ? manifest.Api.Url
            : new Uri(manifest.Api.PluginUrl);
        var executionParameters = new OpenAIFunctionExecutionParameters { IgnoreNonCompliantErrors = true, EnableDynamicPayload = true, EnablePayloadNamespacing = true };
        if (!string.IsNullOrEmpty(manifest.OverrideUrl))
        {
            executionParameters.ServerUrlOverride = new Uri(manifest.OverrideUrl);
        }

        var replaceNonAsciiWithUnderscore = manifest.NameForModel.ReplaceNonAsciiWithUnderscore();
        var plugin = await kernel.ImportOpenAIPluginFunctionsAsync(replaceNonAsciiWithUnderscore, uri, executionParameters);
        return plugin.ToDictionary(x => x.Key, x => x.Value);
    }
    private void FunctionInvokingHandler(object? sender, FunctionInvokingEventArgs e)
    {
        var originalOutput = e.SKContext.Result;
        var originalValues = e.SKContext.Variables;
        var function = e.FunctionView;
        Console.WriteLine($"Executed {function.PluginName}.{function.Name}.\nResult: {originalOutput}\nVariables:\n{string.Join("\n", originalValues.Select(x => $"Name: {x.Key}, Value: {x.Value}"))}");
    }

    private void FunctionInvokedHandler(object? sender, FunctionInvokedEventArgs args)
    {

    }
    public async Task<string> ExecuteFunctionChain(ChatRequestModel chatRequestModel, CancellationToken cancellationToken = default)
    {
        var kernel = await CreateKernelWithPlugins(chatRequestModel.SelectedPlugins, TestConfiguration.OpenAI!.ModelId);
        var context = kernel.CreateNewContext();
        foreach (var variable in chatRequestModel.Variables ?? new Dictionary<string, string>())
        {
            _loggerFactory.CreateLogger<CoreKernelService>().LogInformation("Adding variable {key} with value {value}",
                variable.Key, variable.Value);
            context.Variables[variable.Key] = variable.Value;
        }

        var chain = chatRequestModel.SelectedFunctions.OrderBy(x => x.Order).Select(x => x.SkFunction);
        var result = await kernel.RunAsync(context.Variables, cancellationToken: cancellationToken, chain.ToArray());
        return result.Result();
    }
    private async Task<HandlebarsPlanner> CreateHandlebarsPlanner(ChatRequestModel requestModel)
    {
        var kernel = await CreateKernelWithPlugins(requestModel.SelectedPlugins);
        var functions = kernel.Functions.GetFunctionViews();
        kernel.FunctionInvoked += FunctionInvokedHandler;
        var config = new HandlebarsPlannerConfig
        {
            MaxTokens = 2000,
            SemanticMemoryConfig = new SemanticMemoryConfig
            {
                RelevancyThreshold = 0.77,
                MaxRelevantFunctions = 20
            },
            AllowLoops = true,
        };
        foreach (var exclude in requestModel.ExcludedFunctions ?? new List<string>())
        {
            config.ExcludedFunctions.Add(exclude);
        }

        foreach (var include in requestModel.RequredFunctions ?? new List<string>())
        {
            var pluginname = functions.FirstOrDefault(x => x.Name == include)?.PluginName ?? "";
            config.SemanticMemoryConfig.IncludedFunctions.Add((pluginname, include));
        }
        var planner = new HandlebarsPlanner(kernel, config);
        return planner;
    }
    private async Task<ISequentialPlanner> CreateSequentialPlanner(ChatRequestModel requestModel)
    {
        var kernel = await CreateKernelWithPlugins(requestModel.SelectedPlugins);
        var functions = kernel.Functions.GetFunctionViews();
        kernel.FunctionInvoked += FunctionInvokedHandler;
        var config = new SequentialPlannerConfig
        {
            MaxTokens = 2000,
            SemanticMemoryConfig = new SemanticMemoryConfig
            {
                RelevancyThreshold = 0.77,
                MaxRelevantFunctions = 20
            },

        };
        foreach (var exclude in requestModel.ExcludedFunctions ?? new List<string>())
        {
            config.ExcludedFunctions.Add(exclude);
        }

        foreach (var include in requestModel.RequredFunctions ?? new List<string>())
        {
            var pluginname = functions.FirstOrDefault(x => x.Name == include)?.PluginName ?? "";
            config.SemanticMemoryConfig.IncludedFunctions.Add((pluginname, include));
        }

        var planner = new SequentialPlanner(kernel, config).WithInstrumentation(_loggerFactory);
        return planner;
    }

    private async Task<(IStepwisePlanner planner, StepwisePlannerConfig config, IKernel kernel)> CreateStepwisePlanner(ChatRequestModel requestModel, CancellationToken token = default)
    {
        var kernel = await CreateKernelWithPlugins(requestModel.SelectedPlugins);
        var functions = kernel.Functions.GetFunctionViews();
        //await kernel.ImportFunctions(new AskUserPlugin(_modalService));
        var config = new StepwisePlannerConfig
        {
            MaxTokens = 8000,
            SemanticMemoryConfig = new SemanticMemoryConfig
            {
                RelevancyThreshold = 0.77,
                MaxRelevantFunctions = 20
            },
            Suffix = "Now, Take a deep breath. Let's break down the problem step by step and think about the best approach. Label steps as they are taken.\n\nContinue the thought process!",
            MaxIterations = 20,


        };
        foreach (var exclude in requestModel.ExcludedFunctions ?? new List<string>())
        {
            config.ExcludedFunctions.Add(exclude);
        }

        foreach (var include in requestModel.RequredFunctions ?? new List<string>())
        {
            var pluginname = functions.FirstOrDefault(x => x.Name == include)?.PluginName ?? "";
            config.SemanticMemoryConfig.IncludedFunctions.Add((pluginname, include));
        }

        var planner = new StepwisePlanner(kernel, config);
        return (planner, config, kernel);

    }

    private record ChatExchange(string UserMessage, string AssistantMessage);

    private readonly List<ChatExchange> _chatExchanges = new();

    public async IAsyncEnumerable<string> ChatWithActionPlanner(string query, ChatRequestModel requestModel,
        bool runAsChat = true, string? askOverride = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var userMessage = $"User: {query}";
        var kernel = await CreateKernelWithPlugins(requestModel.SelectedPlugins);
        var chatService = kernel.GetService<IChatCompletion>();
        var context = kernel.CreateNewContext();
        var functions = kernel.Functions.GetFunctionViews();
        kernel.FunctionInvoking += (_, e) =>
        {
            var soemthing = e.FunctionView;
            YieldAdditionalText?.Invoke($"\n<h4> Executing {soemthing.Name} {soemthing.PluginName}</h4>\n\n");
        };
        //kernel.FunctionInvoked += HandleFunctionInvoked;
        var config = new ActionPlannerConfig
        {
            MaxTokens = 2500,
            SemanticMemoryConfig = new SemanticMemoryConfig
            {
                RelevancyThreshold = 0.77,
                MaxRelevantFunctions = 20
            }
        };
        foreach (var exclude in requestModel.ExcludedFunctions ?? new List<string>())
        {
            config.ExcludedFunctions.Add(exclude);
        }

        foreach (var include in requestModel.RequredFunctions ?? new List<string>())
        {
            var pluginname = functions.FirstOrDefault(x => x.Name == include)?.PluginName ?? "";
            config.SemanticMemoryConfig.IncludedFunctions.Add((pluginname, include));
        }
        var planner = new ActionPlanner(kernel);
        yield return "<h4>Determining the best action to take...</h4><br/>";
        Plan plan;
        var errorMsg = "";
        var iscreateError = false;
        askOverride ??= query;
        try
        {

            plan = await planner.CreatePlanAsync(askOverride, cancellationToken);
        }
        catch (Exception ex)
        {
            plan = new Plan("");
            iscreateError = true;
            errorMsg = ex.ToString();
            Console.WriteLine(ex);
        }

        if (iscreateError)
        {
            var errorSystemPrompt =
                $"The upcoming User's Query generated an error creating a Semantic Kernel Plan using the Action Planner. Apologize for the error and provide the best explantaion you can for the error.\n[Error]\n{errorMsg}";
            var errorChat = chatService.CreateNewChat(errorSystemPrompt);
            await foreach (var token in chatService.GenerateMessageStreamAsync(errorChat, cancellationToken: cancellationToken))
            {
                yield return token;
            }

            yield break;
        }

        var skillName = plan.Steps[0].PluginName ?? plan.PluginName;
        var name = plan.Steps[0].Name ?? plan.Name;
        yield return $"Executing Function <strong>{name}</strong> from plugin {skillName}\n\n";
        string? result;
        try
        {
            var planResult = await plan.InvokeAsync(context, cancellationToken: cancellationToken);
            result = planResult.Result();
        }
        catch (Exception ex)
        {
            result = ex.ToString();
        }

        if (!runAsChat)
        {
            yield return $"<h3> Final Result</h3>\n<p>{result}</p>";
            yield break;
        }

        yield return $"\n<sup><sub>\n```\n{result} \n```\n</sub></sup>\n";

        var response = "";
        await foreach (var token in ExecuteChatStream(query, result, kernel).WithCancellation(cancellationToken))
        {
            response += token;
            yield return token;
        }

        var assistantMessage = $"Assistant: {response}";
        _chatExchanges.Add(new ChatExchange(userMessage, assistantMessage));
    }
    private static StepwiseExecutionResult _executionResults = new();
    private readonly AskUserService _modalService;
    private readonly JsonSerializerOptions _optionsAsIndented = JsonExtensions.JsonOptionsIndented;
    public event Action<string>? YieldAdditionalText;
    public async IAsyncEnumerable<string> ChatWithStepwisePlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (planner, config, kernel) = await CreateStepwisePlanner(chatRequestModel, cancellationToken);
        kernel.CreateSemanticFunction(
            "Generate an answer for the following question: {{$input}}",
            functionName: "GetAnswerForQuestion",
            pluginName: "AnswerBot",
            description: "Given a question, get an answer and return it as the result of the function");
        yield return "<h2> Generating Plan...</h2><br/>";
        askOverride ??= query;
        var (plan, skillStepsHeader, isError) = await GeneratePlanAsync(askOverride, planner);
        yield return skillStepsHeader;
        if (isError)
        {
            yield break;
        }



        yield return "\n<h3>Executing plan...</h3>\n\n";
        var finalResult = "";
        kernel.FunctionInvoking += (_, e) =>
        {
            var soemthing = e.FunctionView;
            YieldAdditionalText?.Invoke($"\n<h4> Executing {soemthing.Name} {soemthing.PluginName}</h4>\n\n");
        };
        kernel.FunctionInvoked += HandleFunctionInvoked;

        var result = await kernel.RunAsync(plan, cancellationToken: cancellationToken);
        var executionResult = result.AsStepwiseExecutionResult(config);
        finalResult = executionResult.Answer;

        if (runAsChat)
        {
            yield return "<h3> Execution Complete</h3><br/><h4> Chat:</h4><hr/>";
            await foreach (var p in ExecuteChatStream(query, finalResult, kernel))
                yield return p;
        }
        else
        {
            yield return "<h3> Execution Complete<h3>";
            yield return "<h4> Final Result<h4>";
            yield return finalResult;
        }
    }
    public async IAsyncEnumerable<string> ChatWithHandlebarsPlanner(string query, ChatRequestModel chatRequestModel, bool runAsChat = true, string? askOverride = null)
    {
        var kernel = CreateKernel("gpt-4-1106-preview");
        kernel.FunctionInvoked += HandleFunctionInvoked;

        var planner = await CreateHandlebarsPlanner(chatRequestModel);
        yield return "<h2> Generating Plan</h2>\n\n";
        var (plan, skillStepsHeader, isError) = await GeneratePlanAsync(askOverride, planner);
        yield return skillStepsHeader;
        if (isError)
        {
            yield break;
        }
        yield return "\n<h3>Executing plan...</h3>\n\n";
        var finalResult = "";
        kernel.FunctionInvoking += (_, e) =>
        {
            var soemthing = e.FunctionView;
            YieldAdditionalText?.Invoke($"\n<h4> Executing {soemthing.Name} {soemthing.PluginName}</h4>\n\n");
        };
        kernel.FunctionInvoked += HandleFunctionInvoked;

        var result = await kernel.RunAsync();
        finalResult = result.GetValue<string>();
        if (runAsChat)
        {
            yield return "<h3> Execution Complete\n\n</h3> Chat:<br/>";
            await foreach (var p in ExecuteChatStream(query, finalResult, kernel))
                yield return p;
        }
        else
        {
            yield return "<h3> Execution Complete</h3>";
            yield return "<h4> Final Result</h4>\n";
            yield return finalResult;
        }

        var metaData =
            $"<strong>{JsonSerializer.Serialize<Dictionary<string, object>>(result.FunctionResults.FirstOrDefault()?.Metadata, _optionsAsIndented)}</strong>";
        var metaDataExpando = $"""

                               <details>
                                 <summary>See Metadata</summary>
                                 
                                 <h5>Metadata</h5>
                                 <p>
                                 {metaData}
                                 </p>
                                 <br/>
                               </details>
                               """;
        yield return metaDataExpando;
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
        yield return planDeetsExpando;
    }

    private void HandleFunctionInvoked(object? sender, FunctionInvokedEventArgs invokedArgs)
    {
        var function = invokedArgs.FunctionView;

        YieldAdditionalText?.Invoke($"\n<h4> {function.Name} {function.PluginName} Completed</h4>\n\n");
        var result = $"<p>{invokedArgs.SKContext.Result}</p>";
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
        var metaData =
            $"<strong>{JsonSerializer.Serialize(invokedArgs.Metadata, _optionsAsIndented)}</strong>";
        var metaDataExpando = $"""

                               <details>
                                 <summary>See Metadata</summary>
                                 
                                 <h5>Metadata</h5>
                                 <p>
                                 {metaData}
                                 </p>
                                 <br/>
                               </details>
                               """;
        YieldAdditionalText?.Invoke(metaDataExpando);
    }

    public async IAsyncEnumerable<string> ChatWithSequentialPlanner(string query, ChatRequestModel chatRequestModel,
        bool runAsChat = true, string? askOverride = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var kernel = CreateKernel("gpt-4-1106-preview");
        kernel.FunctionInvoked += FunctionInvokedHandler;
        var planner = await CreateSequentialPlanner(chatRequestModel);
        //var planner = await CreateStepwisePlanner(chatRequestModel);
        yield return "<h2> Generating Plan</h2>\n\n";
        askOverride ??= query;
        var (plan, skillStepsHeader, isError) = await GeneratePlanAsync(askOverride, planner);

        yield return "<table>";
        yield return skillStepsHeader;
        if (isError)
        {
            yield break;
        }

        var stepNumber = 0;
        foreach (var step in plan.Steps)
        {
            yield return $"<tr><td>{++stepNumber}</td><td>{step.Name}</td><td>{step.PluginName}</td><td>{step.Description}</td></tr>";
        }
        yield return "</table>";
        yield return "<br/>Executing plan...<br/>";
        var finalResult = "";
        await foreach (var item in ExecuteSequentialPlannerByStep(chatRequestModel, kernel, plan, GetFinalResult).WithCancellation(cancellationToken))
        {
            yield return item;
        }

        var result = finalResult;
        Console.WriteLine($"Final Result from plan:\n{result}");
        if (runAsChat)
        {
            yield return "<h3>Execution Complete</h3><h4> Chat:</h4>";
            await foreach (var p in ExecuteChatStream(query, result, kernel).WithCancellation(cancellationToken))
                yield return p;
        }
        else
        {
            yield return "<h3>Execution Complete</h3>";
        }

        yield break;

        void GetFinalResult(string returnedResult) => finalResult = returnedResult;
    }

    private static async IAsyncEnumerable<string> ExecuteSequentialPlannerByStep(ChatRequestModel chatRequestModel, IKernel kernel,
        Plan plan, Action<string> finalResultDelegate, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stepNumber = 0;
        var finalResult = "";
        var context = kernel.CreateNewContext();
        foreach (var input in chatRequestModel.Variables ?? new Dictionary<string, string>())
        {
            context.Variables[input.Key] = input.Value;
        }

        while (plan.HasNextStep)
        {
            var hasError = false;
            yield return $"<h3>Step {++stepNumber}</h3>";
            string? stepResultString;
            try
            {
                var stepResult = await plan.InvokeNextStepAsync(context, cancellationToken);
                stepResultString = stepResult.State.ToString();
                finalResult = stepResult.State.ToString();
                Console.WriteLine($"{string.Join("\n", stepResult.State.Select(x => $"{x.Key} -- {x.Value}"))}");
            }
            catch (SKException ex)
            {
                stepResultString = $"Error on step {stepNumber}\n\n {ex.Message}\n\n{ex.StackTrace}";
                hasError = true;
            }

            yield return $"{stepResultString}\n\n";
            if (hasError) break;
        }

        finalResultDelegate(finalResult);
    }

    private async Task<(Plan plan, string skillStepsHeader, bool isError)> GeneratePlanAsync(string query,
        ISequentialPlanner planner)
    {
        Plan plan;
        var skillStepsHeader = "<tr><th>Step</th> <th>Function</th><th> Skill</th><th> Description</th></tr>";
        var isError = false;
        try
        {
            plan = await planner.CreatePlanAsync(query);
            _loggerFactory.CreateLogger<CoreKernelService>()
                .LogInformation("Plan:\n{plan.ToPlanString()}", plan.ToPlanString());
            // Console.WriteLine($"Plan:\n{plan.ToPlanString()}");
        }
        catch (Exception ex)
        {
            plan = new Plan(query);
            skillStepsHeader = ex.ToString();
            isError = true;
        }

        return (plan, skillStepsHeader, isError);
    }
    private Task<(Plan plan, string skillStepsHeader, bool isError)> GeneratePlanAsync(string query,
        IStepwisePlanner planner)
    {
        Plan plan;
        var skillStepsHeader = "| Name | Value |<br/>|:---:|:---:|<br/>";
        var isError = false;
        try
        {
            plan = planner.CreatePlan(query);
            _loggerFactory.CreateLogger<CoreKernelService>()
                .LogInformation("Plan:\n{plan.ToPlanString()}", plan.ToPlanString());
            //Console.WriteLine($"Plan:\n{plan.ToPlanString()}");
        }
        catch (Exception ex)
        {
            plan = new Plan(query);
            skillStepsHeader = ex.ToString();
            isError = true;
        }

        return Task.FromResult((plan, skillStepsHeader, isError));
    }
    private async Task<(HandlebarsPlan plan, string skillStepsHeader, bool isError)> GeneratePlanAsync(string query,
        HandlebarsPlanner planner)
    {
        HandlebarsPlan plan;
        var skillStepsHeader = "| Name | Value |<br/>|:---:|:---:|<br/>";
        var isError = false;
        try
        {
            plan = await planner.CreatePlanAsync(query);
            _loggerFactory.CreateLogger<CoreKernelService>()
                .LogInformation("Plan:\n{plan.ToPlanString()}", plan.ToString());
            //Console.WriteLine($"Plan:\n{plan.ToPlanString()}");
        }
        catch (Exception ex)
        {
            var kernel = CreateKernel();
            plan = new HandlebarsPlan(kernel, query);
            skillStepsHeader = ex.ToString();
            isError = true;
        }
        return (plan, skillStepsHeader, isError);
    }
    private async IAsyncEnumerable<string> ExecuteChatStream(string query, string result, IKernel kernel)
    {
        var systemPrmpt =
            $$$"""
                You are "Adventures in Semantic Kernal" a helpful, intelligent and resourceful AI chatbot. Respond to the user to the best of your ability using the following Context. If Context indicates an error. Aplogize for the error and explain the error as well as possible.
                The following [Context] is based on the user's query and with in a medium with access to real-time information and web access. Assume [Context] was generated with access to real-time information. Use the [Context] along with the query to respond. Respond with the exact language in [Context] when appropriate.
                NEVER REVEAL THAT ANY CONTEXT WAS PROVIDED.
                [Context]
                {{{result}}}
                """;
        Console.WriteLine($"InitialSysPrompt:\n{systemPrmpt}");
        var chatService = kernel.GetService<IChatCompletion>();
        var engine = new KernelPromptTemplateFactory(_loggerFactory).Create(systemPrmpt, new PromptTemplateConfig());
        var ctx = kernel.CreateNewContext();
        var finalSysPrompt = await engine.RenderAsync(ctx);

        var chat = chatService.CreateNewChat(finalSysPrompt);
        foreach (var exchange in _chatExchanges)
        {
            chat.AddUserMessage(exchange.UserMessage);
            chat.AddAssistantMessage(exchange.AssistantMessage);
        }

        var settings = new OpenAIRequestSettings { Temperature = 0.7, TopP = 1.0, MaxTokens = 3000 };
        chat.AddUserMessage(query);
        var response = "";
        await foreach (var token in chatService.GenerateMessageStreamAsync(chat, settings))
        {
            response += token;
            yield return token;
        }

        var userMessage = $"User: {query}";
        var assistantMessage = $"Assistant: {response}";
        _chatExchanges.Add(new ChatExchange(userMessage, assistantMessage));
    }

    private async Task<IKernel> CreateKernelWithPlugins(IReadOnlyCollection<PluginFunctions> pluginFunctions, string model = "gpt-4-1106-preview")
    {
        var kernel = CreateKernel(model);
        var semanticPlugins = kernel.ImportSemanticFunctionsFromDirectory(RepoFiles.PluginDirectoryPath,
            pluginFunctions.Where(x => x.PluginType == PluginType.Semantic).Select(x => x.PluginName).ToArray());
        foreach (var apiPlugin in pluginFunctions.Where(x => x.PluginType == PluginType.Api))
        {
            var api = await kernel.ImportOpenApiPluginFunctionsAsync(apiPlugin.PluginName,
                Path.Combine(RepoFiles.ApiPluginDirectoryPath, apiPlugin.PluginName, "openapi.json"), new OpenApiFunctionExecutionParameters { EnableDynamicPayload = true, EnablePayloadNamespacing = true, IgnoreNonCompliantErrors = true });
        }

        var natives = pluginFunctions.Where(x => x.PluginType == PluginType.Native)
            .SelectMany(x => x.SkFunctions.Values).ToList();
        natives.ForEach(x => kernel.RegisterCustomFunction(x));
        return kernel;
    }


    #endregion
}