using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SkPluginLibrary.Agents.Models;

namespace SkPluginLibrary.Agents.Extensions;

public static class GroupExtensions
{
    public static ChatHistory AsChatHistory(this List<AgentMessage> agentChatHistory)
    {
        return agentChatHistory.Count == 0 ? [] : new ChatHistory(agentChatHistory.Select(message => new ChatMessageContent(message.Role, $"{message.AgentName}:\n{message.Content}", message.ModelId, message.InnerContent, message.Encoding, message.Metadata)));
    }
}