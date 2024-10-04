using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Plugins;

public class TransitionPlugin(ChatContext chatContext)
{
    [KernelFunction, Description("Transition to next agent when user indicates readiness to move forward")]
    public void TransitionToNextAgent()
    {
        Console.WriteLine("Transitioning to next agent");
        chatContext.IsTranstionNext = true;
    }
    [KernelFunction, Description("Transition to agent at user request if it aligns with another agent's objective more than your own")]
    public void IntentTransition()
    {
        Console.WriteLine("Transitioning to agent at user request");
        chatContext.IsIntentTranstion = true;
    }
}