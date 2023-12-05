using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Orchestration;
using Microsoft.SemanticKernel.TemplateEngine.Basic;
using SkPluginLibrary.Plugins;
using System.Text.Json;
using Microsoft.SemanticKernel.TemplateEngine;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Custom Plugins - crawl wiki,c# repl, web search chat

    public async Task<CodeOutputModel> GenerateCompileAndExecuteReplPlugin(string input, string code = "",
        ReplType replType = ReplType.ReplConsole)
    {
        var kernel = await ChatWithSkKernal();
        var replSkill = new ReplCsharpPlugin(kernel, _scriptService, _compilerService);
        var repl = kernel.ImportFunctions(replSkill);
        var ctx = kernel.CreateNewContext();
        ctx.Variables["existingCode"] = code;
        ctx.Variables["input"] = input;
        if (!ctx.Variables.ContainsKey("sessionId"))
        {
            ctx.Variables["sessionId"] = Guid.NewGuid().ToString();
        }

        KernelResult kernelResult = await kernel.RunAsync(ctx.Variables, repl[replType.ToString()]);
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
        var webPluginInstance = kernel.ImportFunctions(webPlugin);
        var chatService = kernel.GetService<IChatCompletion>();
        var sysPromptTemplate =
            "Answer the user's query using the web search results below. Always include CITATIONS in your response.\n\n[Web Search Results]\n{{$memory_context}}";
        yield return "Searching web for information...\n\n";
        var webResult = await kernel.RunAsync(query, webPluginInstance["ExtractWebSearchQuery"],
            webPluginInstance["SearchAndCiteWeb"]);
        var context = kernel.CreateNewContext();
        //var memoryContextItems = await memory.SearchAsync("websaveCollection", query, 10, .75).ToListAsync();
        //var memoryContext = string.Join("\n", memoryContextItems.Select(x => x.Metadata.Text));
        //Console.WriteLine($"Memory Context: {memoryContext}");
        context.Variables["memory_context"] = webResult.Result();
        var templateEngine = new BasicPromptTemplateFactory(_loggerFactory).Create(sysPromptTemplate, new PromptTemplateConfig());
        var sysPrompt = await templateEngine.RenderAsync(context);
        var chat = chatService.CreateNewChat(sysPrompt);
        chat.AddUserMessage(query);
        await foreach (var token in chatService.GenerateMessageStreamAsync(chat,
                           new OpenAIRequestSettings { MaxTokens = 1500, Temperature = 1.0 }))
        {
            yield return token;
        }
    }

    public async IAsyncEnumerable<string> RunWikiSearchChat(string query)
    {
        var kernel = CreateKernel();
        var wikiPlugin = new WikiChatPlugin(kernel);
        var wiki = kernel.ImportFunctions(wikiPlugin);
        var kernelResult = await kernel.RunAsync(query, wiki["WikiSearchAndChat"]);
        await foreach (var result in kernelResult.ResultStream<string>())
        {
            yield return result;
        }
    }
    #endregion
}