using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Group;

public interface IGroupChat
{
    void AddInitializeMessage(AgentMessage message);

    Task<ChatHistory> CallAsync(ChatHistory conversation = null, int maxRound = 10,
	    CancellationToken ct = default);
}