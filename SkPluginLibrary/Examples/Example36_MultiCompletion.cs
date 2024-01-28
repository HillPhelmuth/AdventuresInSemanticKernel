﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

// The following example shows how to use Semantic Kernel with streaming Multiple Results Chat Completion.
namespace SkPluginLibrary.Examples;

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

        var chatCompletionService = new AzureOpenAIChatCompletionService(
            deploymentName: TestConfiguration.AzureOpenAI.Gpt4DeploymentName,
            endpoint: TestConfiguration.AzureOpenAI.Endpoint,
            apiKey: TestConfiguration.AzureOpenAI.ApiKey,
            modelId: TestConfiguration.AzureOpenAI.Gpt35ModelId);

        await ChatCompletionAsync(chatCompletionService);
    }

    private static async Task OpenAIMultiChatCompletionAsync()
    {
        Console.WriteLine("======== Open AI - Multiple Chat Completion ========");

        var chatCompletionService = new OpenAIChatCompletionService(
            TestConfiguration.OpenAI.Gpt4ModelId,
            TestConfiguration.OpenAI.ApiKey);

        await ChatCompletionAsync(chatCompletionService);
    }

    private static async Task ChatCompletionAsync(IChatCompletionService chatCompletionService)
    {
        var executionSettings = new OpenAIPromptExecutionSettings()
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

        foreach (var chatMessageChoice in await chatCompletionService.GetChatMessageContentsAsync(chatHistory, executionSettings))
        {
            Console.Write(chatMessageChoice.Content);
            Console.WriteLine("\n-------------\n");
        }

        Console.WriteLine();
    }
}