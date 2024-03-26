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
    public async Task<IEnumerable<IInteractiveAgent>> TransitToNextAvailableAgentsAsync(IInteractiveAgent fromAgent, IEnumerable<AgentMessage> messages)
    {
        var nextAgents = new List<IInteractiveAgent>();
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
    private readonly Func<IInteractiveAgent, IInteractiveAgent, IEnumerable<AgentMessage>, Task<bool>>? _canTransition;

   
    internal Transition(IInteractiveAgent from, IInteractiveAgent to, Func<IInteractiveAgent, IInteractiveAgent, IEnumerable<AgentMessage>, Task<bool>>? canTransitionAsync = null)
    {
        From = from;
        To = to;
        _canTransition = canTransitionAsync;
    }

   
    public static Transition Create<TFrom, TToAgent>(TFrom from, TToAgent to, Func<TFrom, TToAgent, IEnumerable<AgentMessage>, Task<bool>>? transitionRuleAsync = null)
        where TFrom : IInteractiveAgent
        where TToAgent : IInteractiveAgent
    {
        return new Transition(from, to, (fromAgent, toAgent, messages) => transitionRuleAsync?.Invoke((TFrom)fromAgent, (TToAgent)toAgent, messages) ?? Task.FromResult(true));
    }

    public IInteractiveAgent From { get; }

    public IInteractiveAgent To { get; }

    /// <summary>
    /// Check if the transition is allowed.
    /// </summary>
    /// <param name="messages">messages</param>
    public Task<bool> CanTransitionAsync(IEnumerable<AgentMessage> messages)
    {
        return _canTransition == null ? Task.FromResult(true) : _canTransition(From, To, messages);
    }
}