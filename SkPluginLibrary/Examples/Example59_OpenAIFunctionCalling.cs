﻿// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;
using System.Text.Json;

// This example shows how to use OpenAI's tool calling capability via the chat completions interface.
namespace SkPluginLibrary.Examples;

/// <summary>
/// Example class for OpenAI function calling.
/// </summary>
public static class Example59_OpenAIFunctionCalling
{
    /// <summary>
    /// Runs the OpenAI function calling example asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunAsync()
    {
        // Create kernel.
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(TestConfiguration.OpenAI.Gpt4ModelId, TestConfiguration.OpenAI.ApiKey);
        builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
        Kernel kernel = builder.Build();

        // Add a plugin with some helper functions we want to allow the model to utilize.
        kernel.Plugins.Add(KernelPluginFactory.CreateFromFunctions("HelperFunctions", new[]
        {
            kernel.CreateFunctionFromMethod(() => DateTime.UtcNow.ToString("R"), "GetCurrentUtcTime"),
            kernel.CreateFunctionFromMethod((string cityName) =>
                cityName switch
                {
                    "Boston" => "61 and rainy",
                    "London" => "55 and cloudy",
                    "Miami" => "80 and sunny",
                    "Paris" => "60 and rainy",
                    "Tokyo" => "50 and sunny",
                    "Sydney" => "75 and sunny",
                    "Tel Aviv" => "80 and sunny",
                    _ => "31 and snowing",
                }, "GetWeatherForCity", "Gets the current weather for the specified city"),
        }));

        await AutomatedWithNonStreaming(kernel);

        await AutomatedWithStreamingPrompt(kernel);

        await RunNonStreamingChatAPIWithManualFunctionCallingAsync(kernel);


        await AutomaticStreamingFunctionCall(kernel);
    }

    /// <summary>
    /// Performs automated function calling with a streaming chat.
    /// </summary>
    /// <param name="kernel">The kernel instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task AutomaticStreamingFunctionCall(Kernel kernel)
    {
        Console.WriteLine("======== Example 4: Use automated function calling with a streaming chat ========");
        {
            OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();

            while (true)
            {
                Console.Write("Question: ");
                string question = Console.ReadLine() ?? string.Empty;
                if (question == "done")
                {
                    break;
                }

                chatHistory.AddUserMessage(question);
                StringBuilder sb = new();
                await foreach (var update in chat.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel))
                {
                    if (update.Content is not null)
                    {
                        Console.Write(update.Content);
                        sb.Append(update.Content);
                    }
                }
                chatHistory.AddAssistantMessage(sb.ToString());
                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Performs manual function calling with a non-streaming prompt.
    /// </summary>
    /// <param name="kernel">The kernel instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunNonStreamingChatAPIWithManualFunctionCallingAsync(Kernel kernel)
    {
        Console.WriteLine("Manual function calling with a non-streaming prompt.");

       
        IChatCompletionService chat = kernel.GetRequiredService<IChatCompletionService>();

        // Configure the chat service to enable manual function calling
        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions };

        // Create chat history with the initial user message
        ChatHistory chatHistory = new();
        chatHistory.AddUserMessage("Given the current time of day and weather, what is the likely color of the sky in Boston?");

        while (true)
        {
            // Start or continue chat based on the chat history
            ChatMessageContent result = await chat.GetChatMessageContentAsync(chatHistory, settings, kernel);
            if (result.Content is not null)
            {
                Console.Write(result.Content);
            }

            // Get function calls from the chat message content and quit the chat loop if no function calls are found.
            IEnumerable<FunctionCallContent> functionCalls = FunctionCallContent.GetFunctionCalls(result);
            if (!functionCalls.Any())
            {
                break;
            }

            // Preserving the original chat message content with function calls in the chat history.
            chatHistory.Add(result);

            // Iterating over the requested function calls and invoking them
            foreach (FunctionCallContent functionCall in functionCalls)
            {
                try
                {
                    // Invoking the function
                    FunctionResultContent resultContent = await functionCall.InvokeAsync(kernel);

                    // Adding the function result to the chat history
                    chatHistory.Add(resultContent.ToChatMessage());
                }
                catch (Exception ex)
                {
                    // Adding function exception to the chat history.
                    chatHistory.Add(new FunctionResultContent(functionCall, ex).ToChatMessage());
                    // or
                    //chatHistory.Add(new FunctionResultContent(functionCall, "Error details that LLM can reason about.").ToChatMessage());
                }
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    /// Performs automated function calling with a streaming prompt.
    /// </summary>
    /// <param name="kernel">The kernel instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task AutomatedWithStreamingPrompt(Kernel kernel)
    {
        Console.WriteLine("======== Example 2: Use automated function calling with a streaming prompt ========");
        {
            OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
            await foreach (var update in kernel.InvokePromptStreamingAsync("Given the current time of day and weather, what is the likely color of the sky in Boston?", new KernelArguments(settings)))
            {
                Console.Write(update);
            }
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Performs automated function calling with a non-streaming prompt.
    /// </summary>
    /// <param name="kernel">The kernel instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task AutomatedWithNonStreaming(Kernel kernel)
    {
        Console.WriteLine("======== Example 1: Use automated function calling with a non-streaming prompt ========");
        {
            OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
            Console.WriteLine(await kernel.InvokePromptAsync("Given the current time of day and weather, what is the likely color of the sky in Boston?", new(settings)));
            Console.WriteLine();
        }
    }
}
