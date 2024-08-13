using Microsoft.SemanticKernel;

namespace SemanticKernelAgentOrchestration.Models;

public class InteractiveAgent : ChatAgent
{
    public InteractiveAgent(AgentProxy agent, Kernel kernel, bool engageuser = false) : base(agent, kernel)
    {

        Plugins = agent.Plugins;
        if (engageuser)
            AddInteractivePlugin();

    }

    public string Id { get; } = Guid.NewGuid().ToString();
       
    public List<KernelPlugin> Plugins { get; set; }

    public bool IsPrimary { get; set; }
    public void AddInteractivePlugin()
    {
        var function = KernelFunctionFactory.CreateFromMethod(this.GetHumanInputAsync, "AskUser", "Ask user for information, or request clarification from user.");
        var plugin = KernelPluginFactory.CreateFromFunctions("InteractiveAgentPlugin", "", [function]);
        Plugins.Add(plugin);
    }
}
