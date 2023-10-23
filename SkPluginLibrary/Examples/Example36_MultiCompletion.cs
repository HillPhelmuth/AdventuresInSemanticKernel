﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;

namespace SkPluginLibrary.Examples;

/**
 * The following example shows how to use Semantic Kernel with streaming Multiple Results Chat Completion.
 */
// ReSharper disable once InconsistentNaming
public static class Example36_MultiCompletion
{
    public static async Task RunAsync()
    {
        await AzureOpenAIMultiChatCompletionAsync();
        await OpenAIMultiChatCompletionAsync();
    }

    private static async Task AzureOpenAIMultiChatCompletionAsync()
    {
        Console.WriteLine("======== Azure OpenAI - Multiple Chat Completion ========");

        var chatCompletion = new AzureChatCompletion(
            TestConfiguration.AzureOpenAI.ChatDeploymentName,
            TestConfiguration.AzureOpenAI.Endpoint,
            TestConfiguration.AzureOpenAI.ApiKey);

        await ChatCompletionAsync(chatCompletion);
    }

    private static async Task OpenAIMultiChatCompletionAsync()
    {
        Console.WriteLine("======== Open AI - Multiple Chat Completion ========");

        IChatCompletion chatCompletion = new OpenAIChatCompletion(
            TestConfiguration.OpenAI.ChatModelId,
            TestConfiguration.OpenAI.ApiKey);

        await ChatCompletionAsync(chatCompletion);
    }

    private static async Task ChatCompletionAsync(IChatCompletion chatCompletion)
    {
        var requestSettings = new OpenAIRequestSettings()
        {
            MaxTokens = 200,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            Temperature = 1,
            TopP = 0.5,
            ResultsPerPrompt = 2,
        };

        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Write one paragraph about why AI is awesome");

        await foreach (string message in chatCompletion.GenerateMessageStreamAsync(chatHistory))
        {
            Console.Write(message);
            Console.WriteLine("-------------");
        }

        Console.WriteLine();
    }
}