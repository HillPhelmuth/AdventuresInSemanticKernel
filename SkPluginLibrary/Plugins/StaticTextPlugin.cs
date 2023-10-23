// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace SkPluginLibrary.Plugins;

[SuppressMessage("Design", "CA1052:Type is a static holder type but is neither static nor NotInheritable",
    Justification = "Static classes are not currently supported by the semantic kernel.")]
public class StaticTextPlugin
{
    [SKFunction, Description("Change all string chars to uppercase")]
    public static string Uppercase([Description("Text to uppercase")] string input)
    {
        return input.ToUpperInvariant();
    }

    [SKFunction, Description("Append the day variable")]

    public static string AppendDay(string input, [SKName("day"), Description("Value of the day to append")] string day, SKContext context)
    {
        return input + context.Variables["day"];
    }
}
