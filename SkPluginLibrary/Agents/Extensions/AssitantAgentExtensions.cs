using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Experimental.Agents;
using SemanticKernelAgentOrchestration.Models;

namespace SkPluginLibrary.Agents.Extensions
{
	internal static class AssitantAgentExtensions
	{
		public static AgentProxy AgentProxy(this IAgent agent)
		{
			return new AgentProxy { Name = agent.Name, Description = agent.Description, Instructions = agent.Instructions, Plugins = agent.Plugins.ToList() };
		}
	}
}
