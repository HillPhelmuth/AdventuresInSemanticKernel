using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SkPluginLibrary.Agents.Models;

/// <summary>
/// Represents an interactive agent that can request human input and provide responses.
/// </summary>
public interface IInteractiveAgent
{
    public string Name { get; }
    string Description { get; }

    Task<ChatMessageContent?> RunAgentAsync(ChatHistory chatHistory, PromptExecutionSettings? settings = null,
	    CancellationToken cancellationToken = default);
}
