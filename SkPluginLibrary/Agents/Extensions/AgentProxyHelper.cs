using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents;
using SemanticKernelAgentOrchestration.Models;

namespace SkPluginLibrary.Agents.Extensions;

public static class AgentProxyHelper
{
    public static ChatCompletionAgent AsChatCompletionAgent(this AgentProxy agent, Kernel kernel)
    {
        var cloned = kernel.Clone();
        cloned.Plugins.AddRange(agent.Plugins);
        var args = new KernelArguments(new OpenAIPromptExecutionSettings(){ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions});
        return new ChatCompletionAgent()
            { Name = FixNameToMatchPattern(agent.Name), Description = agent.Description, Instructions = agent.Instructions, Kernel = cloned, Arguments = args};
    }

    private static string FixNameToMatchPattern(string name)
    {
        var pattern = new Regex(@"^[a-zA-Z0-9_-]+$");
        if (!pattern.IsMatch(name))
        {
            name = name.Replace(" ", "_");
        }
        return name;
    }
}