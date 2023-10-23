using Microsoft.SemanticKernel;
using System.Text;

namespace SkPluginLibrary.Models;


public class Plugins
{
    private ChatRequestModel _chatRequestModel;

    public Plugins(ChatRequestModel chatRequestModel)
    {
        _chatRequestModel = chatRequestModel;
    }
    public string Name { get; set; }
    public PluginType PluginType { get; set; }
    public Dictionary<string, ISKFunction> Functions { get; set; }
    public List<string>? LocalPluginNames { get; set; }
    public List<string>? ApiPluginNames { get; set; }
    public Dictionary<string, ISKFunction> CorePlugins { get; set; } = new();
    public Dictionary<string, ISKFunction> CustomPlugins { get; set; } = new();

    public Dictionary<string, ISKFunction>? NativePlugins =>
        CorePlugins.Concat(CustomPlugins).ToDictionary(x => x.Key, x => x.Value);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Execution Type {_chatRequestModel.ExecutionType.ToString()}");
        if (LocalPluginNames != null)
        {
            sb.AppendLine($"Local Plugins: {string.Join(", ", LocalPluginNames)}");
        }

        if (_chatRequestModel.ChatGptPlugins != null)
        {
            sb.AppendLine($"Chat GPT Plugins: {string.Join(", ", _chatRequestModel.ChatGptPlugins.Select(x => x.Title))}");
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
    public List<PluginFunctions> SelectedPlugins { get; set; } = new();
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
    public static List<Function> ToFunctionList(this Dictionary<string, ISKFunction> functions)
    {
        return functions.Select(x => new Function(x.Key, x.Value)).ToList();
    }

    public static PluginFunctions ToPluginFunctions(this Dictionary<string, ISKFunction> functions,
        string pluginlName, PluginType pluginType)
    {
        return new PluginFunctions(pluginlName, pluginType)
        {
            Functions = functions.ToFunctionList()
        };
    }
}