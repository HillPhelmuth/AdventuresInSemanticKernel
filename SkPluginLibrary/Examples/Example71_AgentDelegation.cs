﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Experimental.Agents;
using SkPluginLibrary.Plugins;
using SkPluginLibrary.Plugins.NativePlugins;
using SkPluginLibrary.Resources;

namespace SkPluginLibrary.Examples;

/// <summary>
/// Showcase complex Open AI Agent interactions using semantic kernel.
/// </summary>
public static class Example71_AgentDelegation
{
    /// <summary>
    /// Specific model is required that supports agents and function calling.
    /// Currently this is limited to Open AI hosted services.
    /// </summary>
    private const string OpenAIFunctionEnabledModel = "gpt-3.5-turbo-1106";

    // Track agents for clean-up
    private static readonly List<IAgent> s_agents = new();

    /// <summary>
    /// Show how to combine coordinate multiple agents.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("======== Example71_AgentDelegation ========");

        if (TestConfiguration.OpenAI.ApiKey == null)
        {
            Console.WriteLine("OpenAI apiKey not found. Skipping example.");
            return;
        }

        IAgentThread? thread = null;

        try
        {
            var plugin = KernelPluginFactory.CreateFromType<MenuPlugin>();
            var menuAgent =
                Track(
                    await new AgentBuilder()
                        .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                        .FromTemplate(EmbeddedResource.Read("Agents.ToolAgent.yaml"))
                        .WithDescription("Answer questions about how the menu uses the tool.")
                        .WithPlugin(plugin)
                        .BuildAsync());

            var parrotAgent =
                Track(
                    await new AgentBuilder()
                        .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                        .FromTemplate(EmbeddedResource.Read("Agents.ParrotAgent.yaml"))
                        .BuildAsync());

            var toolAgent =
                Track(
                    await new AgentBuilder()
                        .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                        .FromTemplate(EmbeddedResource.Read("Agents.ToolAgent.yaml"))
                        .WithPlugin(parrotAgent.AsPlugin())
                        .WithPlugin(menuAgent.AsPlugin())
                        .BuildAsync());

            var messages = new string[]
            {
                "What's on the menu?",
                "Can you talk like pirate?",
                "Thank you",
            };

            thread = await toolAgent.NewThreadAsync();
            foreach (var response in messages.Select(m => thread.InvokeAsync(toolAgent, m)))
            {
                await foreach (var message in response)
                {
                    Console.WriteLine($"[{message.Id}]");
                    Console.WriteLine($"# {message.Role}: {message.Content}");
                }
            }
        }
        finally
        {
            // Clean-up (storage costs $)
            await Task.WhenAll(
                thread?.DeleteAsync() ?? Task.CompletedTask,
                Task.WhenAll(s_agents.Select(a => a.DeleteAsync())));
        }
    }

    private static IAgent Track(IAgent agent)
    {
        s_agents.Add(agent);

        return agent;
    }
}