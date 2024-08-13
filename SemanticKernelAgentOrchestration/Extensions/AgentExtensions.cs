using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Extensions;

public static class AgentExtensions
{
	public static KernelPlugin AsPlugin(this ChatAgent agent)
	{
		return new InteractiveAgentPlugin(PluginAllowedName(agent.Name), agent, agent.Description);
	}
	private static string PluginAllowedName(string name)
	{
		var pattern = @"[^a-zA-Z0-9_]";
		return Regex.Replace(name, pattern, "");
	}
}