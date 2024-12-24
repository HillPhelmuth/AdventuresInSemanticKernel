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
    [KernelFunction, Description("Transition to next agent when your objectives are completed")]
    public void TransitionToNextAgent(Kernel kernel)
    {
        var chatContext = kernel.GetRequiredService<ChatContext>();
        
        if (chatContext.ActiveForms.Any() && chatContext.ActiveAgent.Name == "FormBuilder")
        {
            Console.WriteLine("Cannot transition to next agent until all forms are completed");
            return;
        }
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