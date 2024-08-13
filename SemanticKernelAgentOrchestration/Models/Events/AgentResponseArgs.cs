using Microsoft.SemanticKernel;

namespace SemanticKernelAgentOrchestration.Models.Events;

/// <summary>
/// Initializes a new instance of the <see cref="AgentResponseArgs"/> class.
/// </summary>
public class AgentResponseArgs : EventArgs
{
	/// <summary>
	/// Initializes a new instance of the <see cref="AgentResponseArgs"/> class.
	/// </summary>
	/// <param name="agentChatMessage">The agent chat message associated with the response.</param>
	public AgentResponseArgs(ChatMessageContent agentChatMessage)
	{
		AgentChatMessage = agentChatMessage;
	}
	public AgentResponseArgs(StreamingChatMessageContent agentChatMessage)
	{
		AgentChatMessageUpdate = agentChatMessage;
	}

	/// <summary>
    /// Gets the agent chat message associated with the response.
    /// </summary>
    public ChatMessageContent? AgentChatMessage { get; }
	public StreamingChatMessageContent? AgentChatMessageUpdate { get; }
	public bool IsCompleted { get; internal set; }
	public bool IsStartToken { get; internal set; }
}
public class AgentStreamingResponseArgs(StreamingChatMessageContent agentChatMessage) : EventArgs
{
    /// <summary>
    /// Gets the agent chat message update associated with the response.
    /// </summary>
    public StreamingChatMessageContent AgentChatMessageUpdate { get; } = agentChatMessage;
    public bool IsCompleted { get; internal set; }
    public bool IsStartToken { get; internal set; }
}
