using Microsoft.Extensions.AI;

namespace SemanticKernelAgentOrchestration.Models;

public class InteractiveAgent : ChatAgent
{
    public InteractiveAgent(AgentProxy agent, IChatClient chatClient, bool engageuser = false) : base(agent, chatClient)
    {
        Plugins = agent.Plugins;
        if (engageuser)
            AddInteractivePlugin();
    }

    public string Id { get; } = Guid.NewGuid().ToString();

    public List<ToolPlugin> Plugins { get; set; }

    public bool IsPrimary { get; set; }
    public void AddInteractivePlugin()
    {
        var function = AIFunctionFactory.Create(GetHumanInputAsync, "AskUser", "Ask user for information, or request clarification from user.");
        ExtraTools.Add(function);
    }
}
