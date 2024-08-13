using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Connectors.Sqlite;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Text;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins;

public class PromptExpertPlugin
{
    private static ISemanticTextMemory? _semanticTextMemory;
    private static IConfiguration _configuration;
    private static string? _apiKey;
    //private const string PromptHelperCollection = "promptHelperCollection";
    private const string PromptHelperPrompt =
        """
        You are a prompt expert. Using the Expert Knowledge below, provide instructions on how to improve the provided prompt.

        ## Expert Knowledge
        {{TextMemory.Recall input=$input collection=$collection relevance=$relevance limit=$limit}}

        ## Prompt
        {{ $prompt }}

        ## Issues Identified
        {{ $input }}

        ## Task
        Provide specific instructions or suggestions on how to improve the prompt. Include reasons for each suggestion from the Expert Knowledge.
        **Important**! DO NOT RE-WRITE THE PROMPT YOURSELF. Only provide instructions and suggestions.
        """;
    public PromptExpertPlugin(IConfiguration configuration)
    {
        _configuration = configuration;
        _apiKey = _configuration["OpenAI:ApiKey"];

    }
    [KernelFunction, Description("Evaulate prompts for effectiveness in guiding AI interactions")]
    [return: Description("Json object containing the score and explanation")]
    public async Task<string> EvaluatePrompt(Kernel kernel, [Description("The prompt to evaluate")] string prompt)
    {
        var settings = new OpenAIPromptExecutionSettings { ResponseFormat = "json_object", Temperature = 0.3, TopP = 0.5, MaxTokens = 1024 };

        var args = new KernelArguments(settings) { ["prompt"] = prompt, ["guide"] = PromptConstants.PromptGuideTopics };
        var result = await kernel.InvokePromptAsync(PromptConstants.EvaluatorFunctionPrompt, args);
        return result.GetValue<string>() ?? "Inform the user about an error executing evaluations";
    }

    [KernelFunction, Description("Get expert advice on how to improve a prompt")]
    public async Task<string> PromptExpertAdvice(Kernel kernel, [Description("The prompt that needs improvement")] string prompt, [Description("issues found by the evaluator")] string issues, [Description("Optional specific question for the expert")] string question = "")
    {
        if (string.IsNullOrEmpty(question))
        {
            question = issues;
        }
        if (_semanticTextMemory is null) await CreateMemoryStore();
			
        var plugin = KernelPluginFactory.CreateFromObject(new TextMemoryPlugin(_semanticTextMemory), "TextMemory");
        kernel.Plugins.Add(plugin);
			
        var settings = new OpenAIPromptExecutionSettings { Temperature = 0.7, MaxTokens = 512 };
        var args = new KernelArguments(settings)
        {
            ["input"] = question,
            ["limit"] = 10,
            ["relevance"] = 0.40,
            ["collection"] = CollectionName.PromptEngineerCollection,
            ["prompt"] = prompt,
            ["issues"] = issues
        };
        var result = await kernel.InvokePromptAsync(PromptHelperPrompt, args);
        kernel.Plugins.Remove(plugin);
        return result.GetValue<string>() ?? "Inform the user about an error executing Get Expert Advice and proceed without it.";

    }

    private static async Task CreateMemoryStore()
    {
        var sqliteConnect = await SqliteMemoryStore.ConnectAsync(_configuration["Sqlite:ConnectionString"]);
        var collections = await sqliteConnect.GetCollectionsAsync().ToListAsync();
        if (!collections.Contains(CollectionName.PromptEngineerCollection))
        {
            await sqliteConnect.CreateCollectionAsync(CollectionName.PromptEngineerCollection);
        }
        _semanticTextMemory = new MemoryBuilder()
            .WithOpenAITextEmbeddingGeneration(_configuration["OpenAI:EmbeddingModelId"], _configuration["OpenAI:ApiKey"])
            .WithMemoryStore(sqliteConnect).Build();
    }

    public static async Task SavePromptGuide(IConfiguration configuration)
    {
        // ToDo Remove this method
        //return;
        _configuration = configuration;
        await CreateMemoryStore();
        var files = Directory.GetFiles(@"C:\Users\adamh\source\repos\PromptLab\PromptLab.Core\PromptMdFiles", "*.md");
        var fileText = new Dictionary<string, string>();

        foreach (var file in files)
        {
            var lineBuilder = new StringBuilder();
            var lines = await File.ReadAllLinesAsync(file);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.Contains("import {")) continue;
                lineBuilder.AppendLine(line);
            }
            fileText[Path.GetFileNameWithoutExtension(file)] = lineBuilder.ToString();
        }
			
        foreach (var (name, text) in fileText)
        {
            var lines = TextChunker.SplitMarkDownLines(text, 200, StringHelpers.GetTokens);
            var chunks = TextChunker.SplitMarkdownParagraphs(lines, 512, 102, name, StringHelpers.GetTokens);
            foreach (var chunk in chunks)
            {
                var id = await _semanticTextMemory.SaveInformationAsync(CollectionName.PromptEngineerCollection, chunk, $"{name}-{chunks.IndexOf(chunk)}", name);
            }
        }

    }
}
public class PromptEval
{
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;
    public override string ToString()
    {
        return $"Score: {Score}<br/>Explanation: {Explanation}";
    }
}