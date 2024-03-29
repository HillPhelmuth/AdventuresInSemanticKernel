using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.Memory;

namespace SkPluginLibrary.Abstractions;

public interface ITokenization
{
    Task<string?> GenerateResponseWithLogitBias(Dictionary<int, int> logitBiasSettings, string query);
    IAsyncEnumerable<string> GenerateLongText(string query = "Write a 2000 word essay about the life of Abraham Lincoln");

    Dictionary<int, (List<TokenString>, int)> ChunkAndTokenize(string input, int lineMax, int chunkMax,
        int overlap);

    Task<string> SaveChunks(List<TokenizedChunk> chunks, string model = "text-embedding-3-small");
    Task<List<MemoryQueryResult>> SearchInChunks(string query, int limit = 1, double threshold = 0.7d);
    Task<ChatChoice> GetLogProbs(string input, float temp, float topP, string systemPrompt = "You are a helpful AI model", string model = "gpt-3.5-turbo");
}