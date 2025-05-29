using Microsoft.SemanticKernel.ChatCompletion;
using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel.Agents;
using SkPluginLibrary.Agents.Models.Events;


namespace SkPluginLibrary.Agents.SkAgents;

public class UserProxySkAgent : ChatHistoryAgent
{
    public event EventHandler<SkAgentInputRequestArgs>? AgentInputRequest;
    [Obsolete("Use InvokeAsync with AgentThread instead.")]
    public override async IAsyncEnumerable<ChatMessageContent> InvokeAsync(ChatHistory history, KernelArguments? arguments = null, Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = new())
    {
        var content = await GetHumanInputAsync();
        yield return new ChatMessageContent(AuthorRole.User, content){AuthorName = Name};
    }

    [Obsolete("Use InvokeStreamingAsync with AgentThread instead.")]
    public override async IAsyncEnumerable<StreamingChatMessageContent> InvokeStreamingAsync(ChatHistory history, KernelArguments? arguments = null, Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = new())
    {
        var content = await GetHumanInputAsync();
        yield return new StreamingChatMessageContent(AuthorRole.User, content) { AuthorName = Name };
    }
    public async Task<string?> GetHumanInputAsync()
    {
        // Trigger the event to request input
        AgentInputRequest?.Invoke(this, new SkAgentInputRequestArgs(this));

        // Return the task that will eventually have the result
        var response = await Tcs.Task;
        ResetForNextInput();
        return response;
    }

    public void ProvideInput(string input)
    {
        Tcs.TrySetResult(input);
    }

    public void ResetForNextInput()
    {
        Tcs = new TaskCompletionSource<string?>();
    }
    public TaskCompletionSource<string?> Tcs { get; set; } = new();

    public override async IAsyncEnumerable<AgentResponseItem<ChatMessageContent>> InvokeAsync(ICollection<ChatMessageContent> messages, AgentThread? thread = null, AgentInvokeOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = new())
    {
        var content = await GetHumanInputAsync();
        yield return new AgentResponseItem<ChatMessageContent>(new ChatMessageContent(AuthorRole.User, content) { AuthorName = Name }, thread ?? new ChatHistoryAgentThread(new ChatHistory(messages)));
    }

    public override async IAsyncEnumerable<AgentResponseItem<StreamingChatMessageContent>> InvokeStreamingAsync(ICollection<ChatMessageContent> messages, AgentThread? thread = null,
        AgentInvokeOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = new())
    {
        var content = await GetHumanInputAsync();
        yield return new AgentResponseItem<StreamingChatMessageContent>(new StreamingChatMessageContent(AuthorRole.User, content) { AuthorName = Name }, thread ?? new ChatHistoryAgentThread(new ChatHistory(messages)));
    }

    protected override Task<AgentChannel> RestoreChannelAsync(string channelState, CancellationToken cancellationToken)
    {
        return this.CreateChannelAsync(cancellationToken);
    }
}