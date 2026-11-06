using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AgentFrameworkLibrary;

public sealed class HumanParticipantClient(string name, Func<string, CancellationToken, Task<string>> input) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var answer = await input($"{name}: {messages.LastOrDefault()?.Text}", cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);
        return new(new ChatMessage(ChatRole.Assistant, answer) { AuthorName = name });
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text) { AuthorName = name };
    }
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() { }
}
