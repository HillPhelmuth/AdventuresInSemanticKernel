using Microsoft.SemanticKernel.ChatCompletion;
using SkPluginLibrary.Agents.Models;

namespace SkPluginLibrary.Agents.Group;

public interface IGroupChat
{
    void AddInitializeMessage(AgentMessage message);

    Task<ChatHistory> CallAsync(ChatHistory conversation = null, int maxRound = 10,
	    CancellationToken ct = default);
}