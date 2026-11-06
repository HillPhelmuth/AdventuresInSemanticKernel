using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models.Events;

/// <summary>
/// Event args raised when an agent produces a response (a full message or a streaming update).
/// </summary>
public class AgentResponseArgs : EventArgs
{
	public AgentResponseArgs(ChatMessage agentChatMessage)
	{
		AgentChatMessage = agentChatMessage;
	}
	public AgentResponseArgs(AgentResponseUpdate agentChatMessage)
	{
		AgentChatMessageUpdate = agentChatMessage;
	}

    /// <summary>Gets the agent chat message associated with the response.</summary>
    public ChatMessage? AgentChatMessage { get; }
	public AgentResponseUpdate? AgentChatMessageUpdate { get; }
	public bool IsCompleted { get; internal set; }
	public bool IsStartToken { get; internal set; }
}
public class AgentStreamingResponseArgs(AgentResponseUpdate agentChatMessage) : EventArgs
{
    public AgentResponseUpdate AgentChatMessageUpdate { get; } = agentChatMessage;
    public bool IsCompleted { get; internal set; }
    public bool IsStartToken { get; internal set; }
}
