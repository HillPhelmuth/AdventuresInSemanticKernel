using ChatComponents;
using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models;

/// <summary>
/// Represents an interactive agent that can request human input and provide responses.
/// </summary>
public interface IChatAgent
{
    public string Name { get; }
    string Description { get; }

    Task<ChatMessage?> RunAgentAsync(ChatHistory chatHistory, ChatOptions? settings = null,
        CancellationToken cancellationToken = default);
}
