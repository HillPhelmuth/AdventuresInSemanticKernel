// Copyright (c) Microsoft. All rights reserved.

using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

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

    public static async Task ObservabilityWithFiltersAsync()
    {
        // Create a kernel with OpenAI chat completion
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(
                modelId: TestConfiguration.OpenAI.Gpt35ModelId,
                apiKey: TestConfiguration.OpenAI.ApiKey);

        kernelBuilder.Plugins.AddFromType<TimeInformation>();

        // Add filter using DI
        kernelBuilder.Services.AddSingleton<IFunctionInvocationFilter, MyFunctionFilter>();

        Kernel kernel = kernelBuilder.Build();

        // Add filter without DI
        kernel.PromptRenderFilters.Add(new MyPromptFilter());

        // Invoke the kernel with a prompt and allow the AI to automatically invoke functions
        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
        Console.WriteLine(await kernel.InvokePromptAsync("How many days until Christmas? Explain your thinking.", new(settings)));
    }

    /// <summary>
    /// Shows how to observe the execution of a <see cref="KernelPlugin"/> instance with hooks.
    /// </summary>
   
    [Obsolete("Events are deprecated in favor of filters.")]
    public static async Task ObservabilityWithHooksAsync()
    {
        // Create a kernel with OpenAI chat completion
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(
                modelId: TestConfiguration.OpenAI.Gpt35ModelId,
                apiKey: TestConfiguration.OpenAI.ApiKey);

        kernelBuilder.Plugins.AddFromType<TimeInformation>();

        Kernel kernel = kernelBuilder.Build();

        // Handler which is called before a function is invoked
        void MyInvokingHandler(object? sender, FunctionInvokingEventArgs e)
        {
            Console.WriteLine($"Invoking {e.Function.Name}");
        }

        // Handler which is called before a prompt is rendered
        void MyRenderingHandler(object? sender, PromptRenderingEventArgs e)
        {
            Console.WriteLine($"Rendering prompt for {e.Function.Name}");
        }

        // Handler which is called after a prompt is rendered
        void MyRenderedHandler(object? sender, PromptRenderedEventArgs e)
        {
            Console.WriteLine($"Rendered prompt: {e.RenderedPrompt}");
        }

        // Handler which is called after a function is invoked
        void MyInvokedHandler(object? sender, FunctionInvokedEventArgs e)
        {
            if (e.Result.Metadata is not null && e.Result.Metadata.ContainsKey("Usage"))
            {
                Console.WriteLine("Token usage: {0}", e.Result.Metadata?["Usage"]?.AsJson());
            }
        }

        // Add the handlers to the kernel
        kernel.FunctionInvoking += MyInvokingHandler;
        kernel.PromptRendering += MyRenderingHandler;
        kernel.PromptRendered += MyRenderedHandler;
        kernel.FunctionInvoked += MyInvokedHandler;

        // Invoke the kernel with a prompt and allow the AI to automatically invoke functions
        OpenAIPromptExecutionSettings settings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
        Console.WriteLine(await kernel.InvokePromptAsync("How many days until Christmas? Explain your thinking.", new(settings)));
    }

    /// <summary>
    /// A plugin that returns the current time.
    /// </summary>
    private sealed class TimeInformation
    {
        [KernelFunction]
        [Description("Retrieves the current time in UTC.")]
        public string GetCurrentUtcTime() => DateTime.UtcNow.ToString("R");
    }

    /// <summary>
    /// Function filter for observability.
    /// </summary>
    private sealed class MyFunctionFilter() : IFunctionInvocationFilter
    {

        public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            Console.WriteLine($"Invoking {context.Function.Name}");

            await next(context);

            var metadata = context.Result?.Metadata;

            if (metadata is not null && metadata.ContainsKey("Usage"))
            {
                Console.WriteLine($"Token usage: {metadata["Usage"]?.AsJson()}");
            }
        }
    }

    /// <summary>
    /// Prompt filter for observability.
    /// </summary>
    private sealed class MyPromptFilter : IPromptRenderFilter
    {

        public async Task OnPromptRenderAsync(PromptRenderContext context, Func<PromptRenderContext, Task> next)
        {
            Console.WriteLine($"Rendering prompt for {context.Function.Name}");

            await next(context);

            Console.WriteLine($"Rendered prompt: {context.RenderedPrompt}");
        }
    }
}