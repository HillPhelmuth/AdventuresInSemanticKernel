﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Web;

// ReSharper disable once InconsistentNaming
namespace SkPluginLibrary.Examples;

public static class Example11_WebSearchQueries
{
    public static async Task RunAsync()
    {
        Console.WriteLine("======== WebSearchQueries ========");

        IKernel kernel = Kernel.Builder.WithLoggerFactory(ConsoleLogger.LoggerFactory).Build();

        // Load native plugins
        var plugin = new SearchUrlPlugin();
        var bing = kernel.ImportFunctions(plugin, "search");

        // Run
        var ask = "What's the tallest building in Europe?";
        var result = await kernel.RunAsync(
            ask,
            bing["BingSearchUrl"]
        );

        Console.WriteLine(ask + "\n");
        Console.WriteLine(result.GetValue<string>());
    }
}