namespace SkPluginLibrary.Models;

public class CodeSampleTemplate
{
    public static string GetCodeFromTemplate(string className, string fileCode)
    {
        var text = $$""""
                //using statements for Semantic Kernel will be added dynamically. Others you need to use can be added here.

                public static class Program
                {
                    public static async Task Main()
                    {
                        await {{className}}.RunAsync();
                    }

                }
                {{fileCode}}
                """";
        return text;
    }

    public const string UsingStatements = @"using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Orchestration;
using System.Globalization;
using SkPluginLibrary.Plugins;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Bing;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI;
using Microsoft.SemanticKernel.TemplateEngine.Basic;
using Microsoft.SemanticKernel.Plugins.Web.Google;
using System.Net;
using Microsoft.SemanticKernel.Http;
using Microsoft.SemanticKernel.Reliability.Basic;
using Polly;
using System.Diagnostics;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextEmbedding;
using Microsoft.SemanticKernel.Diagnostics;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Planners;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Connectors.Memory.AzureCognitiveSearch;
using Microsoft.SemanticKernel.Connectors.Memory.Chroma;
using Microsoft.SemanticKernel.Connectors.Memory.Kusto;
using Microsoft.SemanticKernel.Connectors.Memory.Pinecone;
using Microsoft.SemanticKernel.Connectors.Memory.Postgres;
using Microsoft.SemanticKernel.Connectors.Memory.Qdrant;
using Microsoft.SemanticKernel.Connectors.Memory.Redis;
using Microsoft.SemanticKernel.Connectors.Memory.Sqlite;
using Microsoft.SemanticKernel.Connectors.Memory.Weaviate;
using Npgsql;
using Pgvector.Npgsql;
using StackExchange.Redis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.SemanticKernel.AI;
using Microsoft.SemanticKernel.AI.TextCompletion;
using Microsoft.SemanticKernel.AI.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletion;
using Microsoft.SemanticKernel.AI.ImageGeneration;
using Microsoft.SemanticKernel.Functions.OpenAPI.Extensions;
using Microsoft.SemanticKernel.Functions.OpenAPI.Model;
using Microsoft.SemanticKernel.Functions.OpenAPI.Authentication;
using Microsoft.SemanticKernel.Functions.OpenAPI.Plugins;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using SkPluginLibrary.Resources;
using System.ComponentModel;
using System.Xml;
using System.Xml.XPath;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.TextCompletion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Services;
using Microsoft.SemanticKernel.TemplateEngine;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.Reliability.Polly;
using Polly.Retry;
using NCalcPlugins;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.ChatCompletionWithData;
using Microsoft.SemanticKernel.Text;
using System.IO;
using Microsoft.DeepDev;
using Microsoft.ML.Tokenizers;
using SharpToken;
using static Microsoft.SemanticKernel.Text.TextChunker;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.Events;
using Microsoft.SemanticKernel.Connectors.AI.OpenAI.AzureSdk;
using SkPluginLibrary.Models;
using SkPluginLibrary.Models.Helpers;
using SkPluginLibrary.Plugins;
using System.Net.Http;
using Microsoft.SemanticKernel.TemplateEngine.Handlebars;";
}