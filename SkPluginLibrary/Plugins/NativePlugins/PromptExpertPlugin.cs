using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using SkPluginLibrary.Services;
using static SkPluginLibrary.CoreKernelService;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class PromptExpertPlugin
{
    private static TextMemory? _textMemory;
    private static IConfiguration _configuration = null!;
    private const string PromptHelperPrompt =
        """
        You are a prompt expert. Using the Expert Knowledge below, provide instructions on how to improve the provided prompt.

        ## Expert Knowledge
        {{ $expertKnowledge }}

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
    }

    [Description("Evaulate prompts for effectiveness in guiding AI interactions")]
    [return: Description("Json object containing the score and explanation")]
    public async Task<string> EvaluatePrompt([Description("The prompt to evaluate")] string prompt)
    {
        var options = new ChatOptions { ResponseFormat = ChatResponseFormat.Json, Temperature = 0.3f, TopP = 0.5f, MaxOutputTokens = 1024 };
        var result = await PromptRunner.RunAsync(ChatClientFactory.Create(), PromptConstants.EvaluatorFunctionPrompt,
            new Dictionary<string, object?> { ["prompt"] = prompt, ["guide"] = PromptConstants.PromptGuideTopics }, options);
        return string.IsNullOrEmpty(result) ? "Inform the user about an error executing evaluations" : result;
    }

    [Description("Get expert advice on how to improve a prompt")]
    public async Task<string> PromptExpertAdvice([Description("The prompt that needs improvement")] string prompt, [Description("issues found by the evaluator")] string issues, [Description("Optional specific question for the expert")] string question = "")
    {
        if (string.IsNullOrEmpty(question))
        {
            question = issues;
        }
        _textMemory ??= await CreateMemoryStore();
        var knowledge = await _textMemory.SearchAsync(CollectionName.PromptEngineerCollection, question, 10, 0.40).ToListAsync();
        var result = await PromptRunner.RunAsync(ChatClientFactory.Create(), PromptHelperPrompt, new Dictionary<string, object?>
        {
            ["expertKnowledge"] = System.Text.Json.JsonSerializer.Serialize(knowledge.Select(k => k.Metadata.Text)),
            ["input"] = question,
            ["prompt"] = prompt,
            ["issues"] = issues
        }, new ChatOptions { Temperature = 0.7f, MaxOutputTokens = 512 });
        return string.IsNullOrEmpty(result) ? "Inform the user about an error executing Get Expert Advice and proceed without it." : result;
    }

    private static async Task<TextMemory> CreateMemoryStore()
    {
        var store = await SqliteVectorMemoryStore.ConnectAsync(_configuration["Sqlite:ConnectionString"]!);
        if (!await store.DoesCollectionExistAsync(CollectionName.PromptEngineerCollection))
            await store.CreateCollectionAsync(CollectionName.PromptEngineerCollection);
        return new TextMemory(store, ChatClientFactory.CreateEmbeddingGenerator(_configuration["OpenAI:EmbeddingModelId"]));
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
