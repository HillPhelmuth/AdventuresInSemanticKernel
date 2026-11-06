using System.ClientModel;
using ChatComponents;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using SkPluginLibrary.Plugins.NativePlugins;
using SkPluginLibrary.Services;
using ChatMessage = OpenAI.Chat.ChatMessage;

namespace SkPluginLibrary;

public partial class CoreKernelService
{
    #region Text and Token Tinkering

    public async Task<string?> GenerateResponseWithLogitBias(Dictionary<int, int> logitBiasSettings, string query)
    {
        // Logit bias is an OpenAI-specific option, passed through ChatOptions.RawRepresentationFactory.
        var chatOptions = new ChatCompletionOptions();
        foreach (var (key, value) in logitBiasSettings)
        {
            var val = value > 100 ? 100 :
                value < -100 ? -100 : value; //100 is the max value -100 is the min value for logit bias.
            Console.WriteLine($"Token {key} set to logitBias {value}");
            chatOptions.LogitBiases[key] = val;
        }
        var client = ChatClientFactory.Create("gpt-3.5-turbo-1106", ChatClientFactory.OpenAIProvider, _loggerFactory);
        var reply = await client.GetResponseAsync(query, new ChatOptions { RawRepresentationFactory = _ => chatOptions });
        Console.WriteLine($"RESPONSE GENERATED:----------\n{reply.Text}");
        return reply.Text;
    }

    public async IAsyncEnumerable<string> GenerateLongText(
        string query = "Write a 2000 word essay about the life of Abraham Lincoln")
    {
        var agent = CreateAgent(CreateChatClient(), "You are a helpful AI writer", name: "Writer",
            options: new ChatOptions { MaxOutputTokens = 4000, Temperature = 1.0f, TopP = 1.0f });
        await foreach (var token in StreamText(agent, [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, query)]))
        {
            yield return token;
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
    private TextMemory? _chunkMemory;
    public async Task<string> SaveChunks(List<TokenizedChunk> chunks, string model = "text-embedding-3-small")
    {
        _chunkMemory = new TextMemory(new VectorMemoryStore(), ChatClientFactory.CreateEmbeddingGenerator(model));
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
        var results = await _chunkMemory!.SearchAsync("chunkCollection", query, limit, threshold).ToListAsync();
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
        var chat = ChatClientFactory.CreateOpenAIClient().GetChatClient(model);
        ClientResult<ChatCompletion>? response = await chat.CompleteChatAsync(messages, options);
        return response.Value;
    }
    public async IAsyncEnumerable<TokenString> GetStreamingLogProbs(ChatHistory history,
        AIModel model = AIModel.Gpt41Mini, int maxTokens = 3, float temp = 1.0f, float topP = 1.0f)
    {
        var options = new ChatCompletionOptions { TopLogProbabilityCount = 5, IncludeLogProbabilities = true, MaxOutputTokenCount = maxTokens, Temperature = temp, TopP = topP};
        var messages = new List<ChatMessage>();
        foreach (var message in history)
        {
            if (message.Role == ChatRole.User)
                messages.Add(ChatMessage.CreateUserMessage(message.Text));
            if (message.Role == ChatRole.Assistant)
                messages.Add(ChatMessage.CreateAssistantMessage(message.Text));
            if (message.Role == ChatRole.System)
                messages.Add(ChatMessage.CreateSystemMessage(message.Text));
        }
        var chat = ChatClientFactory.CreateOpenAIClient().GetChatClient(model.GetOpenAIModelName());
        await foreach (var chunk in chat.CompleteChatStreamingAsync(messages, options))
        {
            if (chunk.ContentTokenLogProbabilities == null || chunk.ContentTokenLogProbabilities.Count == 0) continue;
            var logProb = chunk.ContentTokenLogProbabilities[0];
            yield return logProb.AsTokenString();
        }
    }

    public async IAsyncEnumerable<string> GenerateAutoCompleteOptions(string text, int maxTokens = 10, AIModel model = AIModel.Gpt41Mini)
    {
        var plugin = new RawCompletionPlugin(CreateChatClient(model));
        var firstTokenOptions = await plugin.GetTokenWithLogProbs(text, 1);
        foreach (var token in firstTokenOptions?.TopLogProbs.DistinctBy(x => x.StringValue) ?? [])
        {
            var completion = await plugin.Complete(text + token.StringValue, maxTokens);
            yield return $"{token.StringValue} {completion}";
        }
    }

    public async Task<string> GenearteSingleAutoComplete(string text, int maxTokens = 10, AIModel model = AIModel.Gpt41Mini)
    {
        var firstItem = await GenerateAutoCompleteOptions(text, maxTokens, model).FirstOrDefaultAsync();
        return firstItem!;
    }

    #endregion
}
