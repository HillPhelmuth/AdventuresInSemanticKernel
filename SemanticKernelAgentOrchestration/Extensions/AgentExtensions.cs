using System.Text.RegularExpressions;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Extensions;

public static class AgentExtensions
{
	/// <summary>Wraps an agent as a tool group so it can be handed to another agent as a callable tool.</summary>
	public static ToolPlugin AsPlugin(this ChatAgent agent)
	{
		return new InteractiveAgentPlugin(PluginAllowedName(agent.Name), agent, agent.Description);
	}
	private static string PluginAllowedName(string name)
	{
		var pattern = @"[^a-zA-Z0-9_]";
		return Regex.Replace(name, pattern, "");
	}
}
