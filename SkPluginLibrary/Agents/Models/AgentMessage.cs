using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SkPluginLibrary.Agents.Models;

public class AgentMessage : ChatMessageContent
{
    public AgentMessage(AuthorRole role, string? content,string name, string? modelId = null, object? innerContent = null, Encoding? encoding = null, IReadOnlyDictionary<string, object?>? metadata = null) : base(role, content, modelId, innerContent, encoding, metadata)
    {
        AgentName = name;
    }

    public AgentMessage(AuthorRole role, ChatMessageContentItemCollection items, string name, string? modelId = null, object? innerContent = null, Encoding? encoding = null, IReadOnlyDictionary<string, object?>? metadata = null) : base(role, items, modelId, innerContent, encoding, metadata)
    {
        AgentName = name;
    }
    public string? AgentName { get; set; }
}
public class AgentMessageStreamUpdate(
    AuthorRole? role,
    string? content,
    string name,
    object? innerContent = null,
    int choiceIndex = 0,
    string? modelId = null,
    Encoding? encoding = null,
    IReadOnlyDictionary<string, object?>? metadata = null)
    : StreamingChatMessageContent(role, content, innerContent, choiceIndex, modelId, encoding, metadata)
{
    public string? AgentName { get; set; } = name;
}