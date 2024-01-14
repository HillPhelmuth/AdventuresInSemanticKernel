using System.Reflection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.OpenApi;

namespace SkPluginLibrary.Models.Helpers
{
    public static class KernelPluginExtensions
    {
        //public static string Result(this FunctionResult result)
        //{
        //    var value = result.GetValue<object>();
        //    if (IsAsyncEnumerable(value)) return $"This is an IAsyncEnumerable Collection: {value?.GetType().FullName}";
        //    return value switch
        //    {
        //        RestApiOperationResponse apiOperationResponse => apiOperationResponse.Content.ToString() ?? "",
        //        string stringResult => stringResult,
        //        _ => JsonSerializer.Serialize(value)
        //    };
        //}
        public static Dictionary<string, KernelFunction> ToDictionary(this KernelPlugin plugin)
        {
            return plugin.ToDictionary(x => x.Name, x => x);
        }
        public static string Result(this FunctionResult result)
        {
            var value = result.GetValue<object>();
            if (IsAsyncEnumerable(value)) return $"This is an IAsyncEnumerable Collection: {value?.GetType().FullName}";
            return value switch
            {
                RestApiOperationResponse apiOperationResponse => apiOperationResponse.Content.ToString() ?? "",
                string stringResult => stringResult,
                _ => JsonSerializer.Serialize(value)
            };
        }

        public static IAsyncEnumerable<TResult> ResultStream<TResult>(this FunctionResult result)
        {
            var value = result.GetValue<object>();
            if (value is IAsyncEnumerable<object> asyncEnumerable)
            {
                return (IAsyncEnumerable<TResult>)asyncEnumerable;
            }

            throw new Exception("This is not a streaming object");
        }

        public static bool IsStreamingResult(this FunctionResult result)
        {
            var value = result.GetValue<object>();
            return IsAsyncEnumerable(value);
        }
        public static bool IsAsyncEnumerable(object? obj)
        {
            if (obj == null) return false;

            var type = obj.GetType();
            return type.GetInterfaces().Any(iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>));
        }
       
        public static KernelPlugin ImportPluginFromPromptDirectoryYaml(this Kernel kernel, string pluginName)
        {
            var files = Directory.GetFiles(Path.Combine(RepoFiles.PathToYamlPlugins, pluginName), "*.yaml");
            var kFunctions = new List<KernelFunction>();
            foreach (var file in files)
            {
                var yamlText = File.ReadAllText(file);
                try
                {
                    var func = kernel.CreateFunctionFromPromptYaml(yamlText);
                    kFunctions.Add(func);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR DESERIALIZING YAML\n\nFunction: {Path.GetFileName(file)}\n\n{ex}");
                }
            }
            //var functions = Directory.GetFiles(Path.Combine(RepoFiles.PathToYamlPlugins, pluginName), "*.yaml").Select(functionYml => kernel.CreateFunctionFromPromptYaml(File.ReadAllText(functionYml))).ToList();
            var plugin = KernelPluginFactory.CreateFromFunctions(pluginName, kFunctions);
            return plugin;
        }
       
    }
}
