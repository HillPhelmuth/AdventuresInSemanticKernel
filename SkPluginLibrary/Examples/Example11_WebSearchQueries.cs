﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel.Plugins.Web;

namespace SkPluginLibrary.Examples;

public static class Example11_WebSearchQueries
{
    public static async Task RunAsync()
    {
        Console.WriteLine("======== WebSearchQueries ========");

        Kernel kernel = new();

        // Load native plugins
        var bing = kernel.ImportPluginFromType<SearchUrlPlugin>("search");

        // Run
        var ask = "What's the tallest building in Europe?";
        var result = await kernel.InvokeAsync(bing["BingSearchUrl"], new() { ["input"] = ask });

        Console.WriteLine(ask + "\n");
        Console.WriteLine(result.GetValue<string>());
    }
}