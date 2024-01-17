using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Models.Helpers;

public static class FunctionExtensions
{
    public static KernelPlugin ToPlugin(this Dictionary<string, KernelFunction> functions,
        string pluginName)
    {
        var result = KernelPluginFactory.CreateFromFunctions(pluginName, functions.Values.ToList());
        return result;
    }
}