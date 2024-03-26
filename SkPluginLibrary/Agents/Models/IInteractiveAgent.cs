using Microsoft.SemanticKernel;
using SkPluginLibrary.Agents.Models.Events;

namespace SkPluginLibrary.Agents.Models;

/// <summary>
/// Represents an interactive agent that can request human input and provide responses.
/// </summary>
public interface IInteractiveAgent
{
    public string Name { get; }
    string Description { get; }

    /// <summary>
    /// Event that is triggered to request human input.
    /// </summary>
    event AgentInputRequestEventHandler? AgentInputRequest;

    /// <summary>
    /// Overrides the GetHumanInputAsync method to trigger the event and return the task that will eventually have the result.
    /// </summary>
    /// <returns>The task that will eventually have the human input response.</returns>
    Task<string?> GetHumanInputAsync();

    /// <summary>
    /// Provides the input to the agent.
    /// </summary>
    /// <param name="input">The input provided by the user.</param>
    void ProvideInput(string input);

    /// <summary>
    /// Resets the agent for the next input.
    /// </summary>
    void ResetForNextInput();

    Task<AgentMessage?> RunAgentAsync(List<AgentMessage> chatHistory, PromptExecutionSettings? settings = null, CancellationToken cancellationToken = default);
}
