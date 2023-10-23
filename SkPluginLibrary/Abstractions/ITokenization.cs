namespace SkPluginLibrary.Abstractions;

public interface ITokenization
{
    Task<string?> GenerateResponseWithLogitBias(Dictionary<int, int> logitBiasSettings, string query);
    IAsyncEnumerable<string> GenerateLongText(string query = "Write a 1200 word essay about the life of Abraham Lincoln");

    Dictionary<int, (List<TokenString>, int)> ChunkAndTokenize(string input, int lineMax, int chunkMax,
        int overlap);
}