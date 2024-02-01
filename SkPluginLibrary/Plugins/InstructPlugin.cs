using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SkPluginLibrary.Plugins;

public class InstructPlugin
{
    private readonly Kernel _kernel;
    public InstructPlugin(Kernel kernel)
    {
            _kernel = kernel;
        }

    private const string FunctionPrompt = """
                                          You are a generative AI chatbot instruction prompt generator.
                                          For this chatbot the user input will be used to find relevant [related content] that is up-to-date so chatbot doesn't have to rely on training data.
                                          As the instruction prompt generator, you are responsible for providing a short and succinct instruction that defines the [related content] in a way that is related to the user input and functions as a semantic bridge between them.

                                          You will be provided with the original user input and the [related content]. Your response should only be the instruction.

                                          [user input]
                                          {{$input}}

                                          [related content]
                                          {{$related}}
                                          """;
    [KernelFunction, Description("Generates an instruction prompt that semantically bridges the gap between user input and relevant content")]
    public async Task<string> Bridge(string input, string related)
    {
            var ctx = new KernelArguments
            {
                ["input"] = input,
                ["related"] = related
            };
            var function = _kernel.CreateFunctionFromPrompt(FunctionPrompt, executionSettings: new OpenAIPromptExecutionSettings { Temperature = 0.0, TopP = 0.0, MaxTokens = 256 });
            var kernelResult = await _kernel.InvokeAsync(function, ctx);
            var result = kernelResult.Result();
            Console.WriteLine($"Bridge function generated instruction: '{result}'");
            return result;
        }
}