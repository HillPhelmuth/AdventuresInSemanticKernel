using Microsoft.SemanticKernel.ChatCompletion;
using SemanticKernelAgentOrchestration.Models;

namespace SemanticKernelAgentOrchestration.Group;

/// <summary>
/// Represents a transition between two chat agents.
/// </summary>
public class Transition
{
	private readonly Func<ChatAgent, ChatAgent, ChatHistory, Task<bool>>? _transitionRuleFunction;

	internal Transition(ChatAgent from, ChatAgent to, Func<ChatAgent, ChatAgent, ChatHistory, Task<bool>>? canTransitionAsync = null)
	{
		From = from;
		To = to;
		_transitionRuleFunction = canTransitionAsync;
	}

	/// <summary>
	/// Creates a new transition between two chat agents.
	/// </summary>
	/// <typeparam name="TFrom">The type of the source chat agent.</typeparam>
	/// <typeparam name="TToAgent">The type of the target chat agent.</typeparam>
	/// <param name="from">The source chat agent.</param>
	/// <param name="to">The target chat agent.</param>
	/// <param name="transitionRuleAsync">An optional asynchronous transition rule.</param>
	/// <returns>The created transition.</returns>
	public static Transition Create<TFrom, TToAgent>(TFrom from, TToAgent to, Func<TFrom, TToAgent, ChatHistory, Task<bool>>? transitionRuleAsync = null)
		where TFrom : ChatAgent
		where TToAgent : ChatAgent
	{
		return new Transition(from, to, (fromAgent, toAgent, history) => transitionRuleAsync?.Invoke((TFrom)fromAgent, (TToAgent)toAgent, history) ?? Task.FromResult(true));
	}

	/// <summary>
	/// Gets the source chat agent of the transition.
	/// </summary>
	public ChatAgent From { get; }

	/// <summary>
	/// Gets the target chat agent of the transition.
	/// </summary>
	public ChatAgent To { get; }

	/// <summary>
	/// Checks if the transition can be performed based on the provided chat history.
	/// </summary>
	/// <param name="history">The chat history.</param>
	/// <returns>A task representing the asynchronous operation. The task result indicates whether the transition can be performed.</returns>
	public Task<bool> CanTransitionAsync(ChatHistory history)
	{
		return _transitionRuleFunction == null ? Task.FromResult(true) : _transitionRuleFunction(From, To, history);
	}
}
