﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Examples;

public static class Example61_MultipleLLMs
{
    /// <summary>
    /// Show how to run a prompt function and specify a specific service to use.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("======== Example61_MultipleLLMs ========");

        string azureApiKey = TestConfiguration.AzureOpenAI.ApiKey;
        string azureDeploymentName = TestConfiguration.AzureOpenAI.Gpt4DeploymentName;
        string azureModelId = TestConfiguration.AzureOpenAI.Gpt35ModelId;
        string azureEndpoint = TestConfiguration.AzureOpenAI.Endpoint;

        if (azureApiKey == null || azureDeploymentName == null || azureModelId == null || azureEndpoint == null)
        {
            Console.WriteLine("AzureOpenAI endpoint, apiKey, deploymentName or modelId not found. Skipping example.");
            return;
        }

        string openAIModelId = TestConfiguration.OpenAI.Gpt4ModelId;
        string openAIApiKey = TestConfiguration.OpenAI.ApiKey;

        if (openAIModelId == null || openAIApiKey == null)
        {
            Console.WriteLine("OpenAI credentials not found. Skipping example.");
            return;
        }

        Kernel kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: azureDeploymentName,
                endpoint: azureEndpoint,
                apiKey: azureApiKey,
                serviceId: "AzureOpenAIChat",
                modelId: azureModelId)
            .AddOpenAIChatCompletion(
                modelId: openAIModelId,
                apiKey: openAIApiKey,
                serviceId: "OpenAIChat")
            .Build();

        await RunByServiceIdAsync(kernel, "AzureOpenAIChat");
        await RunByModelIdAsync(kernel, openAIModelId);
        await RunByFirstModelIdAsync(kernel, "gpt-4-turbo", azureModelId, openAIModelId);
    }

    public static async Task RunByServiceIdAsync(Kernel kernel, string serviceId)
    {
        Console.WriteLine($"======== Service Id: {serviceId} ========");

        var prompt = "Hello AI, what can you do for me?";

        KernelArguments arguments = new();
        arguments.ExecutionSettings = new Dictionary<string, PromptExecutionSettings>()
        {
            { serviceId, new PromptExecutionSettings() }
        };
        var result = await kernel.InvokePromptAsync(prompt, arguments);
        Console.WriteLine(result.GetValue<string>());
    }

    public static async Task RunByModelIdAsync(Kernel kernel, string modelId)
    {
        Console.WriteLine($"======== Model Id: {modelId} ========");

        var prompt = "Hello AI, what can you do for me?";

        var result = await kernel.InvokePromptAsync(
            prompt,
            new(new PromptExecutionSettings()
            {
                ModelId = modelId
            }));
        Console.WriteLine(result.GetValue<string>());
    }

    public static async Task RunByFirstModelIdAsync(Kernel kernel, params string[] modelIds)
    {
        Console.WriteLine($"======== Model Ids: {string.Join(", ", modelIds)} ========");

        var prompt = "Hello AI, what can you do for me?";

        var modelSettings = new Dictionary<string, PromptExecutionSettings>();
        foreach (var modelId in modelIds)
        {
            modelSettings.Add(modelId, new PromptExecutionSettings() { ModelId = modelId });
        }
        var promptConfig = new PromptTemplateConfig(prompt) { Name = "HelloAI", ExecutionSettings = modelSettings };

        var function = kernel.CreateFunctionFromPrompt(promptConfig);

        var result = await kernel.InvokeAsync(function);
        Console.WriteLine(result.GetValue<string>());
    }
}