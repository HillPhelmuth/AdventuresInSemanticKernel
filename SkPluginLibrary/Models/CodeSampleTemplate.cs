namespace SkPluginLibrary.Models;

public class CodeSampleTemplate
{
    public static string GetCodeFromTemplate(string className, string fileCode)
    {
        var text = $$"""
                //using statements for Agent Framework will be added dynamically. Others you need to use can be added here.

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
   
    public const string PragmaCodes = "CA1050,CA1707,CA2007,VSTHRD111,CS1591,MAAI001,MEAI001,OPENAI001";
    public const string DisableWarning = $"#pragma warning disable {PragmaCodes}";
    public const string UsingStatements = """
                                          using System;
                                          using System.Collections.Generic;
                                          using System.ComponentModel;
                                          using System.IO;
                                          using System.Linq;
                                          using System.Net.Http;
                                          using System.Text;
                                          using System.Text.Json;
                                          using System.Threading;
                                          using System.Threading.Tasks;
                                          using Microsoft.Agents.AI;
                                          using Microsoft.Agents.AI.Workflows;
                                          using Microsoft.Extensions.AI;
                                          using SkPluginLibrary;
                                          using SkPluginLibrary.Models;
                                          using SkPluginLibrary.Services;
                                          using SkPluginLibrary.Plugins;
                                          using SkPluginLibrary.Plugins.NativePlugins;
                                          using SkPluginLibrary.Examples;
                                          """;
}
