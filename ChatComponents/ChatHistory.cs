using Microsoft.Extensions.AI;

namespace ChatComponents;

/// <summary>
/// Thin convenience wrapper over a list of Microsoft.Extensions.AI <see cref="ChatMessage"/>s.
/// Keeps the familiar AddUserMessage/AddAssistantMessage helpers so UI code reads the same as before,
/// while remaining directly usable with any <see cref="IChatClient"/> or Agent Framework agent.
/// </summary>
public class ChatHistory : List<ChatMessage>
{
    public ChatHistory() { }
    public ChatHistory(string systemMessage) => AddSystemMessage(systemMessage);
    public ChatHistory(IEnumerable<ChatMessage> messages) : base(messages) { }

    public void AddSystemMessage(string content) => Add(new ChatMessage(ChatRole.System, content));
    public void AddUserMessage(string content) => Add(new ChatMessage(ChatRole.User, content));
    public void AddAssistantMessage(string content) => Add(new ChatMessage(ChatRole.Assistant, content));
}
