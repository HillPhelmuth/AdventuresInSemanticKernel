using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.Graph;
using Microsoft.SemanticKernel.Agents.History;
using SemanticKernelAgentOrchestration.Models.Events;
using SkPluginLibrary.Agents.Models.Events;


namespace SkPluginLibrary.Agents.SkAgents;

public class UserProxySkAgent : ChatHistoryKernelAgent
{
    public event EventHandler<SkAgentInputRequestArgs>? AgentInputRequest;
    public override async IAsyncEnumerable<ChatMessageContent> InvokeAsync(ChatHistory history, KernelArguments? arguments = null, Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = new())
    {
        var content = await GetHumanInputAsync();
        yield return new ChatMessageContent(AuthorRole.User, content){AuthorName = Name};
    }

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
    protected override Task<AgentChannel> RestoreChannelAsync(string channelState, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}