using SkPluginLibrary.Agents.Models;

namespace SkPluginLibrary.Agents.Group;

public interface IGroupChat
{
    void AddInitializeMessage(AgentMessage message);

    Task<List<AgentMessage>> CallAsync(List<AgentMessage>? conversation = null, int maxRound = 10,
        CancellationToken ct = default);
}