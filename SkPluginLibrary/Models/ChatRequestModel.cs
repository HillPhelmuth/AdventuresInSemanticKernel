using Microsoft.SemanticKernel;
using System.Text;

namespace SkPluginLibrary.Models;


public class Plugins(ChatRequestModel chatRequestModel)
{
    public string Name { get; set; }
    public PluginType PluginType { get; set; }
    public Dictionary<string, KernelFunction> Functions { get; set; }
    public List<string>? LocalPluginNames { get; set; }
    public List<string>? ApiPluginNames { get; set; }
    public Dictionary<string, KernelFunction> CorePlugins { get; set; } = new();
    public Dictionary<string, KernelFunction> CustomPlugins { get; set; } = new();

    public Dictionary<string, KernelFunction>? NativePlugins =>
        CorePlugins.Concat(CustomPlugins).ToDictionary(x => x.Key, x => x.Value);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Execution Type {chatRequestModel.ExecutionType.ToString()}");
        if (LocalPluginNames != null)
        {
            sb.AppendLine($"Local Plugins: {string.Join(", ", LocalPluginNames)}");
        }

        if (chatRequestModel.ChatGptPlugins != null)
        {
            sb.AppendLine($"Chat GPT Plugins: {string.Join(", ", chatRequestModel.ChatGptPlugins.Select(x => x.Title))}");
        }

        if (ApiPluginNames != null)
        {
            sb.AppendLine($"API Plugins: {string.Join(", ", ApiPluginNames)}");
        }
        if (CorePlugins.Any())
        {
            sb.AppendLine($"Core Plugins: {string.Join(", ", CorePlugins.Select(x => x.Key))}");
        }

        if (CustomPlugins.Any())
        {
            sb.AppendLine($"Custom Plugins: {string.Join(", ", CustomPlugins.Select(x => x.Key))}");
        }

        return sb.ToString();
    }
}

public class ChatRequestModel
{
    private readonly Plugins _plugins;

    public ChatRequestModel()
    {
        _plugins = new Plugins(this);
    }

    public ExecutionType ExecutionType { get; set; }
    public List<KernelPlugin> SelectedPlugins { get; set; } = new();
    public List<ChatGptPlugin>? ChatGptPlugins { get; set; }
    public List<string>? ExcludedFunctions { get; set; }
    public List<string>? RequredFunctions { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
    public List<Function> SelectedFunctions { get; set; } = new();

    public Plugins Plugins => _plugins;

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Execution Type {ExecutionType.ToString()}");
        if (Plugins.LocalPluginNames != null)
        {
            sb.AppendLine($"Local Plugins: {string.Join(", ", Plugins.LocalPluginNames)}");
        }

        if (ChatGptPlugins != null)
        {
            sb.AppendLine($"Chat GPT Plugins: {string.Join(", ", ChatGptPlugins.Select(x => x.Title))}");
        }

        if (Plugins.ApiPluginNames != null)
        {
            sb.AppendLine($"API Plugins: {string.Join(", ", Plugins.ApiPluginNames)}");
        }
        if (Plugins.CorePlugins.Any())
        {
            sb.AppendLine($"Core Plugins: {string.Join(", ", Plugins.CorePlugins.Select(x => x.Key))}");
        }

        if (Plugins.CustomPlugins.Any())
        {
            sb.AppendLine($"Custom Plugins: {string.Join(", ", Plugins.CustomPlugins.Select(x => x.Key))}");
        }

        return sb.ToString();
        return _plugins.ToString();
    }
}

public static class FunctionExtensions
{
    public static List<Function> ToFunctionList(this Dictionary<string, KernelFunction> functions)
    {
        return functions.Select(x => new Function(x.Key, x.Value)).ToList();
    }

    public static KernelPlugin ToPlugin(this Dictionary<string, KernelFunction> functions,
        string pluginName, PluginType pluginType)
    {
        var result = KernelPluginFactory.CreateFromFunctions(pluginName, functions.Values.ToList());
        return result;
    }
}