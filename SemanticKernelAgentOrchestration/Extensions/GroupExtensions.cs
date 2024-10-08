﻿using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Extensions;

public static class GroupExtensions
{
    public static ChatHistory AsChatHistory(this List<AgentMessage> agentChatHistory)
    {
        return agentChatHistory.Count == 0 ? [] : new ChatHistory(agentChatHistory.Select(message => new ChatMessageContent(message.Role, $"{message.AuthorName}:\n{message.Content}", message.ModelId, message.InnerContent, message.Encoding, message.Metadata)));
    }
    public static string FormatMessage(this ChatMessageContent message)
    {
        var sb = new StringBuilder();
        // write from
        sb.AppendLine($"Message from <strong>{message?.AuthorName}</strong>");
        // write a seperator
        sb.AppendLine("\n");
        sb.AppendLine(message?.Content);
        // write a seperator
        sb.AppendLine("<hr/>");

        return sb.ToString();
    }
}