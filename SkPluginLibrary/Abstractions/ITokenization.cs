﻿using Azure.AI.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using OpenAI.Chat;

namespace SkPluginLibrary.Abstractions;

public interface ITokenization
{
    Task<string?> GenerateResponseWithLogitBias(Dictionary<int, int> logitBiasSettings, string query);
    IAsyncEnumerable<string> GenerateLongText(string query = "Write a 2000 word essay about the life of Abraham Lincoln");

    Dictionary<int, (List<TokenString>, int)> ChunkAndTokenize(string input, int lineMax, int chunkMax,
        int overlap);

    Task<string> SaveChunks(List<TokenizedChunk> chunks, string model = "text-embedding-3-small");
    Task<List<MemoryQueryResult>> SearchInChunks(string query, int limit = 1, double threshold = 0.7d);
    Task<ChatCompletion> GetLogProbs(string input, float temp, float topP,
        string systemPrompt = "You are a helpful AI model", string model = "gpt-3.5-turbo");
    IAsyncEnumerable<string> GenerateAutoCompleteOptions(string text, int maxTokens = 10, AIModel model = AIModel.Gpt41Mini);
    Task<string> GenearteSingleAutoComplete(string text, int maxTokens = 10, AIModel model = AIModel.Gpt41Mini);

    //static IKernelBuilder GetKernelBuilder(AIModel aiModel);
    IAsyncEnumerable<TokenString> GetStreamingLogProbs(ChatHistory history,
        AIModel model = AIModel.Gpt41Mini, int maxTokens = 3, float temp = 1.0f, float topP = 1.0f);
}