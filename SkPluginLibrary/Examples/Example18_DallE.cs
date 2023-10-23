﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.AI.ImageGeneration;

namespace SkPluginLibrary.Examples;

/**
 * The following example shows how to use Semantic Kernel with OpenAI Dall-E 2 to create images
 */

// ReSharper disable once InconsistentNaming
public static class Example18_DallE
{
    public static async Task RunAsync()
    {
        await OpenAIDallEAsync();
    }

    private static async Task OpenAIDallEAsync()
    {
        Console.WriteLine("======== OpenAI Dall-E 2 Image Generation ========");

        IKernel kernel = new KernelBuilder()
            .WithLoggerFactory(ConsoleLogger.LoggerFactory)
            // Add your image generation service
            .WithOpenAIImageGenerationService(TestConfiguration.OpenAI.ApiKey)
            // Add your chat completion service 
            .WithOpenAIChatCompletionService(TestConfiguration.OpenAI.ChatModelId, TestConfiguration.OpenAI.ApiKey)
            .Build();

        IImageGeneration dallE = kernel.GetService<IImageGeneration>();

        var imageDescription = "A cute baby sea otter";
        var image = await dallE.GenerateImageAsync(imageDescription, 256, 256);

        Console.WriteLine(imageDescription);
        Console.WriteLine("Image URL: " + image);

        /* Output:

    A cute baby sea otter
    Image URL: https://oaidalleapiprodscus.blob.core.windows.net/private/....

    */

        Console.WriteLine("======== Chat with images ========");

        IChatCompletion chatGPT = kernel.GetService<IChatCompletion>();
        var chatHistory = chatGPT.CreateNewChat(
            "You're chatting with a user. Instead of replying directly to the user" +
            " provide the description of an image that expresses what you want to say." +
            " The user won't see your message, they will see only the image. The system " +
            " generates an image using your description, so it's important you describe the image with details.");

        var msg = "Hi, I'm from Tokyo, where are you from?";
        chatHistory.AddUserMessage(msg);
        Console.WriteLine("User: " + msg);

        string reply = await chatGPT.GenerateMessageAsync(chatHistory);
        chatHistory.AddAssistantMessage(reply);
        image = await dallE.GenerateImageAsync(reply, 256, 256);
        Console.WriteLine("Bot: " + image);
        Console.WriteLine("Img description: " + reply);

        msg = "Oh, wow. Not sure where that is, could you provide more details?";
        chatHistory.AddUserMessage(msg);
        Console.WriteLine("User: " + msg);

        reply = await chatGPT.GenerateMessageAsync(chatHistory);
        chatHistory.AddAssistantMessage(reply);
        image = await dallE.GenerateImageAsync(reply, 256, 256);
        Console.WriteLine("Bot: " + image);
        Console.WriteLine("Img description: " + reply);

        /* Output:

    User: Hi, I'm from Tokyo, where are you from?
    Bot: https://oaidalleapiprodscus.blob.core.windows.net/private/...
    Img description: [An image of a globe with a pin dropped on a location in the middle of the ocean]

    User: Oh, wow. Not sure where that is, could you provide more details?
    Bot: https://oaidalleapiprodscus.blob.core.windows.net/private/...
    Img description: [An image of a map zooming in on the pin location, revealing a small island with a palm tree on it]

    */
    }


}