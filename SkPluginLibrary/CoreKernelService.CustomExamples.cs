using Microsoft.SemanticKernel;
using SkPluginLibrary.Plugins;
using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkPluginLibrary.Models.Hooks;
using System.Text.RegularExpressions;
using System.Text;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Custom Plugins - crawl wiki,c# repl, web search chat, novel writer, prompt engineer

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
        kernel.FunctionInvocationFilters.Add(functionHook);
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
        kernel.FunctionInvocationFilters.Add(functionHook);
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
    public async Task<NovelOutline> GenerateNovelIdea(NovelGenre genre)
    {
        var kernel = CreateKernel();
        const string Prompt = """
                              You are a novel idea generator. Provided a Genre, generate a novel idea.
                              The idea should contain a Title, a Theme, a few Character Details, and a few key Plot Events.
                              ## Output
                              Your output must be in json using the following format:
                              ```json
                              {
                              	"Title": "Title of the Novel",
                                  "Theme": "Theme of the Novel",
                                  "Characters": "Character Details",
                                  "PlotEvents": "Plot Events"
                              }
                              ```

                              ## Genre
                              {{ $genre }}
                              """;
        var settings = new OpenAIPromptExecutionSettings { MaxTokens = 1024, Temperature = 0.7, ResponseFormat = "json_object" };
        var args = new KernelArguments(settings) { ["genre"] = genre.ToString() };
        var json = await kernel.InvokePromptAsync<string>(Prompt, args);
        return JsonSerializer.Deserialize<NovelOutline>(json);
    }
    public async Task<string> CreateNovelOutline(string theme, string characterDetails = "", string plotEvents = "",
	    string novelTitle = "", int chapters = 15, AIModel aIModel = AIModel.Planner)
	{
		var kernel = CreateKernel(aIModel);
        var novelWriter = new NovelWriterPlugin(aIModel);
        var plugin = kernel.ImportPluginFromObject(novelWriter);
		var createOutlineFunc = plugin["CreateNovelOutline"];
        _title = novelTitle;
		var args = new KernelArguments()
		{
            ["theme"] = theme,
			["characterDetails"] = characterDetails,
			["plotEvents"] = plotEvents,
			["novelTitle"] = novelTitle,
            ["chapters"] = chapters
		};
		var outline = await createOutlineFunc.InvokeAsync<string>(kernel, args);
        _description = 
            $"""
            Theme or topic:
            {theme}

            Character Details:
            {characterDetails}

            Plot Events:
            {plotEvents}
            """;
		//AdditionalAgentText?.Invoke($"<p>{outline}</p>");
        return outline;
    }
    private string _description = "";
    private string _title = "";
    public async IAsyncEnumerable<string> WriteNovel(string outline, AIModel aiModel = AIModel.Planner,
	    CancellationToken cancellationToken = default)
    {
        
		var chapters = SplitMarkdownByHeaders(outline);
        var kernel = CreateKernel(aiModel);
        
        var novelPlugin = new NovelWriterPlugin(aiModel);
        var plugin = kernel.ImportPluginFromObject(novelPlugin);
        var initialContext = new KernelArguments()
		{
			["chapters"] = outline,
			["description"] = _description,
            ["title"] = _title
		};
		var writeChapterFunc = plugin["WriteChapterStreaming"];
        var summarizeChapterFunc = plugin["SummarizeChapter"];
        var summaryBuilder = new StringBuilder();
        var previousChapter = "none, 1st chapter";
        AdditionalAgentText?.Invoke(JsonSerializer.Serialize(chapters));
        foreach (var chapter in chapters)
        {
            if (cancellationToken.IsCancellationRequested) break;
            var writeArgs = new KernelArguments()
            {
                ["outline"] = chapter,
                ["previousChapter"] = previousChapter,
                ["summary"] = summaryBuilder.ToString(),
                ["storyDescription"] = _description
            };
            var chapterText = "";
            await foreach (var token in writeChapterFunc.InvokeStreamingAsync<string>(kernel, writeArgs, cancellationToken))
            {
                yield return token;
                chapterText += token;
            }
            yield return "<hr />";
            //AdditionalAgentText?.Invoke($"<p>{chapterText}</p>");
            previousChapter = chapterText;
            var summarizeArgs = new KernelArguments()
            {
                ["chapterText"] = chapterText
            };
            var summary = await summarizeChapterFunc.InvokeAsync<string>(kernel, summarizeArgs, cancellationToken);
            summaryBuilder.AppendLine(summary);
           
        }
    }
  
	public static List<string> SplitMarkdownByHeaders(string markdownText)
    {
		var result = new List<string>();
		var headerPattern = new Regex(@"^(## .+)$", RegexOptions.Multiline);

		var matches = headerPattern.Matches(markdownText);

		var lastIndex = 0;
		foreach (Match match in matches)
		{
			if (lastIndex != match.Index)
			{
				if (lastIndex != 0)
				{
					var segment = markdownText.Substring(lastIndex, match.Index - lastIndex).Trim();
					result.Add(segment);
				}
				else
				{
					// Capture the first header and content if the first header is at the start
					var firstSegment = markdownText[..match.Index].Trim();
					if (!string.IsNullOrEmpty(firstSegment))
					{
						result.Add(firstSegment);
					}
				}
			}
			lastIndex = match.Index;
		}

		if (lastIndex == 0) return result;
		var lastSegment = markdownText[lastIndex..].Trim();
		result.Add(lastSegment);

		return result;
	}
    private void HandleCustomFunctionInvoked(object? sender, FunctionInvocationContext invokedArgs)
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
