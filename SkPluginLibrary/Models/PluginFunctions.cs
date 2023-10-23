using Microsoft.SemanticKernel;


namespace SkPluginLibrary.Models;

public class PluginFunctions
{
    public PluginFunctions(string pluginName, PluginType pluginType = PluginType.Semantic)
    {
        PluginName = pluginName;
        PluginType = pluginType;
    }
    public PluginType PluginType { get; set; }
    public string PluginName { get; set; }
    public List<Function> Functions { get; set; } = new();
    public Dictionary<string, ISKFunction> SkFunctions => Functions.ToDictionary(x => x.Name, x => x.SkFunction);
    public bool IsSelected { get; set; }

}