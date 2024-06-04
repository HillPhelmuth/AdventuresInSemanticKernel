using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPluginLibrary.Agents.Models.Events;

namespace SkPluginLibrary.Agents.Models;

public abstract class InteractiveAgentBase(AgentProxy agent, Kernel kernel) : IInteractiveAgent
{
    public AgentProxy Agent { get; } = agent;
    public string Name => Agent.Name;
    public string Description => Agent.Description;
    public string SystemPrompt => Agent.SystemPrompt;
    public Kernel Kernel { get; } = kernel;
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
    // ReSharper disable ValueParameterNotUsed
    public virtual event AgentStreamingResponseEventHandler? AgentStreamingResponse { add { } remove { } }

    public virtual async Task<ChatMessageContent?> RunAgentAsync(ChatHistory chatHistory,
	    PromptExecutionSettings? settings = null, CancellationToken cancellationToken = default)
    {
        var chat = Kernel.Services.GetRequiredService<IChatCompletionService>();
        var chatmessageHistory = new ChatHistory(SystemPrompt);
        if (chatHistory is not null)
            chatmessageHistory.AddRange(chatHistory);
        settings ??= new OpenAIPromptExecutionSettings() { Temperature = Temperature, ChatSystemPrompt = SystemPrompt, ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };

        var result = await chat.GetChatMessageContentAsync(chatmessageHistory, settings, Kernel, cancellationToken);
        
        var agentChatMessageContent = new AgentMessage(result.Role, result.Content, Name, result.ModelId, result.InnerContent, result.Encoding, result.Metadata);
        AgentResponse?.Invoke(this, new AgentResponseArgs(agentChatMessageContent));
        return agentChatMessageContent;
    }

    public virtual Task<ChatMessageContent?> RunAgent(string input, CancellationToken cancellationToken = default)
    {
        return RunAgentAsync([new AgentMessage(AuthorRole.Assistant,input, Name)], null, cancellationToken);
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
}
