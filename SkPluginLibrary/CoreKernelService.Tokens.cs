using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using System.Text.Json;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Text and Token Tinkering

    public async Task<string?> GenerateResponseWithLogitBias(Dictionary<int, int> logitBiasSettings, string query)
    {
        var chatSettings = ChatRequestSettingsWithLogitBias(logitBiasSettings);
        var chat = new OpenAIChatCompletion("gpt-3.5-turbo-1106", Env.Var("OPENAI_API_KEY"), loggerFactory: _loggerFactory);
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
        string query = "Write a 2000 word essay about the life of Abraham Lincoln")
    {
        var kernel = CreateKernel();
        var chatService = kernel.GetService<IChatCompletion>();
        var chat = chatService.CreateNewChat("You are a helpful AI writer");
        var chatSettings = new OpenAIRequestSettings() { MaxTokens = 4000, Temperature = 1.0, TopP = 1.0 };
        chat.AddUserMessage(query);
        await foreach (var token in chatService.GenerateMessageStreamAsync(chat, chatSettings))
        {
            yield return token;
        }
    }

    public Dictionary<int, (List<TokenString>, int)> ChunkAndTokenize(string input, int lineMax, int chunkMax,
        int overlap)
    {
        var lines = TextChunker.SplitPlainTextLines(input, lineMax, StringHelpers.GetTokens);
        var chunks = TextChunker.SplitPlainTextParagraphs(lines, chunkMax, overlap, "", StringHelpers.GetTokens);
        var index = 0;

        return chunks.ToDictionary(chunk => index++, chunk => (StringHelpers.EncodeDecodeWithSpaces(chunk), StringHelpers.GetTokens(chunk)));
    }

    #endregion

    #region Text Search
    private ISemanticTextMemory? _chunkMemory;
    public async Task<string> SaveChunks(List<TokenizedChunk> chunks)
    {
        _chunkMemory = CreateMemoryStore();
        var ids = new List<string>();
        foreach (var chunk in chunks)
        {
            var id = await _chunkMemory.SaveInformationAsync("chunkCollection", chunk.Text, chunk.ChunkNumber.ToString());
            ids.Add(id);
        }
        return $"{ids.Count} items saved with ids: {string.Join(", ", ids)}";
    }
    public async Task<List<MemoryQueryResult>> SearchInChunks(string query, int limit = 1, double threshold = 0.7d)
    {
        var results = await _chunkMemory.SearchAsync("chunkCollection", query, limit, threshold).ToListAsync();
        return results;
    }

    #endregion
}