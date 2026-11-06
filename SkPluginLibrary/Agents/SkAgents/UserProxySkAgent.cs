using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using SkPluginLibrary.Agents.Models.Events;

namespace SkPluginLibrary.Agents.SkAgents;

/// <summary>
/// A human-in-the-loop participant for Agent Framework group chats. It is an <see cref="AIAgent"/> whose
/// "response" is whatever the person types: when it is its turn it raises <see cref="AgentInputRequest"/>
/// and waits for <see cref="ProvideInput"/>.
/// </summary>
public class UserProxySkAgent : AIAgent
{
    public UserProxySkAgent(string name = "User", string? description = null)
    {
        _name = name;
        _description = description;
    }

    private readonly string _name;
    private readonly string? _description;
    public new string Name => _name;
    protected override string? IdCore => _name;
    public override string? Description => _description;

    public event EventHandler<SkAgentInputRequestArgs>? AgentInputRequest;

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

    protected override async Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session = null,
        AgentRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = await GetHumanInputAsync();
        return new AgentResponse(new ChatMessage(ChatRole.User, content) { AuthorName = Name });
    }

    protected override async IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages,
        AgentSession? session = null, AgentRunOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var content = await GetHumanInputAsync();
        yield return new AgentResponseUpdate(ChatRole.User, content) { AuthorName = Name };
    }

    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken = default) =>
        new(new UserProxySession());

    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) => new(JsonSerializer.SerializeToElement(new { }));

    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedState, JsonSerializerOptions? jsonSerializerOptions = null,
        CancellationToken cancellationToken = default) => new(new UserProxySession());

    private sealed class UserProxySession : AgentSession;
}
