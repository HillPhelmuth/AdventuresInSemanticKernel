using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Group;

public class TransitionGraph
{
	public TransitionGraph(IEnumerable<Transition> transitions)
	{
		this._transitions.AddRange(transitions);
	}
	private readonly List<Transition> _transitions = [];
	public IEnumerable<Transition> Transitions => _transitions;

	/// <summary>
	/// Get the next available agents that the messages can be transitioned to.
	/// </summary>
	/// <param name="fromAgent">The starting agent</param>
	/// <param name="messages">The chat history messages</param>
	/// <returns>A list of agents that the messages can be transitioned to</returns>
	public async Task<IEnumerable<ChatAgent>> TransitToNextAvailableAgentsAsync(
		ChatAgent fromAgent, ChatHistory messages)
	{
		var nextAgents = new List<ChatAgent>();
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