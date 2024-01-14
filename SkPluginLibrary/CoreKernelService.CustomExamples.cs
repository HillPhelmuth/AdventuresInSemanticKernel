using Microsoft.SemanticKernel;
using SkPluginLibrary.Plugins;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Custom Plugins - crawl wiki,c# repl, web search chat

    public async Task<CodeOutputModel> GenerateCompileAndExecuteReplPlugin(string input, string code = "",
        ReplType replType = ReplType.ReplConsole)
    {
        var kernel = await ChatWithSkKernal();
        var chatService = kernel.Services.GetRequiredService<IChatCompletionService>();
        var replSkill = new ReplCsharpPlugin(kernel, _scriptService, _compilerService);
        var repl = kernel.ImportPluginFromObject(replSkill);
        var kernelArgs = new KernelArguments
        {
            ["existingCode"] = code,
            ["input"] = input
        };
        kernelArgs["sessionId"] = Guid.NewGuid().ToString();

        FunctionResult kernelResult = await kernel.InvokeAsync(repl[replType.ToString()], kernelArgs);
        Console.WriteLine(kernelResult.ToString());
       
        var codeResult = JsonSerializer.Deserialize<CodeOutputModel>(kernelResult.Result());
        var outCode = $"{code}\n{codeResult?.Code}";
        var output = codeResult?.Output;
        return new CodeOutputModel { Code = outCode.TrimStart('\n'), Output = output };
    }

    public async IAsyncEnumerable<string> RunWebSearchChat(string query)
    {
        var kernel = CreateKernel();
        var memory = CreateSemanticMemory(MemoryStoreType.InMemory);
        var webPlugin = new WebCrawlPlugin(kernel, _bingSearchService, memory);
        var webPluginInstance = kernel.ImportPluginFromObject(webPlugin);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var sysPromptTemplate =
            "Answer the user's query using the web search results below. Always include CITATIONS in your response.\n\n[Web Search Results]\n{{$memory_context}}";
        yield return "Searching web for information...\n\n";
        var webResult = await kernel.InvokeAsync(webPluginInstance["ExtractWebSearchQuery"], new KernelArguments { ["input"] = query});
        var queryResult = await kernel.InvokeAsync(webPluginInstance["SearchAndCiteWeb"], new KernelArguments { ["input"] = webResult.ToString() });
        var context = new KernelArguments
        {
            ["memory_context"] = queryResult.Result()
        };

        var templateEngine = new KernelPromptTemplateFactory(_loggerFactory).Create(new PromptTemplateConfig() { Template = sysPromptTemplate});
        var sysPrompt = await templateEngine.RenderAsync(kernel, context);
        var chat = new ChatHistory(sysPrompt);
        chat.AddUserMessage(query);
        await foreach (var token in chatService.GetStreamingChatMessageContentsAsync(chat,
                           new OpenAIPromptExecutionSettings { MaxTokens = 1500, Temperature = 1.0 }))
        {
            yield return token.Content;
        }
    }

    public async IAsyncEnumerable<string> RunWikiSearchChat(string query)
    {
        var kernel = CreateKernel();
        var wikiPlugin = new WikiChatPlugin(kernel);
        var wiki = kernel.ImportPluginFromObject(wikiPlugin);
        var args = new KernelArguments
        {
            ["input"] = query
        };
        var streamingKernel = await kernel.InvokeAsync<IAsyncEnumerable<string>>(wiki["WikiSearchAndChat"], args);
        var kernelResult = streamingKernel;
        await foreach (var result in kernelResult)
        {
            yield return result;
        }
    }
    #endregion
}