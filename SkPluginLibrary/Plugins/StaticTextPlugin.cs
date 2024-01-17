﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace SkPluginLibrary.Plugins;

public sealed class StaticTextPlugin
{
    [KernelFunction, Description("Change all string chars to uppercase")]
    public static string Uppercase([Description("Text to uppercase")] string input) =>
        input.ToUpperInvariant();

    [KernelFunction, Description("Append the day variable")]
    public static string AppendDay(
        [Description("Text to append to")] string input,
        [Description("Value of the day to append")] string day) =>
        input + day;
}
