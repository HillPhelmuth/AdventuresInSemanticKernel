﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.TemplateEngine.Basic;

// ReSharper disable once InconsistentNaming
namespace SkPluginLibrary.Examples;

public static class Example56_TemplateNativeFunctionsWithMultipleArguments
{
    /// <summary>
    /// Show how to invoke a Native Function written in C# with multiple arguments
    /// from a Semantic Function written in natural language
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("======== TemplateNativeFunctionsWithMultipleArguments ========");

        IKernel kernel = new KernelBuilder()
     .WithLoggerFactory(ConsoleLogger.LoggerFactory)
     .WithOpenAIChatCompletionService(TestConfiguration.OpenAI.ModelId, TestConfiguration.OpenAI.ApiKey, alsoAsTextCompletion: true)
     .Build();

        var variableName = "word2";
        var variableValue = " Potter";
        var context = kernel.CreateNewContext();
        context.Variables[variableName] = variableValue;

        // Load native plugin into the kernel function collection, sharing its functions with prompt templates
        // Functions loaded here are available as "text.*"
        kernel.ImportFunctions(new TextPlugin(), "text");

        // Semantic Function invoking text.Concat native function with named arguments input and input2 where input is a string and input2 is set to a variable from context called word2.
        const string FunctionDefinition = @"
 Write a haiku about the following: {{text.Concat input='Harry' input2=$word2}}
";

        // This allows to see the prompt before it's sent to OpenAI
        Console.WriteLine("--- Rendered Prompt");
        var promptRenderer = new BasicPromptTemplateEngine();
        var renderedPrompt = await promptRenderer.RenderAsync(FunctionDefinition, context);
        Console.WriteLine(renderedPrompt);

        // Run the prompt / semantic function
        var haiku = kernel.CreateSemanticFunction(FunctionDefinition, requestSettings: new OpenAIRequestSettings() { MaxTokens = 100 });

        // Show the result
        Console.WriteLine("--- Semantic Function result");
        var result = await kernel.RunAsync(context.Variables, haiku);
        Console.WriteLine(result.GetValue<string>());

        /* OUTPUT:

--- Rendered Prompt

Write a haiku about the following: Harry Potter

--- Semantic Function result
A boy with a scar,
Wizarding world he explores,
Harry Potter's tale.
     */
    }
}