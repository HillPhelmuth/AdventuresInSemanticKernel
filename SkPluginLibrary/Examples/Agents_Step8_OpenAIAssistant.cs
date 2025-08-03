// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Assistants;
using OpenAI.Chat;
using System.ClientModel;
using static SkPluginLibrary.Examples.SamplesHelper;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

#pragma warning disable OPENAI001
namespace SkPluginLibrary.Examples;

/// <summary>
/// This example demonstrates that outside of initialization (and cleanup), using
/// <see cref="OpenAIAssistantAgent"/> is no different from <see cref="ChatCompletionAgent"/>
/// even with with a <see cref="KernelPlugin"/>.
/// </summary>
public static class Agents_Step8_OpenAIAssistant
{
    private const string HostName = "Host";
    private const string HostInstructions = "Answer questions about the menu.";

    
    public static async Task RunAsync()
    {
        // Define the agent
        var client = OpenAIAssistantAgent.CreateOpenAIClient(new ApiKeyCredential(TestConfiguration.OpenAI.ApiKey));
        var assistantClient = client.GetAssistantClient();
        var creationOptions = new AssistantCreationOptions() { Instructions = HostInstructions, Name = HostName};
        

        // Initialize plugin and add to the agent's Kernel (same as direct Kernel usage).
        KernelPlugin plugin = KernelPluginFactory.CreateFromType<MenuPlugin>();
        plugin.Select(f => f.ToToolDefinition(plugin.Name)).ToList().ForEach(td => creationOptions.Tools.Add(td));
        var assistant = await assistantClient.CreateAssistantAsync(TestConfiguration.OpenAI.Gpt4MiniModelId,
            creationOptions);
        var agent = new OpenAIAssistantAgent(assistant, assistantClient);
        // Create a chat for agent interaction.
        var thread = new OpenAIAssistantAgentThread(assistantClient);

        // Respond to user input
        try
        {
            await InvokeAgentAsync("Hello");
            await InvokeAgentAsync("What is the special soup?");
            await InvokeAgentAsync("What is the special drink?");
            await InvokeAgentAsync("Thank you");
        }
        finally
        {
            await thread.DeleteAsync();
            await assistantClient.DeleteAssistantAsync(agent.Id);
        }

        // Local function to invoke agent and display the conversation messages.
        async Task InvokeAgentAsync(string input)
        {
            var message = new ChatMessageContent(AuthorRole.User, input);

            Console.WriteLine($"# {AuthorRole.User}: '{input}'");

            await foreach (var content in agent.InvokeAsync(message, thread))
            {
                WriteAgentChatMessage(content);
            }
        }
    }
    private static void WriteAgentChatMessage(ChatMessageContent message)
    {
        // Include ChatMessageContent.AuthorName in output, if present.
        string authorExpression = message.Role == AuthorRole.User ? string.Empty : $" - {message.AuthorName ?? "*"}";
        // Include TextContent (via ChatMessageContent.Content), if present.
        string contentExpression = string.IsNullOrWhiteSpace(message.Content) ? string.Empty : message.Content;
        bool isCode = message.Metadata?.ContainsKey(OpenAIAssistantAgent.CodeInterpreterMetadataKey) ?? false;
        string codeMarker = isCode ? "\n  [CODE]\n" : " ";
        Console.WriteLine($"\n# {message.Role}{authorExpression}:{codeMarker}{contentExpression}");

        // Provide visibility for inner content (that isn't TextContent).
        foreach (KernelContent item in message.Items)
        {
            if (item is AnnotationContent annotation)
            {
                Console.WriteLine($"  [{item.GetType().Name}] {annotation.Quote}: File #{annotation.FileId}");
            }
            else if (item is FileReferenceContent fileReference)
            {
                Console.WriteLine($"  [{item.GetType().Name}] File #{fileReference.FileId}");
            }
            else if (item is ImageContent image)
            {
                Console.WriteLine($"  [{item.GetType().Name}] {image.Uri?.ToString() ?? image.DataUri ?? $"{image.Data?.Length} bytes"}");
            }
            else if (item is FunctionCallContent functionCall)
            {
                Console.WriteLine($"  [{item.GetType().Name}] {functionCall.Id}");
            }
            else if (item is FunctionResultContent functionResult)
            {
                Console.WriteLine($"  [{item.GetType().Name}] {functionResult.CallId} - {functionResult.Result?.AsJson() ?? "*"}");
            }
        }

        if (message.Metadata?.TryGetValue("Usage", out object? usage) ?? false)
        {
            if (usage is RunStepTokenUsage assistantUsage)
            {
                WriteUsage(assistantUsage.TotalTokenCount, assistantUsage.InputTokenCount, assistantUsage.OutputTokenCount);
            }
            else if (usage is ChatTokenUsage chatUsage)
            {
                WriteUsage(chatUsage.TotalTokenCount, chatUsage.InputTokenCount, chatUsage.OutputTokenCount);
            }
        }

        void WriteUsage(int totalTokens, int inputTokens, int outputTokens)
        {
            Console.WriteLine($"  [Usage] Tokens: {totalTokens}, Input: {inputTokens}, Output: {outputTokens}");
        }
    }

    private sealed class MenuPlugin
    {
        public const string CorrelationIdArgument = "correlationId";

        private readonly List<string> _correlationIds = [];

        public IReadOnlyList<string> CorrelationIds => this._correlationIds;

        [KernelFunction, Description("Provides a list of specials from the menu.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Too smart")]
        public string GetSpecials()
        {
            return @"
Special Soup: Clam Chowder
Special Salad: Cobb Salad
Special Drink: Chai Tea
";
        }

        [KernelFunction, Description("Provides the price of the requested menu item.")]
        public string GetItemPrice(
            [Description("The name of the menu item.")]
            string menuItem)
        {
            return "$9.99";
        }
    }
}
