using Microsoft.Extensions.AI;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Plugins.NativePlugins;

public class InstructPlugin
{
    private readonly IChatClient _chatClient;
    public InstructPlugin(IChatClient chatClient)
    {
        _chatClient = chatClient;
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
    [Description("Generates an instruction prompt that semantically bridges the gap between user input and relevant content")]
    public async Task<string> Bridge(string input, string related)
    {
        var result = await PromptRunner.RunAsync(_chatClient, FunctionPrompt,
            new Dictionary<string, object?> { ["input"] = input, ["related"] = related },
            new ChatOptions { Temperature = 0.0f, TopP = 0.0f, MaxOutputTokens = 256 });
        Console.WriteLine($"Bridge function generated instruction: '{result}'");
        return result;
    }
}
