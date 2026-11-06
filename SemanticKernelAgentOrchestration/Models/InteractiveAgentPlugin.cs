using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models;

/// <summary>Exposes a <see cref="ChatAgent"/> as a single callable tool so another agent can delegate to it.</summary>
public class InteractiveAgentPlugin : ToolPlugin
{
    private const string AgentFunctionName = "RunAgent";
    public InteractiveAgentPlugin(string name, ChatAgent agent, string? description = null)
        : base(name, [CreateRunAgentFunction(agent)], description)
    {
    }

    private static AIFunction CreateRunAgentFunction(ChatAgent agent)
    {
        async Task<string> RunAgent(string input, CancellationToken cancellationToken)
        {
            var message = await agent.RunAgent(input, cancellationToken);
            return message?.Text ?? "";
        }
        return AIFunctionFactory.Create(RunAgent, AgentFunctionName, agent.Description);
    }
}
