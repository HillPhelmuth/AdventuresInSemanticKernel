using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models;

/// <summary>A chat message attributed to a named agent.</summary>
public class AgentMessage : ChatMessage
{
    public AgentMessage(ChatRole role, string? content, string name) : base(role, content)
    {
        AuthorName = name;
    }

    public AgentMessage(ChatRole role, IList<AIContent> contents, string name) : base(role, contents)
    {
        AuthorName = name;
    }
}
