using System.ClientModel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using System.Text.Json;
using OpenAI;
using SkPluginLibrary.Plugins;
using OpenAI.Chat;
using SkPluginLibrary.Plugins.NativePlugins;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Text and Token Tinkering

    public async Task<string?> GenerateResponseWithLogitBias(Dictionary<int, int> logitBiasSettings, string query)
    {
        var chatSettings = ChatRequestSettingsWithLogitBias(logitBiasSettings);
        var chat = new OpenAIChatCompletionService("gpt-3.5-turbo-1106", Env.Var("OPENAI_API_KEY"), loggerFactory: _loggerFactory);
        var history = new ChatHistory();
        history.AddUserMessage(query);
        var reply = await chat.GetChatMessageContentAsync(history, chatSettings);
        Console.WriteLine($"RESPONSE GENERATED:----------\n{reply}");
        return reply.ToString();
    }

    private static OpenAIPromptExecutionSettings ChatRequestSettingsWithLogitBias(Dictionary<int, int> logitBiasSettings)
    {
        var chatSettings = new OpenAIPromptExecutionSettings();
        // This will make the model try its best to avoid or employ any of the related words/tokens.
        foreach (var (key, value) in logitBiasSettings)
        {
            var val = value > 100 ? 100 :
                value < -100 ? -100 : value; //100 is the max value -100 is the min value for logit bias.
            Console.WriteLine($"Token {key} set to logitBias {value}");
            chatSettings.TokenSelectionBiases ??= new Dictionary<int, int>();
            chatSettings.TokenSelectionBiases.TryAdd(key, val);
            Console.WriteLine($"All biases set: {JsonSerializer.Serialize(chatSettings.TokenSelectionBiases)}");
        }

        return chatSettings;
    }

    public async IAsyncEnumerable<string> GenerateLongText(
        string query = "Write a 2000 word essay about the life of Abraham Lincoln")
    {
        var kernel = CreateKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chat = new ChatHistory("You are a helpful AI writer");
        var chatSettings = new OpenAIPromptExecutionSettings() { MaxTokens = 4000, Temperature = 1.0, TopP = 1.0 };
        chat.AddUserMessage(query);
        await foreach (var token in chatService.GetStreamingChatMessageContentsAsync(chat, chatSettings))
        {
            yield return token.ToString();
        }
    }

    public Dictionary<int, (List<TokenString>, int)> ChunkAndTokenize(string input, int lineMax, int chunkMax,
        int overlap)
    {
        var chunks = Chunks(input, lineMax, chunkMax, overlap);
        var index = 0;

        return chunks.ToDictionary(chunk => index++, chunk => (StringHelpers.EncodeDecodeWithSpaces(chunk), StringHelpers.GetTokens(chunk)));
    }

    private static List<string> Chunks(string input, int lineMax, int chunkMax, int overlap)
    {
        var lines = TextChunker.SplitMarkDownLines(input, lineMax, StringHelpers.GetTokens);
        var chunks = TextChunker.SplitMarkdownParagraphs(lines, chunkMax, overlap, "", StringHelpers.GetTokens);
        return chunks;
    }

    #endregion

    #region Text Search
    private ISemanticTextMemory? _chunkMemory;
    public async Task<string> SaveChunks(List<TokenizedChunk> chunks, string model = "text-embedding-3-small")
    {
        _chunkMemory = CreateMemoryStore(model: model);
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

    #region LogProbs

    public async Task<ChatCompletion> GetLogProbs(string input, float temp, float topP,
        string systemPrompt = "You are a helpful AI model", string model = "gpt-3.5-turbo")
    {
        var options = new ChatCompletionOptions { TopLogProbabilityCount = 5, IncludeLogProbabilities = true, Temperature = temp, TopP = topP };
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage(input)
        };
        var chat = new OpenAIClient(TestConfiguration.OpenAI.ApiKey).GetChatClient(model);
        ClientResult<ChatCompletion>? response = await chat.CompleteChatAsync(messages, options);
        var chatChoice = response.Value;
        return chatChoice;
    }
    public async IAsyncEnumerable<TokenString> GetStreamingLogProbs(ChatHistory history,
        AIModel model = AIModel.Gpt41Mini, int maxTokens = 3, float temp = 1.0f, float topP = 1.0f)
    {
        var options = new ChatCompletionOptions { TopLogProbabilityCount = 5, IncludeLogProbabilities = true, MaxOutputTokenCount = maxTokens, Temperature = temp, TopP = topP};
        var messages = new List<ChatMessage>();
        foreach (var message in history)
        {
            if (message.Role == AuthorRole.User)
                messages.Add(ChatMessage.CreateUserMessage(message.Content));
            if (message.Role == AuthorRole.Assistant)
                messages.Add(ChatMessage.CreateAssistantMessage(message.Content));
            if (message.Role == AuthorRole.System)
                messages.Add(ChatMessage.CreateSystemMessage(message.Content));
        }
        var chat = new OpenAIClient(TestConfiguration.OpenAI.ApiKey).GetChatClient(model.GetOpenAIModelName());
        await foreach (var chunk in chat.CompleteChatStreamingAsync(messages, options))
        {
            if (chunk.ContentTokenLogProbabilities == null || chunk.ContentTokenLogProbabilities.Count == 0) continue;
            var logProb = chunk.ContentTokenLogProbabilities[0];
            yield return logProb.AsTokenString();
        }
    }
    

    public async IAsyncEnumerable<string> GenerateAutoCompleteOptions(string text, int maxTokens = 10, AIModel model = AIModel.Gpt41Mini)
    {
        var kernel = CreateKernel(model);
        var plugin = kernel.ImportPluginFromType<RawCompletionPlugin>();
        var args = new KernelArguments { ["text"] = text, ["maxTokens"] = 1 };
        var logProbFunction = plugin["GetTokenWithLogProbs"];
        var completionFunction = plugin["Complete"];
        var firstTokenOptions = await kernel.InvokeAsync<TokenString>(logProbFunction, args);
        foreach (var token in firstTokenOptions?.TopLogProbs.DistinctBy(x => x.StringValue) ?? [])
        {
            var tokenArgs = new KernelArguments { ["text"] = text + token.StringValue, ["maxTokens"] = maxTokens };
            var completion = await kernel.InvokeAsync<string>(completionFunction, tokenArgs);
            yield return $"{token.StringValue} {completion}";
        }

    }

    public async Task<string> GenearteSingleAutoComplete(string text, int maxTokens = 10, AIModel model = AIModel.Gpt41Mini)
    {
        var firstItem = await GenerateAutoCompleteOptions(text, maxTokens, model).FirstOrDefaultAsync();
        return firstItem;
    }

    #endregion
}