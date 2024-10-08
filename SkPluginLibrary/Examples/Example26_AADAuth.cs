﻿// Copyright (c) Microsoft. All rights reserved.

using Azure.Identity;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SkPluginLibrary.Examples;

/// <summary>
/// This example shows how to connect your app to Azure OpenAI using
/// Azure Active Directory(AAD) authentication, as opposed to API keys.
///
/// The example uses <see cref="DefaultAzureCredential"/>, which you can configure to support
/// multiple authentication strategies:
///
/// -Env vars present in Azure VMs
/// -Azure Managed Identities
/// -Shared tokens
/// -etc.
/// </summary>
public static class Example26_AADAuth
{
    public static async Task RunAsync()
    {
        Console.WriteLine("======== SK with AAD Auth ========");

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

        Kernel kernel = Kernel.CreateBuilder()
            // Add Azure OpenAI chat completion service using DefaultAzureCredential AAD auth
            .AddAzureOpenAIChatCompletion(
                deploymentName: TestConfiguration.AzureOpenAI.Gpt4DeploymentName,
                endpoint: TestConfiguration.AzureOpenAI.Endpoint,
                credentials: new DefaultAzureCredential(authOptions))
            .Build();

        IChatCompletionService chatGPT = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        // User message
        chatHistory.AddUserMessage("Tell me a joke about hourglasses");

        // Bot reply
        var reply = await chatGPT.GetChatMessageContentAsync(chatHistory);
        Console.WriteLine(reply);

        /* Output: Why did the hourglass go to the doctor? Because it was feeling a little run down! */
    }
}