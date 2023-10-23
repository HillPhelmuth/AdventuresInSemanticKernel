// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using SkPluginLibrary.Plugins;
using System.Globalization;

// ReSharper disable once InconsistentNaming
namespace SkPluginLibrary.Examples;

public static class Example03_Variables
{
    private static readonly ILoggerFactory s_loggerFactory = ConsoleLogger.LoggerFactory;

    public static async Task RunAsync()
    {
        Console.WriteLine("======== Variables ========");

        IKernel kernel = new KernelBuilder().WithLoggerFactory(s_loggerFactory).Build();
        var textFunctions = kernel.ImportFunctions(new StaticTextPlugin(), "text");

        var variables = new ContextVariables("Today is: ");
        variables.Set("day", DateTimeOffset.Now.ToString("dddd", CultureInfo.CurrentCulture));

        KernelResult result = await kernel.RunAsync(variables,
            textFunctions["AppendDay"],
            textFunctions["Uppercase"]);

        Console.WriteLine(result.GetValue<string>());
    }
}