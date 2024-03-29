using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Agents.Models;

/// <summary>
/// Represents an interactive agent that can request human input and provide responses.
/// </summary>
public interface IInteractiveAgent
{
    public string Name { get; }
    string Description { get; }

    Task<AgentMessage?> RunAgentAsync(List<AgentMessage> chatHistory, PromptExecutionSettings? settings = null, CancellationToken cancellationToken = default);
}
