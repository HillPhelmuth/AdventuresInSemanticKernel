using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPluginLibrary.Agents.Extensions;
using System.Text.Json.Serialization;
using SkPluginLibrary.Models.JsonConverters;

namespace SkPluginLibrary.Agents.Models;

public class AgentProxy
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string SystemPrompt => $"""
                                   # Name
                                   You are {Name}.
                                   # Description
                                   {Description}.
                                   # Instructions
                                   {Instructions}
                                   """;
    public string? GptModel { get; set; }

    [JsonIgnore]
    public List<KernelPlugin> Plugins
    {
        get => _plugins;
        set
        {
            PluginNames = value.Select(x => x.Name).ToList();
            _plugins = value;
        }
    }

    private List<KernelPlugin> _plugins = [];
    private List<string> _pluginNames = [];
    public List<string> PluginNames
    {
        get => Plugins.Count != 0 ? Plugins.Select(x => x.Name).ToList() : _pluginNames;
        set => _pluginNames = value;
    }


    public bool IsUserProxy { get; set; }

    public bool IsPrimary { get; set; }
}

public enum AgentInteractionType
{
    [Description("None")]
    None,
    [Description("Single Agent with standard plugins")]
    SingleAgent,
    [Description("Primary Agent with sub-agents added as plugins")]
    AgentWithSubAgentsAsPlugins,
    [Description("Chat interaction with all Agents called as an OpenAI functions")]
    ChatWithAgentsAsOpenAiToolFunctions
}