using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Experimental.Agents;
using SkPluginLibrary.Agents.Models;

namespace SkPluginLibrary.Agents.Group;

public class TransitionGraph
{
    public TransitionGraph(IEnumerable<Transition> transitions)
    {
        this._transitions.AddRange(transitions);
    }
    private readonly List<Transition> _transitions = [];
    public IEnumerable<Transition> Transitions => _transitions;

    /// <summary>
    /// Get the next available agents that the messages can be transit to.
    /// </summary>
    /// <param name="fromAgent">the from agent</param>
    /// <param name="messages">messages</param>
    /// <returns>A list of agents that the messages can be transit to</returns>
    public async Task<IEnumerable<InteractiveAgentBase>> TransitToNextAvailableAgentsAsync(
	    InteractiveAgentBase fromAgent, ChatHistory messages)
    {
        var nextAgents = new List<InteractiveAgentBase>();
        var availableTransitions = _transitions.FindAll(t => t.From == fromAgent) ?? Enumerable.Empty<Transition>();
        foreach (var transition in availableTransitions)
        {
            if (await transition.CanTransitionAsync(messages))
            {
                nextAgents.Add(transition.To);
            }
        }

        return nextAgents;
    }
}
public class Transition
{
    private readonly Func<InteractiveAgentBase, InteractiveAgentBase, ChatHistory, Task<bool>>? _canTransition;

   
    internal Transition(InteractiveAgentBase from, InteractiveAgentBase to, Func<InteractiveAgentBase, InteractiveAgentBase, ChatHistory, Task<bool>>? canTransitionAsync = null)
    {
        From = from;
        To = to;
        _canTransition = canTransitionAsync;
    }

   
    public static Transition Create<TFrom, TToAgent>(TFrom from, TToAgent to, Func<TFrom, TToAgent, ChatHistory, Task<bool>>? transitionRuleAsync = null)
        where TFrom : InteractiveAgentBase
        where TToAgent : InteractiveAgentBase
    {
        return new Transition(from, to, (fromAgent, toAgent, messages) => transitionRuleAsync?.Invoke((TFrom)fromAgent, (TToAgent)toAgent, messages) ?? Task.FromResult(true));
    }

    public InteractiveAgentBase From { get; }

    public InteractiveAgentBase To { get; }

    /// <summary>
    /// Check if the transition is allowed.
    /// </summary>
    /// <param name="messages">messages</param>
    public Task<bool> CanTransitionAsync(ChatHistory messages)
    {
        return _canTransition == null ? Task.FromResult(true) : _canTransition(From, To, messages);
    }
}