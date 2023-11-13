﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Microsoft.SemanticKernel.Orchestration;

// ReSharper disable once InconsistentNaming
namespace SkPluginLibrary.Examples;

public static class Example21_ChatGptPlugins
{
    public static async Task RunAsync()
    {
        await RunChatGptPluginAsync();
    }

    private static async Task RunChatGptPluginAsync()
    {
        var kernel = new KernelBuilder().WithLoggerFactory(ConsoleLogger.LoggerFactory).Build();

        //This HTTP client is optional. SK will fallback to a default internal one if omitted.
        using HttpClient httpClient = new();

        //Import a ChatGPT plugin via URI
        var plugin = await kernel.ImportOpenApiPluginFunctionsAsync("<plugin name>", new Uri("<chatGPT-plugin>"), new OpenApiFunctionExecutionParameters(httpClient));

        //Add arguments for required parameters, arguments for optional ones can be skipped.
        var contextVariables = new ContextVariables();
        contextVariables.Set("<parameter-name>", "<parameter-value>");

        //Run
        var kernelResult = await kernel.RunAsync(contextVariables, plugin["<function-name>"]);

        var result = kernelResult.GetValue<RestApiOperationResponse>();

        Console.WriteLine("Function execution result: {0}", result?.Content?.ToString());
        Console.ReadLine();

        //--------------- Example of using Klarna ChatGPT plugin ------------------------

        //var kernel = new KernelBuilder().WithLoggerFactory(ConsoleLogger.LoggerFactory).Build();

        //var plugin = await kernel.ImportOpenApiPluginFunctionsAsync("Klarna", new Uri("https://www.klarna.com/.well-known/ai-plugin.json"));

        //var contextVariables = new ContextVariables();
        //contextVariables.Set("q", "Laptop");      // A precise query that matches one very small category or product that needs to be searched for to find the products the user is looking for. If the user explicitly stated what they want, use that as a query. The query is as specific as possible to the product name or category mentioned by the user in its singular form, and don't contain any clarifiers like latest, newest, cheapest, budget, premium, expensive or similar. The query is always taken from the latest topic, if there is a new topic a new query is started.
        //contextVariables.Set("size", "3");        // number of products returned
        //contextVariables.Set("budget", "200");    // maximum price of the matching product in local currency, filters results
        //contextVariables.Set("countryCode", "US");// ISO 3166 country code with 2 characters based on the user location. Currently, only US, GB, DE, SE and DK are supported.

        //var kernelResult = await kernel.RunAsync(contextVariables, plugin["productsUsingGET"]);

        //var result = kernelResult.GetValue<RestApiOperationResponse>();

        //Console.WriteLine("Function execution result: {0}", result?.Content?.ToString());
        //Console.ReadLine();
    }
}