using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Experimental.Agents;
using System.Text.RegularExpressions;

namespace SkPluginLibrary.Agents.Models;

public static class AgentExtensions
{
    public static AgentProxy AgentProxy(this IAgent agent)
    {
        return new AgentProxy
        {
            Description = agent.Description ?? "no description",
            Instructions = agent.Instructions,
            Name = agent.Name,
            Plugins = [.. agent.Plugins],
            IsPrimary = false
        };
    }
    public static KernelPlugin AsPlugin(this InteractiveAgentBase agent)
    {
        return new InteractiveAgentPlugin(PluginAllowedName(agent.Name), agent, agent.Description);
    }
    private static string PluginAllowedName(string name)
    {
        var pattern = @"[^a-zA-Z0-9_]";
        return Regex.Replace(name, pattern, "");
    }
}