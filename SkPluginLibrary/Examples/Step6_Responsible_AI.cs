﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;

// This example shows how to use rendering event hooks to ensure that prompts are rendered in a responsible manner.
namespace SkPluginLibrary.Examples;

public static class Step6_Responsible_AI
{
    /// <summary>
    /// Show how to use rendering event hooks to ensure that prompts are rendered in a responsible manner.
    /// </summary>
    public static async Task RunAsync()
    {
        // Create a kernel with OpenAI chat completion
        Kernel kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(
                modelId: TestConfiguration.OpenAI.Gpt4ModelId,
                apiKey: TestConfiguration.OpenAI.ApiKey)
            .Build();

        // Handler which is called before a prompt is rendered
        void MyRenderingHandler(object? sender, PromptRenderingEventArgs e)
        {
            if (e.Arguments.ContainsName("card_number"))
            {
                e.Arguments["card_number"] = "**** **** **** ****";
            }
        }

        // Handler which is called after a prompt is rendered
        void MyRenderedHandler(object? sender, PromptRenderedEventArgs e)
        {
            e.RenderedPrompt += " NO SEXISM, RACISM OR OTHER BIAS/BIGOTRY";

            Console.WriteLine(e.RenderedPrompt);
        }

        // Add the handlers to the kernel
        kernel.PromptRendering += MyRenderingHandler;
        kernel.PromptRendered += MyRenderedHandler;

        KernelArguments arguments = new() { { "card_number", "4444 3333 2222 1111" } };
        Console.WriteLine(await kernel.InvokePromptAsync("Tell me some useful information about this credit card number {{$card_number}}?", arguments));
    }
}