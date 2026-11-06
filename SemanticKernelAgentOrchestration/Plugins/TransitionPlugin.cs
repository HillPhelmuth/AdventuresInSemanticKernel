using System.ComponentModel;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Plugins;

public class TransitionPlugin(ChatContext chatContext)
{
    [Description("Transition to next agent when user indicates readiness to move forward")]
    public void TransitionToNextAgent()
    {
        Console.WriteLine("Transitioning to next agent");
        chatContext.IsTranstionNext = true;
    }
    [Description("Transition to agent at user request if it aligns with another agent's objective more than your own")]
    public void IntentTransition()
    {
        Console.WriteLine("Transitioning to agent at user request");
        chatContext.IsIntentTranstion = true;
    }
    public ToolPlugin AsToolPlugin() => ToolPlugin.FromObject(this, nameof(TransitionPlugin));
}
