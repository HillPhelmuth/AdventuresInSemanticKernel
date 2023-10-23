﻿// Copyright (c) Microsoft. All rights reserved.

using Azure.Identity;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;

namespace SkPluginLibrary.Examples;

/**
 * This example shows how to connect your app to Azure OpenAI using
 * Azure Active Directory (AAD) authentication, as opposed to API keys.
 *
 * The example uses DefaultAzureCredential, which you can configure to support
 * multiple authentication strategies:
 *
 * - Env vars present in Azure VMs
 * - Azure Managed Identities
 * - Shared tokens
 * - etc.
 */

// ReSharper disable once InconsistentNaming
public static class Example26_AADAuth
{
    public static async Task RunAsync()
    {
        Console.WriteLine("======== SK with AAD Auth (AZURE OPENAI ONLY) ========");

        // Optional: choose which authentication to support
        var authOptions = new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = true,
            ExcludeManagedIdentityCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeAzureCliCredential = true,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeInteractiveBrowserCredential = false,
            ExcludeAzureDeveloperCliCredential = true,
            ExcludeWorkloadIdentityCredential = true,
            ExcludeAzurePowerShellCredential = true
        };

        IKernel kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            // Add Azure chat completion service using DefaultAzureCredential AAD auth
            .WithAzureChatCompletionService(
                TestConfiguration.AzureOpenAI.ChatDeploymentName,
                TestConfiguration.AzureOpenAI.Endpoint,
                new DefaultAzureCredential(authOptions))
            .Build();

        IChatCompletion chatGPT = kernel.GetService<IChatCompletion>();
        var chatHistory = chatGPT.CreateNewChat();

        // User message
        chatHistory.AddUserMessage("Tell me a joke about hourglasses");

        // Bot reply
        string reply = await chatGPT.GenerateMessageAsync(chatHistory);
        Console.WriteLine(reply);

        /* Output: Why did the hourglass go to the doctor? Because it was feeling a little run down! */
    }
}