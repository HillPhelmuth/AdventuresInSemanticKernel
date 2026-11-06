using ChatComponents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SemanticKernelAgentOrchestration.Models.Events;

namespace SemanticKernelAgentOrchestration.Models;

/// <summary>
/// Base class for the orchestration agents. Each agent wraps a Microsoft Agent Framework
/// <see cref="AIAgent"/> (a <see cref="ChatClientAgent"/> over an <see cref="IChatClient"/>).
/// </summary>
public abstract class ChatAgent : IChatAgent
{
	protected ChatAgent(AgentProxy agent, IChatClient chatClient)
	{
		Name = agent.Name;
		Description = agent.Description;
		SystemPrompt = agent.SystemPrompt;
		Agent = agent;
		ChatClient = chatClient;
	}

	public AgentProxy Agent { get; }
	public string Name { get; }
	public string Description { get; }
	public string SystemPrompt { get; }
	public string SystemPromptOverride(string prompt, bool includeSystemPrompt = true)
    {
        return includeSystemPrompt ? $"{SystemPrompt}\n{prompt}" : prompt;
    }
    public IChatClient ChatClient { get; }
	public ChatHistoryType ChatHistoryType { get; set; } = ChatHistoryType.All;

	/// <summary>Function-calling middleware shared across agents so tool calls can be observed.</summary>
	public List<AgentFunctionMiddleware> FunctionMiddleware { get; } = [];

	/// <summary>Extra tools (beyond the agent proxy's plugins), such as the interactive AskUser tool.</summary>
	protected List<AITool> ExtraTools { get; } = [];

	public void AddTools(IEnumerable<AITool> tools) => ExtraTools.AddRange(tools);

	protected virtual IList<AITool> GetTools()
	{
		var tools = new List<AITool>();
		foreach (var plugin in Agent.Plugins)
		{
			tools.AddRange(plugin.Functions.Where(f => Agent.ExcludedFunctions.All(e => e.Name != f.Name)).Select(plugin.Prefixed));
		}
		tools.AddRange(ExtraTools);
		return tools;
	}

	/// <summary>Builds the Agent Framework agent for this participant with its current tools.</summary>
	public AIAgent BuildAgent(ChatOptions? options = null, string? instructions = null)
	{
		var chatOptions = options ?? new ChatOptions { Temperature = Temperature };
		chatOptions.Instructions = instructions ?? SystemPrompt;
		var tools = GetTools();
		if (tools.Count > 0) chatOptions.Tools = tools;
		AIAgent aiAgent = new ChatClientAgent(ChatClient, new ChatClientAgentOptions
		{
			Name = EnsurePattern(Name),
			Description = Description,
			ChatOptions = chatOptions
		});
		if (FunctionMiddleware.Count == 0) return aiAgent;
		var builder = new AIAgentBuilder(aiAgent);
		foreach (var middleware in FunctionMiddleware) builder.Use(middleware.InvokeAsync);
		return builder.Build();
	}

    protected List<Func<ChatHistory, Task<ChatHistory>>>? PreReplyHooks { get; set; }
	public void RegisterPreReplyHook(Func<ChatHistory, Task<ChatHistory>> hook)
	{
		PreReplyHooks ??= new();
		PreReplyHooks.Add(hook);
	}
	protected List<Func<ChatHistory, AgentMessage, Task<AgentMessage>>>? PostReplyHooks { get; set; }
	public void RegisterPostReplyHook(Func<ChatHistory, AgentMessage, Task<AgentMessage>> hook)
	{
		PostReplyHooks ??= new();
		PostReplyHooks.Add(hook);
	}
	public event AgentInputRequestEventHandler? AgentInputRequest;
	public event AgentResponseEventHandler? AgentResponse;
	public float Temperature { get; set; } = 0.7f;
	protected virtual void OnAgentResponse(AgentResponseArgs agentResponseArgs)
	{
		AgentResponse?.Invoke(this, agentResponseArgs);
	}

	public virtual async Task<ChatMessage?> RunAgentAsync(ChatHistory chatHistory,
		ChatOptions? settings = null, CancellationToken cancellationToken = default)
	{
		var aiAgent = BuildAgent(settings);
		var messages = PrepareMessages(chatHistory);
		var result = await aiAgent.RunAsync(messages, cancellationToken: cancellationToken);
		var agentChatMessageContent = new AgentMessage(ChatRole.Assistant, result.Text, Name);
		AgentResponse?.Invoke(this, new AgentResponseArgs(agentChatMessageContent));
		return agentChatMessageContent;
	}

	protected List<ChatMessage> PrepareMessages(ChatHistory? chatHistory)
	{
		var messages = new List<ChatMessage>();
		if (chatHistory is null) return messages;
		foreach (var message in chatHistory)
		{
			// The system prompt is supplied through the agent's instructions, so skip stray system messages.
			if (message.Role == ChatRole.System) continue;
			var copy = message.Clone();
			copy.AuthorName = EnsurePattern(copy.AuthorName);
			messages.Add(copy);
		}
		return messages;
	}

	public static string? EnsurePattern(string? input)
	{
		if (string.IsNullOrEmpty(input)) return input;
		return System.Text.RegularExpressions.Regex.Replace(input, @"[^a-zA-Z0-9_-]", "_");
	}

	public virtual Task<ChatMessage?> RunAgent(string input, CancellationToken cancellationToken = default)
	{
		return RunAgentAsync([new AgentMessage(ChatRole.Assistant, input, Name)], null, cancellationToken);
	}

	public virtual async Task<string?> GetHumanInputAsync()
	{
		// Trigger the event to request input
		AgentInputRequest?.Invoke(this, new AgentInputRequestEventArgs(this));

		// Return the task that will eventually have the result
		var response = await Tcs.Task;
		ResetForNextInput();
		return response;
	}

	public virtual void ProvideInput(string input)
	{
		Tcs.TrySetResult(input);
	}

	public void ResetForNextInput()
	{
		Tcs = new TaskCompletionSource<string?>();
	}
	public TaskCompletionSource<string?> Tcs { get; set; } = new();
    public override string ToString()
    {
        return $"Name: {Name}\nDescription/Objective: {Description}";
    }
}

public enum ChatHistoryType
{
	All,
	SelfAndUser,
	None
}
