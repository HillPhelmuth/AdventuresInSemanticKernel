using System.Globalization;
using OpenAI.Chat;
using Tiktoken;

namespace AgentFrameworkLibrary;

public sealed record TokenPiece(int Id, string Text);
public sealed record TextChunk(int Index, string Text, int Tokens);

public sealed class TokenService(RuntimeConfiguration configuration)
{
    private readonly Encoder _encoder = ModelToEncoder.For("gpt-4o");
    public int Count(string text) => _encoder.CountTokens(text);
    public IReadOnlyList<TokenPiece> Tokens(string text) =>
        _encoder.Encode(text).Select(id => new TokenPiece(id, _encoder.Decode([id]))).ToArray();

    public IReadOnlyList<TextChunk> Chunk(string text, int lineMax = 100, int chunkMax = 300, int overlap = 30)
    {
        if (lineMax < 1 || chunkMax < lineMax || overlap < 0 || overlap >= chunkMax)
            throw new ArgumentException("Require 0 < line budget <= chunk budget and 0 <= overlap < chunk budget.");
        var elements = StringInfo.GetTextElementEnumerator(text);
        var pieces = new List<string>();
        while (elements.MoveNext()) pieces.Add(elements.GetTextElement());
        var chunks = new List<TextChunk>();
        var start = 0;
        while (start < pieces.Count)
        {
            var end = start;
            var current = "";
            var line = "";
            var boundary = start;
            while (end < pieces.Count)
            {
                var candidate = current + pieces[end];
                if (Count(candidate) > chunkMax) break;
                current = candidate;
                if (Count(line + pieces[end]) > lineMax) { boundary = end; line = ""; }
                line += pieces[end];
                end++;
                if (pieces[end - 1].Contains('\n')) { boundary = end; line = ""; }
            }
            if (end == start) throw new ArgumentException("The budget is too small for one Unicode text element.");
            if (end < pieces.Count && boundary > start) { end = boundary; current = string.Concat(pieces.GetRange(start, end - start)); }
            chunks.Add(new TextChunk(chunks.Count, current, Count(current)));
            if (end == pieces.Count) break;
            var next = end;
            while (next > start + 1 && Count(string.Concat(pieces.GetRange(next - 1, end - next + 1))) <= overlap) next--;
            start = next;
        }
        return chunks;
    }

    public async Task<ChatCompletion> CompleteWithOptionsAsync(string text, float temperature, float topP,
        int maxTokens, bool logprobs, IReadOnlyDictionary<int, int>? bias = null, CancellationToken cancellationToken = default)
    {
        var options = CreateCompletionOptions(temperature, topP, maxTokens, logprobs, bias);
        var client = configuration.Current.CreateClient().GetChatClient(configuration.Current.Model);
        var response = (await client.CompleteChatAsync([new UserChatMessage(text)], options, cancellationToken)).Value;
        if (logprobs && response.ContentTokenLogProbabilities is not { Count: > 0 })
            throw new NotSupportedException("The selected model did not return the requested log probabilities. Check its capabilities and model configuration.");
        return response;
    }

    public ChatCompletionOptions CreateCompletionOptions(float temperature, float topP, int maxTokens, bool logprobs,
        IReadOnlyDictionary<int, int>? bias = null)
    {
        if (logprobs && !configuration.Current.SupportsLogprobs)
            throw new NotSupportedException("Log probabilities are not enabled for this provider/model. Configure a supported model explicitly.");
        if (bias?.Count > 0 && configuration.Current.Provider != "OpenAI")
            throw new NotSupportedException("Logit bias is only enabled for OpenAI in this application.");
        if (bias?.Count > 0 && !configuration.Current.Model.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase) &&
            !configuration.Current.Model.StartsWith("gpt-4.1", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Token IDs for logit bias require a GPT-4o or GPT-4.1 model using this playground's tokenizer.");
        if (maxTokens < 1 || !float.IsFinite(temperature) || !float.IsFinite(topP) || temperature is < 0 or > 2 || topP is <= 0 or > 1) throw new ArgumentOutOfRangeException(nameof(maxTokens));
        var options = new ChatCompletionOptions
        {
            Temperature = temperature, TopP = topP, MaxOutputTokenCount = maxTokens,
            IncludeLogProbabilities = logprobs, TopLogProbabilityCount = logprobs ? 5 : null
        };
        foreach (var (id, value) in bias ?? new Dictionary<int, int>())
        {
            if (id < 0 || value is < -100 or > 100) throw new ArgumentOutOfRangeException(nameof(bias));
            options.LogitBiases[id] = value;
        }
        return options;
    }
}
