// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPluginLibrary.Models.Hooks;

namespace SkPluginLibrary.Examples;

public static class Step7_Observability
{
    /// <summary>
    /// Shows different ways observe the execution of a <see cref="KernelPlugin"/> instances.
    /// </summary>
    public static async Task RunAsync()
    {
        // Create a kernel with OpenAI chat completion
        Console.WriteLine("------------------\nObservability with hooks.\n--------------------\n NOTE: This form of observability is obsolete.\n");
        await ObservabilityWithHooksAsync();
        Console.WriteLine("------------------\nObservability with new Filters abstractions.\n--------------------\n");
        await ObservabilityWithFiltersAsync();
    }

    private static async Task ObservabilityWithHooksAsync()
    {
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: TestConfiguration.OpenAI.Gpt35ModelId,
            apiKey: TestConfiguration.OpenAI.ApiKey);
        kernelBuilder.Plugins.AddFromType<TimeInformation>();
        Kernel kernel = kernelBuilder.Build();

        // Add the handlers to the kernel
        kernel.FunctionInvoking += MyInvokingHandler;
        kernel.PromptRendered += MyRenderedHandler;
        kernel.FunctionInvoked += MyInvokedHandler;

        // Invoke the kernel with a prompt and allow the AI to automatically invoke functions
        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
        Console.WriteLine(await kernel.InvokePromptAsync("How many days until Christmas? Explain your thinking.", new(settings)));
        return;

        // Handler which is called before a function is invoked
        void MyInvokingHandler(object? sender, FunctionInvokingEventArgs e)
        {
            Console.WriteLine($"Invoking {e.Function.Name}");
        }

        // Handler which is called after a prompt is rendered
        void MyRenderedHandler(object? sender, PromptRenderedEventArgs e)
        {
            Console.WriteLine($"Prompt sent to model: {e.RenderedPrompt}");
        }

        // Handler which is called after a function is invoked
        void MyInvokedHandler(object? sender, FunctionInvokedEventArgs e)
        {
            if (e.Result.Metadata is not null && e.Result.Metadata.ContainsKey("Usage"))
            {
                Console.WriteLine($"Token usage: {e.Result.Metadata?["Usage"]?.AsJson()}");
            }
        }
    }

    public static async Task ObservabilityWithFiltersAsync()
    {
        // Create a kernel with OpenAI chat completion
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(
            modelId: TestConfiguration.OpenAI.Gpt35ModelId,
            apiKey: TestConfiguration.OpenAI.ApiKey);

        kernelBuilder.Plugins.AddFromType<TimeInformation>();

        // Add filter using DI
        
        kernelBuilder.Services.AddSingleton<IFunctionFilter, MyFunctionFilter>();

        Kernel kernel = kernelBuilder.Build();

        // Add filter without DI
        kernel.PromptFilters.Add(new MyPromptFilter());

        // Invoke the kernel with a prompt and allow the AI to automatically invoke functions
        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
        WriteLine(await kernel.InvokePromptAsync("How many days until Christmas? Explain your thinking.", new(settings)));
    }
    private static void WriteLine(object objects)
    {
        Console.WriteLine(objects);
    }
    /// <summary>
    /// A plugin that returns the current time.
    /// </summary>
    public class TimeInformation
    {
        [KernelFunction]
        [Description("Retrieves the current time in UTC.")]
        public string GetCurrentUtcTime() => DateTime.UtcNow.ToString("R");
    }
    private sealed class MyFunctionFilter : IFunctionFilter
    {
       
        public void OnFunctionInvoked(FunctionInvokedContext context)
        {
            var metadata = context.Result.Metadata;

            if (metadata is not null && metadata.ContainsKey("Usage"))
            {
                Console.WriteLine($"Token usage: {metadata["Usage"]?.AsJson()}");
            }
        }

        public void OnFunctionInvoking(FunctionInvokingContext context)
        {
            Console.WriteLine($"Invoking {context.Function.Name}");
        }
    }

    /// <summary>
    /// Prompt filter for observability.
    /// </summary>
    private sealed class MyPromptFilter : IPromptFilter
    {

        public void OnPromptRendered(PromptRenderedContext context)
        {
            Console.WriteLine($"Prompt sent to model: {context.RenderedPrompt}");
        }

        public void OnPromptRendering(PromptRenderingContext context)
        {
            Console.WriteLine($"Rendering prompt for {context.Function.Name}");
        }
    }
}