using Microsoft.SemanticKernel;
using SkPluginLibrary.Plugins;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using SkPluginLibrary.Models.Hooks;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Custom Plugins - crawl wiki,c# repl, web search chat

    public async Task<CodeOutputModel> GenerateCompileAndExecuteReplPlugin(string input, string code = "",
        ReplType replType = ReplType.ReplConsole)
    {
        var kernel = CreateKernel();
        var chatService = kernel.Services.GetRequiredService<IChatCompletionService>();
        var replSkill = new ReplCsharpPlugin(kernel, _scriptService, _compilerService);
        var repl = kernel.ImportPluginFromObject(replSkill);
        var kernelArgs = new KernelArguments
        {
            ["existingCode"] = code,
            ["input"] = input,
            ["sessionId"] = Guid.NewGuid().ToString()
        };

        FunctionResult kernelResult = await kernel.InvokeAsync(repl[replType.ToString()], kernelArgs);
        Console.WriteLine(kernelResult.ToString());
       
        var codeResult = JsonSerializer.Deserialize<CodeOutputModel>(kernelResult.Result());
        var outCode = $"{code}\n{codeResult?.Code}";
        var output = codeResult?.Output;
        return new CodeOutputModel { Code = outCode.TrimStart('\n'), Output = output };
    }

    public async IAsyncEnumerable<string> RunWebSearchChat(string query)
    {
        var kernel = CreateKernel(AIModel.Gpt4);
        var webPlugin = new WebCrawlPlugin(_bingSearchService);
        var webPluginInstance = kernel.ImportPluginFromObject(webPlugin, "WebSearchPlugin");
        var sysPromptTemplate =
            """
            Answer the user's query using the web search results below. 
            Always search the web before responding. 
            Always include CITATIONS in your response.
            """;
        var functionHook = new FunctionFilterHook();
        kernel.FunctionFilters.Add(functionHook);
        functionHook.FunctionInvoking += (_, e) =>
        {
            var soemthing = e.Function;
            if (e.Function.Name.StartsWith("func")) return;
            AdditionalAgentText?.Invoke($"\n<h4> Executing {soemthing.Name} {soemthing.Metadata.PluginName}</h4>\n\n");
        };
        functionHook.FunctionInvoked += HandleCustomFunctionInvoked;
        query = $"""
                 Find the answer to the user question on the web

                 [User Question]

                 {query}

                 Include web page links
                 """;
        await foreach (var update in kernel.InvokePromptStreamingAsync(query, new KernelArguments(new OpenAIPromptExecutionSettings { MaxTokens = 1500, Temperature = 1.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ChatSystemPrompt = sysPromptTemplate })))
        {
            var content = update.ToString();
            
            yield return content;
        }
        
    }
    public event Action<string>? AdditionalAgentText;
    public async IAsyncEnumerable<string> RunWikiSearchChat(string query)
    {
        var kernel = CreateKernel(AIModel.Gpt4);
        var functionHook = new FunctionFilterHook();
        kernel.FunctionFilters.Add(functionHook);
        functionHook.FunctionInvoking += (_, e) =>
        {
            var soemthing = e.Function;
            AdditionalAgentText?.Invoke($"\n<h4> Executing {soemthing.Name} {soemthing.Metadata.PluginName}</h4>\n\n");
        };
        functionHook.FunctionInvoked += HandleCustomFunctionInvoked;
        var wikiPlugin = new WikiChatPlugin();
        var wiki = kernel.ImportPluginFromObject(wikiPlugin);
        var systemPrompt =
            """
            Answer the user's query using Wikipedia search results. 
            Always search the Wikipedia before responding. 
            Always end your response with a list of ## Citations that include links to relevant Wikipedia pages.
            """;
        var settings = new OpenAIPromptExecutionSettings { MaxTokens = 1500, Temperature = 1.0, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, ChatSystemPrompt = systemPrompt };
        query = $"""
                 Find the answer to the user question on Wikipedia

                 [User Question]

                 {query}

                 Include page links
                 """;
        var args = new KernelArguments(settings)
        {
            ["input"] = query
        };
        
        var streamingKernel = kernel.InvokePromptStreamingAsync(query, args);
       
        //var kernelResult = streamingKernel;
        await foreach (var result in streamingKernel)
        {
            yield return result.ToString();
        }
    }
    private void HandleCustomFunctionInvoked(object? sender, FunctionInvokedContext invokedArgs)
    {
        var function = invokedArgs.Function;
        if (invokedArgs.Function.Name.StartsWith("func")) return;
        AdditionalAgentText?.Invoke($"\n<h4> {function.Name} {function.Metadata.PluginName} Completed</h4>\n\n");
        var result = $"<p>{invokedArgs.Result}</p>";
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

        AdditionalAgentText?.Invoke(resultsExpando);
        
    }
    #endregion
}