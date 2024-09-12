using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace SkPluginLibrary.Plugins;

public class RawCompletionPlugin
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
	[KernelFunction, Description("Generates a completion for the given text")]
	public async Task<string> Complete(Kernel kernel, string text, int maxTokens = 10)
	{
		
		var settings = new OpenAIPromptExecutionSettings { Temperature = 0.0, TopP = 0.0, MaxTokens = maxTokens };
		var ctx = new KernelArguments(settings)
		{
			["text"] = text
		};
		//var function = kernel.CreateFunctionFromPrompt(RawCompletionPrompt, executionSettings: settings);
		var kernelResult = await kernel.InvokePromptAsync(RawCompletionPrompt, ctx);
		var result = kernelResult.Result();
		Console.WriteLine($"RawCompletion function generated completion: '{result}'");
		return result;
	}
	[KernelFunction, Description("Create a single token as well as the top log probs")]
	[return:Description("A `TokenString` object representing the token and its log probabilities")]
	public async Task<TokenString?> GetTokenWithLogProbs(Kernel kernel, string text, int maxTokens = 1)
	{
		var settings = new OpenAIPromptExecutionSettings { MaxTokens = maxTokens, Logprobs = true, TopLogprobs = 5};
		var ctx = new KernelArguments(settings)
		{
			["text"] = text
		};
		var kernelResult = await kernel.InvokePromptAsync(RawCompletionPrompt, ctx);
        var result = kernelResult.Metadata?["ContentTokenLogProbabilities"] as IReadOnlyList<ChatTokenLogProbabilityInfo>;
		Console.WriteLine($"FunctionResult.Metadata:\n-------------------------\n{JsonSerializer.Serialize(kernelResult.Metadata)} \n--------------------------------------\n");
		Console.WriteLine($"RawCompletion function generated completion: '{result}'");
		return result?[0].AsTokenString();
	}

}