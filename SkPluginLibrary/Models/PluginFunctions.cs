using Microsoft.SemanticKernel;


namespace SkPluginLibrary.Models;

public class PluginFunctions(string pluginName, PluginType pluginType = PluginType.Prompt)
{
    public PluginType PluginType { get; set; } = pluginType;
    public string PluginName { get; set; } = pluginName;
    public List<Function> Functions { get; set; } = new();
    public Dictionary<string, KernelFunction> SkFunctions => Functions.ToDictionary(x => x.Name, x => x.SkFunction);
    public bool IsSelected { get; set; }

}