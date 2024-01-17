using Microsoft.SemanticKernel.Experimental.Agents;

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
}