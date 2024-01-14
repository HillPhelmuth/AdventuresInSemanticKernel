﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Experimental.Agents;

// ReSharper disable once InconsistentNaming
namespace SkPluginLibrary.Examples;

/// <summary>
/// Showcase hiearchical Open AI Agent interactions using semantic kernel.
/// </summary>
public static class Example73_AgentAuthoring
{
    /// <summary>
    /// Specific model is required that supports agents and parallel function calling.
    /// Currently this is limited to Open AI hosted services.
    /// </summary>
    private const string OpenAIFunctionEnabledModel = "gpt-4-1106-preview";

    // Track agents for clean-up
    private static readonly List<IAgent> s_agents = new();

    /// <summary>
    /// Show how to combine coordinate multiple agents.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("======== Example73_AgentAuthoring ========");

        if (TestConfiguration.OpenAI.ApiKey == null)
        {
            Console.WriteLine("OpenAI apiKey not found. Skipping example.");
            return;
        }

        // Run demo by invoking agent directly
        await RunAgentAsync();

        // Run demo by invoking agent as a plugin
        await RunAsPluginAsync();
    }

    private static async Task RunAgentAsync()
    {
        try
        {
            // Initialize the agent with tools
            IAgent articleGenerator = await CreateArticleGeneratorAsync();

            // "Stream" messages as they become available
            await foreach (IChatMessage message in articleGenerator.InvokeAsync("Thai food is the best in the world"))
            {
                Console.WriteLine($"[{message.Id}]");
                Console.WriteLine($"# {message.Role}: {message.Content}");
            }
        }
        finally
        {
            await Task.WhenAll(s_agents.Select(a => a.DeleteAsync()));
        }
    }

    private static async Task RunAsPluginAsync()
    {
        try
        {
            // Initialize the agent with tools
            IAgent articleGenerator = await CreateArticleGeneratorAsync();

            // Invoke as a plugin function
            string response = await articleGenerator.AsPlugin().InvokeAsync("Thai food is the best in the world");

            // Display final result
            Console.WriteLine(response);
        }
        finally
        {
            await Task.WhenAll(s_agents.Select(a => a.DeleteAsync()));
        }
    }

    private static async Task<IAgent> CreateArticleGeneratorAsync()
    {
        // Initialize the outline agent
        var outlineGenerator = await CreateOutlineGeneratorAsync();
        // Initialize the research agent
        var sectionGenerator = await CreateResearchGeneratorAsync();

        // Initialize agent so that it may be automatically deleted.
        return
            Track(
                await new AgentBuilder()
                    .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                    .WithInstructions("You write concise opinionated articles that are published online.  Use an outline to generate an article with one section of prose for each top-level outline element.  Each section is based on research with a maximum of 120 words.")
                    .WithName("Article Author")
                    .WithDescription("Author an article on a given topic.")
                    .WithPlugin(outlineGenerator.AsPlugin())
                    .WithPlugin(sectionGenerator.AsPlugin())
                    .BuildAsync());
    }

    private static async Task<IAgent> CreateOutlineGeneratorAsync()
    {
        // Initialize agent so that it may be automatically deleted.
        return
            Track(
                await new AgentBuilder()
                    .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                    .WithInstructions("Produce an single-level outline (no child elements) based on the given topic with at most 3 sections.")
                    .WithName("Outline Generator")
                    .WithDescription("Generate an outline.")
                    .BuildAsync());
    }

    private async static Task<IAgent> CreateResearchGeneratorAsync()
    {
        // Initialize agent so that it may be automatically deleted.
        return
            Track(
                await new AgentBuilder()
                    .WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey)
                    .WithInstructions("Provide insightful research that supports the given topic based on your knowledge of the outline topic.")
                    .WithName("Researcher")
                    .WithDescription("Author research summary.")
                    .BuildAsync());
    }

    private static IAgent Track(IAgent agent)
    {
        s_agents.Add(agent);

        return agent;
    }
}