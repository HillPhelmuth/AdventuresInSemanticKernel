// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Examples;

/// <summary>
/// This example shows how to use GPT3.5 Chat model for prompts and prompt functions.
/// </summary>
public static class Example27_PromptFunctionsUsingChatGPT
{
    public static async Task RunAsync()
    {
        Console.WriteLine("======== Using Chat GPT model for text generation ========");

        Kernel kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: TestConfiguration.AzureOpenAI.Gpt4DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                apiKey: TestConfiguration.AzureOpenAI.ApiKey,
                modelId: TestConfiguration.AzureOpenAI.Gpt35ModelId)
            .Build();

        var func = kernel.CreateFunctionFromPrompt(
            "List the two planets closest to '{{$input}}', excluding moons, using bullet points.");

        var result = await func.InvokeAsync(kernel, new() { ["input"] = "Jupiter" });
        Console.WriteLine(result.GetValue<string>());

        /*
    Output:
       - Saturn
       - Uranus
    */
    }
}