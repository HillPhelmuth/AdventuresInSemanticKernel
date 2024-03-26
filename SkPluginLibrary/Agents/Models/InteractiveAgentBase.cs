using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SkPluginLibrary.Agents.Extensions;
using SkPluginLibrary.Agents.Models.Events;

namespace SkPluginLibrary.Agents.Models;

public abstract class InteractiveAgentBase(AgentProxy agent, Kernel kernel) : IInteractiveAgent
{
    public AgentProxy Agent { get; } = agent;
    public string Name => Agent.Name;
    public string Description => Agent.Description;
    public string SystemPrompt => Agent.SystemPrompt;
    public Kernel Kernel { get; } = kernel;
    public event AgentInputRequestEventHandler? AgentInputRequest;
    public event AgentResponseEventHandler? AgentResponse;
    // ReSharper disable ValueParameterNotUsed
    public virtual event AgentStreamingResponseEventHandler? AgentStreamingResponse { add { } remove { } }

    public virtual async Task<AgentMessage?> RunAgentAsync(List<AgentMessage> chatHistory, PromptExecutionSettings? settings = null, CancellationToken cancellationToken = default)
    {
        var chat = Kernel.Services.GetRequiredService<IChatCompletionService>();
        var chatmessageHistory = new ChatHistory(SystemPrompt);
        chatmessageHistory.AddRange(chatHistory.AsChatHistory());
        settings ??= new OpenAIPromptExecutionSettings() { ChatSystemPrompt = SystemPrompt, ToolCallBehavior = Kernel.Plugins.Count > 0 ? ToolCallBehavior.AutoInvokeKernelFunctions : null };

        var result = await chat.GetChatMessageContentAsync(chatmessageHistory, settings, Kernel, cancellationToken);
        
        var agentChatMessageContent = new AgentMessage(result.Role, result.Content, Name, result.ModelId, result.InnerContent, result.Encoding, result.Metadata);
        AgentResponse?.Invoke(this, new AgentResponseArgs(agentChatMessageContent));
        return agentChatMessageContent;
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