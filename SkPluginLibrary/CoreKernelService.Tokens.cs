using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Text;
using System.Text.Json;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Text and Token Tinkering

    public async Task<string?> GenerateResponseWithLogitBias(Dictionary<int, int> logitBiasSettings, string query)
    {
        var chatSettings = ChatRequestSettingsWithLogitBias(logitBiasSettings);
        var chat = new OpenAIChatCompletion("gpt-3.5-turbo", Env.Var("OPENAI_API_KEY"), loggerFactory: _loggerFactory);
        var history = chat.CreateNewChat();
        history.AddUserMessage(query);
        var reply = await chat.GenerateMessageAsync(history, chatSettings);
        Console.WriteLine($"RESPONSE GENERATED:----------\n{reply}");
        return reply;
    }

    private static OpenAIRequestSettings ChatRequestSettingsWithLogitBias(Dictionary<int, int> logitBiasSettings)
    {
        var chatSettings = new OpenAIRequestSettings();
        // This will make the model try its best to avoid or employ any of the related words/tokens.
        foreach (var (key, value) in logitBiasSettings)
        {
            var val = value > 100 ? 100 :
                value < -100 ? -100 : value; //100 is the max value -100 is the min value for logit bias.
            Console.WriteLine($"Token {key} set to logitBias {value}");
            chatSettings.TokenSelectionBiases.TryAdd(key, val);
            Console.WriteLine($"All biases set: {JsonSerializer.Serialize(chatSettings.TokenSelectionBiases)}");
        }

        return chatSettings;
    }

    public async IAsyncEnumerable<string> GenerateLongText(
        string query = "Write a 1200 word essay about the life of Abraham Lincoln")
    {
        var kernel = CreateKernel("gpt-3.5-turbo");
        var chatService = kernel.GetService<IChatCompletion>();
        var chat = chatService.CreateNewChat("You are a helpful AI writer");
        var chatSettings = new OpenAIRequestSettings() { MaxTokens = 2000, Temperature = 1.0, TopP = 1.0 };
        chat.AddUserMessage(query);
        await foreach (var token in chatService.GenerateMessageStreamAsync(chat, chatSettings))
        {
            yield return token;
        }
    }

    public Dictionary<int, (List<TokenString>, int)> ChunkAndTokenize(string input, int lineMax, int chunkMax,
        int overlap)
    {
        var result = new Dictionary<int, (List<TokenString>, int)>();
        var lines = TextChunker.SplitPlainTextLines(input, lineMax, StringHelpers.GetTokens);
        var chunks = TextChunker.SplitPlainTextParagraphs(lines, chunkMax, overlap, "", StringHelpers.GetTokens);
        var index = 0;
        foreach (var chunk in chunks)
        {
            result.Add(index++, (StringHelpers.EncodeDecodeWithSpaces(chunk), StringHelpers.GetTokens(chunk)));
        }

        return result;
    }

    #endregion
}