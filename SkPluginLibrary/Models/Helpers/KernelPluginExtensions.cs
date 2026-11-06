using System.Text.Json;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;
using SkPluginLibrary.Services;

namespace SkPluginLibrary.Models.Helpers
{
    /// <summary>Helpers for working with tool groups (<see cref="ToolPlugin"/>) and tools (<see cref="AIFunction"/>).</summary>
    public static class KernelPluginExtensions
    {
        public static Dictionary<string, AIFunction> ToDictionary(this ToolPlugin plugin) =>
            plugin.Functions.ToDictionary(x => x.Name, x => x);

        public static ToolPlugin ToPlugin(this Dictionary<string, AIFunction> functions, string pluginName) =>
            new(pluginName, functions.Values);

        /// <summary>Invokes a tool and returns its result as text.</summary>
        public static async Task<string> InvokeToStringAsync(this AIFunction function, Dictionary<string, object?>? variables = null,
            CancellationToken cancellationToken = default)
        {
            var args = new AIFunctionArguments();
            foreach (var item in variables ?? []) args[item.Key] = item.Value;
            var value = await function.InvokeAsync(args, cancellationToken);
            return ResultToString(value);
        }

        public static string ResultToString(object? value) => value switch
        {
            null => "",
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() ?? "",
            JsonElement e => e.GetRawText(),
            ChatMessage message => message.Text,
            _ => JsonSerializer.Serialize(value)
        };

        public static bool IsAsyncEnumerable(object? obj)
        {
            if (obj == null) return false;
            var type = obj.GetType();
            return type.GetInterfaces().Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        }

        /// <summary>Loads every YAML prompt in a plugin directory as a tool.</summary>
        public static ToolPlugin CreatePluginFromPromptDirectoryYaml(string pluginName, Func<IChatClient>? clientFactory = null)
        {
            var files = Directory.GetFiles(Path.Combine(RepoFiles.PathToYamlPlugins, pluginName), "*.yaml");
            var functions = new List<AIFunction>();
            foreach (var file in files)
            {
                try
                {
                    functions.Add(YamlPromptFunction.FromYaml(File.ReadAllText(file), clientFactory));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR DESERIALIZING YAML\n\nFunction: {Path.GetFileName(file)}\n\n{ex}");
                }
            }
            return new ToolPlugin(pluginName, functions);
        }
    }
}
