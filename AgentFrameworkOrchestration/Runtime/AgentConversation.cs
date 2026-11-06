using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkOrchestration;

public sealed class AgentConversation(AIAgent agent, IDisposable? clientLease = null) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AgentSession? _session;
    private bool _disposed;
    private readonly Dictionary<string, ToolApprovalRequestContent> _pending = [];
    public IReadOnlyCollection<ToolApprovalRequestContent> PendingApprovals => _pending.Values.ToArray();

    public async IAsyncEnumerable<AgentResponseUpdate> RunAsync(string input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        await EnterAsync(cancellationToken);
        try
        {
            if (_pending.Count != 0) throw new InvalidOperationException("Resolve the pending tool approvals before sending another message.");
            await foreach (var update in StreamAsync([new ChatMessage(ChatRole.User, input)], cancellationToken))
                yield return update;
        }
        finally { _gate.Release(); }
    }

    public async IAsyncEnumerable<AgentResponseUpdate> RespondAsync(string requestId, bool approved,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnterAsync(cancellationToken);
        try
        {
            if (!_pending.Remove(requestId, out var request))
                throw new InvalidOperationException("This approval request is unknown, expired, or already answered.");
            await foreach (var update in StreamAsync([new ChatMessage(ChatRole.User, [request.CreateResponse(approved)])], cancellationToken))
                yield return update;
        }
        finally { _gate.Release(); }
    }

    private async Task EnterAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("A conversation run is already in progress.");
        if (_disposed) { _gate.Release(); throw new ObjectDisposedException(nameof(AgentConversation)); }
    }

    private async IAsyncEnumerable<AgentResponseUpdate> StreamAsync(IEnumerable<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _session ??= await agent.CreateSessionAsync(cancellationToken);
        await foreach (var update in agent.RunStreamingAsync(messages, _session, cancellationToken: cancellationToken))
        {
            foreach (var request in update.Contents.OfType<ToolApprovalRequestContent>())
                _pending[request.RequestId] = request;
            yield return update;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await EnterAsync(cancellationToken);
        try { _session = null; _pending.Clear(); }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _pending.Clear(); _session = null;
            clientLease?.Dispose();
        }
        finally { _gate.Release(); }
    }
}
