using SkPluginLibrary.Agents.SkAgents;

namespace SkPluginLibrary.Agents.Models.Events
{
    public class SkAgentInputRequestArgs(UserProxySkAgent agent) : EventArgs
    {
        public UserProxySkAgent Agent { get; set; } = agent;
    }
}
