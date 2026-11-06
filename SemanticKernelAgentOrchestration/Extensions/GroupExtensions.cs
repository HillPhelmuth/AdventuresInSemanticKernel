using System.Text;
using ChatComponents;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Extensions;

public static class GroupExtensions
{
    public static ChatHistory AsChatHistory(this List<AgentMessage> agentChatHistory)
    {
        return agentChatHistory.Count == 0 ? [] : new ChatHistory(agentChatHistory.Select(message => new ChatMessage(message.Role, $"{message.AuthorName}:\n{message.Text}")));
    }
    public static string FormatMessage(this ChatMessage message)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Message from <strong>{message?.AuthorName}</strong>");
        sb.AppendLine("\n");
        sb.AppendLine(message?.Text);
        sb.AppendLine("<hr/>");

        return sb.ToString();
    }
}
