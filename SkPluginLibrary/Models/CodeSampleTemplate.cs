namespace SkPluginLibrary.Models;

public class CodeSampleTemplate
{
    public static string GetCodeFromTemplate(string className, string fileCode)
    {
        var text = $$"""
                //using statements for Semantic Kernel will be added dynamically. Others you need to use can be added here.

                public static class Program
                {
                    public static async Task Main()
                    {
                        await {{className}}.RunAsync();
                    }

                }
                {{fileCode}}
                """;
        return text;
    }
   
    public const string PragmaCodes = "CA1050,CA1707,CA2007,VSTHRD111,CS1591,SKEXP0001,SKEXP0010,SKEXP0020,SKEXP0030,SKEXP0040,SKEXP0050,SKEXP0060,SKEXP0070,SKEXP0090,SKEXP0100,SKEXP0101,SKEXP0110";
    public const string DisableWarning = $"#pragma warning disable {PragmaCodes}";
    public const string UsingStatements = """
                                          using System.Globalization;
                                          using SkPluginLibrary.Plugins;
                                          using System.Net;
                                          using Microsoft.Extensions.DependencyInjection;
                                          using Microsoft.Extensions.Http.Resilience;
                                          using Microsoft.Extensions.Logging;
                                          using StackExchange.Redis;
                                          using System.Runtime.CompilerServices;
                                          using System.Net.Http.Headers;
                                          using System.Net.Mime;
                                          using System.Text;
                                          using System.Text.Json;
                                          using System.Text.Json.Serialization;
                                          using SkPluginLibrary.Resources;
                                          using Microsoft.Identity.Client;
                                          using System.Numerics.Tensors;
                                          using System.Runtime.InteropServices;
                                          using Azure.Identity;
                                          using System.Diagnostics;
                                          using Azure.AI.OpenAI;
                                          using Azure.Core;
                                          using Azure.Core.Pipeline;
                                          using Microsoft.DeepDev;
                                          using Microsoft.ML.Tokenizers;
                                          using SharpToken;
                                          using static Microsoft.SemanticKernel.Text.TextChunker;
                                          using System.Text.RegularExpressions;
                                          using System.ComponentModel;
                                          using System.Diagnostics.CodeAnalysis;
                                          using SkPluginLibrary.Examples.DictionaryPlugin;
                                          using SkPluginLibrary.Models;
                                          using SkPluginLibrary.Models.Helpers;
                                          using SkPluginLibrary.Models.Hooks;
                                          using System;
                                          using System.Collections.Generic;
                                          using System.IO;
                                          using System.Linq;
                                          using System.Net.Http;
                                          using System.Threading;
                                          using System.Threading.Tasks;
                                          using JsonSerializer = System.Text.Json.JsonSerializer;
                                          using static SkPluginLibrary.Examples.SamplesHelper;
                                          using Xunit;
                                          using Xunit.Abstractions;
                                          """;
}