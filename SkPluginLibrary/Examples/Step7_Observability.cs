// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.DependencyInjection;

namespace SkPluginLibrary.Examples;

public sealed class Step7_Observability
{
    /// <summary>
    /// Shows how to observe the execution of a <see cref="KernelPlugin"/> instance with filters.
    /// </summary>
    public async Task ObservabilityWithFiltersAsync()
    {
        // Create a kernel with OpenAI chat completion
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddOpenAIChatCompletion(
                modelId: TestConfiguration.OpenAI.Gpt4MiniModelId,
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
    private sealed class MyFunctionFilter : IFunctionInvocationFilter
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
