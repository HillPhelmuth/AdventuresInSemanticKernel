using Microsoft.SemanticKernel;

namespace SkPluginLibrary.Agents.Models.Events;

/// <summary>
/// Initializes a new instance of the <see cref="AgentResponseArgs"/> class.
/// </summary>
/// <param name="agentChatMessage">The agent chat message associated with the response.</param>
public class AgentResponseArgs(ChatMessageContent agentChatMessage) : EventArgs
{

    /// <summary>
    /// Gets the agent chat message associated with the response.
    /// </summary>
    public ChatMessageContent AgentChatMessage { get; } = agentChatMessage;
}
public class AgentStreamingResponseArgs(StreamingChatMessageContent agentChatMessage) : EventArgs
{
    /// <summary>
    /// Gets the agent chat message associated with the response.
    /// </summary>
    public StreamingChatMessageContent AgentChatMessageUpdate { get; } = agentChatMessage;
    public bool IsCompleted { get; internal set; }
    public bool IsStartToken { get; internal set; }
}
