// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace SkPluginLibrary.Plugins;

public class StaticTextPlugin
{
    [KernelFunction, Description("Change all string chars to uppercase")]
    public static string Uppercase([Description("Text to uppercase")] string input)
    {
        return input.ToUpperInvariant();
    }

    [KernelFunction, Description("Append the day variable")]

    public static string AppendDay(string input, [Description("Value of the day to append")] string day, SKContext context)
    {
        return input + context.Variables["day"];
    }
}
