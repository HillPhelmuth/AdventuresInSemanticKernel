using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPluginLibrary.Agents.Extensions;

namespace SkPluginLibrary.Agents.Models;

public class InteractiveAgent : InteractiveAgentBase
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
#pragma warning disable CS8765