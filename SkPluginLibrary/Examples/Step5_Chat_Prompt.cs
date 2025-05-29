// Copyright (c) Microsoft. All rights reserved.

namespace SkPluginLibrary.Examples;

public sealed class Step5_Chat_Prompt
{
    /// <summary>
    /// Show how to construct a chat prompt and invoke it.
    /// </summary>
    public async Task RunAsync()
    {
        // Create a kernel with OpenAI chat completion
        Kernel kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: TestConfiguration.OpenAI.Gpt4MiniModelId,
                apiKey: TestConfiguration.OpenAI.ApiKey)
            .Build();

        // Invoke the kernel with a chat prompt and display the result
        string chatPrompt = """
            <message role="user">What is Seattle?</message>
            <message role="system">Respond with JSON.</message>
            """;

        Console.WriteLine(await kernel.InvokePromptAsync(chatPrompt));
    }
}
