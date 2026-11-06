using Microsoft.Extensions.AI;
using OpenAI.Chat;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Plugins.NativePlugins;

/// <summary>Raw text completion, including per-token log probabilities (read from the OpenAI response behind the chat client).</summary>
public class RawCompletionPlugin(IChatClient chatClient)
{
	private const string RawCompletionPrompt =
                    """
					You are a text completion AI. Continue the text below. No introductions or other pleasantries. Simply pick up precisely where the Text left off as naturally as possible. DO NOT INCLUDE THE TEXT ITSELF IN YOUR RESPONSE.

					## Example
					**Text:**
					I am a pro foot
					**Completion:**
					ball player.

					## Actual Work
					**Text:**
					{{ $text }}
					**Completion:**
					""";

	[Description("Generates a completion for the given text")]
	public async Task<string> Complete(string text, int maxTokens = 10)
	{
		var options = new ChatOptions { Temperature = 0.0f, TopP = 0.0f, MaxOutputTokens = maxTokens };
		var result = await PromptRunner.RunAsync(chatClient, RawCompletionPrompt, new Dictionary<string, object?> { ["text"] = text }, options);
		Console.WriteLine($"RawCompletion function generated completion: '{result}'");
		return result;
	}

	[Description("Create a single token as well as the top log probs")]
	[return:Description("A `TokenString` object representing the token and its log probabilities")]
	public async Task<TokenString?> GetTokenWithLogProbs(string text, int maxTokens = 1)
	{
		// Provider-specific options flow through ChatOptions.RawRepresentationFactory.
		var options = new ChatOptions
		{
			MaxOutputTokens = maxTokens,
			RawRepresentationFactory = _ => new ChatCompletionOptions { IncludeLogProbabilities = true, TopLogProbabilityCount = 5 }
		};
		var response = await chatClient.GetResponseAsync(PromptRunner.Render(RawCompletionPrompt, new Dictionary<string, object?> { ["text"] = text }), options);
		var logProbs = (response.RawRepresentation as ChatCompletion)?.ContentTokenLogProbabilities;
		return logProbs is { Count: > 0 } ? logProbs[0].AsTokenString() : null;
	}
}
