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
   
    public const string PragmaCodes = "CA1050,CA1707,CA2007,VSTHRD111,CS1591,SKEXP0001,SKEXP0004,SKEXP0042,SKEXP0002,SKEXP0003,SKEXP0010,SKEXP0011,SKEXP0012,SKEXP0020,SKEXP0021,SKEXP0022,SKEXP0023,SKEXP0024,SKEXP0025,SKEXP0026,SKEXP0027,SKEXP0028,SKEXP0029,SKEXP0030,SKEXP0031,SKEXP0032,SKEXP0040,SKEXP0041,SKEXP0050,SKEXP0051,SKEXP0052,SKEXP0053,SKEXP0054,SKEXP0055,SKEXP0060,SKEXP0061,SKEXP0101,SKEXP0102";
    public const string DisableWarning = $"#pragma warning disable {PragmaCodes}";
    public const string UsingStatements = """
                                          using Microsoft.SemanticKernel.Plugins.Core;
                                          using System.Globalization;
                                          using Microsoft.SemanticKernel;
                                          using SkPluginLibrary.Plugins;
                                          using Microsoft.SemanticKernel.Connectors.OpenAI;
                                          using Microsoft.SemanticKernel.Plugins.Web;
                                          using Microsoft.SemanticKernel.Plugins.Web.Bing;
                                          using Microsoft.SemanticKernel.Plugins.Web.Google;
                                          using System.Net;
                                          using Microsoft.Extensions.DependencyInjection;
                                          using Microsoft.Extensions.Http.Resilience;
                                          using Microsoft.Extensions.Logging;
                                          using Microsoft.SemanticKernel.Connectors.AzureAISearch;
                                          using Microsoft.SemanticKernel.Memory;
                                          using Microsoft.SemanticKernel.Connectors.Chroma;
                                          using Microsoft.SemanticKernel.Connectors.Pinecone;
                                          using Microsoft.SemanticKernel.Connectors.Qdrant;
                                          using Microsoft.SemanticKernel.Connectors.Redis;
                                          using Microsoft.SemanticKernel.Connectors.Sqlite;
                                          using Microsoft.SemanticKernel.Connectors.Weaviate;
                                          using Microsoft.SemanticKernel.Plugins.Memory;
                                          using StackExchange.Redis;
                                          using System.Runtime.CompilerServices;
                                          using Microsoft.SemanticKernel.TextGeneration;
                                          using Microsoft.SemanticKernel.ChatCompletion;
                                          using Microsoft.SemanticKernel.TextToImage;
                                          using Microsoft.SemanticKernel.Plugins.OpenApi;
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
                                          using Microsoft.SemanticKernel.Plugins.Grpc;
                                          using System.Diagnostics;
                                          using Microsoft.SemanticKernel.Planning.Handlebars;
                                          using Azure.AI.OpenAI;
                                          using Azure.Core;
                                          using Azure.Core.Pipeline;
                                          using Microsoft.DeepDev;
                                          using Microsoft.ML.Tokenizers;
                                          using Microsoft.SemanticKernel.Text;
                                          using SharpToken;
                                          using static Microsoft.SemanticKernel.Text.TextChunker;
                                          using System.Text.RegularExpressions;
                                          using System.ComponentModel;
                                          using System.Diagnostics.CodeAnalysis;
                                          using Microsoft.SemanticKernel.Services;
                                          using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
                                          using SkPluginLibrary.Examples.DictionaryPlugin;
                                          using Microsoft.SemanticKernel.Planning;
                                          using Microsoft.SemanticKernel.Experimental.Agents;
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
                                          """;
}